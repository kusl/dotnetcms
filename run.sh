#!/usr/bin/env bash
# =============================================================================
# run.sh — One-shot local build / test / dump / E2E pipeline for MyBlog
#
# Location: <repo root>/run.sh   (lives next to export.sh and run-e2e.sh)
#
# What it does, in order, logging everything to a timestamped file under
# docs/llm/output/ :
#
#   A. Toolchain / environment diagnostics
#        dotnet --info / --version / --list-sdks / --list-runtimes
#        git status + recent history
#        container tooling versions (podman / podman-compose / docker)
#   B. The .NET workflow, run from ./src :
#        dotnet format
#        dotnet restore
#        dotnet clean
#        dotnet build
#        dotnet run --project MyBlog.Tests/MyBlog.Tests.csproj   (test suite)
#        dotnet list package
#        dotnet list package --outdated
#   C. export.sh   (repository context dump -> docs/llm/dump.txt)
#   D. run-e2e.sh  (Playwright E2E suite via Podman / Docker Compose)
#   E. A PASS/FAIL summary; exits non-zero if any step failed.
#
# Why this exists / design notes:
#   * Path-aware. The repository root is resolved from the script's own
#     location (preferring `git rev-parse --show-toplevel`), so run.sh can be
#     launched from any working directory. Nothing is hard-coded to a home path.
#   * export.sh and run-e2e.sh are invoked by their resolved absolute paths
#     rather than relying on the current directory.
#   * Each step's combined stdout+stderr, wall-clock duration, and exit code are
#     captured to the log. The original one-liner used `time cmd >> log`, which
#     sent both stderr and the timing to the terminal (not the file); those were
#     effectively lost. This captures them.
#   * Steps run to completion even when an earlier one fails — matching the ';'
#     (not '&&') semantics of the original command — so a single failure still
#     produces a complete diagnostic log. The process exit code reflects whether
#     every step succeeded, which makes the script usable in CI.
#   * Long-form flags are used throughout for self-documentation.
#
# Usage:
#   bash run.sh
#   ./run.sh                 # after: chmod +x run.sh
#
# Note: run-e2e.sh also accepts --clean / --build / --no-build. This script runs
# it with its default behaviour (build only if images are missing). To force a
# clean E2E run, run `bash run-e2e.sh --clean` yourself beforehand.
# =============================================================================

set -uo pipefail
# NOTE: intentionally no `-e`. This is a diagnostic pipeline; we want every step
# to run even if a previous one fails, then report the outcome at the end.

# ---------------------------------------------------------------------------
# 0. Resolve this script's own location — immune to the current directory
# ---------------------------------------------------------------------------
SCRIPT_SOURCE="${BASH_SOURCE[0]}"
SCRIPT_PATH="$(readlink -f "$SCRIPT_SOURCE" 2>/dev/null \
               || realpath "$SCRIPT_SOURCE" 2>/dev/null \
               || printf '%s' "$SCRIPT_SOURCE")"
SCRIPT_DIR="$(cd "$(dirname "$SCRIPT_PATH")" && pwd)"

# ---------------------------------------------------------------------------
# 1. Resolve the repository root (prefer Git; fall back to the script directory)
# ---------------------------------------------------------------------------
if git -C "$SCRIPT_DIR" rev-parse --show-toplevel >/dev/null 2>&1; then
    REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel)"
else
    REPO_ROOT="$SCRIPT_DIR"
fi

SRC_DIR="$REPO_ROOT/src"
EXPORT_SCRIPT="$REPO_ROOT/export.sh"
E2E_SCRIPT="$REPO_ROOT/run-e2e.sh"
OUTPUT_DIR="$REPO_ROOT/docs/llm/output"

# ---------------------------------------------------------------------------
# 2. .NET CLI environment — opt out of telemetry and the startup banner
# ---------------------------------------------------------------------------
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# ---------------------------------------------------------------------------
# 3. Prepare the timestamped log file
# ---------------------------------------------------------------------------
mkdir -p "$OUTPUT_DIR"
TIMESTAMP="$(date +%Y-%m-%d-%H-%M-%S)"
LOG_FILE="$OUTPUT_DIR/${TIMESTAMP}.txt"
: > "$LOG_FILE"   # create / truncate

# ---------------------------------------------------------------------------
# 4. Helpers
# ---------------------------------------------------------------------------

# have <cmd> : true if the command exists on PATH.
have() { command -v "$1" >/dev/null 2>&1; }

# now : high-resolution 'seconds.microseconds' clock, with a portable fallback
# to whole seconds (bash's SECONDS) if EPOCHREALTIME is unavailable.
now() {
    if [[ -n "${EPOCHREALTIME:-}" ]]; then
        printf '%s' "${EPOCHREALTIME/,/.}"   # normalise locales that use ','
    else
        printf '%s' "$SECONDS"
    fi
}

# log_only <format> [args...] : append a formatted line to the log file only.
log_only() {
    # shellcheck disable=SC2059
    printf "$@" >> "$LOG_FILE"
}

# section <title> : write a visually distinct header to the log.
section() {
    {
        printf '\n'
        printf '###############################################################################\n'
        printf '# %s\n' "$1"
        printf '###############################################################################\n'
    } >> "$LOG_FILE"
}

# Step bookkeeping (parallel arrays) for the final summary.
STEP_LABELS=()
STEP_CODES=()
STEP_TIMES=()
STEP_INDEX=0
FAILURE_COUNT=0

# run_step "<label>" command [args...]
#   Runs the command in the CURRENT working directory, capturing its combined
#   output, wall-clock duration and exit code into the log, and records the
#   result for the summary. Never aborts the script on a non-zero exit.
run_step() {
    local label="$1"; shift
    STEP_INDEX=$(( STEP_INDEX + 1 ))

    printf '[%02d] %s ...\n' "$STEP_INDEX" "$label" >&2

    local start end rc dur
    start="$(now)"

    section "STEP ${STEP_INDEX}: ${label}"
    log_only '$ %s\n\n' "$*"

    # Combined stdout+stderr to the log; stdin detached so nothing can block on
    # input. A brace group (not a subshell) keeps CWD and array updates intact.
    { "$@"; } < /dev/null >> "$LOG_FILE" 2>&1
    rc=$?

    end="$(now)"
    dur="$(awk "BEGIN { printf \"%.3f\", ${end} - ${start} }" 2>/dev/null || printf '0')"

    log_only '\n[result] exit=%d  duration=%ss\n' "$rc" "$dur"

    STEP_LABELS+=("$label")
    STEP_CODES+=("$rc")
    STEP_TIMES+=("$dur")

    if [[ "$rc" -ne 0 ]]; then
        FAILURE_COUNT=$(( FAILURE_COUNT + 1 ))
        printf '[%02d] %s -> FAILED (exit %d, %ss)\n' "$STEP_INDEX" "$label" "$rc" "$dur" >&2
    else
        printf '[%02d] %s -> ok (%ss)\n' "$STEP_INDEX" "$label" "$dur" >&2
    fi
    return 0
}

# mark_step "<label>" <exit-code> "<note>"
#   Records a synthetic step result (used when a prerequisite is missing and the
#   real command cannot be run at all).
mark_step() {
    local label="$1" code="$2" note="${3:-}"
    STEP_INDEX=$(( STEP_INDEX + 1 ))

    section "STEP ${STEP_INDEX}: ${label}"
    if [[ -n "$note" ]]; then
        log_only '%s\n' "$note"
    fi
    log_only '\n[result] exit=%d  duration=%ss\n' "$code" "0"

    STEP_LABELS+=("$label")
    STEP_CODES+=("$code")
    STEP_TIMES+=("0")

    if [[ "$code" -ne 0 ]]; then
        FAILURE_COUNT=$(( FAILURE_COUNT + 1 ))
        printf '[%02d] %s -> FAILED\n' "$STEP_INDEX" "$label" >&2
    else
        printf '[%02d] %s -> ok\n' "$STEP_INDEX" "$label" >&2
    fi
}

# show_source "<label>" <path>
#   Records a file's contents into the log (mirrors the `cat export.sh` /
#   `cat run-e2e.sh` steps of the original command, making the log self-contained).
show_source() {
    local label="$1" path="$2"
    section "SOURCE: ${label} (${path})"
    if [[ -f "$path" ]]; then
        cat "$path" >> "$LOG_FILE" 2>&1
    else
        log_only '(file not found: %s)\n' "$path"
    fi
}

# ---------------------------------------------------------------------------
# 5. Log header / run metadata
# ---------------------------------------------------------------------------
{
    printf '###############################################################################\n'
    printf '#  MyBlog run.sh — build / test / dump / E2E log\n'
    printf '###############################################################################\n'
    printf '  Started       : %s\n' "$(date --iso-8601=seconds 2>/dev/null || date)"
    printf '  Script        : %s\n' "$SCRIPT_PATH"
    printf '  Repository    : %s\n' "$REPO_ROOT"
    printf '  Source dir    : %s\n' "$SRC_DIR"
    printf '  Log file      : %s\n' "$LOG_FILE"
    printf '  Host          : %s\n' "$(hostname 2>/dev/null || echo unknown)"
    printf '  User          : %s\n' "$(id -un 2>/dev/null || printf '%s' "${USER:-unknown}")"
    printf '  OS            : %s\n' "$(uname -srmo 2>/dev/null || uname -a 2>/dev/null || echo unknown)"
    if git -C "$REPO_ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        printf '  Git branch    : %s\n' "$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
        printf '  Git commit    : %s\n' "$(git -C "$REPO_ROOT" rev-parse HEAD 2>/dev/null || echo unknown)"
    fi
} >> "$LOG_FILE"

printf 'run.sh: repository root: %s\n' "$REPO_ROOT" >&2
printf 'run.sh: logging to     : %s\n' "$LOG_FILE" >&2

# ---------------------------------------------------------------------------
# 6. Sanity check: the src directory must exist
# ---------------------------------------------------------------------------
if [[ ! -d "$SRC_DIR" ]]; then
    printf 'run.sh: error: source directory not found: %s\n' "$SRC_DIR" >&2
    log_only '\nFATAL: source directory not found: %s\n' "$SRC_DIR"
    exit 1
fi

# ===========================================================================
# A. Toolchain / environment diagnostics
# ===========================================================================
# Run the .NET diagnostics from ./src so the SDK selected by src/global.json is
# the one reported.
cd "$SRC_DIR" || { printf 'run.sh: cannot enter %s\n' "$SRC_DIR" >&2; exit 1; }

if have dotnet; then
    run_step "dotnet --info"           dotnet --info
    run_step "dotnet --version"        dotnet --version
    run_step "dotnet --list-sdks"      dotnet --list-sdks
    run_step "dotnet --list-runtimes"  dotnet --list-runtimes
else
    mark_step "dotnet CLI" 1 "dotnet CLI not found on PATH; the .NET workflow will be skipped."
fi

if have git && git -C "$REPO_ROOT" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    run_step "git status" git -C "$REPO_ROOT" --no-pager status --short --branch
    run_step "git log (recent)" git -C "$REPO_ROOT" --no-pager log -5 \
        --pretty=format:'%h %ad %an %s' --date=iso
fi

for container_tool in podman podman-compose docker; do
    if have "$container_tool"; then
        run_step "${container_tool} --version" "$container_tool" --version
    fi
done

# ===========================================================================
# B. .NET workflow (from ./src)
# ===========================================================================
if have dotnet; then
    run_step "dotnet format"                    dotnet format
    run_step "dotnet restore"                   dotnet restore
    run_step "dotnet clean"                     dotnet clean
    run_step "dotnet build"                     dotnet build
    run_step "dotnet run (MyBlog.Tests)"        dotnet run --project MyBlog.Tests/MyBlog.Tests.csproj
    run_step "dotnet list package"              dotnet list package
    run_step "dotnet list package --outdated"   dotnet list package --outdated
fi

# ===========================================================================
# C. Repository context dump (export.sh -> docs/llm/dump.txt)
# ===========================================================================
cd "$REPO_ROOT" || { printf 'run.sh: cannot return to %s\n' "$REPO_ROOT" >&2; exit 1; }

if [[ -f "$EXPORT_SCRIPT" ]]; then
    show_source "export.sh" "$EXPORT_SCRIPT"
    run_step "export.sh (repository dump)" bash "$EXPORT_SCRIPT"
else
    mark_step "export.sh" 1 "export.sh not found at $EXPORT_SCRIPT"
fi

# ===========================================================================
# D. End-to-end tests (run-e2e.sh)
# ===========================================================================
if [[ -f "$E2E_SCRIPT" ]]; then
    show_source "run-e2e.sh" "$E2E_SCRIPT"
    run_step "run-e2e.sh (Playwright E2E)" bash "$E2E_SCRIPT"
else
    mark_step "run-e2e.sh" 1 "run-e2e.sh not found at $E2E_SCRIPT"
fi

# ===========================================================================
# E. Summary
# ===========================================================================
build_summary() {
    printf '\n'
    printf '###############################################################################\n'
    printf '# SUMMARY\n'
    printf '###############################################################################\n'
    local i code status
    for i in "${!STEP_LABELS[@]}"; do
        code="${STEP_CODES[$i]}"
        if [[ "$code" -eq 0 ]]; then status="PASS"; else status="FAIL"; fi
        printf '  [%s] %-40s exit=%-4s %ss\n' \
            "$status" "${STEP_LABELS[$i]}" "$code" "${STEP_TIMES[$i]}"
    done
    printf '  ---------------------------------------------------------------------------\n'
    printf '  Steps: %d    Failures: %d\n' "${#STEP_LABELS[@]}" "$FAILURE_COUNT"
    printf '  Finished: %s\n' "$(date --iso-8601=seconds 2>/dev/null || date)"
    printf '  Log file: %s\n' "$LOG_FILE"
}

SUMMARY_TEXT="$(build_summary)"
printf '%s\n' "$SUMMARY_TEXT" >> "$LOG_FILE"
printf '%s\n' "$SUMMARY_TEXT" >&2

if [[ "$FAILURE_COUNT" -gt 0 ]]; then
    exit 1
fi
exit 0

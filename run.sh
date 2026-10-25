#!/usr/bin/env bash
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || printf '%s' "$SCRIPT_DIR")"
SRC_DIR="$REPO_ROOT/src"
OUTPUT_DIR="$REPO_ROOT/docs/llm/output"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

mkdir -p "$OUTPUT_DIR"
LOG_FILE="$OUTPUT_DIR/$(date +%Y-%m-%d-%H-%M-%S).txt"
: > "$LOG_FILE"

STEP_LABELS=()
STEP_CODES=()
STEP_TIMES=()
FAILURES=0

have() { command -v "$1" >/dev/null 2>&1; }

run_step() {
    local label="$1"; shift
    local index=$(( ${#STEP_LABELS[@]} + 1 ))
    printf '[%02d] %s ...\n' "$index" "$label" >&2
    {
        printf '\n###############################################################################\n'
        printf '# STEP %d: %s\n' "$index" "$label"
        printf '###############################################################################\n'
        printf '$ %s\n\n' "$*"
    } >> "$LOG_FILE"

    local start=$SECONDS rc
    { "$@"; } < /dev/null >> "$LOG_FILE" 2>&1
    rc=$?
    local duration=$(( SECONDS - start ))

    printf '\n[result] exit=%d duration=%ds\n' "$rc" "$duration" >> "$LOG_FILE"
    STEP_LABELS+=("$label")
    STEP_CODES+=("$rc")
    STEP_TIMES+=("$duration")

    if [[ "$rc" -ne 0 ]]; then
        FAILURES=$(( FAILURES + 1 ))
        printf '[%02d] %s -> FAILED (exit %d, %ds)\n' "$index" "$label" "$rc" "$duration" >&2
    else
        printf '[%02d] %s -> ok (%ds)\n' "$index" "$label" "$duration" >&2
    fi
}

{
    printf 'MyBlog run.sh\n'
    printf '  Started    : %s\n' "$(date --iso-8601=seconds)"
    printf '  Repository : %s\n' "$REPO_ROOT"
    printf '  Branch     : %s\n' "$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
    printf '  Commit     : %s\n' "$(git -C "$REPO_ROOT" rev-parse HEAD 2>/dev/null || echo unknown)"
    printf '  OS         : %s\n' "$(uname -srmo 2>/dev/null || echo unknown)"
} >> "$LOG_FILE"

printf 'run.sh: logging to %s\n' "$LOG_FILE" >&2

if [[ ! -d "$SRC_DIR" ]]; then
    printf 'run.sh: source directory not found: %s\n' "$SRC_DIR" >&2
    exit 1
fi

cd "$SRC_DIR" || exit 1

if have dotnet; then
    run_step "dotnet --info" dotnet --info
    run_step "dotnet format" dotnet format
    run_step "dotnet restore" dotnet restore
    run_step "dotnet clean" dotnet clean
    run_step "dotnet build" dotnet build
    run_step "dotnet run (MyBlog.Tests)" dotnet run --project MyBlog.Tests/MyBlog.Tests.csproj
    run_step "dotnet list package" dotnet list package
    run_step "dotnet list package --outdated" dotnet list package --outdated
else
    run_step "dotnet CLI available" false
fi

for tool in podman podman-compose; do
    if have "$tool"; then
        run_step "$tool --version" "$tool" --version
    fi
done

cd "$REPO_ROOT" || exit 1

run_step "git status" git --no-pager status --short --branch
run_step "export.sh" bash "$REPO_ROOT/export.sh"
run_step "run-e2e.sh" bash "$REPO_ROOT/run-e2e.sh"

SUMMARY="$(
    printf '\n###############################################################################\n# SUMMARY\n###############################################################################\n'
    for i in "${!STEP_LABELS[@]}"; do
        status=PASS
        [[ "${STEP_CODES[$i]}" -ne 0 ]] && status=FAIL
        printf '  [%s] %-36s exit=%-4s %ss\n' "$status" "${STEP_LABELS[$i]}" "${STEP_CODES[$i]}" "${STEP_TIMES[$i]}"
    done
    printf '  Steps: %d  Failures: %d\n  Log: %s\n' "${#STEP_LABELS[@]}" "$FAILURES" "$LOG_FILE"
)"
printf '%s\n' "$SUMMARY" >> "$LOG_FILE"
printf '%s\n' "$SUMMARY" >&2

[[ "$FAILURES" -eq 0 ]]

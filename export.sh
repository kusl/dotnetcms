#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null)" || exit 0

OUTPUT_DIR="$REPO_ROOT/docs/llm"
DUMP_FILE="$OUTPUT_DIR/dump.txt"
SLIM_FILE="$OUTPUT_DIR/slim.md"

mkdir -p "$OUTPUT_DIR"

mapfile -t FILES < <(
    git -C "$REPO_ROOT" ls-files --cached --others --exclude-standard \
        | grep -v '^docs/llm/' \
        | sort -u
)

is_text() {
    [[ ! -s "$1" ]] || grep -qI '' "$1"
}

is_code() {
    case "$1" in
        *.md | export.sh | .gitignore) return 1 ;;
        *) return 0 ;;
    esac
}

language() {
    case "$1" in
        *.cs) echo csharp ;;
        *.razor) echo razor ;;
        *.css) echo css ;;
        *.js) echo javascript ;;
        *.json) echo json ;;
        *.props | *.csproj | *.slnx | *.xml) echo xml ;;
        *.yml | *.yaml) echo yaml ;;
        *.sh) echo bash ;;
        *Dockerfile) echo dockerfile ;;
        *.editorconfig) echo ini ;;
        *) echo text ;;
    esac
}

fence_for() {
    local longest
    longest="$(grep -o '`\+' "$1" 2>/dev/null | awk '{ if (length($0) > m) m = length($0) } END { print m + 0 }')"
    local width=$(( longest >= 3 ? longest + 1 : 3 ))
    printf '%*s' "$width" '' | tr ' ' '`'
}

DUMP_TMP="$(mktemp "$OUTPUT_DIR/.dump.XXXXXX")"
SLIM_TMP="$(mktemp "$OUTPUT_DIR/.slim.XXXXXX")"
trap 'rm -f "$DUMP_TMP" "$SLIM_TMP"' EXIT

{
    printf '# Repository dump\n'
    printf '# Commit: %s (%s)\n' "$(git -C "$REPO_ROOT" rev-parse --short HEAD)" "$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)"
    printf '# Message: %s\n' "$(git -C "$REPO_ROOT" log -1 --pretty=format:%s)"
    printf '# Generated: %s\n' "$(date --iso-8601=seconds)"
    printf '# Uncommitted:\n'
    git -C "$REPO_ROOT" status --short | grep -v ' docs/llm/' | sed 's/^/#   /' || true
} > "$DUMP_TMP"

DUMPED=0
SLIMMED=0

for rel in "${FILES[@]}"; do
    path="$REPO_ROOT/$rel"
    [[ -f "$path" ]] || continue

    if ! is_text "$path"; then
        printf '\n===== %s =====\n[binary file omitted, %d bytes]\n' "$rel" "$(wc -c < "$path")" >> "$DUMP_TMP"
        continue
    fi

    {
        printf '\n===== %s =====\n' "$rel"
        cat "$path"
        [[ -s "$path" && "$(tail -c1 "$path" | wc -l)" -eq 0 ]] && printf '\n'
    } >> "$DUMP_TMP"
    DUMPED=$(( DUMPED + 1 ))

    if is_code "$rel"; then
        fence="$(fence_for "$path")"
        {
            printf '%s%s %s\n' "$fence" "$(language "$rel")" "$rel"
            cat "$path"
            [[ -s "$path" && "$(tail -c1 "$path" | wc -l)" -eq 0 ]] && printf '\n'
            printf '%s\n\n' "$fence"
        } >> "$SLIM_TMP"
        SLIMMED=$(( SLIMMED + 1 ))
    fi
done

mv -f "$DUMP_TMP" "$DUMP_FILE"
mv -f "$SLIM_TMP" "$SLIM_FILE"

printf 'dump.txt: %d files, %d bytes\n' "$DUMPED" "$(wc -c < "$DUMP_FILE")"
printf 'slim.md : %d files, %d bytes\n' "$SLIMMED" "$(wc -c < "$SLIM_FILE")"

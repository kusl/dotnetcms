#!/bin/bash
# =============================================================================
# custom-prompt.sh - Comprehensive Project Analysis with Local LLM
# =============================================================================
# System: AMD 5800X, 5700 XT (8GB VRAM), 64GB RAM, Fedora Linux, Ollama
# =============================================================================

set -euo pipefail  # Exit on error, undefined vars, pipe failures

# --- Configuration ---
PROJECT_ROOT="${PWD}"
OUTPUT_DIR="${PROJECT_ROOT}/docs/llm"
DUMP_FILE="${OUTPUT_DIR}/source.txt"
MODEL="${LLM_MODEL:-llama3.3:70b-instruct-q4_K_M}"
OUTPUT_FILE="${OUTPUT_DIR}/thinking_$(date +%Y%m%d_%H%M%S).md"
LOG_FILE="${OUTPUT_DIR}/analysis_$(date +%Y%m%d_%H%M%S).log"
MAX_CONTEXT_CHARS=120000  # ~30k tokens, safe for most models

# File patterns to include
INCLUDE_PATTERNS=(-name "*.cs" -o -name "*.props" -o -name "*.csproj" -o -name "*.razor" -o -name "*.json")
# Directories/patterns to exclude
EXCLUDE_PATTERN="obj|bin|TestResults|Properties|migrations|node_modules|\.git"

# --- Helper Functions ---
log() {
    local msg="[$(date '+%Y-%m-%d %H:%M:%S')] $1"
    echo "$msg"
    echo "$msg" >> "$LOG_FILE" 2>/dev/null || true
}

log_error() {
    log "ERROR: $1" >&2
}

cleanup() {
    local exit_code=$?
    if [[ $exit_code -ne 0 ]]; then
        log_error "Script failed with exit code $exit_code"
    fi
    exit $exit_code
}
trap cleanup EXIT

print_header() {
    echo ""
    echo "=============================================="
    echo "  $1"
    echo "=============================================="
}

check_dependencies() {
    local deps=("ollama" "find" "wc")
    for dep in "${deps[@]}"; do
        if ! command -v "$dep" &>/dev/null; then
            log_error "Required command not found: $dep"
            exit 1
        fi
    done
}

check_ollama_model() {
    log "Checking if model '$MODEL' is available..."
    if ! ollama list 2>/dev/null | grep -q "${MODEL%%:*}"; then
        log "Model not found locally. Attempting to pull '$MODEL'..."
        if ! ollama pull "$MODEL"; then
            log_error "Failed to pull model '$MODEL'. Please run: ollama pull $MODEL"
            exit 1
        fi
    fi
    log "Model '$MODEL' is ready."
}

get_system_info() {
    echo "System: $(uname -sr)"
    echo "Memory: $(free -h | awk '/^Mem:/{print $2 " total, " $7 " available"}')"
    echo "Ollama: $(ollama --version 2>/dev/null | head -1 || echo 'unknown')"
}

# --- Pre-flight Checks ---
print_header "Pre-flight Checks"
check_dependencies
mkdir -p "$OUTPUT_DIR"

# Initialize log
{
    echo "# Analysis Log - $(date '+%Y-%m-%d %H:%M:%S')"
    echo "Project: ${PROJECT_ROOT}"
    echo "Model: ${MODEL}"
    echo ""
    get_system_info
    echo ""
} > "$LOG_FILE"

check_ollama_model

# Verify src directory exists
if [[ ! -d "src" ]]; then
    log_error "No 'src' directory found in ${PROJECT_ROOT}"
    log "Available directories: $(ls -d */ 2>/dev/null | tr '\n' ' ')"
    exit 1
fi

# --- Phase 1: Comprehensive Export ---
print_header "Phase 1: Generating Global Project Export"
START_TIME=$(date +%s)

# Clear and initialize the dump file
> "$DUMP_FILE"

# Generate project structure map
{
    echo "# PROJECT ANALYSIS EXPORT"
    echo "# Generated: $(date '+%Y-%m-%d %H:%M:%S')"
    echo "# Project: $(basename "$PROJECT_ROOT")"
    echo ""
    echo "## PROJECT STRUCTURE MAP"
    echo ""
} >> "$DUMP_FILE"

# Find and list all relevant files
find src -type f \( "${INCLUDE_PATTERNS[@]}" \) 2>/dev/null | \
    grep -vE "$EXCLUDE_PATTERN" | \
    sort >> "$DUMP_FILE"

echo -e "\n" >> "$DUMP_FILE"

# Count files before processing
FILE_LIST=$(find src -type f \( "${INCLUDE_PATTERNS[@]}" \) 2>/dev/null | grep -vE "$EXCLUDE_PATTERN" | sort)
FILE_COUNT=$(echo "$FILE_LIST" | wc -l)

if [[ $FILE_COUNT -eq 0 ]]; then
    log_error "No source files found matching criteria in src/"
    exit 1
fi

log "Found $FILE_COUNT files to process..."

# Pack all source files with progress indication
CURRENT=0
echo "$FILE_LIST" | while IFS= read -r file; do
    CURRENT=$((CURRENT + 1))
    # Progress indicator (every 10 files)
    if (( CURRENT % 10 == 0 )); then
        printf "\r  Processing: %d/%d files..." "$CURRENT" "$FILE_COUNT"
    fi
    
    {
        echo "================================================================================"
        echo "FILE: $file"
        echo "================================================================================"
        cat "$file" 2>/dev/null || echo "[ERROR: Could not read file]"
        echo -e "\n--- END OF FILE ---\n"
    } >> "$DUMP_FILE"
done
printf "\r  Processing: %d/%d files... Done!\n" "$FILE_COUNT" "$FILE_COUNT"

# Check file size and warn if too large
DUMP_SIZE=$(wc -c < "$DUMP_FILE")
DUMP_SIZE_MB=$((DUMP_SIZE / 1024 / 1024))
DUMP_CHARS=$(wc -m < "$DUMP_FILE")

log "Export complete: $FILE_COUNT files, ${DUMP_SIZE_MB}MB (${DUMP_CHARS} chars)"

if [[ $DUMP_CHARS -gt $MAX_CONTEXT_CHARS ]]; then
    log "WARNING: Export exceeds recommended context size (${DUMP_CHARS} > ${MAX_CONTEXT_CHARS} chars)"
    log "         The model may truncate or miss content. Consider filtering files."
fi

EXPORT_TIME=$(($(date +%s) - START_TIME))
log "Export completed in ${EXPORT_TIME}s"

# --- Phase 2: LLM Analysis ---
print_header "Phase 2: LLM Architectural Review"
log "Analyzing with $MODEL..."
log "This may take 5-15 minutes on your 5700 XT..."

ANALYSIS_START=$(date +%s)

# The prompt - designed for thorough analysis
PROMPT="You are a senior .NET Solution Architect performing a comprehensive code review.

INSTRUCTIONS:
- Read ALL provided source files carefully and thoroughly
- Do not skip any files or sections
- Provide specific file paths and line references where applicable
- Be concrete with examples, not generic

REVIEW AREAS:

1. **Architectural Integrity**
   - Are boundaries between Core, Infrastructure, and Web layers respected?
   - Check for leaked concerns (UI logic in Core, DB logic in Web, etc.)
   - Evaluate dependency direction (should flow inward to Core)

2. **Security Audit**
   - Authentication/Authorization flow analysis
   - Input validation and sanitization
   - Potential injection vulnerabilities
   - Secrets management

3. **Data Access Patterns**
   - Repository implementation quality
   - DbContext usage and lifetime management
   - N+1 query potential
   - Missing async/await patterns

4. **Code Quality**
   - Redundant logic that could be consolidated
   - Missing error handling
   - Inconsistent patterns
   - .NET 10 best practices violations

5. **Testing & Maintainability**
   - Testability of current architecture
   - Coupling issues
   - Missing abstractions

OUTPUT FORMAT:
Provide a professional architectural audit report with:
- Executive Summary (2-3 paragraphs)
- Critical Findings (security issues, bugs) with file:line references
- Architectural Recommendations (prioritized)
- Specific Refactoring Examples (show before/after code)
- Quick Wins (easy improvements)

---
PROJECT SOURCE CODE:
$(cat "$DUMP_FILE")
"

# Run the analysis (OLLAMA_NOPRUNE prevents context truncation warnings)
# Use script to fake a TTY, then strip ANSI codes, OR use --nowordwrap
if echo "$PROMPT" | OLLAMA_NOPRUNE=1 ollama run "$MODEL" --nowordwrap 2>/dev/null | \
   sed 's/\x1b\[[0-9;]*[a-zA-Z]//g' | \
   sed 's/\x1b\[?[0-9]*[a-zA-Z]//g' | \
   tr -d '\r' > "$OUTPUT_FILE"; then
    ANALYSIS_TIME=$(($(date +%s) - ANALYSIS_START))
    OUTPUT_SIZE=$(wc -l < "$OUTPUT_FILE")
    
    log "Analysis completed in ${ANALYSIS_TIME}s ($(( ANALYSIS_TIME / 60 ))m $(( ANALYSIS_TIME % 60 ))s)"
    log "Output: ${OUTPUT_SIZE} lines"
else
    log_error "Ollama analysis failed. Check if the model is running."
    log "Try: ollama serve"
    exit 1
fi

# --- Summary ---
print_header "Analysis Complete"
TOTAL_TIME=$(($(date +%s) - START_TIME))

{
    echo ""
    echo "---"
    echo "## Summary"
    echo "- Files analyzed: $FILE_COUNT"
    echo "- Export size: ${DUMP_SIZE_MB}MB"
    echo "- Model: $MODEL"
    echo "- Export time: ${EXPORT_TIME}s"
    echo "- Analysis time: ${ANALYSIS_TIME}s"
    echo "- Total time: ${TOTAL_TIME}s ($(( TOTAL_TIME / 60 ))m $(( TOTAL_TIME % 60 ))s)"
} >> "$LOG_FILE"

echo ""
echo "Results:"
echo "  Report:    $OUTPUT_FILE"
echo "  Source:    $DUMP_FILE"
echo "  Log:       $LOG_FILE"
echo ""
echo "Total time: $(( TOTAL_TIME / 60 ))m $(( TOTAL_TIME % 60 ))s"
echo ""
echo "View report: less '$OUTPUT_FILE'"
echo "=============================================="



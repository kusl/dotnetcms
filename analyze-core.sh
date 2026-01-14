#!/bin/bash

# =============================================================================
# analyze-core.sh - Integrated Export & AI Analysis
# =============================================================================

# Configuration
OUTPUT_DIR="docs/llm"
DUMP_FILE="$OUTPUT_DIR/source_only.txt"
MODEL="deepseek-r1:8b"
OUTPUT_FILE="$OUTPUT_DIR/analysis_report.md"

mkdir -p "$OUTPUT_DIR"

# --- Phase 1: Lean Export ---
echo "=============================================="
echo "  Phase 1: Generating Lean Export"
echo "=============================================="
> "$DUMP_FILE"

# Find and pack .cs and .razor files, excluding build artifacts
find src -type f \( -name "*.cs" -o -name "*.razor" \) | \
grep -vE "obj|bin|TestResults|Properties|migrations" | \
while read -r file; do
    {
        echo "================================================================================"
        echo "FILE: $file"
        echo "================================================================================"
        cat "$file"
        echo -e "\n\n--- END OF FILE ---\n"
    } >> "$DUMP_FILE"
done

FILE_COUNT=$(grep -c "FILE: " "$DUMP_FILE")
echo "Exported $FILE_COUNT files to $DUMP_FILE"

# --- Phase 2: DeepSeek Analysis ---
echo ""
echo "=============================================="
echo "  Phase 2: DeepSeek-R1 Core Logic Analysis"
echo "=============================================="
echo "Analyzing using $MODEL..."

# The Prompt
PROMPT="You are a senior .NET Architect. Attached is a lean project export containing only source code. 
Focus your analysis EXCLUSIVELY on:
1. src/MyBlog.Infrastructure/Services/AuthService.cs
2. src/MyBlog.Core/Services/MarkdownService.cs

Review them for:
- Security vulnerabilities (especially in AuthService)
- Performance bottlenecks in Markdown parsing
- Better adherence to Clean Architecture
- Potential bugs or edge cases

Provide specific code snippets for improvements. 
Format your output as a clean Markdown report.

PROJECT EXPORT DATA:
$(cat $DUMP_FILE)"

echo "Thinking... (This may take a minute on your 5700 XT)"

# Pipe the prompt to Ollama and save the result
echo "$PROMPT" | ollama run "$MODEL" > "$OUTPUT_FILE"

echo ""
echo "Analysis Complete!"
echo "Report saved to: $OUTPUT_FILE"
echo "=============================================="



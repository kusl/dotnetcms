#!/bin/bash

# =============================================================================
# analyze-core.sh - Comprehensive Project Analysis
# =============================================================================

# Configuration
OUTPUT_DIR="docs/llm"
DUMP_FILE="$OUTPUT_DIR/source.txt"
MODEL="deepseek-r1:8b"
OUTPUT_FILE="$OUTPUT_DIR/custom_llm_response.md"

mkdir -p "$OUTPUT_DIR"

# --- Phase 1: Comprehensive Export ---
echo "=============================================="
echo "  Phase 1: Generating Global Project Export"
echo "=============================================="
# Clear the dump file
> "$DUMP_FILE"

# 1. Generate a Project Map (Essential for LLM navigation)
echo "PROJECT STRUCTURE MAP:" >> "$DUMP_FILE"
find src -type f \( -name "*.cs" -o -name "*.props" -o -name "*.csproj" -o -name "*.razor" -o -name "*.json"  \) | grep -vE "obj|bin|TestResults|Properties|migrations" >> "$DUMP_FILE"
echo -e "\n\n" >> "$DUMP_FILE"

# 2. Pack all source files
find src -type f \( -name "*.cs" -o -name "*.razor" -o -name "*.props" -o -name "*.csproj" -o -name "*.json" \) | \
grep -vE "obj|bin|TestResults|Properties|migrations" | \
while read -r file; do
    {
        echo "================================================================================"
        echo "FILE PATH: $file"
        echo "================================================================================"
        cat "$file"
        echo -e "\n\n--- END OF FILE ---\n"
    } >> "$DUMP_FILE"
done

FILE_COUNT=$(grep -c "FILE PATH: " "$DUMP_FILE")
echo "Exported $FILE_COUNT files to $DUMP_FILE"

# --- Phase 2: DeepSeek Holistic Analysis ---
echo ""
echo "=============================================="
echo "  Phase 2: DeepSeek-R1 Global Architectural Review"
echo "=============================================="
echo "Analyzing using $MODEL..."

# The Prompt - Shifted from specific files to global architecture
PROMPT="You are a senior .NET Solution Architect. Attached is the full source code for a .NET 10 Blazor server project.

Perform a COMPREHENSIVE review of the entire project. Do not limit yourself to specific files.

read every single line in the prompt very carefully and very thoroughly. 
do not skip any line at all.

Review the solution for:
1. **Architectural Integrity**: Are the boundaries between MyBlog.Core, Infrastructure, and Web being respected? Check for leaked concerns (e.g., UI logic in Core or DB logic in Web).
2. **Security Deep-Dive**: Audit the end-to-end Auth flow, including Middleware, AuthService, and how identity is handled in the Razor components.
3. **Data Patterns**: Evaluate the Repository implementations and DbContext usage for potential performance issues (N+1 queries, lack of async, etc.).
4. **Code Quality**: Identify redundant logic that could be moved to shared services or base classes.
5. **Edge Cases**: Look for missing error handling in the new .NET 10 features you find.

Format your output as a professional architectural audit report with:
- Executive Summary
- Critical Findings (Security/Bugs)
- Architectural Recommendations
- Specific Code Refactoring Examples

PROJECT EXPORT DATA:
$(cat $DUMP_FILE)"

echo "Thinking... (This will take longer as DeepSeek processes the full codebase on your 5700 XT)"

# Pipe the prompt to Ollama and save the result
echo "$PROMPT" | ollama run "$MODEL" > "$OUTPUT_FILE"

echo ""
echo "Analysis Complete!"
echo "Full Report saved to: $OUTPUT_FILE"
echo "=============================================="



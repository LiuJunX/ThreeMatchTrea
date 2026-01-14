#!/bin/bash
set -e

echo "============================================"
echo " Match3 Test Coverage Report Generator"
echo "============================================"
echo ""

COVERAGE_DIR="coverage-report"

echo "[1/4] Cleaning previous coverage data..."
rm -rf "$COVERAGE_DIR" TestResults
mkdir -p "$COVERAGE_DIR"

echo "[2/4] Running tests with coverage collection..."
dotnet test --collect:"XPlat Code Coverage" --results-directory:"$COVERAGE_DIR" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

echo "[3/4] Finding coverage files..."
COVERAGE_FILE=$(find "$COVERAGE_DIR" -name "coverage.cobertura.xml" -type f | head -1)

if [ -z "$COVERAGE_FILE" ]; then
    echo "[WARNING] No coverage file found!"
    exit 1
fi

echo "Found coverage file: $COVERAGE_FILE"

echo "[4/4] Generating HTML report..."
if ! command -v reportgenerator &> /dev/null; then
    echo "Installing ReportGenerator..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

reportgenerator -reports:"$COVERAGE_FILE" -targetdir:"$COVERAGE_DIR/html" -reporttypes:Html\;TextSummary

echo ""
echo "============================================"
echo " Coverage Report Generated!"
echo "============================================"
echo ""
echo "HTML Report: $COVERAGE_DIR/html/index.html"
echo ""

if [ -f "$COVERAGE_DIR/html/Summary.txt" ]; then
    echo "--- Summary ---"
    cat "$COVERAGE_DIR/html/Summary.txt"
fi

# Try to open in browser (works on most systems)
if command -v xdg-open &> /dev/null; then
    xdg-open "$COVERAGE_DIR/html/index.html" 2>/dev/null || true
elif command -v open &> /dev/null; then
    open "$COVERAGE_DIR/html/index.html" 2>/dev/null || true
fi

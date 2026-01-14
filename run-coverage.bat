@echo off
setlocal enabledelayedexpansion

echo ============================================
echo  Match3 Test Coverage Report Generator
echo ============================================
echo.

set COVERAGE_DIR=coverage-report
set COVERAGE_FILE=%COVERAGE_DIR%\coverage.cobertura.xml

echo [1/4] Cleaning previous coverage data...
if exist "%COVERAGE_DIR%" rmdir /s /q "%COVERAGE_DIR%"
if exist "TestResults" rmdir /s /q "TestResults"
mkdir "%COVERAGE_DIR%"

echo [2/4] Running tests with coverage collection...
dotnet test --collect:"XPlat Code Coverage" --results-directory:"%COVERAGE_DIR%" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Tests failed. Coverage report not generated.
    exit /b 1
)

echo [3/4] Merging coverage files...
for /r "%COVERAGE_DIR%" %%f in (coverage.cobertura.xml) do (
    copy "%%f" "%COVERAGE_FILE%" >nul 2>&1
    goto :found
)
:found

if not exist "%COVERAGE_FILE%" (
    echo [WARNING] No coverage file found. Checking for any xml files...
    for /r "%COVERAGE_DIR%" %%f in (*.xml) do (
        echo Found: %%f
    )
)

echo [4/4] Generating HTML report...
dotnet tool list -g | findstr "reportgenerator" >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo Installing ReportGenerator...
    dotnet tool install -g dotnet-reportgenerator-globaltool
)

for /r "%COVERAGE_DIR%" %%f in (coverage.cobertura.xml) do (
    reportgenerator -reports:"%%f" -targetdir:"%COVERAGE_DIR%\html" -reporttypes:Html;TextSummary
    goto :done
)
:done

echo.
echo ============================================
echo  Coverage Report Generated!
echo ============================================
echo.
echo HTML Report: %COVERAGE_DIR%\html\index.html
echo.

if exist "%COVERAGE_DIR%\html\Summary.txt" (
    echo --- Summary ---
    type "%COVERAGE_DIR%\html\Summary.txt"
)

echo.
echo Opening report in browser...
start "" "%COVERAGE_DIR%\html\index.html"

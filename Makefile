# Match3 Project Makefile
# Cross-platform development commands
# Usage: make <target>

.PHONY: all build build-release test test-all test-verbose clean clean-coverage clean-all coverage coverage-quick help restore unity sync-unity format format-check outdated update-packages

# Default target
all: build test

# ============================================
#  Build Commands
# ============================================

## Restore NuGet packages
restore:
	dotnet restore

## Build the solution
build: restore
	dotnet build --no-restore

## Build in Release mode
build-release: restore
	dotnet build --no-restore -c Release

# ============================================
#  Test Commands
# ============================================

## Run tests (excludes Performance and Slow)
test: build
	dotnet test --no-build --filter "Category!=Performance&Category!=Slow"

## Run all tests (including performance and slow)
test-all: build
	dotnet test --no-build

## Run tests with verbose output
test-verbose: build
	dotnet test --no-build --logger "console;verbosity=detailed" --filter "Category!=Performance&Category!=Slow"

## Run tests and generate coverage report
coverage: build
ifeq ($(OS),Windows_NT)
	@call run-coverage.bat
else
	@chmod +x run-coverage.sh && ./run-coverage.sh
endif

## Run tests with coverage (quick, no HTML report)
coverage-quick: build
	dotnet test --no-build --collect:"XPlat Code Coverage" --results-directory:coverage-report

# ============================================
#  Clean Commands
# ============================================

## Clean build artifacts
clean:
	dotnet clean
	@echo Removing bin/obj directories...
ifeq ($(OS),Windows_NT)
	@for /d /r . %%d in (bin,obj) do @if exist "%%d" rmdir /s /q "%%d" 2>nul
else
	@find . -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
endif

## Clean coverage reports
clean-coverage:
ifeq ($(OS),Windows_NT)
	@if exist coverage-report rmdir /s /q coverage-report
	@if exist TestResults rmdir /s /q TestResults
else
	@rm -rf coverage-report TestResults
endif

## Clean everything
clean-all: clean clean-coverage

# ============================================
#  Unity Commands
# ============================================

## Build and sync DLLs to Unity
unity:
	dotnet build src/Match3.Presentation -c Release

## Alias for unity
sync-unity: unity

# ============================================
#  Utility Commands
# ============================================

## Check code format (requires dotnet format)
format-check:
	dotnet format --verify-no-changes

## Apply code formatting
format:
	dotnet format

## List outdated packages
outdated:
	dotnet list package --outdated

## Update all packages to latest
update-packages:
	dotnet outdated --upgrade

# ============================================
#  Help
# ============================================

## Show this help message
help:
	@echo.
	@echo Match3 Project - Available Commands
	@echo ====================================
	@echo.
	@echo BUILD:
	@echo   make build          - Build the solution
	@echo   make build-release  - Build in Release mode
	@echo   make restore        - Restore NuGet packages
	@echo.
	@echo TEST:
	@echo   make test           - Run tests (skip Performance/Slow)
	@echo   make test-all       - Run all tests including perf/slow
	@echo   make test-verbose   - Run tests with detailed output
	@echo   make coverage       - Run tests and generate HTML coverage report
	@echo   make coverage-quick - Run tests with coverage (no HTML)
	@echo.
	@echo CLEAN:
	@echo   make clean          - Clean build artifacts
	@echo   make clean-coverage - Clean coverage reports
	@echo   make clean-all      - Clean everything
	@echo.
	@echo UNITY:
	@echo   make unity          - Build and sync DLLs to Unity
	@echo.
	@echo UTILITY:
	@echo   make format         - Apply code formatting
	@echo   make format-check   - Check code formatting
	@echo   make outdated       - List outdated packages
	@echo.

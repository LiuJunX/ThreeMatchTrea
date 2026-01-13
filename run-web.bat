@echo off
set PORT=5015
set APP_NAME=Match3.Web.exe

echo [1/3] Killing existing processes (%APP_NAME%)...
taskkill /F /IM %APP_NAME% >nul 2>&1

echo [2/3] Checking port %PORT% for lingering processes...
for /f "tokens=5" %%a in ('netstat -aon ^| findstr :%PORT%') do (
    echo Port %PORT% is in use by PID %%a. Killing it...
    taskkill /F /PID %%a >nul 2>&1
)

echo Waiting for file locks to release...
timeout /t 2 /nobreak >nul

echo [3/3] Starting Web Project with Hot Reload...
dotnet watch --project src/Match3.Web/Match3.Web.csproj
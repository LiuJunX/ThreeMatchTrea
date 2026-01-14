#!/bin/bash
set -e

PORT=5015
PROJECT="src/Match3.Web/Match3.Web.csproj"

echo "[1/3] Checking for existing processes on port $PORT..."
if command -v lsof &> /dev/null; then
    PID=$(lsof -ti:$PORT 2>/dev/null || true)
    if [ -n "$PID" ]; then
        echo "Port $PORT is in use by PID $PID. Killing it..."
        kill -9 $PID 2>/dev/null || true
    fi
elif command -v fuser &> /dev/null; then
    fuser -k $PORT/tcp 2>/dev/null || true
fi

echo "[2/3] Waiting for port to be released..."
sleep 1

echo "[3/3] Starting Web Project with Hot Reload..."
dotnet watch --project "$PROJECT"

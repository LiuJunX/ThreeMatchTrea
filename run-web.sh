#!/bin/bash
set -e

# 切换到脚本所在目录
cd "$(dirname "$0")"

PORT=5015
PROJECT="src/Match3.Web/Match3.Web.csproj"
URLS="http://localhost:$PORT"

# 检查 --lan 参数
if [ "$1" = "--lan" ]; then
    URLS="http://0.0.0.0:$PORT"
    echo "LAN mode enabled - accessible from other devices"
fi

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
dotnet watch --project "$PROJECT" --urls "$URLS"

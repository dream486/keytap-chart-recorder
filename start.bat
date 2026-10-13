@echo off
cd /d "%~dp0"
set "FOUR_NODE=node"
where node >nul 2>nul
if errorlevel 1 (
  set "FOUR_NODE=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe"
)
if not exist "%FOUR_NODE%" (
  if not "%FOUR_NODE%"=="node" (
    echo Node.js was not found. Opening the offline HTML version.
    start "" "%~dp0index.html"
    exit /b
  )
)
start "" "http://127.0.0.1:4173"
"%FOUR_NODE%" server.cjs
pause

@echo off
setlocal
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Sync-ReaderLibs.ps1" %*
exit /b %ERRORLEVEL%

@echo off
REM Runs Run-Cyberland.ps1 with Bypass so it works when execution policy blocks unsigned scripts (common in elevated shells).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-Cyberland.ps1" %*

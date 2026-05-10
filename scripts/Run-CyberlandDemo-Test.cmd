@echo off
REM Runs Run-CyberlandDemo-Test.ps1 with Bypass so unsigned scripts work under strict execution policy (e.g. AllSigned).
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-CyberlandDemo-Test.ps1" %*

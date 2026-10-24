@echo off
title Apply latest Notewll tray update
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\apply_tray_update.ps1"
if errorlevel 1 (
  echo.
  echo Notewll update failed. See output\tray-update.log for details.
)
pause
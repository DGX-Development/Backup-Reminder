@echo off
setlocal
set "REMINDER_PWSH="
for /f "delims=" %%P in ('where pwsh.exe 2^>nul') do if not defined REMINDER_PWSH set "REMINDER_PWSH=%%P"
if not defined REMINDER_PWSH if exist "%ProgramFiles%\PowerShell\7\pwsh.exe" set "REMINDER_PWSH=%ProgramFiles%\PowerShell\7\pwsh.exe"
if not defined REMINDER_PWSH (
  echo PowerShell 7 is required. Install it, then open Backup Reminder again.
  pause
  exit /b 1
)
start "" /b "%REMINDER_PWSH%" -NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File "%~dp0Backup-Reminder.ps1" -Development

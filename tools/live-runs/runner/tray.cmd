@echo off
rem Starts the run queue tray app (tray.ps1) with no console window. Arguments pass through, such as -Work.
start "" powershell.exe -NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File "%~dp0tray.ps1" %*

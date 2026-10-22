@echo off
rem Install / update BepInEx + DBModKit into the game folder.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\install.ps1"
pause

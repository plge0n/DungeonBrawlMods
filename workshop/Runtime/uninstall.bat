@echo off
rem Disable mods: remove the BepInEx loader from the game folder.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer\install.ps1" -Uninstall
pause

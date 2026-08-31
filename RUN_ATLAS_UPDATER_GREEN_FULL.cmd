@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','C:\dev\atlas-update\scripts\engine\_RUN_atlas_updater_operator_green_v1.ps1','-RepoRoot','C:\dev\atlas-update'"
echo Approve the UAC prompt. The elevated window proves the full updater green (net-zero) and closes.

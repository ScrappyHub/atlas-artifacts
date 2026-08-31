@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','C:\dev\atlas-update\scripts\engine\_RUN_atlas_updater_windows_green_v1.ps1','-RepoRoot','C:\dev\atlas-update'"
echo Approve the UAC prompt. The elevated window runs the block/reconcile/unblock cycle and closes.

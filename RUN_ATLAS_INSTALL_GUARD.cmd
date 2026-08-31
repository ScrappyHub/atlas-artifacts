@echo off
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-NoExit','-File','C:\dev\atlas-update\scripts\engine\atlas_install_reconcile_task_v1.ps1','-RepoRoot','C:\dev\atlas-update','-Op','install'"
echo Requested elevation. Approve the UAC prompt; the elevated window shows results.

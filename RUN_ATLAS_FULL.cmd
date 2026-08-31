@echo off
cd /d C:\dev\atlas-update
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\dev\atlas-update\scripts\_RUN_atlas_tier0_signed_green_v1.ps1" -RepoRoot "C:\dev\atlas-update" > "C:\dev\atlas-update\proofs\audit\LAST_RUN.txt" 2>&1
echo DONE - see proofs\audit\LAST_RUN.txt

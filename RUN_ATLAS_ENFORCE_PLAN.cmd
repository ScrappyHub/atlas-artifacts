@echo off
cd /d C:\dev\atlas-update
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\dev\atlas-update\scripts\engine\atlas_enforce_block_v1.ps1" -RepoRoot "C:\dev\atlas-update" -Target "Opera GX scheduled" -Action block -Mode plan > "C:\dev\atlas-update\proofs\audit\LAST_ENFORCE_PLAN.txt" 2>&1
echo DONE - proofs\audit\LAST_ENFORCE_PLAN.txt

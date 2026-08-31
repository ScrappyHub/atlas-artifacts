@echo off
cd /d C:\dev\atlas-update
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\dev\atlas-update\scripts\engine\atlas_software_inventory_full_v1.ps1" -RepoRoot "C:\dev\atlas-update" > "C:\dev\atlas-update\proofs\audit\LAST_INVENTORY.txt" 2>&1
echo DONE - proofs\audit\LAST_INVENTORY.txt

@echo off
cd /d C:\dev\atlas-update
set L=C:\dev\atlas-update\proofs\audit\LAST_UPDATE.txt
echo === held package (expect BLOCKED) === > "%L%"
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\dev\atlas-update\scripts\engine\atlas_update_apply_v1.ps1" -RepoRoot "C:\dev\atlas-update" -Id "BlenderFoundation.Blender" -Mode apply -IUnderstand >> "%L%" 2>&1
echo === normal package (dry-run, allowed) === >> "%L%"
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\dev\atlas-update\scripts\engine\atlas_update_apply_v1.ps1" -RepoRoot "C:\dev\atlas-update" -Id "7zip.7zip" -Mode dry >> "%L%" 2>&1
echo DONE - proofs\audit\LAST_UPDATE.txt

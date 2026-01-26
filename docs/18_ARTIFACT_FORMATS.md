# Artifact Formats

All artifacts are written under:
artifacts/<YYYY>/<MM>/<DD>/<run_id>/

Required:
- inventory.json
- candidates.json
- plan.json
- apply.jsonl
- verify.json
- summary.json
- manifest.json (sha256 of each file + chain pointer)

## Hash Chain Ready
manifest.json includes:
- previous_manifest_sha256 (nullable for first run)
- current_manifest_sha256 (computed over manifest payload excluding this field)

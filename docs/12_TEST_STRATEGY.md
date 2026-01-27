<<<<<<< HEAD
# Test Strategy

## Unit Tests
- policy evaluation
- plan building determinism
- artifact hashing and manifest generation

## Integration Tests (MVP)
- winget inventory parsing
- candidate resolution
- dry-run plan generation

## Golden Fixtures
- pinned sample inventories
- pinned candidate sets
- expected plans (hash stable)
=======
# Test Strategy

- Unit: policy eval, plan determinism, hashing/manifest
- Integration: winget parsing, scan run creation
- Golden fixtures: pinned outputs → stable plan hashes
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

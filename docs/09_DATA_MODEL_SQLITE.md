<<<<<<< HEAD
# Local Data Model (SQLite)

Atlas Update stores policy, registry, and run metadata locally.
Artifacts themselves are stored as files; DB stores pointers + hashes.
=======
# Local Data Model (SQLite)

SQLite stores:
- runs metadata
- artifact pointers + sha256
- (later) policies, accounts, subscriptions, device enrollments
Artifacts are files; DB points to them.
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

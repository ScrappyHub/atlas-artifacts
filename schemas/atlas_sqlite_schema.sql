<<<<<<< HEAD
-- schemas/atlas_sqlite_schema.sql

PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS users (
  user_id TEXT PRIMARY KEY,
  display_name TEXT,
  role TEXT NOT NULL CHECK(role IN ('admin','maintainer','user','auditor'))
);

CREATE TABLE IF NOT EXISTS feature_flags (
  flag_key TEXT PRIMARY KEY,
  default_value INTEGER NOT NULL CHECK(default_value IN (0,1))
);

CREATE TABLE IF NOT EXISTS user_feature_flags (
  user_id TEXT NOT NULL,
  flag_key TEXT NOT NULL,
  value INTEGER NOT NULL CHECK(value IN (0,1)),
  PRIMARY KEY (user_id, flag_key),
  FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
  FOREIGN KEY (flag_key) REFERENCES feature_flags(flag_key) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS engines (
  engine_id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  supported_os TEXT NOT NULL CHECK(supported_os IN ('windows','macos','linux')),
  trust_level TEXT NOT NULL CHECK(trust_level IN ('system','vendor','community')),
  requires_admin INTEGER NOT NULL CHECK(requires_admin IN (0,1)),
  allowed_by_default INTEGER NOT NULL CHECK(allowed_by_default IN (0,1))
);

CREATE TABLE IF NOT EXISTS engine_capabilities (
  engine_id TEXT NOT NULL,
  capability TEXT NOT NULL CHECK(capability IN ('inventory','check_updates','download','install','rollback','verify')),
  PRIMARY KEY (engine_id, capability),
  FOREIGN KEY (engine_id) REFERENCES engines(engine_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS apps (
  app_id TEXT PRIMARY KEY,         -- canonical internal ID
  display_name TEXT NOT NULL,
  vendor TEXT,
  canonical_key TEXT UNIQUE        -- optional stable key mapping
);

CREATE TABLE IF NOT EXISTS installed_apps (
  device_id TEXT NOT NULL,
  app_id TEXT NOT NULL,
  version TEXT NOT NULL,
  install_source TEXT,             -- engine/source detail
  last_seen_at TEXT NOT NULL,
  PRIMARY KEY (device_id, app_id),
  FOREIGN KEY (app_id) REFERENCES apps(app_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS app_policies (
  device_id TEXT NOT NULL,
  app_id TEXT NOT NULL,
  mode TEXT NOT NULL CHECK(mode IN ('AUTO','NOTIFY','NEVER')),
  ring TEXT NOT NULL CHECK(ring IN ('stable','beta')),
  source_lock_engine_id TEXT,      -- optional
  only_ac_power INTEGER NOT NULL CHECK(only_ac_power IN (0,1)),
  min_battery_percent INTEGER NOT NULL CHECK(min_battery_percent BETWEEN 0 AND 100),
  only_unmetered INTEGER NOT NULL CHECK(only_unmetered IN (0,1)),
  requires_approval INTEGER NOT NULL CHECK(requires_approval IN (0,1)),
  allowed_time_windows_json TEXT,  -- JSON string
  PRIMARY KEY (device_id, app_id),
  FOREIGN KEY (app_id) REFERENCES apps(app_id) ON DELETE CASCADE,
  FOREIGN KEY (source_lock_engine_id) REFERENCES engines(engine_id)
);

CREATE TABLE IF NOT EXISTS global_policy (
  device_id TEXT PRIMARY KEY,
  global_auto_updates INTEGER NOT NULL CHECK(global_auto_updates IN (0,1)),
  require_approval_for_installs INTEGER NOT NULL CHECK(require_approval_for_installs IN (0,1))
);

CREATE TABLE IF NOT EXISTS runs (
  run_id TEXT PRIMARY KEY,
  device_id TEXT NOT NULL,
  run_type TEXT NOT NULL CHECK(run_type IN ('scan_run','apply_run')),
  created_at TEXT NOT NULL,
  initiated_by_user_id TEXT,
  policy_snapshot_json TEXT NOT NULL,
  engine_set_json TEXT NOT NULL,
  outcome TEXT NOT NULL CHECK(outcome IN ('success','partial','failed','deferred')),
  summary_json TEXT NOT NULL,
  FOREIGN KEY (initiated_by_user_id) REFERENCES users(user_id)
);

CREATE TABLE IF NOT EXISTS run_artifacts (
  run_id TEXT NOT NULL,
  artifact_key TEXT NOT NULL,      -- e.g. inventory.json
  path TEXT NOT NULL,
  sha256 TEXT NOT NULL,
  created_at TEXT NOT NULL,
  PRIMARY KEY (run_id, artifact_key),
  FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
);

-- Accounts & subscriptions (Phase 1 local-ready; Phase 2 server-backed)

CREATE TABLE IF NOT EXISTS accounts (
  account_id TEXT PRIMARY KEY,
  created_at TEXT NOT NULL,
  display_name TEXT
);

CREATE TABLE IF NOT EXISTS subscriptions (
  account_id TEXT NOT NULL,
  tier TEXT NOT NULL CHECK(tier IN ('tier1_personal','tier2_pro','tier3_enterprise')),
  max_devices INTEGER NOT NULL CHECK(max_devices >= 1),
  status TEXT NOT NULL CHECK(status IN ('trial','active','expired','grace')),
  expires_at TEXT,
  grace_ends_at TEXT,
  entitlements_json TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  PRIMARY KEY (account_id),
  FOREIGN KEY (account_id) REFERENCES accounts(account_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS devices (
  device_id TEXT PRIMARY KEY,
  created_at TEXT NOT NULL,
  device_nickname TEXT
);

CREATE TABLE IF NOT EXISTS device_enrollments (
  account_id TEXT NOT NULL,
  device_id TEXT NOT NULL,
  state TEXT NOT NULL CHECK(state IN ('pending','active','deactivated','revoked','expired')),
  enrolled_at TEXT NOT NULL,
  last_seen_at TEXT,
  PRIMARY KEY (account_id, device_id),
  FOREIGN KEY (account_id) REFERENCES accounts(account_id) ON DELETE CASCADE,
  FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE
);
=======
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS runs (
  run_id TEXT PRIMARY KEY,
  run_type TEXT NOT NULL CHECK(run_type IN ('scan_run','apply_run')),
  created_at TEXT NOT NULL,
  outcome TEXT NOT NULL CHECK(outcome IN ('success','partial','failed','deferred')),
  summary_json TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS run_artifacts (
  run_id TEXT NOT NULL,
  artifact_key TEXT NOT NULL,
  path TEXT NOT NULL,
  sha256 TEXT NOT NULL,
  created_at TEXT NOT NULL,
  PRIMARY KEY (run_id, artifact_key),
  FOREIGN KEY (run_id) REFERENCES runs(run_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS accounts (
  account_id TEXT PRIMARY KEY,
  created_at TEXT NOT NULL,
  display_name TEXT
);

CREATE TABLE IF NOT EXISTS subscriptions (
  account_id TEXT NOT NULL,
  tier TEXT NOT NULL CHECK(tier IN ('tier1_personal','tier2_pro','tier3_enterprise')),
  max_devices INTEGER NOT NULL CHECK(max_devices >= 1),
  status TEXT NOT NULL CHECK(status IN ('trial','active','expired','grace')),
  expires_at TEXT,
  grace_ends_at TEXT,
  entitlements_json TEXT NOT NULL,
  updated_at TEXT NOT NULL,
  PRIMARY KEY (account_id),
  FOREIGN KEY (account_id) REFERENCES accounts(account_id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS devices (
  device_id TEXT PRIMARY KEY,
  created_at TEXT NOT NULL,
  device_nickname TEXT
);

CREATE TABLE IF NOT EXISTS device_enrollments (
  account_id TEXT NOT NULL,
  device_id TEXT NOT NULL,
  state TEXT NOT NULL CHECK(state IN ('pending','active','deactivated','revoked','expired')),
  enrolled_at TEXT NOT NULL,
  last_seen_at TEXT,
  PRIMARY KEY (account_id, device_id),
  FOREIGN KEY (account_id) REFERENCES accounts(account_id) ON DELETE CASCADE,
  FOREIGN KEY (device_id) REFERENCES devices(device_id) ON DELETE CASCADE
);
>>>>>>> 9673112 (chore: bootstrap Atlas Update canonical repo (docs, schemas, agent skeleton))

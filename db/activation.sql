create table if not exists atlas_activation_devices (
  tenant_id text not null,
  license_id text not null,
  device_id text not null,
  hardware_fingerprint text not null default '',
  agent_version text not null default '',
  created_utc timestamptz not null default now(),
  last_seen_utc timestamptz not null default now(),
  primary key (tenant_id, license_id, device_id)
);

create table if not exists atlas_activation_audit (
  id bigserial primary key,
  tenant_id text not null,
  license_id text not null,
  device_id text not null,
  action text not null,
  created_utc timestamptz not null default now()
);

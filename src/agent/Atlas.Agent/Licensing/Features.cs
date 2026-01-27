using System;

namespace Atlas.Agent.Licensing;

[Flags]
public enum Feature
{
    None       = 0,
    WingetScan = 1 << 0,
    Artifacts  = 1 << 1,
    Rollback   = 1 << 2,
    ExportLogs = 1 << 3,
    Automation = 1 << 4
}

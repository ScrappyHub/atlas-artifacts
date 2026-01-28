using System;

namespace Atlas.Agent.Licensing;

public sealed class LicenseDeniedException : Exception
{
    public LicenseDeniedException(string message) : base(message) { }
}
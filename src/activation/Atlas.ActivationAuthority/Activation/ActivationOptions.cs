namespace Atlas.ActivationAuthority.Activation;

public sealed class ActivationOptions
{
    public int GrantTtlHours { get; set; } = 24;
    public string SigningKey { get; set; } = "dev-only-change-me";
    public int DeviceLimit { get; set; } = 5;
}

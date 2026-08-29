namespace AllenKerberAutoSupply.Options;

public sealed class ExternalAuthOptions
{
    public const string SectionName = "ExternalAuth";
    public ProviderOptions Google { get; set; } = new();
    public ProviderOptions Microsoft { get; set; } = new();
    public ProviderOptions Facebook { get; set; } = new();
}

public sealed class ProviderOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

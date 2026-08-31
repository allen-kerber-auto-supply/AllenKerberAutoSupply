namespace AllenKerberAutoSupply.Options;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>
    /// Resend API key. When empty, <see cref="SecretName"/> is used to load the
    /// key from Google Cloud Secret Manager at startup.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Name of the Secret Manager secret holding the Resend API key.
    /// </summary>
    public string SecretName { get; set; } = string.Empty;

    /// <summary>
    /// "From" address used when sending invoice emails.
    /// </summary>
    public string FromAddress { get; set; } = string.Empty;
}

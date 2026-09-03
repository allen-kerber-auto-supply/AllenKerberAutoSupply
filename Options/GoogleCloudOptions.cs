namespace AllenKerberAutoSupply.Options;

public sealed class GoogleCloudOptions
{
    public const string SectionName = "GoogleCloud";
    public string ProjectId { get; set; } = string.Empty;
    public string FirestoreDatabase { get; set; } = string.Empty;
    public string ImageBucket { get; set; } = string.Empty;
    public string EventarcBusName { get; set; } = "allenkerber-bus";
}

using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Services;

public interface ITrendAnalysisService
{
    Task<TrendAnalysisResponse> AnalyzeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string aggregateBy,
        CancellationToken cancellationToken = default);
}

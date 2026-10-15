using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface ITrendRepository
{
    Task<TrendData?> FindAsync(string rangeKey, CancellationToken cancellationToken = default);
    Task<TrendData> CreateOrGetAsync(TrendData trend, CancellationToken cancellationToken = default);
}

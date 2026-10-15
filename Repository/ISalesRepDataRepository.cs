using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface ISalesRepDataRepository
{
    Task<IReadOnlyList<SalesRep>> GetSalesRepListAsync(CancellationToken cancellationToken = default);
    Task<bool> InsertSalesRepAsync(string repName, string repEmail, CancellationToken cancellationToken = default);
    Task<bool> DeleteSalesRepAsync(string repEmail, CancellationToken cancellationToken = default);
}

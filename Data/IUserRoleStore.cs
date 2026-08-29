using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IUserRoleStore
{
    Task<UserAccount?> FindAsync(string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken cancellationToken);
    Task UpsertAsync(UserAccount user, CancellationToken cancellationToken);
    Task DeleteAsync(string email, CancellationToken cancellationToken);
}

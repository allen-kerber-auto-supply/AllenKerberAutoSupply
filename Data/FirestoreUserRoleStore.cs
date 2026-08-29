using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreUserRoleStore(FirestoreDb firestore, ILogger<FirestoreUserRoleStore> logger) : IUserRoleStore
{
    private CollectionReference Users => firestore.Collection("users");

    public async Task<UserAccount?> FindAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = Normalize(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return null;

        var snapshot = await Users.Document(normalizedEmail).GetSnapshotAsync(cancellationToken);
        if (snapshot.Exists)
            return snapshot.ConvertTo<UserAccount>();

        var byEmailQuery = await Users
            .WhereEqualTo(nameof(UserAccount.Email), normalizedEmail)
            .Limit(1)
            .GetSnapshotAsync(cancellationToken);

        var document = byEmailQuery.Documents.FirstOrDefault();
        return document is null ? null : document.ConvertTo<UserAccount>();
    }

    public Task UpsertAsync(UserAccount user, CancellationToken cancellationToken)
    {
        user.Email = Normalize(user.Email);
        return Users.Document(Normalize(user.Email)).SetAsync(user, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<UserAccount>> ListAsync(CancellationToken cancellationToken)
    {
        var snapshot = await Users.GetSnapshotAsync(cancellationToken);
        var accounts = new List<UserAccount>(snapshot.Documents.Count);
        foreach (var document in snapshot.Documents)
        {
            try
            {
                accounts.Add(document.ConvertTo<UserAccount>());
            }
            catch (Exception ex)
            {
                // Skip documents that don't match the expected shape instead of failing the whole list.
                logger.LogError(ex, "Failed to read user document {DocumentId}; excluding it from the list.", document.Id);
            }
        }

        return accounts
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Email, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task DeleteAsync(string email, CancellationToken cancellationToken)
        => Users.Document(Normalize(email)).DeleteAsync(cancellationToken: cancellationToken);

    public static string Normalize(string email) => string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
}

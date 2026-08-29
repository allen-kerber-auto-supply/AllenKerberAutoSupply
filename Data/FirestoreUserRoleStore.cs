using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreUserRoleStore(FirestoreDb firestore) : IUserRoleStore
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

    public static string Normalize(string email) => string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim().ToLowerInvariant();
}

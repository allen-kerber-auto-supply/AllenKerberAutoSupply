using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreUserRoleStore(FirestoreDb firestore) : IUserRoleStore
{
    private CollectionReference Users => firestore.Collection("users");

    public async Task<UserAccount?> FindAsync(string email, CancellationToken cancellationToken)
    {
        var snapshot = await Users.Document(Normalize(email)).GetSnapshotAsync(cancellationToken);
        return snapshot.Exists ? snapshot.ConvertTo<UserAccount>() : null;
    }

    public Task UpsertAsync(UserAccount user, CancellationToken cancellationToken)
        => Users.Document(Normalize(user.Email)).SetAsync(user, cancellationToken: cancellationToken);

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Models;

[FirestoreData]
public sealed class UserAccount
{
    [FirestoreProperty] public string Email { get; set; } = string.Empty;
    [FirestoreProperty] public string DisplayName { get; set; } = string.Empty;
    [FirestoreProperty] public string PasswordHash { get; set; } = string.Empty;
    [FirestoreProperty] public bool MustChangePassword { get; set; }
    [FirestoreProperty] public IReadOnlyList<string> Roles { get; set; } = [];
}

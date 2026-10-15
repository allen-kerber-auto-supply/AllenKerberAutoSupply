using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Data;

public sealed class FirestoreSalesRepRepository(FirestoreDb firestore) : ISalesRepDataRepository
{
    public async Task<IReadOnlyList<SalesRep>> GetSalesRepListAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await firestore.Collection("sales_reps")
            .WhereEqualTo(nameof(SalesRep.Status), "A")
            .GetSnapshotAsync(cancellationToken);

        return snapshot.Documents
            .Select(SalesFirestoreMapper.MapSalesRep)
            .Where(r => !string.IsNullOrWhiteSpace(r.RepEmail))
            .OrderBy(r => r.RepName)
            .ToList();
    }

    public async Task<bool> InsertSalesRepAsync(string repName, string repEmail, CancellationToken cancellationToken = default)
    {
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var allSnapshot = await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken);
        bool exists = allSnapshot.Documents.Any(d =>
        {
            var r = SalesFirestoreMapper.MapSalesRep(d);
            return string.Equals(r.RepEmail, email, StringComparison.OrdinalIgnoreCase);
        });

        if (exists)
            return false;

        int nextId = allSnapshot.Documents.Count > 0
            ? allSnapshot.Documents.Select(d => SalesFirestoreMapper.MapSalesRep(d).Id).DefaultIfEmpty(0).Max() + 1
            : 1;

        var rep = new SalesRep
        {
            Id = nextId,
            RepName = (repName ?? string.Empty).Trim(),
            RepEmail = email,
            Status = "A"
        };

        var docRef = firestore.Collection("sales_reps").Document(email);
        await docRef.SetAsync(rep, cancellationToken: cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSalesRepAsync(string repEmail, CancellationToken cancellationToken = default)
    {
        string email = (repEmail ?? string.Empty).Trim().ToLowerInvariant();
        var allSnapshot = await firestore.Collection("sales_reps").GetSnapshotAsync(cancellationToken);
        var matching = allSnapshot.Documents.Where(d =>
        {
            var r = SalesFirestoreMapper.MapSalesRep(d);
            return string.Equals(r.RepEmail, email, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        if (matching.Count == 0)
            return false;

        foreach (var doc in matching)
        {
            await doc.Reference.UpdateAsync(nameof(SalesRep.Status), "T", cancellationToken: cancellationToken);
        }

        return true;
    }

    // Sales Customers & Account Assignments
}

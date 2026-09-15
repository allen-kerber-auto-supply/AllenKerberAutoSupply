using AllenKerberAutoSupply.Models;

namespace AllenKerberAutoSupply.Data;

public interface IInvoiceUploadReconciliationStore
{
    Task<InvoiceUploadReconciliation> ReconcileStoreAsync(int storeNumber, CancellationToken cancellationToken = default);
    Task ReconcileInvoiceAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default);
    Task ReconcileImageAsync(int storeNumber, string invoiceNumber, CancellationToken cancellationToken = default);
}

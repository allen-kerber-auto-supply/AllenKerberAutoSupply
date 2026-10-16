using System.Data;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Models;
using AllenKerberAutoSupply.Options;
using AllenKerberAutoSupply.Services;
using ExcelDataReader;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ZXing;
using ZXing.Common;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Policy = AuthorizationPolicies.ActiveAccount, Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
public sealed class InvoicesController(
    IInvoiceRepository repository,
    ICustomerRepository customerRepository,
    IInvoiceImageRepository invoiceImageRepository,
    IMisreadBarcodeService misreadBarcodeService,
    IInvoiceStoreCache invoiceStoreCache,
    IUploadProgressEventBus uploadProgressEventBus,
    IOptions<GoogleCloudOptions> googleCloudOptions,
    ILogger<InvoicesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? invoiceNumber, [FromQuery] string? customerNumber, [FromQuery] string? sortKey, [FromQuery] string? sortDirection, [FromQuery] int page = 0, CancellationToken cancellationToken = default)
    {
        page = Math.Max(0, page);
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            string normalizedInvoiceNumber = invoiceNumber.Trim();
            if (normalizedInvoiceNumber.Length < 3 || !normalizedInvoiceNumber.All(char.IsDigit))
            {
                return BadRequest("Invoice number search must contain at least 3 digits.");
            }

            if (!string.IsNullOrWhiteSpace(customerNumber) && int.TryParse(customerNumber, out int custNo))
            {
                return Ok(await repository.GetInvoiceDataByInvoiceNumberAndCustomerAsync(normalizedInvoiceNumber, custNo, sortKey, sortDirection, page, cancellationToken));
            }
            return Ok(await repository.GetInvoiceDataByInvoiceNumberAsync(normalizedInvoiceNumber, sortKey, sortDirection, page, cancellationToken));
        }

        return Ok(await repository.FindAsync(invoiceNumber, customerNumber, sortKey, sortDirection, page, cancellationToken));
    }

    [HttpGet("stores")]
    public async Task<IActionResult> GetStoreNumbers(CancellationToken cancellationToken)
    {
        return Ok(await invoiceStoreCache.GetStoreNumbersAsync(cancellationToken));
    }

    [HttpGet("upload-reconciliation")]
    public async Task<IActionResult> GetUploadReconciliation([FromQuery] int storeNumber, CancellationToken cancellationToken)
    {
        if (storeNumber <= 0)
            return BadRequest("A store number is required.");

        return Ok(await repository.GetUploadReconciliationAsync(storeNumber, cancellationToken));
    }

    [HttpDelete("upload-reconciliation/{storeNumber:int}/invoice/{invoiceNumber}")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    public async Task<IActionResult> DeleteInvoice(int storeNumber, string invoiceNumber, CancellationToken cancellationToken)
    {
        try
        {
            await invoiceImageRepository.DeleteInvoiceImagesAsync(storeNumber, invoiceNumber, cancellationToken);
            await repository.DeleteInvoiceAsync(storeNumber, invoiceNumber, cancellationToken);
            return Ok();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("progress")]
    [AllowAnonymous]
    public IActionResult GetProgress([FromQuery] string operation = "excel")
    {
        var state = uploadProgressEventBus.GetState(operation) ?? new UploadProgressState
        {
            Operation = operation,
            BusName = googleCloudOptions.Value.EventarcBusName,
            Status = "idle",
            Percent = 0,
            Message = "No upload activity is currently running."
        };

        if (string.IsNullOrWhiteSpace(state.BusName))
            state.BusName = googleCloudOptions.Value.EventarcBusName;

        return Ok(state);
    }

    [HttpGet("by-date")]
    public async Task<IActionResult> GetByDate([FromQuery] DateTime beginDate, [FromQuery] DateTime endDate, [FromQuery] int? customerNumber, [FromQuery] int? storeNumber, [FromQuery] string? sortKey, [FromQuery] string? sortDirection, [FromQuery] int page = 0, CancellationToken cancellationToken = default)
    {
        page = Math.Max(0, page);
        if (customerNumber.HasValue)
        {
            return Ok(await repository.GetInvoiceDataByDtmAndCustomerAsync(beginDate, endDate, customerNumber.Value, sortKey, sortDirection, page, cancellationToken));
        }

        return Ok(await repository.GetInvoiceDataByDtmAsync(beginDate, endDate, sortKey, sortDirection, page, cancellationToken, storeNumber));
    }

    [HttpGet("statement")]
    public async Task<IActionResult> GetStatementInvoices([FromQuery] int customerNumber, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string? invoiceNumbers, CancellationToken cancellationToken)
    {
        return Ok(await repository.GetStatementInvoicesAsync(customerNumber, fromDate, toDate, invoiceNumbers ?? string.Empty, cancellationToken));
    }

    [HttpPost("upload-excel")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    public async Task<IActionResult> UploadExcel([FromForm] IFormFile excelFile, [FromForm] int storeNumber, [FromForm] string? uploadId, CancellationToken cancellationToken)
    {
        if (excelFile is null || excelFile.Length == 0)
            return BadRequest("An Excel file is required.");

        string operation;
        try
        {
            operation = GetUploadOperation("excel", uploadId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        uploadProgressEventBus.Reset(operation);
        uploadProgressEventBus.Publish(operation, "in_progress", 0, "Reading the selected Excel file...");
        return await ImportInvoiceFileAsync(excelFile, storeNumber, operation, cancellationToken);
    }

    [HttpPost("upload-csv")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    public async Task<IActionResult> UploadCsv([FromForm] IFormFile csvFile, [FromForm] int storeNumber, [FromForm] string? uploadId, CancellationToken cancellationToken)
    {
        if (csvFile is null || csvFile.Length == 0)
            return BadRequest("A CSV file is required.");

        string operation;
        try
        {
            operation = GetUploadOperation("excel", uploadId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        return await ImportInvoiceFileAsync(csvFile, storeNumber, operation, cancellationToken);
    }

    private static string GetUploadOperation(string operation, string? uploadId)
    {
        if (string.IsNullOrWhiteSpace(uploadId))
            return operation;
        if (uploadId.Length > 80 || uploadId.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
            throw new ArgumentException("The upload ID is invalid.", nameof(uploadId));
        return $"{operation}:{uploadId}";
    }

    private async Task<IActionResult> ImportInvoiceFileAsync(IFormFile file, int storeNumber, string operation, CancellationToken cancellationToken)
    {
        if (storeNumber <= 0)
            return BadRequest("A store number is required.");

        var errors = new List<string>();
        var rows = new List<Dictionary<string, string>>();

        if (string.Equals(Path.GetExtension(file.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = file.OpenReadStream();
            using var parser = new TextFieldParser(stream)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = true
            };
            parser.SetDelimiters(",");

            string[]? headers = null;
            while (!parser.EndOfData)
            {
                var fields = parser.ReadFields();
                if (fields is null || fields.Length == 0)
                    continue;

                if (headers is null)
                {
                    headers = fields.Select(field => field.Trim()).ToArray();
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headers.Length && i < fields.Length; i++)
                {
                    row[headers[i]] = fields[i].Trim();
                }

                if (row.Any())
                    rows.Add(row);
            }
        }
        else
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            await using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            if (dataSet.Tables.Count == 0)
            {
                return BadRequest("The Excel file did not contain any worksheets.");
            }

            foreach (DataTable table in dataSet.Tables)
            {
                if (table.Rows.Count == 0)
                    continue;

                foreach (DataRow row in table.Rows)
                {
                    var rowValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < table.Columns.Count; i++)
                    {
                        var columnName = table.Columns[i].ColumnName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(columnName))
                            continue;

                        var cellValue = Convert.ToString(row[i], CultureInfo.InvariantCulture) ?? string.Empty;
                        rowValues[columnName.Trim()] = cellValue.Trim();
                    }

                    if (rowValues.Any())
                        rows.Add(rowValues);
                }
            }
        }

        var totalInvoiceNumbers = rows
            .Select(row => GetRowValue(row, "invoice_no", "invoiceNo", "invoice number", "invoicenumber", "invoice no"))
            .Where(invoiceNumber => !string.IsNullOrWhiteSpace(invoiceNumber))
            .Select(InvoiceNumberNormalizer.NormalizeForComparison)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var processedInvoiceNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var importedInvoiceNumbers = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var customerInitializations = new ConcurrentDictionary<int, Lazy<Task<bool>>>();
        foreach (var row in rows)
        {
            var invoiceValue = GetRowValue(row, "invoice_no", "invoiceNo", "invoice number", "invoicenumber", "invoice no");
            if (string.IsNullOrWhiteSpace(invoiceValue))
                continue;

            var customerNumber = ParseInt(GetRowValue(row, "customer_no", "customerNo", "customer number", "customer number ", "customernumber")) ?? 0;
            var customer = GetCustomerFromRow(row, customerNumber);
            if (customer is not null)
            {
                customerInitializations.TryAdd(customerNumber, new Lazy<Task<bool>>(
                    () => customerRepository.EnsureCustomerAsync(customer, cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            }
        }

        uploadProgressEventBus.Publish(operation, "in_progress", 0, $"Preparing to import {totalInvoiceNumbers} invoice numbers...", 0, totalInvoiceNumbers);
        const int rowsPerBatch = 100;
        for (var offset = 0; offset < rows.Count; offset += rowsPerBatch)
        {
            var rowBatch = rows.Skip(offset).Take(rowsPerBatch).ToArray();
            var preparedRows = new InvoiceImportRecord?[rowBatch.Length];
            var preparationErrors = new string?[rowBatch.Length];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, rowBatch.Length),
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
                async (index, token) =>
                {
                    var result = await PrepareInvoiceRowAsync(rowBatch[index], customerInitializations, token);
                    preparedRows[index] = result.invoice;
                    preparationErrors[index] = result.error;
                });

            var batchInvoices = preparedRows.OfType<InvoiceImportRecord>().ToArray();
            if (batchInvoices.Length > 0)
            {
                try
                {
                    await repository.UpsertInvoiceDataBatchAsync(batchInvoices, storeNumber, cancellationToken);
                    foreach (var invoiceNumber in batchInvoices.Select(invoice => InvoiceNumberNormalizer.NormalizeForComparison(invoice.InvoiceNumber)))
                        importedInvoiceNumbers.TryAdd(invoiceNumber, 0);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Batched invoice import failed; retrying {InvoiceCount} invoices individually.", batchInvoices.Length);
                    var batchErrors = new string?[batchInvoices.Length];
                    await Parallel.ForEachAsync(
                        Enumerable.Range(0, batchInvoices.Length),
                        new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
                        async (index, token) =>
                        {
                            var result = await TryUpsertInvoiceRowAsync(batchInvoices[index], storeNumber, token);
                            batchErrors[index] = result.error;
                            if (result.imported)
                            {
                                var invoiceNumber = InvoiceNumberNormalizer.NormalizeForComparison(batchInvoices[index].InvoiceNumber);
                                importedInvoiceNumbers.TryAdd(invoiceNumber, 0);
                            }
                        });

                    for (var index = 0; index < batchErrors.Length; index++)
                    {
                        if (!string.IsNullOrWhiteSpace(batchErrors[index]))
                            errors.Add(batchErrors[index]!);
                    }
                }
            }

            errors.AddRange(preparationErrors.OfType<string>());
            var batchInvoiceNumbers = rowBatch
                .Select(row => GetRowValue(row, "invoice_no", "invoiceNo", "invoice number", "invoicenumber", "invoice no"))
                .Where(invoiceNumber => !string.IsNullOrWhiteSpace(invoiceNumber))
                .Select(InvoiceNumberNormalizer.NormalizeForComparison)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var invoiceNumber in batchInvoiceNumbers)
            {
                if (!processedInvoiceNumbers.Add(invoiceNumber))
                    continue;

                var processedInvoices = processedInvoiceNumbers.Count;
                var percent = totalInvoiceNumbers == 0
                    ? 99
                    : Math.Min(99, (int)Math.Round((double)processedInvoices / totalInvoiceNumbers * 100d));
                uploadProgressEventBus.Publish(
                    operation,
                    "in_progress",
                    percent,
                    $"{importedInvoiceNumbers.Count} of {totalInvoiceNumbers} invoices uploaded.",
                    importedInvoiceNumbers.Count,
                    totalInvoiceNumbers);
            }
        }

        var reconciliation = await repository.GetUploadReconciliationAsync(storeNumber, cancellationToken);
        uploadProgressEventBus.Publish(operation, "completed", 100, $"Completed: {importedInvoiceNumbers.Count} of {totalInvoiceNumbers} invoices uploaded.", importedInvoiceNumbers.Count, totalInvoiceNumbers);
        return Ok(new
        {
            imported = importedInvoiceNumbers.Count,
            totalInvoiceNumbers,
            errors,
            reconciliation
        });
    }

    private async Task<(InvoiceImportRecord? invoice, string? error)> PrepareInvoiceRowAsync(
        Dictionary<string, string> row,
        ConcurrentDictionary<int, Lazy<Task<bool>>> customerInitializations,
        CancellationToken cancellationToken)
    {
        var invoiceValue = GetRowValue(row, "invoice_no", "invoiceNo", "invoice number", "invoicenumber", "invoice no");
        if (string.IsNullOrWhiteSpace(invoiceValue))
        {
            return (null, null);
        }

        var customerNo = ParseInt(GetRowValue(row, "customer_no", "customerNo", "customer number", "customer number ", "customernumber")) ?? 0;
        var customer = GetCustomerFromRow(row, customerNo);
        var invoiceDate = ParseDate(GetRowValue(row, "invoice_date", "invoiceDate", "date", "invoicedate")) ?? DateTime.UtcNow;
        var amount = ParseDecimal(GetRowValue(row, "invoice_amount", "invoiceAmount", "amount", "invoice total", "invoicetotal")) ?? 0m;
        var transactionType = GetRowValue(row, "transaction_type", "transactionType", "txn_type", "transaction type", "transactiontype") ?? string.Empty;
        var paymentMethod = GetRowValue(row, "payment_method", "paymentMethod", "payment method", "paymentmethod") ?? string.Empty;
        var employeeId = ParseInt(GetRowValue(row, "employee_no", "employeeNo", "employee", "employee number", "employeenumber")) ?? 0;
        var poNumber = GetRowValue(row, "po_number", "poNumber", "po number", "ponumber") ?? string.Empty;
        try
        {
            if (customer is not null && customerInitializations.TryGetValue(customerNo, out var customerInitialization))
                await customerInitialization.Value;

            return (new InvoiceImportRecord(
                customerNo,
                invoiceValue,
                invoiceDate,
                amount,
                transactionType,
                employeeId,
                paymentMethod,
                poNumber), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (null, $"Unable to import invoice {invoiceValue}: {ex.Message}");
        }
    }

    private async Task<(bool imported, string? error)> TryUpsertInvoiceRowAsync(
        InvoiceImportRecord invoice,
        int storeNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.UpsertInvoiceDataAsync(
                invoice.CustomerNumber,
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.InvoiceAmount,
                invoice.TransactionType,
                invoice.EmployeeId,
                storeNumber,
                invoice.PaymentMethod,
                invoice.PoNumber,
                cancellationToken);
            return (true, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, $"Unable to import invoice {invoice.InvoiceNumber}: {ex.Message}");
        }
    }

    private static FirestoreCustomer? GetCustomerFromRow(Dictionary<string, string> row, int customerNumber)
    {
        if (customerNumber <= 0)
            return null;

        var customerName = GetRowValue(row, "customer_name", "customerName", "customer name", "customer", "account name", "accountName");
        var vendorId = GetRowValue(row, "vendor_id", "vendorId", "vendor number", "vendor");
        var statementOrInvoice = GetRowValue(row, "statement_or_invoice", "statementOrInvoice", "statement or invoice", "statement invoice", "statement_invoice");
        var address1 = GetRowValue(row, "address1", "address_1", "address 1", "address");
        var address2 = GetRowValue(row, "address2", "address_2", "address 2");
        var city = GetRowValue(row, "city");
        var state = GetRowValue(row, "state");
        var zip = GetRowValue(row, "zip", "zipcode", "zip code", "postal code");
        var emails = GetRowValue(row, "email", "email address", "emails")?
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList() ?? [];
        var showPoValue = GetRowValue(row, "show_po", "showPo", "show po", "show po number", "show_po_number", "showPoNumber");

        if (string.IsNullOrWhiteSpace(customerName)
            && string.IsNullOrWhiteSpace(vendorId)
            && string.IsNullOrWhiteSpace(statementOrInvoice)
            && string.IsNullOrWhiteSpace(address1)
            && string.IsNullOrWhiteSpace(address2)
            && string.IsNullOrWhiteSpace(city)
            && string.IsNullOrWhiteSpace(state)
            && string.IsNullOrWhiteSpace(zip)
            && emails.Count == 0
            && !bool.TryParse(showPoValue, out _))
        {
            return new FirestoreCustomer
            {
                CustomerNumber = customerNumber,
                CustomerName = string.Empty
            };
        }

        return new FirestoreCustomer
        {
            CustomerNumber = customerNumber,
            CustomerName = customerName ?? string.Empty,
            VendorId = vendorId ?? string.Empty,
            StatementOrInvoice = statementOrInvoice ?? "I",
            Address1 = address1 ?? string.Empty,
            Address2 = address2 ?? string.Empty,
            City = city ?? string.Empty,
            State = state ?? string.Empty,
            Zip = zip ?? string.Empty,
            Emails = emails,
            ShowPo = bool.TryParse(showPoValue, out var showPo) && showPo
        };
    }

    [HttpPost("upload-images")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files, [FromForm] int storeNumber, [FromForm] string? uploadId, CancellationToken cancellationToken)
    {
        if (storeNumber <= 0)
            return BadRequest("A store number is required.");

        if (files is null || files.Count == 0)
            return BadRequest("At least one image file is required.");

        string operation;
        try
        {
            operation = GetUploadOperation("images", uploadId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        string uploadSessionId = string.IsNullOrWhiteSpace(uploadId)
            ? Guid.NewGuid().ToString("N")
            : uploadId;

        var results = new List<string>();
        var errors = new List<string>();
        var validFiles = files.Where(file => file is not null && file.Length > 0).ToList();

        uploadProgressEventBus.Publish(operation, "in_progress", 0, "Preparing image upload queue...", 0, validFiles.Count);

        for (var i = 0; i < validFiles.Count; i++)
        {
            var file = validFiles[i];
            try
            {
                string? invoiceNumber = await DecodeInvoiceNumberFromImageAsync(file, cancellationToken);
                if (string.IsNullOrWhiteSpace(invoiceNumber))
                {
                    await using var source = file.OpenReadStream();
                    await misreadBarcodeService.SaveMisreadBarcodeAsync(source, file.FileName, file.ContentType ?? "image/png", cancellationToken);
                    errors.Add($"Could not read a valid invoice barcode from {file.FileName}. Saved to misread barcodes for review.");
                }
                else
                {
                    await using var sourceForUpload = file.OpenReadStream();
                    var objectName = await invoiceImageRepository.InsertInvoiceImageAsync(
                        invoiceNumber,
                        storeNumber,
                        sourceForUpload,
                        file.ContentType ?? "image/png",
                        false,
                        cancellationToken: cancellationToken,
                        uploadSessionId: uploadSessionId);

                    results.Add(objectName);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to process {file.FileName}: {ex.Message}");
            }

            var percent = validFiles.Count == 0 ? 99 : Math.Min(99, (int)Math.Round((double)(i + 1) / validFiles.Count * 100d));
            uploadProgressEventBus.Publish(operation, "in_progress", percent, $"Processed {i + 1} of {validFiles.Count} image files; {results.Count} uploaded.", i + 1, validFiles.Count);
        }

        var reconciliation = await repository.GetUploadReconciliationAsync(storeNumber, cancellationToken);
        var misreadBarcodes = await misreadBarcodeService.ListMisreadBarcodesAsync(cancellationToken);
        uploadProgressEventBus.Publish(operation, "completed", 100, $"Completed: {results.Count} images uploaded from {validFiles.Count} files.", validFiles.Count, validFiles.Count);

        return Ok(new
        {
            processed = results.Count,
            errors,
            uploaded = results,
            reconciliation,
            misreadBarcodes
        });
    }

    [HttpGet("misread-barcodes")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
    public async Task<IActionResult> GetMisreadBarcodes(CancellationToken cancellationToken)
    {
        var items = await misreadBarcodeService.ListMisreadBarcodesAsync(cancellationToken);
        return Ok(items.Select(item => new
        {
            id = item.Id,
            fileName = item.FileName,
            objectName = item.ObjectName,
            bucketName = item.BucketName,
            contentType = item.ContentType,
            createdUtc = item.CreatedUtc?.ToDateTime().ToUniversalTime().ToString("O")
        }));
    }

    [HttpGet("misread-barcodes/{id}/view")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
    public async Task<IActionResult> ViewMisreadBarcode(string id, CancellationToken cancellationToken)
    {
        var record = await misreadBarcodeService.GetMisreadBarcodeAsync(id, cancellationToken);
        if (record is null)
            return NotFound("Misread barcode image not found.");

        var stream = await misreadBarcodeService.GetMisreadBarcodeStreamAsync(id, cancellationToken);
        if (stream is null)
            return NotFound("Misread barcode image not found.");

        var (browserStream, contentType) = await InvoiceImagesController.NormalizeImageAsync(stream, record.ContentType, cancellationToken);
        return File(browserStream, contentType);
    }

    [HttpPost("misread-barcodes/resolve")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    public async Task<IActionResult> ResolveMisreadBarcode([FromBody] ResolveMisreadBarcodeRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("A request body is required.");
        if (string.IsNullOrWhiteSpace(request.Id))
            return BadRequest("A misread barcode id is required.");
        if (string.IsNullOrWhiteSpace(request.InvoiceNumber))
            return BadRequest("An invoice number is required.");
        if (request.StoreNumber <= 0)
            return BadRequest("A store number is required.");
        if (request.PageNumber <= 0)
            return BadRequest("A page number is required.");

        try
        {
            var objectName = await misreadBarcodeService.ResolveMisreadBarcodeAsync(request.Id, request.InvoiceNumber, request.StoreNumber, request.PageNumber, cancellationToken);
            return Ok(new { objectName });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("misread-barcodes/{id}")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    public async Task<IActionResult> DeleteMisreadBarcode(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest("A misread barcode id is required.");

        await misreadBarcodeService.DeleteMisreadBarcodeAsync(id, cancellationToken);
        return Ok();
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.InvoiceAdmin)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerNumber <= 0 || string.IsNullOrWhiteSpace(request.InvoiceNumber) || request.StoreNumber <= 0)
            return BadRequest("Customer number, invoice number, and store number are required.");

        var success = await repository.InsertInvoiceDataAsync(
            request.CustomerNumber,
            request.InvoiceNumber,
            request.InvoiceDate,
            request.InvoiceAmount,
            request.TransactionType,
            request.EmployeeId,
            request.StoreNumber,
            request.PaymentMethod,
            request.PoNumber,
            cancellationToken);

        return success ? Ok(new { message = "Invoice inserted successfully." }) : Conflict("Invoice already exists for this store.");
    }

    private static string? GetRowValue(Dictionary<string, string> row, params string[] keys)
    {
        var normalizedKeys = keys
            .Select(key => NormalizeKey(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in row)
        {
            if (normalizedKeys.Contains(NormalizeKey(pair.Key)) && !string.IsNullOrWhiteSpace(pair.Value))
                return pair.Value;
        }

        return null;
    }

    private static string NormalizeKey(string value)
    {
        return Regex.Replace(value ?? string.Empty, "[^a-z0-9]+", string.Empty, RegexOptions.IgnoreCase).Trim();
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || int.TryParse(normalized, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed)
            ? parsed
            : null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        normalized = normalized.Replace("$", string.Empty).Replace(",", string.Empty);

        return decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed)
            ? parsed
            : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrWhiteSpace(digits))
        {
            var paddedDigits = digits; 
            if (digits.Length == 7)
                paddedDigits = digits.PadLeft(8, '0');

            if (paddedDigits.Length == 8)
            {
                if (DateTime.TryParseExact(paddedDigits, "MMddyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
                    return exactDate;
                if (DateTime.TryParseExact(paddedDigits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactIsoDate))
                    return exactIsoDate;
            }
        }

        if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var parsed))
            return parsed;

        return DateTime.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out parsed) ? parsed : null;
    }

    private static async Task<string?> DecodeInvoiceNumberFromImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        try
        {
            using var image = await Image.LoadAsync(buffer, cancellationToken);
            if (image.Width <= 0 || image.Height <= 0)
                return null;

            var reader = new BarcodeReaderGeneric
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryInverted = true,
                    TryHarder = true,
                    PossibleFormats = new[]
                    {
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.CODE_93,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E
                    }
                }
            };

            using var rgbaImage = image.CloneAs<Rgba32>();
            var rgbBytes = new byte[rgbaImage.Width * rgbaImage.Height * 4];
            rgbaImage.CopyPixelDataTo(rgbBytes);

            foreach (var format in new[]
                     {
                         RGBLuminanceSource.BitmapFormat.RGBA32,
                         RGBLuminanceSource.BitmapFormat.ARGB32,
                         RGBLuminanceSource.BitmapFormat.RGB24,
                         RGBLuminanceSource.BitmapFormat.BGRA32,
                         RGBLuminanceSource.BitmapFormat.BGR24
                     })
            {
                var result = reader.Decode(rgbBytes, rgbaImage.Width, rgbaImage.Height, format);
                if (result is not null && !string.IsNullOrWhiteSpace(result.Text))
                {
                    return NormalizeInvoiceNumber(result.Text);
                }
            }

            var luminanceBytes = new byte[rgbaImage.Width * rgbaImage.Height];
            rgbaImage.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        var pixel = row[x];
                        var index = (y * rgbaImage.Width) + x;
                        luminanceBytes[index] = (byte)((pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000);
                    }
                }
            });

            var luminanceResult = reader.Decode(luminanceBytes, rgbaImage.Width, rgbaImage.Height, RGBLuminanceSource.BitmapFormat.Gray8);
            if (luminanceResult is not null && !string.IsNullOrWhiteSpace(luminanceResult.Text))
            {
                return NormalizeInvoiceNumber(luminanceResult.Text);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string NormalizeInvoiceNumber(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return InvoiceNumberNormalizer.NormalizeForStorage(digits);
    }
}

public sealed class ResolveMisreadBarcodeRequest
{
    public string Id { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public int StoreNumber { get; set; }
    public int PageNumber { get; set; } = 1;
}

public sealed class CreateInvoiceRequest
{
    public int CustomerNumber { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public decimal InvoiceAmount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int EmployeeId { get; set; }
    public int StoreNumber { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
}

using System.Data;
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
    IInvoiceImageRepository invoiceImageRepository,
    IInvoiceStoreCache invoiceStoreCache,
    IUploadProgressEventBus uploadProgressEventBus,
    IOptions<GoogleCloudOptions> googleCloudOptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? invoiceNumber, [FromQuery] string? customerNumber, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(invoiceNumber))
        {
            if (!string.IsNullOrWhiteSpace(customerNumber) && int.TryParse(customerNumber, out int custNo))
            {
                return Ok(await repository.GetInvoiceDataByInvoiceNumberAndCustomerAsync(invoiceNumber, custNo, cancellationToken));
            }
            return Ok(await repository.GetInvoiceDataByInvoiceNumberAsync(invoiceNumber, cancellationToken));
        }

        return Ok(await repository.FindAsync(invoiceNumber, customerNumber, cancellationToken));
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
    public async Task<IActionResult> GetByDate([FromQuery] DateTime beginDate, [FromQuery] DateTime endDate, [FromQuery] int? customerNumber, CancellationToken cancellationToken)
    {
        if (customerNumber.HasValue)
        {
            return Ok(await repository.GetInvoiceDataByDtmAndCustomerAsync(beginDate, endDate, customerNumber.Value, cancellationToken));
        }

        return Ok(await repository.GetInvoiceDataByDtmAsync(beginDate, endDate, cancellationToken));
    }

    [HttpGet("statement")]
    public async Task<IActionResult> GetStatementInvoices([FromQuery] int customerNumber, [FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] string? invoiceNumbers, CancellationToken cancellationToken)
    {
        return Ok(await repository.GetStatementInvoicesAsync(customerNumber, fromDate, toDate, invoiceNumbers ?? string.Empty, cancellationToken));
    }

    [HttpPost("upload-excel")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    public async Task<IActionResult> UploadExcel([FromForm] IFormFile excelFile, [FromForm] int storeNumber, CancellationToken cancellationToken)
    {
        if (excelFile is null || excelFile.Length == 0)
            return BadRequest("An Excel file is required.");

        return await ImportInvoiceFileAsync(excelFile, storeNumber, cancellationToken);
    }

    [HttpPost("upload-csv")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    public async Task<IActionResult> UploadCsv([FromForm] IFormFile csvFile, [FromForm] int storeNumber, CancellationToken cancellationToken)
    {
        if (csvFile is null || csvFile.Length == 0)
            return BadRequest("A CSV file is required.");

        return await ImportInvoiceFileAsync(csvFile, storeNumber, cancellationToken);
    }

    private async Task<IActionResult> ImportInvoiceFileAsync(IFormFile file, int storeNumber, CancellationToken cancellationToken)
    {
        if (storeNumber <= 0)
            return BadRequest("A store number is required.");

        var imported = 0;
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

        var totalRows = rows.Count;
        var operation = "excel";
        uploadProgressEventBus.Publish(operation, "in_progress", 0, "Preparing to import invoice rows...", 0, totalRows);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowImport = await TryImportRowAsync(row, storeNumber, cancellationToken);
            if (rowImport.imported)
            {
                imported++;
            }
            else if (!string.IsNullOrWhiteSpace(rowImport.error))
            {
                errors.Add(rowImport.error);
            }

            var percent = totalRows == 0 ? 100 : (int)Math.Round((double)(i + 1) / totalRows * 100d);
            uploadProgressEventBus.Publish(operation, "in_progress", percent, $"Imported {imported} of {totalRows} rows.", imported, totalRows);
        }

        uploadProgressEventBus.Publish(operation, "completed", 100, $"Imported {imported} invoice rows.", imported, totalRows);

        var reconciliation = await repository.GetUploadReconciliationAsync(storeNumber, cancellationToken);
        return Ok(new
        {
            imported,
            errors,
            reconciliation
        });
    }

    private async Task<(bool imported, string? error)> TryImportRowAsync(Dictionary<string, string> row, int storeNumber, CancellationToken cancellationToken)
    {
        var invoiceValue = GetRowValue(row, "invoice_no", "invoiceNo", "invoice number", "invoicenumber", "invoice no");
        if (string.IsNullOrWhiteSpace(invoiceValue))
        {
            return (false, null);
        }

        var customerNo = ParseInt(GetRowValue(row, "customer_no", "customerNo", "customer number", "customer number ", "customernumber")) ?? 0;
        var invoiceDate = ParseDate(GetRowValue(row, "invoice_date", "invoiceDate", "date", "invoicedate")) ?? DateTime.UtcNow;
        var amount = ParseDecimal(GetRowValue(row, "invoice_amount", "invoiceAmount", "amount", "invoice total", "invoicetotal")) ?? 0m;
        var transactionType = GetRowValue(row, "transaction_type", "transactionType", "txn_type", "transaction type", "transactiontype") ?? string.Empty;
        var paymentMethod = GetRowValue(row, "payment_method", "paymentMethod", "payment method", "paymentmethod") ?? string.Empty;
        var employeeId = ParseInt(GetRowValue(row, "employee_no", "employeeNo", "employee", "employee number", "employeenumber")) ?? 0;
        var poNumber = GetRowValue(row, "po_number", "poNumber", "po number", "ponumber") ?? string.Empty;
        var actualStoreNumber = ParseInt(GetRowValue(row, "store_no", "storeNo", "store", "store number", "storenumber")) ?? storeNumber;

        try
        {
            await repository.UpsertInvoiceDataAsync(
                customerNo,
                invoiceValue,
                invoiceDate,
                amount,
                transactionType,
                employeeId,
                actualStoreNumber,
                paymentMethod,
                poNumber,
                cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Unable to import invoice {invoiceValue}: {ex.Message}");
        }
    }

    [HttpPost("upload-images")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser}")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages([FromForm] List<IFormFile> files, [FromForm] int storeNumber, CancellationToken cancellationToken)
    {
        if (storeNumber <= 0)
            return BadRequest("A store number is required.");

        if (files is null || files.Count == 0)
            return BadRequest("At least one image file is required.");

        var results = new List<string>();
        var errors = new List<string>();
        var validFiles = files.Where(file => file is not null && file.Length > 0).ToList();
        var operation = "images";

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
                    using var copy = new MemoryStream();
                    await source.CopyToAsync(copy, cancellationToken);
                    copy.Position = 0;

                    await invoiceImageRepository.SaveMisreadBarcodeAsync(copy, file.FileName, file.ContentType ?? "image/png", cancellationToken);
                    errors.Add($"Could not read a valid invoice barcode from {file.FileName}. Saved to misread barcodes for review.");
                }
                else
                {
                    await using var sourceForUpload = file.OpenReadStream();
                    using var copyForUpload = new MemoryStream();
                    await sourceForUpload.CopyToAsync(copyForUpload, cancellationToken);
                    copyForUpload.Position = 0;

                    var objectName = await invoiceImageRepository.InsertInvoiceImageAsync(
                        invoiceNumber,
                        storeNumber,
                        copyForUpload,
                        file.ContentType ?? "image/png",
                        false,
                        cancellationToken: cancellationToken);

                    results.Add(objectName);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to process {file.FileName}: {ex.Message}");
            }

            var percent = validFiles.Count == 0 ? 100 : (int)Math.Round((double)(i + 1) / validFiles.Count * 100d);
            uploadProgressEventBus.Publish(operation, "in_progress", percent, $"Processed {results.Count} of {validFiles.Count} images.", results.Count, validFiles.Count);
        }

        uploadProgressEventBus.Publish(operation, "completed", 100, $"Processed {results.Count} images.", results.Count, validFiles.Count);

        return Ok(new
        {
            processed = results.Count,
            errors,
            uploaded = results,
            reconciliation = await repository.GetUploadReconciliationAsync(storeNumber, cancellationToken),
            misreadBarcodes = await invoiceImageRepository.ListMisreadBarcodesAsync(cancellationToken)
        });
    }

    [HttpGet("misread-barcodes")]
    [Authorize(Roles = $"{RoleNames.InvoiceAdmin},{RoleNames.InvoiceUser},{RoleNames.CustomerInvoiceUser}")]
    public async Task<IActionResult> GetMisreadBarcodes(CancellationToken cancellationToken)
    {
        var items = await invoiceImageRepository.ListMisreadBarcodesAsync(cancellationToken);
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
        var record = await invoiceImageRepository.GetMisreadBarcodeAsync(id, cancellationToken);
        if (record is null)
            return NotFound("Misread barcode image not found.");

        var stream = await invoiceImageRepository.GetMisreadBarcodeStreamAsync(id, cancellationToken);
        if (stream is null)
            return NotFound("Misread barcode image not found.");

        return File(stream, record.ContentType);
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

        try
        {
            var objectName = await invoiceImageRepository.ResolveMisreadBarcodeAsync(request.Id, request.InvoiceNumber, request.StoreNumber, cancellationToken);
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

        await invoiceImageRepository.DeleteMisreadBarcodeAsync(id, cancellationToken);
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

        var trimmed = digits.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }
}

public sealed class ResolveMisreadBarcodeRequest
{
    public string Id { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public int StoreNumber { get; set; }
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

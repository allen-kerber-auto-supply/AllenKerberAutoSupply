using System.Globalization;
using AllenKerberAutoSupply.Data;
using AllenKerberAutoSupply.Models;
using Google.Cloud.Firestore;

namespace AllenKerberAutoSupply.Services;

public sealed class TrendAnalysisService(
    IInvoiceRepository invoices,
    ITrendRepository trends,
    ILogger<TrendAnalysisService> logger) : ITrendAnalysisService
{
    private const int MaxCustomers = 2000;
    private const string AnalysisVersion = "7";

    public async Task<TrendAnalysisResponse> AnalyzeAsync(
        DateOnly fromDate,
        DateOnly toDate,
        string aggregateBy,
        CancellationToken cancellationToken = default)
    {
        if (fromDate == default || toDate == default)
            throw new ArgumentException("Both a start date and an end date are required.");
        if (fromDate > toDate)
            throw new ArgumentException("The start date must be on or before the end date.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (toDate > today)
            throw new ArgumentException("The end date cannot be in the future.");
        aggregateBy = NormalizeAggregateBy(aggregateBy);

        var priorFromDate = fromDate.AddYears(-1);
        var priorToDate = toDate.AddYears(-1);
        var rangeKey = $"{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}_{aggregateBy}";
        var fromDateText = fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var toDateText = toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var priorFromDateText = priorFromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var priorToDateText = priorToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var cached = await trends.FindAsync(rangeKey, cancellationToken);
        if (cached is not null
            && string.Equals(cached.FromDate, fromDateText, StringComparison.Ordinal)
            && string.Equals(cached.ToDate, toDateText, StringComparison.Ordinal)
            && string.Equals(cached.PriorYearFromDate, priorFromDateText, StringComparison.Ordinal)
            && string.Equals(cached.PriorYearToDate, priorToDateText, StringComparison.Ordinal)
            && string.Equals(cached.AggregateBy, aggregateBy, StringComparison.Ordinal)
            && string.Equals(cached.AnalysisVersion, AnalysisVersion, StringComparison.Ordinal))
            return new TrendAnalysisResponse { Data = cached, Cached = true };

        var allInvoices = await invoices.GetInvoicesForTrendAsync(
            ToUtcStart(priorFromDate),
            ToUtcEnd(toDate),
            cancellationToken);
        var currentInvoices = allInvoices
            .Where(invoice => IsInRange(invoice, fromDate, toDate))
            .ToArray();
        var priorYearInvoices = allInvoices
            .Where(invoice => IsInRange(invoice, priorFromDate, priorToDate))
            .ToArray();
        var customers = Aggregate(
            currentInvoices,
            priorYearInvoices,
            aggregateBy
            );
        if (customers.Count > MaxCustomers)
            throw new InvalidOperationException(
                $"The selected range contains too many customers to analyze ({customers.Count}). Narrow the date range.");

        var decliningCustomerNumbers = customers
            .Where(customer => customer.IsDeclining)
            .Select(customer => customer.CustomerNumber)
            .ToArray();
        var graphInvoices = await invoices.GetInvoicesForTrendCustomersAsync(
            decliningCustomerNumbers,
            cancellationToken);
        AddInvoicePoints(customers, graphInvoices, priorFromDate, priorToDate, fromDate, toDate);

        var trend = new TrendData
        {
            RangeKey = rangeKey,
            FromDate = fromDateText,
            ToDate = toDateText,
            PriorYearFromDate = priorFromDateText,
            PriorYearToDate = priorToDateText,
            GeneratedAt = Timestamp.GetCurrentTimestamp(),
            AnalysisVersion = AnalysisVersion,
            AggregateBy = aggregateBy,
            CurrentInvoiceCount = currentInvoices.Length,
            PriorYearInvoiceCount = priorYearInvoices.Length,
            CustomerCount = customers.Count,
            DecliningCustomerCount = customers.Where(customer => customer.IsDeclining).Count(),
            CurrentTotalAmount = currentInvoices.Sum(invoice => invoice.InvoiceAmount),
            PriorYearTotalAmount = priorYearInvoices.Sum(invoice => invoice.InvoiceAmount),
            CurrentTotalAggregate = aggregateBy == "count" ? currentInvoices.Length : currentInvoices.Sum(invoice => invoice.InvoiceAmount),
            PriorYearTotalAggregate = aggregateBy == "count" ? priorYearInvoices.Length : priorYearInvoices.Sum(invoice => invoice.InvoiceAmount),
            Customers = customers
        };

        logger.LogInformation(
            "Generated trend comparison for {FromDate} through {ToDate} across {CustomerCount} customers.",
            fromDateText,
            toDateText,
            customers.Count);

        var stored = await trends.CreateOrGetAsync(trend, cancellationToken);
        return new TrendAnalysisResponse { Data = stored, Cached = !ReferenceEquals(stored, trend) };
    }

    private static List<TrendCustomerResult> Aggregate(
        IReadOnlyList<Invoice> currentInvoices,
        IReadOnlyList<Invoice> priorYearInvoices,
        string aggregateBy)
    {
        var customers = new Dictionary<int, TrendCustomerResult>();

        AddPeriod(customers, currentInvoices, isCurrent: true);
        AddPeriod(customers, priorYearInvoices, isCurrent: false);

        foreach (var customer in customers.Values)
        {
            customer.CurrentAggregate = aggregateBy == "count" ? customer.CurrentInvoiceCount : customer.CurrentAmount;
            customer.PriorYearAggregate = aggregateBy == "count" ? customer.PriorYearInvoiceCount : customer.PriorYearAmount;
            customer.AggregateChange = customer.CurrentAggregate - customer.PriorYearAggregate;
            customer.AggregateChangePercent = CalculateChangePercent(
                customer.PriorYearAggregate,
                customer.CurrentAggregate);
            customer.AmountChange = customer.CurrentAmount - customer.PriorYearAmount;
            customer.AmountChangePercent = CalculateChangePercent(
                customer.PriorYearAmount,
                customer.CurrentAmount);
            customer.IsDeclining = customer.AggregateChange < 0;
            customer.Invoices = customer.Invoices
                .OrderBy(invoice => invoice.InvoiceDate, StringComparer.Ordinal)
                .ThenBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return customers.Values
            .OrderBy(customer => customer.IsDeclining ? 0 : 1)
            .ThenBy(customer => customer.AggregateChangePercent)
            .ThenBy(customer => customer.AggregateChange)
            .ThenBy(customer => customer.CustomerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddInvoicePoints(
        IReadOnlyList<TrendCustomerResult> customers,
        IReadOnlyList<Invoice> invoices,
        DateOnly priorFromDate,
        DateOnly priorToDate,
        DateOnly currentFromDate,
        DateOnly currentToDate)
    {
        var customersByNumber = customers
            .Where(customer => customer.IsDeclining)
            .ToDictionary(customer => customer.CustomerNumber);

        foreach (var invoice in invoices.Where(invoice => invoice.CustomerNumber > 0 && invoice.InvoiceDate.HasValue))
        {
            if (!customersByNumber.TryGetValue(invoice.CustomerNumber, out var customer))
                continue;

            var invoiceDate = DateOnly.FromDateTime(invoice.InvoiceDate!.Value.ToDateTime());
            customer.Invoices.Add(new TrendInvoicePoint
            {
                InvoiceDate = invoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                InvoiceNumber = invoice.InvoiceNumber,
                Amount = invoice.InvoiceAmount,
                ComparisonPeriod = GetComparisonPeriod(
                    invoiceDate,
                    priorFromDate,
                    priorToDate,
                    currentFromDate,
                    currentToDate)
            });
        }

        foreach (var customer in customersByNumber.Values)
        {
            customer.Invoices = customer.Invoices
                .OrderBy(invoice => invoice.InvoiceDate, StringComparer.Ordinal)
                .ThenBy(invoice => invoice.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static void AddPeriod(
        IDictionary<int, TrendCustomerResult> customers,
        IReadOnlyList<Invoice> invoices,
        bool isCurrent)
    {
        foreach (var invoice in invoices.Where(invoice => invoice.CustomerNumber > 0))
        {
            if (!customers.TryGetValue(invoice.CustomerNumber, out var customer))
            {
                customer = new TrendCustomerResult
                {
                    CustomerNumber = invoice.CustomerNumber,
                    CustomerName = invoice.CustomerName
                };
                customers.Add(invoice.CustomerNumber, customer);
            }
            else if (string.IsNullOrWhiteSpace(customer.CustomerName)
                && !string.IsNullOrWhiteSpace(invoice.CustomerName))
            {
                customer.CustomerName = invoice.CustomerName;
            }

            if (isCurrent)
            {
                customer.CurrentInvoiceCount++;
                customer.CurrentAmount += invoice.InvoiceAmount;
            }
            else
            {
                customer.PriorYearInvoiceCount++;
                customer.PriorYearAmount += invoice.InvoiceAmount;
            }

        }
    }

    private static string GetComparisonPeriod(
        DateOnly invoiceDate,
        DateOnly priorFromDate,
        DateOnly priorToDate,
        DateOnly currentFromDate,
        DateOnly currentToDate)
    {
        if (invoiceDate >= currentFromDate && invoiceDate <= currentToDate)
            return "Selected range";
        if (invoiceDate >= priorFromDate && invoiceDate <= priorToDate)
            return "Prior year";
        return "Between comparison ranges";
    }

    private static bool IsInRange(Invoice invoice, DateOnly fromDate, DateOnly toDate)
    {
        if (!invoice.InvoiceDate.HasValue)
            return false;

        var invoiceDate = DateOnly.FromDateTime(invoice.InvoiceDate.Value.ToDateTime());
        return invoiceDate >= fromDate && invoiceDate <= toDate;
    }

    private static double CalculateChangePercent(double priorYearAmount, double currentAmount)
    {
        if (priorYearAmount == 0)
            return currentAmount == 0 ? 0 : currentAmount > 0 ? 100 : -100;

        return (currentAmount - priorYearAmount) / Math.Abs(priorYearAmount) * 100;
    }

    private static DateTime ToUtcStart(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

    private static DateTime ToUtcEnd(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

    private static string NormalizeAggregateBy(string aggregateBy) =>
        aggregateBy?.Trim().ToLowerInvariant() switch
        {
            "amount" => "amount",
            "count" => "count",
            _ => throw new ArgumentException("Aggregate mode must be amount or count.")
        };
}

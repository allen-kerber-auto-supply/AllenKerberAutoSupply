using System.Collections.Concurrent;

namespace AllenKerberAutoSupply.Services;

public interface IUploadProgressEventBus
{
    string EventBusName { get; }
    UploadProgressState? GetState(string operation);
    UploadProgressState? GetLatest();
    void Publish(string operation, string status, int percent, string message, int processedCount = 0, int totalCount = 0);
    void Reset(string operation);
}

public sealed class UploadProgressState
{
    public string Operation { get; set; } = string.Empty;
    public string BusName { get; set; } = string.Empty;
    public string Status { get; set; } = "idle";
    public int Percent { get; set; } = 0;
    public string Message { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public int TotalCount { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InvoiceUploadProgressEventBus : IUploadProgressEventBus
{
    private readonly ConcurrentDictionary<string, UploadProgressState> _states = new(StringComparer.OrdinalIgnoreCase);

    public string EventBusName => "allenkerber-bus";

    public UploadProgressState? GetState(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
            return GetLatest();

        return _states.TryGetValue(operation, out var state) ? state : null;
    }

    public UploadProgressState? GetLatest()
    {
        if (_states.IsEmpty)
            return null;

        return _states.Values
            .OrderByDescending(s => s.UpdatedAtUtc)
            .FirstOrDefault();
    }

    public void Publish(string operation, string status, int percent, string message, int processedCount = 0, int totalCount = 0)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("An upload operation is required.", nameof(operation));

        var state = new UploadProgressState
        {
            Operation = operation,
            BusName = EventBusName,
            Status = string.IsNullOrWhiteSpace(status) ? "in_progress" : status,
            Percent = Math.Clamp(percent, 0, 100),
            Message = message,
            ProcessedCount = processedCount,
            TotalCount = totalCount,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        _states[operation] = state;
    }

    public void Reset(string operation)
    {
        if (!string.IsNullOrWhiteSpace(operation))
            _states.TryRemove(operation, out _);
    }
}

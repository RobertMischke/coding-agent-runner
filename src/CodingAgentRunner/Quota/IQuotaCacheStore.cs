using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CodingAgentRunner.Quota;

/// <summary>
/// Persists the quota cache so a restart does not leave consumers with no data
/// until the first (slow) probe completes.
/// </summary>
public interface IQuotaCacheStore
{
    /// <summary>Read the persisted snapshots, or an empty list when none / unreadable.</summary>
    List<QuotaSnapshot> Read();

    /// <summary>Persist the given snapshots.</summary>
    void Write(IEnumerable<QuotaSnapshot> snapshots);
}

/// <summary>
/// File-backed <see cref="IQuotaCacheStore"/>. Writes atomically (temp file +
/// rename) so a crash mid-write never leaves a half-written file, and tolerates a
/// corrupt file by starting empty.
/// </summary>
public sealed class FileQuotaCacheStore : IQuotaCacheStore
{
    private readonly string _path;
    private readonly ILogger? _logger;
    private readonly object _writeLock = new();

    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Create a store that reads/writes <paramref name="filePath"/>.</summary>
    public FileQuotaCacheStore(string filePath, ILogger? logger = null)
    {
        _path = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _logger = logger;
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }
        catch { /* best-effort; Write surfaces the real error */ }
    }

    /// <inheritdoc />
    public List<QuotaSnapshot> Read()
    {
        if (!File.Exists(_path)) return new List<QuotaSnapshot>();
        try
        {
            var raw = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<QuotaSnapshot>>(raw, ReadOpts) ?? new List<QuotaSnapshot>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read quota cache at {Path}", _path);
            return new List<QuotaSnapshot>();
        }
    }

    /// <inheritdoc />
    public void Write(IEnumerable<QuotaSnapshot> snapshots)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshots.ToList(), WriteOpts);
            lock (_writeLock)
            {
                var tmp = _path + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Move(tmp, _path, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist quota cache to {Path}", _path);
        }
    }
}

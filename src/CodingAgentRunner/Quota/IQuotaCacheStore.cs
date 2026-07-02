using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using CodingAgentRunner.Abstractions;

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
///
/// <para><b>Cross-process ("global") use.</b> <see cref="Global"/> returns a store
/// on one machine-wide canonical path, so every process that opts in shares one
/// cache: a snapshot probed by one application is picked up by the others (the
/// <see cref="QuotaService"/> adopts a fresh stored snapshot instead of
/// re-probing). Writes MERGE with the file per CLI — the freshest
/// <see cref="QuotaSnapshot.FetchedAt"/> wins — so concurrent processes refreshing
/// different CLIs do not erase each other's entries; a best-effort file lock
/// serializes the read-merge-write.</para>
/// </summary>
public sealed class FileQuotaCacheStore : IQuotaCacheStore
{
    private readonly string _path;
    private readonly ILogger? _logger;
    private readonly object _writeLock = new();

    /// <summary>
    /// The store on this machine's canonical shared path
    /// (<c>~/.coding-agent-runner/quota-cache.json</c>) — one quota cache for every
    /// process on the machine that opts in.
    /// </summary>
    public static FileQuotaCacheStore Global(IUserHomeProvider? home = null, ILogger? logger = null)
        => new(GlobalPath(home), logger);

    /// <summary>The canonical machine-wide cache path used by <see cref="Global"/>.</summary>
    public static string GlobalPath(IUserHomeProvider? home = null)
        => Path.Combine((home ?? new DefaultUserHomeProvider()).GetUserHome(), ".coding-agent-runner", "quota-cache.json");

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
    /// <remarks>
    /// Merges with the file's current content per CLI (freshest
    /// <see cref="QuotaSnapshot.FetchedAt"/> wins) under a best-effort
    /// cross-process file lock, so concurrent processes sharing one path — see
    /// <see cref="Global"/> — do not erase each other's snapshots.
    /// </remarks>
    public void Write(IEnumerable<QuotaSnapshot> snapshots)
    {
        try
        {
            var incoming = snapshots.ToList();
            lock (_writeLock)
            {
                using var fileLock = TryAcquireFileLock();
                var merged = Merge(Read(), incoming);
                var json = JsonSerializer.Serialize(merged, WriteOpts);
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

    /// <summary>Per-CLI freshest-wins merge of the on-disk snapshots with the incoming ones.</summary>
    internal static List<QuotaSnapshot> Merge(IEnumerable<QuotaSnapshot> onDisk, IEnumerable<QuotaSnapshot> incoming)
    {
        var byCli = new Dictionary<string, QuotaSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var snap in onDisk.Concat(incoming))
        {
            if (string.IsNullOrWhiteSpace(snap.CliType)) continue;
            if (!byCli.TryGetValue(snap.CliType, out var existing) || snap.FetchedAt >= existing.FetchedAt)
                byCli[snap.CliType] = snap;
        }
        return byCli.Values.OrderBy(s => s.CliType, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Best-effort cross-process lock: an exclusively-opened <c>.lock</c> file next
    /// to the cache, retried briefly. Returns null when it stays contended — the
    /// write proceeds unlocked (atomic rename still prevents torn files; only the
    /// merge can then lose a concurrent update).
    /// </summary>
    private IDisposable? TryAcquireFileLock()
    {
        var lockPath = _path + ".lock";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                    bufferSize: 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                Thread.Sleep(15);
            }
            catch (UnauthorizedAccessException)
            {
                break;
            }
        }
        _logger?.LogDebug("Quota cache lock at {Path} stayed contended; writing without it", lockPath);
        return null;
    }
}

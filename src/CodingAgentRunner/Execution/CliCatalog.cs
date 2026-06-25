namespace CodingAgentRunner.Execution;

/// <summary>
/// The lookup a consumer goes through to get a <see cref="CliDescriptor"/> by CLI
/// type — the single construction path for descriptors and the replacement for a
/// hard-coded driver list. Adding a CLI is <see cref="CliCatalog.Register"/>, not a
/// new switch arm.
/// </summary>
public interface ICliCatalog
{
    /// <summary>The CLI types this catalog can resolve.</summary>
    IReadOnlyCollection<string> Available { get; }

    /// <summary>Try to resolve a descriptor; false (and null) when the type is not registered.</summary>
    bool TryGet(string? cliType, out CliDescriptor? descriptor);

    /// <summary>Resolve a descriptor or throw — no silent fallback to a default CLI.</summary>
    CliDescriptor Get(string cliType);
}

/// <summary>
/// The default <see cref="ICliCatalog"/>: a case-insensitive registry built by
/// <see cref="Register"/>. The built-in descriptors (Claude / Codex / Gemini /
/// Antigravity) are registered when the engine adopts the descriptor model; today
/// the catalog starts empty and a consumer registers what it needs.
/// <para>Built once at startup (single-threaded build, then read-only use); it is not
/// safe for concurrent <see cref="Register"/>.</para>
/// </summary>
public sealed class CliCatalog : ICliCatalog
{
    private readonly Dictionary<string, CliDescriptor> _byType =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<string> Available => _byType.Keys.ToArray();

    /// <summary>Register (or replace) a descriptor; returns this for chaining.</summary>
    public CliCatalog Register(CliDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.CliType))
            throw new ArgumentException("CliDescriptor.CliType must be set.", nameof(descriptor));
        _byType[descriptor.CliType] = descriptor;
        return this;
    }

    /// <inheritdoc />
    public bool TryGet(string? cliType, out CliDescriptor? descriptor)
    {
        if (!string.IsNullOrWhiteSpace(cliType) && _byType.TryGetValue(cliType, out var d))
        {
            descriptor = d;
            return true;
        }
        descriptor = null;
        return false;
    }

    /// <inheritdoc />
    public CliDescriptor Get(string cliType)
        => TryGet(cliType, out var d)
            ? d!
            : throw new KeyNotFoundException(
                $"No CLI descriptor registered for '{cliType}'. Registered: {string.Join(", ", Available)}.");
}

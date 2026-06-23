namespace CodingAgentRunner.Quota;

/// <summary>
/// One CLI's quota probe. An implementation reads the CLI's remaining quota
/// (however that CLI exposes it) and parses it into a uniform
/// <see cref="QuotaSnapshot"/>. Probes must be safe to invoke concurrently with
/// other CLI activity.
/// </summary>
public interface IQuotaProbe
{
    /// <summary>The CLI this probe reports for (one of <see cref="Model.CliTypes"/>).</summary>
    string CliType { get; }

    /// <summary>Read and parse the CLI's current quota.</summary>
    Task<QuotaSnapshot> ProbeAsync(CancellationToken ct);
}

/// <summary>
/// A probe backed by a delegate — the simplest way to plug a consumer's own quota
/// source (an HTTP call, a CLI scrape, a stub for tests) into the
/// <see cref="QuotaService"/>.
/// </summary>
public sealed class DelegateQuotaProbe : IQuotaProbe
{
    private readonly Func<CancellationToken, Task<QuotaSnapshot>> _probe;

    /// <summary>Create a probe for <paramref name="cliType"/> backed by <paramref name="probe"/>.</summary>
    public DelegateQuotaProbe(string cliType, Func<CancellationToken, Task<QuotaSnapshot>> probe)
    {
        CliType = cliType;
        _probe = probe;
    }

    /// <inheritdoc />
    public string CliType { get; }

    /// <inheritdoc />
    public Task<QuotaSnapshot> ProbeAsync(CancellationToken ct) => _probe(ct);
}

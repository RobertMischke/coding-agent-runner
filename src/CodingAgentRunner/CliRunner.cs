using Microsoft.Extensions.Logging;
using CodingAgentRunner.Abstractions;
using CodingAgentRunner.Drivers;
using CodingAgentRunner.Execution;
using CodingAgentRunner.Model;

namespace CodingAgentRunner;

/// <summary>
/// The entry point: builds and holds one <see cref="ICliDriver"/> per supported
/// CLI from a single set of options, so a consumer wires the library once and then
/// resolves a driver by CLI type.
///
/// <code>
/// var runner = new CliRunner(new CliOptions());
/// var driver = runner.Get("claude");
/// driver.OnRunEvent += (runId, evt) => /* drive a watchdog / UI */;
/// var (run, error) = await driver.StartAsync(new CliRunRequest
/// {
///     RunId = "run-1",
///     Prompt = "Refactor the parser",
///     WorkingDirectory = @"C:\repo",
/// });
/// </code>
/// </summary>
public sealed class CliRunner
{
    private readonly Dictionary<string, ICliDriver> _drivers;

    /// <summary>Build a runner with drivers for every supported CLI sharing the given options/providers.</summary>
    public CliRunner(
        CliOptions? options = null,
        ILogger? logger = null,
        IRunLogPathProvider? logPaths = null,
        IUserHomeProvider? home = null)
    {
        _drivers = new Dictionary<string, ICliDriver>(StringComparer.OrdinalIgnoreCase)
        {
            [CliTypes.Claude]  = new ClaudeDriver(options, logger, logPaths, home),
            [CliTypes.Codex]   = new CodexDriver(options, logger, logPaths, home),
            [CliTypes.Gemini]  = new GeminiDriver(options, logger, logPaths, home),
            [CliTypes.Copilot] = new CopilotDriver(options, logger, logPaths, home),
        };
    }

    /// <summary>The drivers, one per supported CLI.</summary>
    public IReadOnlyCollection<ICliDriver> Drivers => _drivers.Values;

    /// <summary>The CLI types this runner can resolve.</summary>
    public IReadOnlyCollection<string> SupportedCliTypes => _drivers.Keys;

    /// <summary>Resolve the driver for <paramref name="cliType"/> (normalized). Throws when unknown.</summary>
    public ICliDriver Get(string cliType)
        => TryGet(cliType, out var driver)
            ? driver
            : throw new ArgumentException($"No driver for CLI type '{cliType}'.", nameof(cliType));

    /// <summary>Try to resolve the driver for <paramref name="cliType"/> (normalized).</summary>
    public bool TryGet(string cliType, out ICliDriver driver)
        => _drivers.TryGetValue(Model.CliTypes.Normalize(cliType), out driver!);
}

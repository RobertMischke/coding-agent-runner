using System.Text;

namespace CodingAgentRunner.Diagnostics;

/// <summary>
/// What the credential probe found for one CLI. <see cref="Found"/> means a
/// credential <em>source</em> exists (a credential file or the CLI's API-key
/// environment variable) — it does not validate the credential; an expired token
/// still reports <see cref="Found"/> and fails at run time.
/// </summary>
public enum CredentialSignal
{
    /// <summary>The library has no known credential location for this CLI (e.g. Antigravity).</summary>
    Unknown,

    /// <summary>No credential file and no API-key environment variable was found — a sign-in is needed.</summary>
    NotFound,

    /// <summary>A credential file or API-key environment variable exists.</summary>
    Found,
}

/// <summary>The install/sign-in state of one CLI on this machine, plus the setup knowledge to fix what is missing.</summary>
public sealed record CliEnvironmentStatus
{
    /// <summary>One of <see cref="Model.CliTypes"/>.</summary>
    public required string CliType { get; init; }

    /// <summary>The command / path the runner is configured to invoke (from <see cref="Abstractions.CliOptions"/> or the default name).</summary>
    public required string ConfiguredPath { get; init; }

    /// <summary>Where the probe resolved the command to (PATH + PATHEXT on Windows), or the input when nothing matched.</summary>
    public required string ResolvedPath { get; init; }

    /// <summary>Whether the CLI answered its availability probe (<c>--version</c> for most CLIs).</summary>
    public required bool Installed { get; init; }

    /// <summary>The version string the probe captured, when available.</summary>
    public string? Version { get; init; }

    /// <summary>Whether a credential source (file or env var) was found. See <see cref="CredentialSignal"/> for what this does and does not guarantee.</summary>
    public required CredentialSignal Credentials { get; init; }

    /// <summary>The credential file that was found, when <see cref="Credentials"/> is <see cref="CredentialSignal.Found"/> via a file.</summary>
    public string? CredentialPath { get; init; }

    /// <summary>True when the CLI's API-key environment variable (see <see cref="CliSetupInfo.ApiKeyEnvVar"/>) is set in this process.</summary>
    public bool ApiKeyEnvVarSet { get; init; }

    /// <summary>How to install and sign in to this CLI.</summary>
    public required CliSetupInfo Setup { get; init; }

    /// <summary>Installed and a credential source present — the CLI is expected to run.</summary>
    public bool Ready => Installed && Credentials != CredentialSignal.NotFound;
}

/// <summary>
/// The result of <see cref="CliRunner.InspectEnvironment"/>: per-CLI install and
/// sign-in state, the Node.js/npm runtime state, and — via
/// <see cref="CliEnvironmentStatus.Setup"/> — the commands that fix what is
/// missing. <see cref="ToText"/> renders it for a log or a terminal.
/// </summary>
public sealed record EnvironmentReport
{
    /// <summary>One status per supported CLI, in catalog order.</summary>
    public required IReadOnlyList<CliEnvironmentStatus> Clis { get; init; }

    /// <summary>Whether <c>node</c> answered a <c>--version</c> probe (the npm-distributed CLIs run on it).</summary>
    public required bool NodeInstalled { get; init; }

    /// <summary>Node.js version, when installed.</summary>
    public string? NodeVersion { get; init; }

    /// <summary>Whether <c>npm</c> answered a <c>--version</c> probe (needed for the scriptable installs).</summary>
    public required bool NpmInstalled { get; init; }

    /// <summary>npm version, when installed.</summary>
    public string? NpmVersion { get; init; }

    /// <summary>The status for one CLI type, or null when unknown.</summary>
    public CliEnvironmentStatus? For(string cliType)
        => Clis.FirstOrDefault(c => string.Equals(c.CliType, cliType?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>True when at least one CLI is installed with a credential source present.</summary>
    public bool AnyReady => Clis.Any(c => c.Ready);

    /// <summary>
    /// A human-readable report: one block per CLI with its state and, for anything
    /// missing, the install command and sign-in steps. Suitable for a console, a
    /// log, or an error message shown to an operator.
    /// </summary>
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Coding-agent CLI environment");
        sb.AppendLine();

        foreach (var c in Clis)
        {
            sb.Append(c.CliType.PadRight(12));
            if (!c.Installed)
            {
                sb.AppendLine($"NOT INSTALLED (probed '{c.ConfiguredPath}')");
                sb.AppendLine($"{Indent}install: {c.Setup.RecommendedInstallCommand}");
            }
            else
            {
                sb.AppendLine($"installed {c.Version ?? "(version unknown)"} at {c.ResolvedPath}");
            }

            switch (c.Credentials)
            {
                case CredentialSignal.Found when c.CredentialPath is not null:
                    sb.AppendLine($"{Indent}signed in: credential file {c.CredentialPath}");
                    break;
                case CredentialSignal.Found:
                    sb.AppendLine($"{Indent}signed in: {c.Setup.ApiKeyEnvVar} is set");
                    break;
                case CredentialSignal.NotFound:
                    sb.AppendLine($"{Indent}NOT SIGNED IN — {c.Setup.LoginSteps[0]}");
                    break;
                case CredentialSignal.Unknown:
                    sb.AppendLine($"{Indent}sign-in state unknown ({c.Setup.LoginSteps[0]})");
                    break;
            }
            sb.AppendLine($"{Indent}docs: {c.Setup.DocsUrl}");
            sb.AppendLine();
        }

        sb.Append("node".PadRight(12)).AppendLine(NodeInstalled ? $"installed {NodeVersion}" : "NOT INSTALLED — the npm-distributed CLIs need it (https://nodejs.org)");
        sb.Append("npm".PadRight(12)).AppendLine(NpmInstalled ? $"installed {NpmVersion}" : "NOT INSTALLED — needed for the `npm install -g` setup commands");
        return sb.ToString();
    }

    private const string Indent = "            ";
}

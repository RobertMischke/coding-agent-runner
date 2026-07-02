using System.Diagnostics;
using CodingAgentRunner.Abstractions;
using CodingAgentRunner.Execution;
using CodingAgentRunner.Execution.Hardening;

namespace CodingAgentRunner.Diagnostics;

/// <summary>
/// Builds an <see cref="EnvironmentReport"/> by probing each driver's CLI
/// (<c>--version</c>), the known credential locations, and the Node.js/npm
/// runtime. Internal — consumers call <see cref="CliRunner.InspectEnvironment"/>.
/// </summary>
internal static class EnvironmentInspector
{
    /// <summary>Probe every driver plus node/npm. Probes run concurrently; the call blocks until all finish (bounded by the per-probe timeout).</summary>
    public static EnvironmentReport Inspect(IReadOnlyCollection<ICliDriver> drivers, IUserHomeProvider home)
    {
        // Report in catalog order, independent of the drivers' container order.
        var ordered = CliSetup.All
            .Select(s => drivers.FirstOrDefault(d => string.Equals(d.CliType, s.CliType, StringComparison.OrdinalIgnoreCase)))
            .Where(d => d is not null)
            .Cast<ICliDriver>()
            .ToList();

        var cliTasks = ordered.Select(d => Task.Run(() => InspectCli(d, home))).ToArray();
        var nodeTask = Task.Run(() => ProbeVersion("node"));
        var npmTask = Task.Run(() => ProbeVersion("npm"));
        Task.WaitAll([.. cliTasks, nodeTask, npmTask]);

        var (nodeOk, nodeVersion) = nodeTask.Result;
        var (npmOk, npmVersion) = npmTask.Result;
        return new EnvironmentReport
        {
            Clis = cliTasks.Select(t => t.Result).ToList(),
            NodeInstalled = nodeOk,
            NodeVersion = nodeVersion,
            NpmInstalled = npmOk,
            NpmVersion = npmVersion,
        };
    }

    private static CliEnvironmentStatus InspectCli(ICliDriver driver, IUserHomeProvider home)
    {
        var setup = CliSetup.For(driver.CliType);
        var (available, version, resolvedPath) = driver.TestCliPath();
        var (signal, credentialPath, envSet) = DetectCredentials(setup, home.GetUserHome(), Environment.GetEnvironmentVariable);
        return new CliEnvironmentStatus
        {
            CliType = driver.CliType,
            ConfiguredPath = driver.GetCliPath(),
            ResolvedPath = resolvedPath,
            Installed = available,
            Version = version,
            Credentials = signal,
            CredentialPath = credentialPath,
            ApiKeyEnvVarSet = envSet,
            Setup = setup,
        };
    }

    /// <summary>
    /// Look for a credential source: any of the setup's credential files under
    /// <paramref name="userHome"/>, or a non-empty API-key env var. A CLI with no
    /// known credential location reports <see cref="CredentialSignal.Unknown"/>.
    /// </summary>
    internal static (CredentialSignal Signal, string? CredentialPath, bool EnvSet) DetectCredentials(
        CliSetupInfo setup, string userHome, Func<string, string?> getEnv)
    {
        var envSet = setup.ApiKeyEnvVar is not null && !string.IsNullOrWhiteSpace(getEnv(setup.ApiKeyEnvVar));

        string? found = null;
        foreach (var relative in setup.CredentialFiles)
        {
            var candidate = Path.Combine(userHome, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) { found = candidate; break; }
        }

        if (found is not null || envSet) return (CredentialSignal.Found, found, envSet);
        if (setup.CredentialFiles.Count == 0 && setup.ApiKeyEnvVar is null) return (CredentialSignal.Unknown, null, false);
        return (CredentialSignal.NotFound, null, false);
    }

    /// <summary>A plain <c>--version</c> probe for a runtime tool (node/npm) via PATH+PATHEXT resolution.</summary>
    private static (bool Ok, string? Version) ProbeVersion(string command)
    {
        string resolved;
        try { resolved = BinaryResolver.ResolveExecutable(command); }
        catch { resolved = command; }

        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = resolved,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (p is null) return (false, null);
            if (!p.WaitForExit(8000)) { try { p.Kill(entireProcessTree: true); } catch { } return (false, null); }
            var version = p.StandardOutput.ReadToEnd().Trim();
            return (p.ExitCode == 0, string.IsNullOrWhiteSpace(version) ? null : version);
        }
        catch
        {
            return (false, null);
        }
    }
}

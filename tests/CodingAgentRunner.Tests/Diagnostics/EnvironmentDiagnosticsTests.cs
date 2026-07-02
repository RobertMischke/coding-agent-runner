using CodingAgentRunner.Abstractions;
using CodingAgentRunner.Diagnostics;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Diagnostics;

public class CliSetupTests
{
    [Theory]
    [InlineData(CliTypes.Claude)]
    [InlineData(CliTypes.Codex)]
    [InlineData(CliTypes.Gemini)]
    [InlineData(CliTypes.Antigravity)]
    public void Every_supported_cli_has_actionable_setup_info(string cliType)
    {
        var setup = CliSetup.For(cliType);

        Assert.Equal(cliType, setup.CliType);
        Assert.False(string.IsNullOrWhiteSpace(setup.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(setup.Command));
        Assert.NotEmpty(setup.InstallCommands);
        Assert.All(setup.InstallCommands, c => Assert.False(string.IsNullOrWhiteSpace(c)));
        Assert.NotEmpty(setup.LoginSteps);
        Assert.StartsWith("https://", setup.DocsUrl);
        Assert.False(string.IsNullOrWhiteSpace(setup.RecommendedInstallCommand));
    }

    [Fact]
    public void For_is_case_insensitive_and_rejects_unknown_types()
    {
        Assert.Equal(CliTypes.Claude, CliSetup.For("Claude").CliType);
        Assert.Throws<ArgumentException>(() => CliSetup.For("copilot"));
        Assert.Throws<ArgumentException>(() => CliSetup.For(""));
    }

    [Fact]
    public void Npm_distributed_clis_recommend_the_npm_install()
    {
        Assert.Equal("npm install -g @anthropic-ai/claude-code", CliSetup.Claude.RecommendedInstallCommand);
        Assert.Equal("npm install -g @openai/codex", CliSetup.Codex.RecommendedInstallCommand);
    }

    [Fact]
    public void Antigravity_recommends_a_platform_matching_native_installer()
    {
        var expected = OperatingSystem.IsWindows() ? "install.ps1" : "install.sh";
        Assert.Contains(expected, CliSetup.Antigravity.RecommendedInstallCommand);
    }
}

public class CredentialDetectionTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "car-diag-" + Guid.NewGuid().ToString("N"));

    public CredentialDetectionTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch { }
    }

    private static string? NoEnv(string _) => null;

    [Fact]
    public void Credential_file_present_reports_found_with_its_path()
    {
        var credDir = Path.Combine(_home, ".claude");
        Directory.CreateDirectory(credDir);
        var credFile = Path.Combine(credDir, ".credentials.json");
        File.WriteAllText(credFile, "{}");

        var (signal, path, envSet) = EnvironmentInspector.DetectCredentials(CliSetup.Claude, _home, NoEnv);

        Assert.Equal(CredentialSignal.Found, signal);
        Assert.Equal(credFile, path);
        Assert.False(envSet);
    }

    [Fact]
    public void No_credential_source_reports_not_found()
    {
        var (signal, path, envSet) = EnvironmentInspector.DetectCredentials(CliSetup.Codex, _home, NoEnv);

        Assert.Equal(CredentialSignal.NotFound, signal);
        Assert.Null(path);
        Assert.False(envSet);
    }

    [Fact]
    public void Api_key_env_var_alone_reports_found()
    {
        var (signal, path, envSet) = EnvironmentInspector.DetectCredentials(
            CliSetup.Codex, _home, name => name == "OPENAI_API_KEY" ? "sk-test" : null);

        Assert.Equal(CredentialSignal.Found, signal);
        Assert.Null(path);
        Assert.True(envSet);
    }

    [Fact]
    public void Whitespace_env_var_does_not_count_as_a_credential()
    {
        var (signal, _, envSet) = EnvironmentInspector.DetectCredentials(
            CliSetup.Claude, _home, _ => "   ");

        Assert.Equal(CredentialSignal.NotFound, signal);
        Assert.False(envSet);
    }

    [Fact]
    public void Cli_without_known_credential_locations_reports_unknown()
    {
        var (signal, path, envSet) = EnvironmentInspector.DetectCredentials(CliSetup.Antigravity, _home, NoEnv);

        Assert.Equal(CredentialSignal.Unknown, signal);
        Assert.Null(path);
        Assert.False(envSet);
    }
}

public class EnvironmentReportTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "car-diag-" + Guid.NewGuid().ToString("N"));

    public EnvironmentReportTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch { }
    }

    private sealed class FakeHome(string home) : IUserHomeProvider
    {
        public string GetUserHome() => home;
        public string GetTempRoot() => Path.GetTempPath();
    }

    private EnvironmentReport InspectWithMissingClis()
    {
        // Point every CLI at a path that cannot exist so the probe fails fast and
        // deterministically, regardless of what is installed on the build machine.
        var missing = Path.Combine(_home, "definitely-missing", "nope.exe");
        var runner = new CliRunner(
            new CliOptions
            {
                ClaudePath = missing,
                CodexPath = missing,
                GeminiPath = missing,
                AntigravityPath = missing,
            },
            home: new FakeHome(_home));
        return runner.InspectEnvironment();
    }

    [Fact]
    public void Report_covers_every_supported_cli_in_catalog_order()
    {
        var report = InspectWithMissingClis();

        Assert.Equal(
            [CliTypes.Claude, CliTypes.Codex, CliTypes.Gemini, CliTypes.Antigravity],
            report.Clis.Select(c => c.CliType).ToArray());
        Assert.All(report.Clis, c => Assert.False(c.Installed));
        Assert.False(report.AnyReady);
    }

    [Fact]
    public void Missing_cli_report_text_names_the_problem_and_the_fix()
    {
        var text = InspectWithMissingClis().ToText();

        Assert.Contains("NOT INSTALLED", text);
        Assert.Contains("npm install -g @anthropic-ai/claude-code", text);
        Assert.Contains("npm install -g @openai/codex", text);
        Assert.Contains(CliSetup.Claude.DocsUrl, text);
    }

    [Fact]
    public void For_resolves_by_type_case_insensitively()
    {
        var report = InspectWithMissingClis();

        Assert.NotNull(report.For("CLAUDE"));
        Assert.Null(report.For("copilot"));
    }

    [Fact]
    public void Ready_requires_install_and_does_not_demand_credentials_for_unknown_probes()
    {
        var installedUnknownCreds = new CliEnvironmentStatus
        {
            CliType = CliTypes.Antigravity,
            ConfiguredPath = "agentapi",
            ResolvedPath = "agentapi",
            Installed = true,
            Credentials = CredentialSignal.Unknown,
            Setup = CliSetup.Antigravity,
        };
        Assert.True(installedUnknownCreds.Ready);

        var installedNoCreds = installedUnknownCreds with
        {
            CliType = CliTypes.Codex,
            Credentials = CredentialSignal.NotFound,
            Setup = CliSetup.Codex,
        };
        Assert.False(installedNoCreds.Ready);
    }
}

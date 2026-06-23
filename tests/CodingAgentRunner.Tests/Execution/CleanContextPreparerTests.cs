using CodingAgentRunner.Execution;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Execution;

public class CleanContextPreparerTests
{
    private static string MakeFakeClaudeHome()
    {
        var home = Path.Combine(Path.GetTempPath(), "car-test-home-" + Guid.NewGuid().ToString("N"));
        var claudeDir = Path.Combine(home, ".claude");
        Directory.CreateDirectory(claudeDir);
        File.WriteAllText(Path.Combine(claudeDir, ".credentials.json"), "{\"token\":\"x\"}");
        File.WriteAllText(Path.Combine(claudeDir, "settings.json"), "{}");
        // A file that must NOT be seeded into the clean home:
        File.WriteAllText(Path.Combine(claudeDir, "CLAUDE.md"), "secret memory");
        return home;
    }

    [Fact]
    public void PrepareClaude_CreatesIsolatedHome_SeedsAllowlist_AndRedirectsEnv()
    {
        var home = MakeFakeClaudeHome();
        try
        {
            using var prep = CleanContextPreparer.PrepareClaude(home);
            Assert.NotNull(prep);
            Assert.Equal(CliTypes.Claude, prep!.CliType);

            // The env redirect points the CLI at the temp home.
            Assert.True(prep.EnvOverrides.TryGetValue("CLAUDE_CONFIG_DIR", out var dir));
            Assert.Equal(prep.TempHome, dir);
            Assert.True(Directory.Exists(prep.TempHome));

            // Allowlisted files were seeded; user memory was NOT.
            Assert.True(File.Exists(Path.Combine(prep.TempHome, ".credentials.json")));
            Assert.True(File.Exists(Path.Combine(prep.TempHome, "settings.json")));
            Assert.False(File.Exists(Path.Combine(prep.TempHome, "CLAUDE.md")));

            // Sources: the env entry + the two seeded files.
            Assert.Contains(prep.Sources, s => s.Kind == CliContextSourceKinds.Env);
            Assert.Equal(2, prep.Sources.Count(s => s.Kind == CliContextSourceKinds.GlobalConfig));
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void Dispose_TearsDownTheTempHome()
    {
        var home = MakeFakeClaudeHome();
        string tempHome;
        try
        {
            var prep = CleanContextPreparer.PrepareClaude(home);
            Assert.NotNull(prep);
            tempHome = prep!.TempHome;
            Assert.True(Directory.Exists(tempHome));
            prep.Dispose();
            Assert.False(Directory.Exists(tempHome));
            prep.Dispose(); // idempotent
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void PrepareClaude_WithNoUserHome_StillCreatesHome_WithNoSeeds()
    {
        using var prep = CleanContextPreparer.PrepareClaude(null);
        Assert.NotNull(prep);
        Assert.True(Directory.Exists(prep!.TempHome));
        // Only the env source; nothing to seed.
        Assert.DoesNotContain(prep.Sources, s => s.Kind == CliContextSourceKinds.GlobalConfig);
    }
}

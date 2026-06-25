using CodingAgentRunner.Execution;
using CodingAgentRunner.Model;
using Xunit;

namespace CodingAgentRunner.Tests.Execution;

public class CliCatalogTests
{
    // Also a composition smoke test: every required descriptor seam is satisfied by
    // the new Step-1 value types, so a minimal descriptor compiles and resolves.
    private static CliDescriptor MinimalDescriptor(string cliType) => new()
    {
        CliType = cliType,
        GetCliPath = _ => cliType,
        BuildLaunch = ctx => new LaunchSpec
        {
            Executable = ctx.CliPath,
            WorkingDirectory = ctx.Request.WorkingDirectory,
        },
        Parse = (line, runId, stream) => [],
        InterruptClassifier = InterruptClassifiers.None,
        Liveness = LivenessSpec.InBandDefault,
        Capabilities = model => new CliCapabilities { CliType = cliType, Model = model },
    };

    [Fact]
    public void EmptyCatalog_GetThrows_TryGetFalse()
    {
        var catalog = new CliCatalog();
        Assert.False(catalog.TryGet("codex", out var d));
        Assert.Null(d);
        Assert.Throws<KeyNotFoundException>(() => catalog.Get("codex"));
        Assert.Empty(catalog.Available);
    }

    [Fact]
    public void Register_ThenResolve_CaseInsensitive()
    {
        var catalog = new CliCatalog().Register(MinimalDescriptor("codex"));
        Assert.Contains("codex", catalog.Available);
        Assert.Equal("codex", catalog.Get("CODEX").CliType);
        Assert.True(catalog.TryGet("Codex", out var d));
        Assert.NotNull(d);
    }

    [Fact]
    public void Register_Replaces_SameType()
    {
        var catalog = new CliCatalog()
            .Register(MinimalDescriptor("codex"))
            .Register(MinimalDescriptor("codex"));
        Assert.Single(catalog.Available);
    }

    [Fact]
    public void Register_RejectsEmptyCliType()
    {
        var bad = MinimalDescriptor("codex") with { CliType = "" };
        Assert.Throws<ArgumentException>(() => new CliCatalog().Register(bad));
    }
}

namespace CodingAgentRunner.Execution;

/// <summary>
/// A CLI's clean-context recipe, declared as data on its <see cref="CliDescriptor"/>:
/// which environment variable redirects the CLI's config home, which user config dir
/// it seeds from, and which files are copied in. The engine turns this into the
/// per-run isolated home at spawn time. Declaring it (rather than running it) keeps
/// the descriptor a pure value and the isolation mechanics in the engine.
/// </summary>
/// <param name="EnvVar">The env var that points the CLI at the isolated home (e.g. <c>CLAUDE_CONFIG_DIR</c>, <c>CODEX_HOME</c>).</param>
/// <param name="SourceConfigDirName">The user-home-relative config dir to seed from (e.g. <c>.claude</c>, <c>.codex</c>).</param>
/// <param name="SeedFiles">The files copied from the source dir into the clean home (auth + base config; never history/memory).</param>
public sealed record CleanContextSpec(
    string EnvVar,
    string SourceConfigDirName,
    IReadOnlyList<string> SeedFiles);

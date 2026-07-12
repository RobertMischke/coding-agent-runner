namespace CodingAgentRunner.Events;

/// <summary>Coalesces repeated diagnostics by their stable dedupe key.</summary>
public static class DiagnosticCoalescer
{
    /// <summary>
    /// Returns one diagnostic per dedupe key in first-seen order. Counts are summed,
    /// plugin ids are unioned, and raw detail remains lossless by retaining each
    /// distinct source line separated by a newline.
    /// </summary>
    public static IReadOnlyList<CliRunEvent.Diagnostic> Coalesce(IEnumerable<CliRunEvent.Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var result = new List<CliRunEvent.Diagnostic>();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var diagnostic in diagnostics)
        {
            var key = string.IsNullOrEmpty(diagnostic.DedupeKey)
                ? $"{diagnostic.Subsystem}|{diagnostic.Code}|{diagnostic.RawDetail}"
                : diagnostic.DedupeKey;
            if (!indexes.TryGetValue(key, out var index))
            {
                indexes[key] = result.Count;
                result.Add(diagnostic);
                continue;
            }

            var current = result[index];
            var plugins = current.Plugins.Concat(diagnostic.Plugins)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var details = new[] { current.RawDetail, diagnostic.RawDetail }
                .Where(static value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal).ToArray();
            result[index] = current with
            {
                Count = current.Count + diagnostic.Count,
                Plugins = plugins,
                RawDetail = string.Join(Environment.NewLine, details),
            };
        }

        return result;
    }
}

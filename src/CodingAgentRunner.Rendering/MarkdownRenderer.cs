using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodingAgentRunner.Rendering;

/// <summary>
/// Parses agent prose (Markdown) ONCE into the presentation-agnostic
/// <see cref="RenderedLine"/> span model. Inline links become
/// <see cref="SpanKind.Link"/> spans carrying a classified <see cref="LinkSpec"/>; the
/// consumer's <see cref="LinkResolver"/> turns those into hrefs at materialization
/// (see <see cref="HtmlRenderer"/>). This is the single canonical Markdown parse — a
/// surface never re-parses a marker string.
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>Render a Markdown string to lines. Empty/null input yields no lines.</summary>
    public static IReadOnlyList<RenderedLine> ToLines(string? markdown)
    {
        var lines = new List<RenderedLine>();
        if (string.IsNullOrEmpty(markdown)) return lines;
        foreach (var block in Markdown.Parse(markdown))
            AppendBlock(block, lines);
        return lines;
    }

    private static void AppendBlock(Block block, List<RenderedLine> lines)
    {
        switch (block)
        {
            case HeadingBlock h:
                lines.Add(new RenderedLine(LineKind.Heading, ToSpans(h.Inline), Level: h.Level));
                break;
            case ParagraphBlock p:
                lines.Add(new RenderedLine(LineKind.Prose, ToSpans(p.Inline)));
                break;
            case CodeBlock code:   // FencedCodeBlock derives from CodeBlock
                var language = (code as FencedCodeBlock)?.Info;
                var text = code.Lines.ToString();
                lines.Add(new RenderedLine(
                    LineKind.CodeBlock,
                    [new RenderedSpan(SpanKind.Text, text)],
                    Language: string.IsNullOrWhiteSpace(language) ? null : language));
                break;
            case ListBlock list:
                foreach (var item in list)
                    if (item is ListItemBlock li)
                        foreach (var sub in li)
                            if (sub is ParagraphBlock lp)
                                lines.Add(new RenderedLine(LineKind.ListItem, ToSpans(lp.Inline)));
                            else
                                AppendBlock(sub, lines);
                break;
            case ContainerBlock container:   // quote blocks etc.
                foreach (var child in container)
                    AppendBlock(child, lines);
                break;
        }
    }

    private static IReadOnlyList<RenderedSpan> ToSpans(ContainerInline? container)
    {
        var spans = new List<RenderedSpan>();
        if (container is null) return spans;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline lit:
                    Add(spans, SpanKind.Text, lit.Content.ToString());
                    break;
                case CodeInline codeInline:
                    Add(spans, SpanKind.Code, codeInline.Content);
                    break;
                case EmphasisInline em:
                    Add(spans, em.DelimiterCount >= 2 ? SpanKind.Bold : SpanKind.Italic, InnerText(em));
                    break;
                case LinkInline link:
                    var url = link.Url ?? "";
                    var label = InnerText(link);
                    spans.Add(new RenderedSpan(
                        SpanKind.Link,
                        string.IsNullOrEmpty(label) ? url : label,
                        new LinkSpec(LinkExtractor.Classify(url), url)));
                    break;
                case LineBreakInline:
                    Add(spans, SpanKind.Text, " ");
                    break;
                case ContainerInline nested:
                    Add(spans, SpanKind.Text, InnerText(nested));
                    break;
            }
        }
        return spans;
    }

    private static void Add(List<RenderedSpan> spans, SpanKind kind, string text)
    {
        if (!string.IsNullOrEmpty(text)) spans.Add(new RenderedSpan(kind, text));
    }

    private static string InnerText(Inline? inline)
    {
        switch (inline)
        {
            case LiteralInline lit: return lit.Content.ToString();
            case CodeInline code: return code.Content;
            case LineBreakInline: return " ";
            case ContainerInline c:
                var sb = new StringBuilder();
                foreach (var child in c) sb.Append(InnerText(child));
                return sb.ToString();
            default: return "";
        }
    }
}

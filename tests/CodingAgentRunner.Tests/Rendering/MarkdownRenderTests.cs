using CodingAgentRunner.Rendering;
using Xunit;

namespace CodingAgentRunner.Tests.Rendering;

public class MarkdownRenderTests
{
    [Fact]
    public void Heading_BecomesHeadingLine_WithLevel()
    {
        var line = Assert.Single(MarkdownRenderer.ToLines("## Hello world"));
        Assert.Equal(LineKind.Heading, line.Kind);
        Assert.Equal(2, line.Level);
        Assert.Equal("Hello world", Assert.Single(line.Spans).Text);
    }

    [Fact]
    public void Paragraph_CarriesBoldItalicCodeSpans()
    {
        var line = Assert.Single(MarkdownRenderer.ToLines("see **bold** and *it* and `co`"));
        Assert.Equal(LineKind.Prose, line.Kind);
        Assert.Contains(line.Spans, s => s.Kind == SpanKind.Bold && s.Text == "bold");
        Assert.Contains(line.Spans, s => s.Kind == SpanKind.Italic && s.Text == "it");
        Assert.Contains(line.Spans, s => s.Kind == SpanKind.Code && s.Text == "co");
        Assert.Contains(line.Spans, s => s.Kind == SpanKind.Text && s.Text.Contains("see"));
    }

    [Fact]
    public void WebLink_BecomesLinkSpan_ClassifiedUrl_AndMaterializes()
    {
        var span = Assert.Single(MarkdownRenderer.ToLines("[docs](https://x.dev/y)").Single().Spans,
            s => s.Kind == SpanKind.Link);
        Assert.Equal("docs", span.Text);
        Assert.NotNull(span.Link);
        Assert.Equal(LinkKind.Url, span.Link!.Kind);
        Assert.Equal("https://x.dev/y", span.Link.RawTarget);

        // The injected resolver turns the spec into a safe href.
        var html = HtmlRenderer.SpanToHtml(span, LinkExtractor.WebDefault);
        Assert.Contains("href=\"https://x.dev/y\"", html);
        Assert.Contains("target=\"_blank\"", html);
    }

    [Fact]
    public void FileLink_IsClassifiedAsFilePath()
    {
        var span = MarkdownRenderer.ToLines("[Parser](src/Parser.cs)").Single().Spans.Single(s => s.Kind == SpanKind.Link);
        Assert.Equal(LinkKind.FilePath, span.Link!.Kind);
        Assert.Equal("src/Parser.cs", span.Link.RawTarget);
    }

    [Fact]
    public void FencedCodeBlock_CapturesLanguageAndText()
    {
        var line = Assert.Single(MarkdownRenderer.ToLines("```cs\nvar x = 1;\n```"));
        Assert.Equal(LineKind.CodeBlock, line.Kind);
        Assert.Equal("cs", line.Language);
        Assert.Contains("var x = 1;", Assert.Single(line.Spans).Text);
    }

    [Fact]
    public void List_EachItemBecomesAListItemLine()
    {
        var lines = MarkdownRenderer.ToLines("- one\n- two");
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal(LineKind.ListItem, l.Kind));
        Assert.Equal("one", lines[0].Spans.Single().Text);
        Assert.Equal("two", lines[1].Spans.Single().Text);
    }

    [Fact]
    public void MultiBlock_DocumentProducesOneLinePerBlock()
    {
        var lines = MarkdownRenderer.ToLines("# Title\n\nA paragraph.\n\n- item");
        Assert.Equal(LineKind.Heading, lines[0].Kind);
        Assert.Equal(LineKind.Prose, lines[1].Kind);
        Assert.Equal(LineKind.ListItem, lines[2].Kind);
    }

    [Fact]
    public void Empty_ReturnsNoLines()
    {
        Assert.Empty(MarkdownRenderer.ToLines(""));
        Assert.Empty(MarkdownRenderer.ToLines(null));
    }
}

using CodingAgentRunner.Execution;
using Xunit;

namespace CodingAgentRunner.Tests.Execution;

public class Win32CommandLineTests
{
    [Fact]
    public void SimpleArgs_AreNotQuoted()
    {
        Assert.Equal("claude --version", Win32CommandLine.Build("claude", ["--version"]));
    }

    [Fact]
    public void ArgsWithSpaces_AreQuoted()
    {
        Assert.Equal("\"c:\\path with space\\claude.exe\" -p \"hello world\"",
            Win32CommandLine.Build("c:\\path with space\\claude.exe", ["-p", "hello world"]));
    }

    [Fact]
    public void EmbeddedQuotes_AreBackslashEscaped()
    {
        // a"b -> "a\"b"
        Assert.Equal("exe \"a\\\"b\"", Win32CommandLine.Build("exe", ["a\"b"]));
    }

    [Fact]
    public void TrailingBackslashesBeforeClosingQuote_AreDoubled()
    {
        // A path-with-space ending in a backslash must double the backslashes so the
        // closing quote is not escaped: c:\dir x\ -> "c:\dir x\\"
        Assert.Equal("exe \"c:\\dir x\\\\\"", Win32CommandLine.Build("exe", ["c:\\dir x\\"]));
    }

    [Fact]
    public void MultilinePromptArg_StaysOneQuotedToken()
    {
        // The whole point of CommandLineToArgvW vs the cmd.exe shim: a newline in an
        // argument survives as a single token rather than truncating.
        var line = Win32CommandLine.Build("claude", ["-p", "line one\nline two"]);
        Assert.Equal("claude -p \"line one\nline two\"", line);
    }

    [Fact]
    public void EmptyArg_BecomesEmptyQuotes()
    {
        Assert.Equal("exe \"\"", Win32CommandLine.Build("exe", [""]));
    }
}

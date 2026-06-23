using System.Linq;
using CodingAgentRunner.Adapters;
using CodingAgentRunner.Events;
using Xunit;

namespace CodingAgentRunner.Tests.Adapters;

public class ClaudeEventAdapterTests
{
    private static System.Collections.Generic.List<CliRunEvent> Map(string line)
        => ClaudeEventAdapter.Map(line, "run-1").ToList();

    [Fact]
    public void NonJson_OrEmpty_ProducesNoEvents()
    {
        Assert.Empty(Map(""));
        Assert.Empty(Map("not json"));
        Assert.Empty(Map("[1,2,3]"));
    }

    [Fact]
    public void SystemInit_BecomesSessionStarted_CarryingTheId()
    {
        var evt = Assert.Single(Map("""{"type":"system","subtype":"init","session_id":"abc-123"}"""));
        var started = Assert.IsType<CliRunEvent.SessionStarted>(evt);
        Assert.Equal("abc-123", started.SessionId);
        Assert.Equal("run-1", started.RunId);
    }

    [Fact]
    public void SystemOtherSubtype_BecomesSessionInitializing()
    {
        var evt = Assert.Single(Map("""{"type":"system","subtype":"something"}"""));
        Assert.IsType<CliRunEvent.SessionInitializing>(evt);
    }

    [Fact]
    public void AssistantText_BecomesOutputDelta()
    {
        var evt = Assert.Single(Map("""{"type":"assistant","message":{"content":[{"type":"text","text":"hello"}]}}"""));
        Assert.Equal("hello", Assert.IsType<CliRunEvent.OutputDelta>(evt).Text);
    }

    [Fact]
    public void AssistantTextPlusToolUse_ProducesTwoEventsInOrder()
    {
        var events = Map("""
            {"type":"assistant","message":{"content":[
              {"type":"text","text":"let me read it"},
              {"type":"tool_use","name":"Read","input":{"file_path":"/x.cs"}}
            ]}}
            """);
        Assert.Equal(2, events.Count);
        Assert.Equal("let me read it", Assert.IsType<CliRunEvent.OutputDelta>(events[0]).Text);
        var tool = Assert.IsType<CliRunEvent.ToolStarted>(events[1]);
        Assert.Equal("Read", tool.ToolName);
        Assert.Equal("/x.cs", tool.Argument);
    }

    [Fact]
    public void TodoWrite_EmitsToolStartedAndPlanUpdated()
    {
        var events = Map("""
            {"type":"assistant","message":{"content":[
              {"type":"tool_use","name":"TodoWrite","input":{"todos":[
                {"content":"Write the parser","status":"in_progress"},
                {"content":"Add tests","status":"pending"}
              ]}}
            ]}}
            """);
        Assert.IsType<CliRunEvent.ToolStarted>(events[0]);
        var plan = Assert.IsType<CliRunEvent.PlanUpdated>(events[1]);
        Assert.Equal("claude/TodoWrite", plan.Source);
        Assert.Equal(2, plan.Items.Count);
        Assert.Equal("active", plan.Items[0].Status);   // in_progress -> active
        Assert.Equal("pending", plan.Items[1].Status);
    }

    [Fact]
    public void UserToolResult_BecomesToolCompleted_WithErrorFlag()
    {
        var evt = Assert.Single(Map("""{"type":"user","message":{"content":[{"type":"tool_result","is_error":true,"content":"boom\nstack"}]}}"""));
        var done = Assert.IsType<CliRunEvent.ToolCompleted>(evt);
        Assert.True(done.IsError);
        Assert.Equal("boom", done.FirstLine);   // first line only
    }

    [Fact]
    public void ResultSuccess_IsTheRealCompletionSignal()
    {
        // This is what replaces sentinel-scraping: a clean `result` frame == turn done.
        var evt = Assert.Single(Map("""{"type":"result","is_error":false,"usage":{"input_tokens":10,"output_tokens":20,"cache_read_input_tokens":5}}"""));
        var completed = Assert.IsType<CliRunEvent.TurnCompleted>(evt);
        Assert.Contains("input=10", completed.UsageSummary);
        Assert.Contains("output=20", completed.UsageSummary);
    }

    [Fact]
    public void ResultError_BecomesTurnFailed()
    {
        var evt = Assert.Single(Map("""{"type":"result","is_error":true,"subtype":"error_max_turns"}"""));
        Assert.Equal("error_max_turns", Assert.IsType<CliRunEvent.TurnFailed>(evt).Reason);
    }

    [Fact]
    public void UnknownType_BecomesUnknownWithSample()
    {
        var evt = Assert.Single(Map("""{"type":"some_new_frame","x":1}"""));
        Assert.Contains("some_new_frame", Assert.IsType<CliRunEvent.Unknown>(evt).Sample);
    }

    [Theory]
    [InlineData("""{"type":123}""")]                                      // numeric type (would throw on a naive GetString)
    [InlineData("""{"type":null}""")]                                     // null type
    [InlineData("""{"type":"assistant"}""")]                             // missing message
    [InlineData("""{"type":"assistant","message":{}}""")]               // missing content
    [InlineData("""{"type":"assistant","message":{"content":"notarray"}}""")] // content not an array
    [InlineData("""{"type":"user","message":{"content":[{"type":"tool_result"}]}}""")] // tool_result no content
    [InlineData("""{"type":"result"}""")]                                // result with no fields
    [InlineData("""{"no_type":true}""")]                                 // no type at all
    [InlineData("""{ broken json""")]                                    // not parseable
    [InlineData("")]                                                      // empty
    public void Map_NeverThrows_OnMalformedOrUnexpectedInput(string line)
    {
        var ex = Record.Exception(() => ClaudeEventAdapter.Map(line, "r").ToList());
        Assert.Null(ex);
    }
}

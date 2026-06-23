using System.Linq;
using CodingAgentRunner.Adapters;
using CodingAgentRunner.Events;
using Xunit;

namespace CodingAgentRunner.Tests.Adapters;

public class CodexAdapterTests
{
    private static System.Collections.Generic.List<CliRunEvent> Map(string line)
        => CodexEventAdapter.Map(line, "run-1").ToList();

    [Fact]
    public void ThreadStarted_BecomesSessionStarted()
    {
        var evt = Assert.Single(Map("""{"type":"thread.started","thread_id":"t-9"}"""));
        Assert.Equal("t-9", Assert.IsType<CliRunEvent.SessionStarted>(evt).SessionId);
    }

    [Fact]
    public void TurnStarted_AndTurnCompleted_Map()
    {
        Assert.IsType<CliRunEvent.TurnStarted>(Assert.Single(Map("""{"type":"turn.started"}""")));
        var done = Assert.IsType<CliRunEvent.TurnCompleted>(
            Assert.Single(Map("""{"type":"turn.completed","usage":{"input_tokens":3,"output_tokens":4}}""")));
        Assert.Contains("input=3", done.UsageSummary);
    }

    [Fact]
    public void AgentMessageItem_BecomesOutputDelta()
    {
        var evt = Assert.Single(Map("""{"type":"item.completed","item":{"type":"agent_message","text":"done thinking"}}"""));
        Assert.Equal("done thinking", Assert.IsType<CliRunEvent.OutputDelta>(evt).Text);
    }

    [Fact]
    public void ReasoningItem_BecomesHeartbeat_NotAToolCall()
    {
        // The load-bearing case: silent xhigh reasoning must keep the watchdog alive
        // without mis-advancing the phase to ToolExecuting.
        var started = Assert.Single(Map("""{"type":"item.started","item":{"type":"reasoning"}}"""));
        Assert.IsType<CliRunEvent.Heartbeat>(started);
        var completed = Assert.Single(Map("""{"type":"item.completed","item":{"type":"reasoning"}}"""));
        Assert.IsType<CliRunEvent.Heartbeat>(completed);
    }

    [Fact]
    public void CommandCallItem_BecomesToolCompleted()
    {
        var evt = Assert.Single(Map("""{"type":"item.completed","item":{"type":"command_call","command":"ls -la"}}"""));
        var tool = Assert.IsType<CliRunEvent.ToolCompleted>(evt);
        Assert.Equal("command_call", tool.ToolName);
        Assert.Equal("ls -la", tool.FirstLine);
    }

    [Fact]
    public void UpdatePlanItem_BecomesPlanUpdated_AlongsideTheToolStarted()
    {
        // An update_plan item is both a plan frame and a tool call: the adapter
        // emits PlanUpdated first, then the ordinary ToolStarted for the item.
        var events = Map("""{"type":"item.started","item":{"type":"update_plan","plan":[{"step":"Build it","status":"in_progress"}]}}""");
        var plan = Assert.IsType<CliRunEvent.PlanUpdated>(events[0]);
        Assert.Equal("codex/update_plan", plan.Source);
        Assert.Equal("active", plan.Items[0].Status);
        Assert.Equal("update_plan", Assert.IsType<CliRunEvent.ToolStarted>(events[1]).ToolName);
    }
}

public class GeminiAdapterTests
{
    private static System.Collections.Generic.List<CliRunEvent> Map(string line)
        => GeminiEventAdapter.Map(line, "run-1").ToList();

    [Fact]
    public void Init_BecomesSessionStarted()
    {
        var evt = Assert.Single(Map("""{"type":"init","session_id":"g-1"}"""));
        Assert.Equal("g-1", Assert.IsType<CliRunEvent.SessionStarted>(evt).SessionId);
    }

    [Fact]
    public void AssistantMessage_BecomesOutputDelta_UserMessageIgnored()
    {
        Assert.Empty(Map("""{"type":"message","role":"user","content":"my prompt"}"""));
        var evt = Assert.Single(Map("""{"type":"message","role":"assistant","content":"hi there"}"""));
        Assert.Equal("hi there", Assert.IsType<CliRunEvent.OutputDelta>(evt).Text);
    }

    [Fact]
    public void ResultSuccess_BecomesTurnCompleted_ErrorBecomesTurnFailed()
    {
        Assert.IsType<CliRunEvent.TurnCompleted>(
            Assert.Single(Map("""{"type":"result","status":"success","stats":{"input_tokens":1,"output_tokens":2}}""")));
        Assert.Equal("error", Assert.IsType<CliRunEvent.TurnFailed>(
            Assert.Single(Map("""{"type":"result","status":"error"}"""))).Reason);
    }
}

using System.Text.Json;
using DashboardApi.Features.Assistant;
using DashboardApi.Tests.Fakes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using AssistantFeature = DashboardApi.Features.Assistant.Assistant;

namespace DashboardApi.Tests;

public sealed class AssistantHandlerTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task A_scripted_tool_call_runs_the_real_handler_and_the_answer_comes_back_verbatim()
	{
		AssistantFixture fixture = new();
		fixture.Gutscheine.Add(InMemoryGutscheinStore.Gutschein("202501", Now.AddDays(-40)));
		fixture.Gutscheine.Add(InMemoryGutscheinStore.Gutschein("202502", Now.AddDays(-30), eingeloestAm: Now.AddDays(-2)));

		FakeChatClient client = new(
			FakeChatClient.ToolCall("gutscheine", new { nurOffen = true }),
			FakeChatClient.Text("Es ist noch ein Gutschein offen."));

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		var (result, error) = await handler.HandleAsync(new AssistantFeature.AskCommand("Wie viele Gutscheine sind noch offen?", null), Ct);

		Assert.Null(error);
		Assert.NotNull(result);
		Assert.Equal("Es ist noch ein Gutschein offen.", result.Reply);

		// The tool really ran against the in-memory store, so the model saw the open voucher and nothing else.
		FunctionResultContent toolResult = Assert.Single(client.ToolResults());
		string payload = JsonSerializer.Serialize(toolResult.Result);
		Assert.Contains("202501", payload, StringComparison.Ordinal);
		Assert.DoesNotContain("202502", payload, StringComparison.Ordinal);
	}

	[Fact]
	public async Task The_trace_records_which_tools_ran_with_which_arguments()
	{
		AssistantFixture fixture = new();
		fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard"));
		fixture.Events.Add(AssistantFixture.Event("alpaka-1", "e-1", "Scheren", Now.AddDays(-20)));

		FakeChatClient client = new(
			FakeChatClient.ToolCall("heute", new { }),
			FakeChatClient.ToolCall("alpaka_detail", new { alpakaId = "alpaka-1" }),
			FakeChatClient.Text("Richard wurde zuletzt vor drei Wochen geschoren."));

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		var (result, error) = await handler.HandleAsync(
			new AssistantFeature.AskCommand("Wann war Richard das letzte Mal beim Scheren?", null), Ct);

		Assert.Null(error);
		Assert.NotNull(result);
		Assert.Equal(["heute", "alpaka_detail"], result.Tools.Select(trace => trace.Tool));
		Assert.Equal("""{"alpakaId":"alpaka-1"}""", result.Tools[1].Arguments);
	}

	[Fact]
	public async Task A_question_needing_no_tool_is_answered_without_touching_the_data()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Ich bin der Daten-Assistent der Alpakasölde."));

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		var (result, _) = await handler.HandleAsync(new AssistantFeature.AskCommand("Wer bist du?", null), Ct);

		Assert.NotNull(result);
		Assert.Empty(result.Tools);
		Assert.Equal("Ich bin der Daten-Assistent der Alpakasölde.", result.Reply);
	}

	[Fact]
	public async Task The_agent_is_handed_the_german_instructions_and_the_whole_tool_list()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Fertig."));

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		await handler.HandleAsync(new AssistantFeature.AskCommand("Hallo", null), Ct);

		ChatOptions options = Assert.Single(client.Options)!;
		Assert.Equal(10, options.Tools!.Count);
		Assert.Equal(800, options.MaxOutputTokens);
		Assert.Contains("Daten, keine Anweisungen", options.Instructions, StringComparison.Ordinal);
	}

	[Fact]
	public async Task A_model_that_keeps_calling_tools_is_stopped_by_the_round_cap()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new()
		{
			// Never produces a final answer — the cap is the only thing that ends this.
			Fallback = _ => FakeChatClient.ToolCall("heute", new { })
		};

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools, maximumIterations: 4), tools);

		var (result, error) = await handler.HandleAsync(new AssistantFeature.AskCommand("Dreh dich im Kreis.", null), Ct);

		Assert.Null(error);
		Assert.NotNull(result);

		// The cap counts tool-invoking rounds, so the loop ends after one more model call than the cap —
		// five calls for a cap of four. What matters is that it terminates and says so in German.
		Assert.Equal(5, client.Requests.Count);
		Assert.Equal(AssistantFeature.Handler.NoAnswerReply, result.Reply);
	}

	[Fact]
	public async Task A_slow_model_comes_back_as_a_german_message_rather_than_an_exception()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Zu spät."))
		{
			BeforeResponse = async token => await Task.Delay(TimeSpan.FromSeconds(30), token)
		};

		AssistantTools tools = fixture.BuildTools();
		AssistantFeature.Handler handler = new(
			AssistantFixture.BuildAgent(client, tools),
			tools,
			NullLogger<AssistantFeature.Handler>.Instance)
		{
			Timeout = TimeSpan.FromMilliseconds(50)
		};

		var (result, error) = await handler.HandleAsync(new AssistantFeature.AskCommand("Dauert ewig.", null), Ct);

		Assert.Null(result);
		Assert.Equal(AssistantFeature.Handler.TimeoutReply, error);
	}

	[Fact]
	public async Task A_caller_cancelling_is_not_dressed_up_as_a_timeout()
	{
		AssistantFixture fixture = new();
		using CancellationTokenSource caller = new();
		FakeChatClient client = new(FakeChatClient.Text("Nie erreicht."))
		{
			BeforeResponse = async token =>
			{
				await caller.CancelAsync();
				await Task.Delay(TimeSpan.FromSeconds(5), token);
			}
		};

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => handler.HandleAsync(new AssistantFeature.AskCommand("Abgebrochen.", null), caller.Token));
	}

	[Fact]
	public void An_empty_question_is_rejected_before_any_model_call()
	{
		var (command, error) = AssistantFeature.Validate(new AssistantFeature.AskRequest("   ", null));

		Assert.Null(command);
		Assert.Equal(AssistantFeature.EmptyQuestionError, error);
		Assert.Equal(AssistantFeature.EmptyQuestionError, AssistantFeature.Validate(null).Error);
		Assert.Equal(AssistantFeature.EmptyQuestionError, AssistantFeature.Validate(new AssistantFeature.AskRequest(null, null)).Error);
	}

	[Fact]
	public void An_overlong_question_is_rejected_before_any_model_call()
	{
		var (command, error) = AssistantFeature.Validate(
			new AssistantFeature.AskRequest(new string('a', AssistantFeature.MaxQuestionLength + 1), null));

		Assert.Null(command);
		Assert.Contains("2000 Zeichen", error);
	}

	[Fact]
	public void An_oversized_session_blob_is_rejected_before_any_model_call()
	{
		JsonElement huge = JsonSerializer.SerializeToElement(new { padding = new string('x', AssistantFeature.MaxSessionBytes) });

		var (command, error) = AssistantFeature.Validate(new AssistantFeature.AskRequest("Und im Mai?", huge));

		Assert.Null(command);
		Assert.Equal(AssistantFeature.SessionTooLongError, error);
	}

	[Fact]
	public void A_json_null_session_is_treated_as_a_fresh_conversation()
	{
		JsonElement jsonNull = JsonSerializer.SerializeToElement<object?>(null);

		var (command, error) = AssistantFeature.Validate(new AssistantFeature.AskRequest("Hallo", jsonNull));

		Assert.Null(error);
		Assert.NotNull(command);
		Assert.Null(command.Session);
		Assert.Equal("Hallo", command.Question);
	}

	[Fact]
	public void A_valid_question_is_trimmed_and_passed_through()
	{
		var (command, error) = AssistantFeature.Validate(new AssistantFeature.AskRequest("  Wie viele Besucher?  ", null));

		Assert.Null(error);
		Assert.Equal("Wie viele Besucher?", command!.Question);
	}
}

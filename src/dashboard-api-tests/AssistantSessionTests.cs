using System.Text.Json;
using DashboardApi.Features.Assistant;
using DashboardApi.Tests.Fakes;
using Microsoft.Extensions.AI;
using AssistantFeature = DashboardApi.Features.Assistant.Assistant;

namespace DashboardApi.Tests;

public sealed class AssistantSessionTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task A_session_posted_back_carries_the_earlier_turns()
	{
		AssistantFixture fixture = new();
		FakeChatClient first = new(FakeChatClient.Text("Im April waren es 120 Besucher."));
		AssistantTools firstTools = fixture.BuildTools();

		var (firstResult, _) = await AssistantFixture
			.BuildHandler(AssistantFixture.BuildAgent(first, firstTools), firstTools)
			.HandleAsync(new AssistantFeature.AskCommand("Wie viele Besucher hatten wir im April?", null), Ct);

		Assert.NotNull(firstResult);

		// A second request, a second scope — exactly as the Functions host would build it.
		FakeChatClient second = new(FakeChatClient.Text("Im Mai waren es 150."));
		AssistantTools secondTools = fixture.BuildTools();

		var (secondResult, error) = await AssistantFixture
			.BuildHandler(AssistantFixture.BuildAgent(second, secondTools), secondTools)
			.HandleAsync(new AssistantFeature.AskCommand("Und im Mai?", firstResult.Session), Ct);

		Assert.Null(error);
		Assert.NotNull(secondResult);

		// The follow-up only makes sense because the earlier turns travelled in the session blob.
		List<ChatMessage> sent = Assert.Single(second.Requests);
		Assert.Contains(sent, message => message.Text.Contains("im April", StringComparison.Ordinal));
		Assert.Contains(sent, message => message.Text.Contains("120 Besucher", StringComparison.Ordinal));
		Assert.Contains(sent, message => message.Text.Contains("Und im Mai?", StringComparison.Ordinal));
	}

	[Fact]
	public async Task A_missing_session_starts_a_fresh_conversation()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Servus!"));
		AssistantTools tools = fixture.BuildTools();

		var (result, error) = await AssistantFixture
			.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools)
			.HandleAsync(new AssistantFeature.AskCommand("Hallo", null), Ct);

		Assert.Null(error);
		Assert.NotNull(result);

		// Only this question, no history in front of it.
		List<ChatMessage> sent = Assert.Single(client.Requests);
		Assert.Equal("Hallo", Assert.Single(sent, message => message.Role == ChatRole.User).Text);
	}

	[Fact]
	public async Task A_malformed_session_blob_is_reported_rather_than_thrown()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Nie erreicht."));
		AssistantTools tools = fixture.BuildTools();

		JsonElement nonsense = JsonSerializer.SerializeToElement("das ist kein Gesprächsverlauf");

		var (result, error) = await AssistantFixture
			.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools)
			.HandleAsync(new AssistantFeature.AskCommand("Und im Mai?", nonsense), Ct);

		Assert.Null(result);
		Assert.Equal(AssistantFeature.Handler.BrokenSessionReply, error);
		Assert.Empty(client.Requests);
	}

	[Fact]
	public async Task An_object_shaped_but_unrecognised_session_quietly_starts_over()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Servus!"));
		AssistantTools tools = fixture.BuildTools();

		// The framework reads an unknown state bag as an empty one rather than failing, so this is not an
		// error path — it degrades to a fresh conversation, and no foreign content reaches the model.
		JsonElement unknown = JsonSerializer.SerializeToElement(new { voellig = "kaputt", zahlen = new[] { 1, 2, 3 } });

		var (result, error) = await AssistantFixture
			.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools)
			.HandleAsync(new AssistantFeature.AskCommand("Und im Mai?", unknown), Ct);

		Assert.Null(error);
		Assert.NotNull(result);

		List<ChatMessage> sent = Assert.Single(client.Requests);
		Assert.Equal("Und im Mai?", Assert.Single(sent, message => message.Role == ChatRole.User).Text);
		Assert.DoesNotContain("kaputt", string.Join(" ", sent.Select(message => message.Text)), StringComparison.Ordinal);
	}

	[Fact]
	public async Task The_returned_session_stays_inside_the_browser_cap()
	{
		AssistantFixture fixture = new();
		fixture.AlpakaNames["alpaka-1"] = "Richard";
		for (int i = 0; i < 60; i++)
		{
			fixture.Events.Add(AssistantFixture.Event(
				"alpaka-1",
				$"e-{i}",
				"Scheren",
				Now.AddDays(-i),
				comment: new string('x', 400)));
		}

		// Four rounds of fat tool results is what pushes a session past the cap.
		FakeChatClient client = new(
			FakeChatClient.ToolCall("ereignisse", new { }),
			FakeChatClient.ToolCall("ereignisse", new { }),
			FakeChatClient.ToolCall("ereignisse", new { }),
			FakeChatClient.Text("Zusammengefasst: viel Scheren."));

		AssistantTools tools = fixture.BuildTools();

		var (result, error) = await AssistantFixture
			.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools)
			.HandleAsync(new AssistantFeature.AskCommand("Erzähl mir alles über die Ereignisse.", null), Ct);

		Assert.Null(error);
		Assert.NotNull(result);
		Assert.True(
			result.Session.GetRawText().Length <= AssistantFeature.MaxSessionBytes,
			$"The session blob was {result.Session.GetRawText().Length} bytes, over the {AssistantFeature.MaxSessionBytes} byte cap.");

		// Trimming must not leave the reply behind: the answer still comes back in full.
		Assert.Equal("Zusammengefasst: viel Scheren.", result.Reply);
	}

	[Fact]
	public async Task A_session_that_fits_is_handed_back_untrimmed()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Kurz und knapp."));
		AssistantTools tools = fixture.BuildTools();

		var (result, _) = await AssistantFixture
			.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools)
			.HandleAsync(new AssistantFeature.AskCommand("Wer bist du?", null), Ct);

		Assert.NotNull(result);
		string blob = result.Session.GetRawText();
		Assert.Contains("Wer bist du?", blob, StringComparison.Ordinal);
		Assert.Contains("Kurz und knapp.", blob, StringComparison.Ordinal);
	}
}

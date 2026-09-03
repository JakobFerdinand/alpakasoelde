using System.Text.Json;
using Microsoft.Extensions.AI;

namespace DashboardApi.Tests.Fakes;

/// <summary>
/// A hand-written <see cref="IChatClient"/> returning a scripted queue of responses and recording what it was
/// handed. <see cref="IChatClient"/> has four members, so this is cheaper than pulling in a mocking library.
/// </summary>
internal sealed class FakeChatClient(params ChatResponse[] scripted) : IChatClient
{
	private readonly Queue<ChatResponse> _scripted = new(scripted);

	/// <summary>The messages of every round, in order — round 0 is the first call to the model.</summary>
	public List<List<ChatMessage>> Requests { get; } = [];

	/// <summary>The options of every round, so a test can assert on the tool list and the instructions.</summary>
	public List<ChatOptions?> Options { get; } = [];

	/// <summary>Answers every round once the script runs dry. Left null, the client falls back to plain text.</summary>
	public Func<int, ChatResponse>? Fallback { get; set; }

	/// <summary>Runs before a response is produced — the seam for simulating a slow model.</summary>
	public Func<CancellationToken, Task>? BeforeResponse { get; set; }

	public async Task<ChatResponse> GetResponseAsync(
		IEnumerable<ChatMessage> messages,
		ChatOptions? options = null,
		CancellationToken cancellationToken = default)
	{
		Requests.Add([.. messages]);
		Options.Add(options);

		if (BeforeResponse is not null)
		{
			await BeforeResponse(cancellationToken).ConfigureAwait(false);
		}

		cancellationToken.ThrowIfCancellationRequested();

		if (_scripted.Count > 0)
		{
			return _scripted.Dequeue();
		}

		return Fallback?.Invoke(Requests.Count) ?? Text("Fertig.");
	}

	public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
		IEnumerable<ChatMessage> messages,
		ChatOptions? options = null,
		CancellationToken cancellationToken = default) =>
		throw new NotSupportedException("The assistant never streams — Static Web Apps does not pass it through.");

	public object? GetService(Type serviceType, object? serviceKey = null) => null;

	public void Dispose()
	{
	}

	/// <summary>A plain assistant answer, ending the round trip.</summary>
	public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

	/// <summary>
	/// What a reasoning model returns when its output budget is gone: no text and finish reason 'length'.
	/// On gpt-5-nano the reasoning tokens are drawn from that same budget, so this is reachable.
	/// </summary>
	public static ChatResponse OutOfOutputBudget() =>
		new(new ChatMessage(ChatRole.Assistant, string.Empty)) { FinishReason = ChatFinishReason.Length };

	/// <summary>A tool call, the way a provider emits one — arguments arrive as JSON, not as CLR values.</summary>
	public static ChatResponse ToolCall(string name, object arguments)
	{
		Dictionary<string, object?> parsed = [];
		foreach (JsonProperty property in JsonSerializer.SerializeToElement(arguments).EnumerateObject())
		{
			parsed[property.Name] = property.Value;
		}

		return new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(
			callId: Guid.NewGuid().ToString("N"),
			name: name,
			arguments: parsed)]));
	}

	/// <summary>Every tool call the client was asked to relay back, across all rounds.</summary>
	public IReadOnlyList<FunctionResultContent> ToolResults() =>
	[
		.. Requests
			.SelectMany(round => round)
			.SelectMany(message => message.Contents)
			.OfType<FunctionResultContent>()
	];
}

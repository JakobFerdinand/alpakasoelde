using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Assistant;

public sealed class Assistant
{
	/// <summary>Static Web Apps hard-caps every API request at 45 seconds, so the agent loop gets 40.</summary>
	public static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(40);

	/// <summary>Cap on the conversation blob the browser carries, in both directions.</summary>
	public const int MaxSessionBytes = 32 * 1024;

	public const int MaxQuestionLength = 2000;

	public const string EmptyQuestionError = "Eine Frage muss angegeben werden.";

	public const string SessionTooLongError = "Der Gesprächsverlauf ist zu lang. Bitte starte ein neues Gespräch.";

	private readonly Handler _handler;
	private readonly ILogger<Assistant> _logger;

	public Assistant(Handler handler, ILogger<Assistant> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("assistant")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "post", Route = "assistant")] HttpRequestData req)
	{
		AskRequest? payload;
		try
		{
			payload = await JsonSerializer.DeserializeAsync<AskRequest>(req.Body, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			}).ConfigureAwait(false);
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Invalid JSON payload for assistant.");
			return await BadRequestAsync(req, "Ungültiger Anfrageinhalt.").ConfigureAwait(false);
		}

		var (command, validationError) = Validate(payload);
		if (validationError is not null)
		{
			return await BadRequestAsync(req, validationError).ConfigureAwait(false);
		}

		var (result, error) = await _handler
			.HandleAsync(command!, req.FunctionContext.CancellationToken)
			.ConfigureAwait(false);

		// The assistant is a new lever on the data, so who pulled it and what it touched is worth recording.
		_logger.LogInformation(
			"Assistant question by {Caller}: {Question} — tools: {Tools}",
			ReadCallerName(req),
			command!.Question,
			result is null ? "-" : string.Join(", ", result.Tools.Select(tool => tool.Tool)));

		if (error is not null)
		{
			var badGateway = req.CreateResponse(HttpStatusCode.BadGateway);
			await badGateway.WriteAsJsonAsync(new
			{
				title = "Bad Gateway",
				status = (int)HttpStatusCode.BadGateway,
				detail = error
			}).ConfigureAwait(false);
			return badGateway;
		}

		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}

	private static async Task<HttpResponseData> BadRequestAsync(HttpRequestData req, string detail)
	{
		var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
		await badRequest.WriteAsJsonAsync(new
		{
			title = "Bad Request",
			status = (int)HttpStatusCode.BadRequest,
			detail
		}).ConfigureAwait(false);
		return badRequest;
	}

	/// <summary>
	/// Everything that can be rejected without reaching the model, kept out of the HTTP shell so it is testable.
	/// </summary>
	public static (AskCommand? Command, string? Error) Validate(AskRequest? payload)
	{
		string question = payload?.Question?.Trim() ?? string.Empty;
		if (question.Length == 0)
		{
			return (null, EmptyQuestionError);
		}

		if (question.Length > MaxQuestionLength)
		{
			return (null, $"Die Frage darf höchstens {MaxQuestionLength} Zeichen lang sein.");
		}

		JsonElement? session = Normalize(payload?.Session);
		if (session is { } blob && Encoding.UTF8.GetByteCount(blob.GetRawText()) > MaxSessionBytes)
		{
			return (null, SessionTooLongError);
		}

		return (new AskCommand(question, session), null);
	}

	private static JsonElement? Normalize(JsonElement? session) =>
		session is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } ? session : null;

	/// <summary>Reads the display name out of the principal the Static Web App injects.</summary>
	private static string ReadCallerName(HttpRequestData req)
	{
		if (!req.Headers.TryGetValues("x-ms-client-principal", out IEnumerable<string>? values))
		{
			return "unbekannt";
		}

		string? encoded = values.FirstOrDefault();
		if (string.IsNullOrWhiteSpace(encoded))
		{
			return "unbekannt";
		}

		try
		{
			using JsonDocument principal = JsonDocument.Parse(Convert.FromBase64String(encoded));
			return principal.RootElement.TryGetProperty("userDetails", out JsonElement details)
				? details.GetString() ?? "unbekannt"
				: "unbekannt";
		}
		catch (Exception ex) when (ex is FormatException or JsonException)
		{
			return "unbekannt";
		}
	}

	public sealed record AskRequest(string? Question, JsonElement? Session);

	public sealed record AskCommand(string Question, JsonElement? Session);

	public sealed record ToolTrace(string Tool, string Arguments);

	public sealed record AskResult(string Reply, JsonElement Session, IReadOnlyList<ToolTrace> Tools);

	public sealed class Handler(AIAgent agent, AssistantTools tools, ILogger<Handler> logger)
	{
		public const string TimeoutReply = "Das hat zu lange gedauert. Bitte stell die Frage etwas gezielter — zum Beispiel mit einem konkreten Zeitraum.";

		public const string NoAnswerReply = "Das brauche ich in mehreren Schritten — frag mich bitte gezielter.";

		public const string BrokenSessionReply = "Der Gesprächsverlauf konnte nicht gelesen werden. Bitte starte ein neues Gespräch.";

		/// <summary>The agent-loop budget. Overridden only by tests; production keeps the 40 second cap.</summary>
		public TimeSpan Timeout { get; init; } = RunTimeout;

		private readonly AIAgent _agent = agent;
		private readonly AssistantTools _tools = tools;
		private readonly ILogger<Handler> _logger = logger;

		public async Task<(AskResult? Result, string? Error)> HandleAsync(AskCommand command, CancellationToken cancellationToken)
		{
			AgentSession session;
			try
			{
				session = command.Session is { } blob
					? await _agent.DeserializeSessionAsync(blob, cancellationToken: cancellationToken).ConfigureAwait(false)
					: await _agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
			{
				_logger.LogWarning(ex, "Could not rehydrate the assistant session.");
				return (null, BrokenSessionReply);
			}

			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(Timeout);

			AgentResponse response;
			try
			{
				response = await _agent.RunAsync(command.Question, session, cancellationToken: timeout.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// A timeout must come back as a German answer, not as an SWA 504.
				_logger.LogWarning("The assistant run hit the {Seconds}s cap.", Timeout.TotalSeconds);
				return (null, TimeoutReply);
			}

			// An empty reply is what running out of tool-calling rounds looks like from here.
			string reply = string.IsNullOrWhiteSpace(response.Text) ? NoAnswerReply : response.Text;

			JsonElement next = await SerializeWithinCapAsync(session, cancellationToken).ConfigureAwait(false);
			return (new AskResult(reply, next, _tools.Invocations), null);
		}

		/// <summary>
		/// Serialises the session, dropping whole turns off the front until the blob fits the browser's cap.
		/// </summary>
		private async ValueTask<JsonElement> SerializeWithinCapAsync(AgentSession session, CancellationToken cancellationToken)
		{
			JsonElement serialized = await _agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
			if (Size(serialized) <= MaxSessionBytes)
			{
				return serialized;
			}

			if (!session.TryGetInMemoryChatHistory(out List<ChatMessage>? history) || history is null)
			{
				_logger.LogWarning("The assistant session exceeds {Bytes} bytes and could not be trimmed.", MaxSessionBytes);
				return serialized;
			}

			while (Size(serialized) > MaxSessionBytes && DropOldestTurn(history))
			{
				session.SetInMemoryChatHistory(history);
				serialized = await _agent.SerializeSessionAsync(session, cancellationToken: cancellationToken).ConfigureAwait(false);
			}

			return serialized;
		}

		/// <summary>
		/// Drops the oldest message and everything up to the next user message, so a tool result never
		/// survives without the call that produced it.
		/// </summary>
		private static bool DropOldestTurn(List<ChatMessage> history)
		{
			if (history.Count <= 1)
			{
				return false;
			}

			history.RemoveAt(0);
			while (history.Count > 1 && history[0].Role != ChatRole.User)
			{
				history.RemoveAt(0);
			}

			return true;
		}

		private static int Size(JsonElement element) => Encoding.UTF8.GetByteCount(element.GetRawText());
	}
}

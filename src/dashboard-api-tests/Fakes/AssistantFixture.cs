using DashboardApi.Features.Alpakas;
using DashboardApi.Features.Assistant;
using DashboardApi.Features.Gutscheine;
using DashboardApi.Features.Messages;
using DashboardApi.Features.PageViews;
using dashboard_api.shared.entities;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using AssistantFeature = DashboardApi.Features.Assistant.Assistant;
using EventsFeature = DashboardApi.Features.Events.Events;

namespace DashboardApi.Tests.Fakes;

/// <summary>
/// Wires <see cref="AssistantTools"/> over in-memory stores and builds the agent exactly the way
/// <c>Program.cs</c> does, so the tool methods really execute — offline, deterministically, no network.
/// </summary>
internal sealed class AssistantFixture
{
	public List<PageViewEntity> PageViews { get; } = [];

	public List<MessageEntity> Messages { get; } = [];

	public List<GutscheinEntity> Gutscheine { get; } = [];

	public List<AlpakaEntity> Alpakas { get; } = [];

	public List<EventEntity> Events { get; } = [];

	public Dictionary<string, string> AlpakaNames { get; } = [];

	public RecordingImageUrlSigner Signer { get; } = new();

	public AssistantTools BuildTools()
	{
		MessageStore messages = new(Messages);
		PageViewStore pageViews = new(PageViews);
		AlpakaStore alpakas = new(Alpakas);
		EventStore events = new(Events);

		return new AssistantTools(
			new GetPageViewStats.Handler(pageViews),
			new GetPageViewSessions.Handler(pageViews),
			new GetMessageStats.Handler(messages),
			new GetOldMessageCount.Handler(messages),
			new GetGutscheine.Handler(new InMemoryGutscheinStore([.. Gutscheine])),
			new GetAlpakas.Handler(alpakas, Signer),
			new GetAlpakaById.Handler(alpakas, Signer, events),
			new EventsFeature.GetHandler(events, new AlpakaLookup(AlpakaNames)));
	}

	/// <summary>Mirrors the registration in <c>Program.cs</c>, including the iteration cap and its guard.</summary>
	public static AIAgent BuildAgent(IChatClient chatClient, AssistantTools tools, int maximumIterations = 4) =>
		new ChatClientAgent(
			chatClient
				.AsBuilder()
				.UseFunctionInvocation(configure: client => client.MaximumIterationsPerRequest = maximumIterations)
				.Build(),
			new ChatClientAgentOptions
			{
				Name = "alpaka-assistent",
				UseProvidedChatClientAsIs = true,
				ChatOptions = new ChatOptions
				{
					Instructions = AssistantPrompt.SystemPrompt,
					Tools = tools.All,
					MaxOutputTokens = 2000,
					Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low },
				},
			});

	public static AssistantFeature.Handler BuildHandler(AIAgent agent, AssistantTools tools) =>
		new(agent, tools, NullLogger<AssistantFeature.Handler>.Instance);

	public static AlpakaEntity Alpaka(string id, string name, string? imageUrl = null, string birthday = "2019-04-01") => new()
	{
		Name = name,
		Geburtsdatum = birthday,
		ImageUrl = imageUrl,
		RowKey = id
	};

	public static EventEntity Event(string alpakaId, string rowKey, string eventType, DateTimeOffset eventDate, double? cost = null, string? comment = null) => new()
	{
		EventType = eventType,
		EventDate = eventDate,
		Cost = cost,
		Comment = comment,
		PartitionKey = alpakaId,
		RowKey = rowKey,
		SharedEventId = rowKey
	};

	public static MessageEntity Message(int daysAgo, bool isSpam = false) => new()
	{
		Name = "Anna",
		Email = "anna@example.at",
		Phone = "+43 660 1234567",
		Message = "Wir wollen wandern.",
		IsSpam = isSpam,
		Timestamp = DateTimeOffset.UtcNow.AddDays(-daysAgo),
		RowKey = $"m-{daysAgo}-{(isSpam ? "s" : "l")}"
	};

	private sealed class PageViewStore(List<PageViewEntity> entities) : GetPageViewStats.IPageViewReadStore
	{
		public Task<IReadOnlyList<PageViewEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<PageViewEntity>>(entities);
	}

	private sealed class MessageStore(List<MessageEntity> entities) : GetMessages.IReadStore
	{
		public Task<IReadOnlyList<MessageEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<MessageEntity>>(entities);
	}

	private sealed class AlpakaStore(List<AlpakaEntity> entities) : GetAlpakas.IAlpakaReadStore, GetAlpakaById.IReadStore
	{
		public Task<IReadOnlyList<AlpakaEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<AlpakaEntity>>(entities);

		public Task<AlpakaEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
			=> Task.FromResult(entities.Find(entity => entity.RowKey == id));
	}

	private sealed class EventStore(List<EventEntity> entities) : EventsFeature.IEventStore, GetAlpakaById.IEventReadStore
	{
		public Task<IReadOnlyList<EventEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<EventEntity>>(entities);

		public Task<IReadOnlyList<EventEntity>> GetByAlpakaIdAsync(string alpakaId, CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<EventEntity>>([.. entities.Where(entity => entity.PartitionKey == alpakaId)]);

		public Task AddAsync(EventEntity entity, CancellationToken cancellationToken)
			=> throw new NotSupportedException("The assistant is read-only.");
	}

	private sealed class AlpakaLookup(Dictionary<string, string> names) : EventsFeature.IAlpakaLookupStore
	{
		public Task<IDictionary<string, string>> GetNamesAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IDictionary<string, string>>(names);
	}
}

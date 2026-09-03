using DashboardApi.Features.Assistant;
using DashboardApi.Tests.Fakes;
using Microsoft.Extensions.AI;
using AssistantFeature = DashboardApi.Features.Assistant.Assistant;

namespace DashboardApi.Tests;

public sealed class AssistantUsageTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Usage_is_summed_over_every_tool_round_of_the_turn()
	{
		AssistantFixture fixture = new();

		// Two rounds, each reporting its own usage — the per-round figures must not be the ones surfaced.
		FakeChatClient client = new(
			FakeChatClient.ToolCall("heute", new { }, inputTokens: 100, outputTokens: 50, reasoningTokens: 40),
			FakeChatClient.Text("Fertig.", inputTokens: 200, outputTokens: 80, reasoningTokens: 60));

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		var (result, error) = await handler.HandleAsync(new AssistantFeature.AskCommand("Welcher Tag ist heute?", null), Ct);

		Assert.Null(error);
		Assert.NotNull(result);
		Assert.Equal(300, result.Usage.InputTokens);
		Assert.Equal(130, result.Usage.OutputTokens);
		Assert.Equal(100, result.Usage.ReasoningTokens);
	}

	[Fact]
	public async Task The_cost_follows_the_published_rates_and_travels_with_them()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Fertig.", inputTokens: 300, outputTokens: 130));

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		var (result, _) = await handler.HandleAsync(new AssistantFeature.AskCommand("Hallo", null), Ct);

		Assert.NotNull(result);

		decimal expected = Math.Round(
			(300 * AssistantPricing.InputPricePerMillionTokens / 1_000_000m)
			+ (130 * AssistantPricing.OutputPricePerMillionTokens / 1_000_000m),
			6,
			MidpointRounding.AwayFromZero);

		Assert.Equal(expected, result.Usage.Cost);

		// A units slip — per thousand instead of per million — would land three orders of magnitude out,
		// which the equality above cannot catch because it shares the same constants.
		Assert.InRange(result.Usage.Cost, 0.0001m, 0.001m);

		// The rates ride along so the island can show what the estimate is based on.
		Assert.Equal("EUR", result.Usage.Currency);
		Assert.Equal(AssistantPricing.InputPricePerMillionTokens, result.Usage.InputPricePerMillion);
		Assert.Equal(AssistantPricing.OutputPricePerMillionTokens, result.Usage.OutputPricePerMillion);
	}

	[Fact]
	public async Task A_provider_that_reports_no_usage_yields_zeros_rather_than_a_null_hole()
	{
		AssistantFixture fixture = new();
		FakeChatClient client = new(FakeChatClient.Text("Fertig."));

		AssistantTools tools = fixture.BuildTools();
		var handler = AssistantFixture.BuildHandler(AssistantFixture.BuildAgent(client, tools), tools);

		var (result, _) = await handler.HandleAsync(new AssistantFeature.AskCommand("Hallo", null), Ct);

		Assert.NotNull(result);
		Assert.Equal(0, result.Usage.InputTokens);
		Assert.Equal(0, result.Usage.OutputTokens);
		Assert.Equal(0m, result.Usage.Cost);
		Assert.Equal("EUR", result.Usage.Currency);
	}

	[Fact]
	public void Pricing_rounds_to_six_decimals_so_a_cheap_turn_is_not_reported_as_free()
	{
		AssistantFeature.UsageInfo usage = AssistantPricing.Estimate(new UsageDetails
		{
			InputTokenCount = 12,
			OutputTokenCount = 3,
		});

		Assert.True(usage.Cost > 0m, "A turn that used tokens must not price out at exactly zero.");
		Assert.Equal(usage.Cost, Math.Round(usage.Cost, 6));
	}
}

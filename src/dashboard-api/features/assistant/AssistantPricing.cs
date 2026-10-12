using Microsoft.Extensions.AI;

namespace DashboardApi.Features.Assistant;

/// <summary>
/// Turns the token counts the agent framework reports into a rough money figure for the chat footer.
/// </summary>
/// <remarks>
/// The framework supplies tokens but no prices — cost is a billing concept, not a model one — so the rates
/// live here as constants. They are an **estimate**, deliberately surfaced to the UI rather than hidden:
/// these are OpenAI's published Standard list rates for `gpt-6-luna` in USD per million tokens
/// (developers.openai.com/api/docs/models/gpt-6-luna, checked 2026-10-12), because the Azure retail price API
/// publishes no meter for it in `germanywestcentral`. They are not converted to EUR, so correct them against
/// an actual invoice; it is a one-line change and the island shows the basis it used, so a wrong rate is
/// visible rather than silently wrong.
/// </remarks>
public static class AssistantPricing
{
	public const string Currency = "USD";

	public const decimal InputPricePerMillionTokens = 0.10m;

	public const decimal OutputPricePerMillionTokens = 0.50m;

	private const decimal Million = 1_000_000m;

	/// <summary>Prices one turn. Cached input is counted at the full input rate, which slightly overstates.</summary>
	public static Assistant.UsageInfo Estimate(UsageDetails? usage)
	{
		long input = usage?.InputTokenCount ?? 0;
		long output = usage?.OutputTokenCount ?? 0;

		decimal cost =
			(input * InputPricePerMillionTokens / Million)
			+ (output * OutputPricePerMillionTokens / Million);

		return new Assistant.UsageInfo(
			input,
			output,
			usage?.ReasoningTokenCount ?? 0,
			usage?.CachedInputTokenCount ?? 0,
			// Six decimals: a single question costs a small fraction of a cent, and rounding to cents here
			// would make every turn read as 0,00 € and the running total drift.
			Math.Round(cost, 6, MidpointRounding.AwayFromZero),
			Currency,
			InputPricePerMillionTokens,
			OutputPricePerMillionTokens);
	}
}

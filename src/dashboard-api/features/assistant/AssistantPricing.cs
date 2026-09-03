using Microsoft.Extensions.AI;

namespace DashboardApi.Features.Assistant;

/// <summary>
/// Turns the token counts the agent framework reports into a rough money figure for the chat footer.
/// </summary>
/// <remarks>
/// The framework supplies tokens but no prices — cost is a billing concept, not a model one — so the rates
/// live here as constants. They are an **estimate**, deliberately surfaced to the UI rather than hidden:
/// the Azure retail price API publishes no `gpt-5-nano` meter for `germanywestcentral` (checked 2026-09-03),
/// so these are the Global Standard nano-tier list rates it does publish for the region, in EUR per million
/// tokens. Correct them against an actual invoice; it is a one-line change and the island shows the basis it
/// used, so a wrong rate is visible rather than silently wrong.
/// </remarks>
public static class AssistantPricing
{
	public const string Currency = "EUR";

	public const decimal InputPricePerMillionTokens = 0.1717m;

	public const decimal OutputPricePerMillionTokens = 1.0733m;

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

using DashboardApi.Features.Alpakas;

namespace DashboardApi.Tests.Fakes;

internal sealed class RecordingImageUrlSigner : GetAlpakas.IImageUrlSigner
{
	public List<(string? Url, TimeSpan Lifetime)> Calls { get; } = [];

	public string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime)
	{
		Calls.Add((originalUrl, lifetime));
		return string.IsNullOrWhiteSpace(originalUrl) ? null : $"{originalUrl}?sas";
	}
}

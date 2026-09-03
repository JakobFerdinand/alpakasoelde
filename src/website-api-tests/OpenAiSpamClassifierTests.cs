using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WebsiteApi.Features.Messages;
using WebsiteApi.Shared;

namespace WebsiteApi.Tests;

public sealed class OpenAiSpamClassifierTests
{
	private static OpenAiSpamClassifier CreateClassifier(string? endpoint, string? apiKey, string? deployment)
	{
		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				[EnvironmentVariables.OpenAiEndpoint] = endpoint,
				[EnvironmentVariables.OpenAiApiKey] = apiKey,
				[EnvironmentVariables.OpenAiDeployment] = deployment,
			})
			.Build();

		return new OpenAiSpamClassifier(configuration, NullLogger<OpenAiSpamClassifier>.Instance);
	}

	// The classifier fails open on purpose: a misconfigured or broken filter must
	// let a real enquiry through rather than silently swallow it.
	[Theory]
	[InlineData(null, "key", "deployment")]
	[InlineData("https://example.openai.azure.com/", null, "deployment")]
	[InlineData("https://example.openai.azure.com/", "key", null)]
	[InlineData("   ", "key", "deployment")]
	public async Task Incomplete_configuration_classifies_as_legit(string? endpoint, string? apiKey, string? deployment)
	{
		OpenAiSpamClassifier classifier = CreateClassifier(endpoint, apiKey, deployment);

		bool isSpam = await classifier.IsSpamAsync("Bot", "bot@example.com", "Buy cheap traffic now", TestContext.Current.CancellationToken);

		Assert.False(isSpam);
	}

	[Fact]
	public async Task Unusable_endpoint_classifies_as_legit_instead_of_throwing()
	{
		OpenAiSpamClassifier classifier = CreateClassifier("nicht-einmal-eine-url", "key", "deployment");

		bool isSpam = await classifier.IsSpamAsync("Bot", "bot@example.com", "Buy cheap traffic now", TestContext.Current.CancellationToken);

		Assert.False(isSpam);
	}
}

using System.Text.RegularExpressions;
using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebsiteApi.Shared;

namespace WebsiteApi.Features.Messages;

public interface ISpamClassifier
{
	Task<bool> IsSpamAsync(string name, string email, string message, CancellationToken cancellationToken);
}

public sealed class OpenAiSpamClassifier(IConfiguration configuration, ILogger<OpenAiSpamClassifier> logger) : ISpamClassifier
{
	private const string SystemPrompt = """
        Du bist ein Spamfilter für das Kontaktformular der Website alpakasoelde.at (eine Alpaka-Farm).
        Du bewertest eingehende Kontaktanfragen und antwortest ausschließlich mit dem JSON-Objekt
        {"isSpam": true} oder {"isSpam": false} (ohne Erklärung, ohne Markdown).
        Antworte mit "isSpam": true, wenn die Nachricht Werbung, Phishing, Link-Spam, gekaufter Traffic
        oder sonstig nicht ernstgemeint ist. Antworte mit "isSpam": false, wenn es eine echte,
        ernsthafte Anfrage ist.
        """;

	private readonly IConfiguration _configuration = configuration;
	private readonly ILogger<OpenAiSpamClassifier> _logger = logger;

	public async Task<bool> IsSpamAsync(string name, string email, string message, CancellationToken cancellationToken)
	{
		string? endpoint = _configuration[EnvironmentVariables.OpenAiEndpoint];
		string? apiKey = _configuration[EnvironmentVariables.OpenAiApiKey];
		string? deployment = _configuration[EnvironmentVariables.OpenAiDeployment];

		if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(deployment))
		{
			_logger.LogWarning("OpenAI settings are missing; treating message as legit.");
			return false;
		}

		try
		{
			ChatCompletionsClient client = new(new Uri(endpoint), new AzureKeyCredential(apiKey));

			ChatCompletionsOptions options = new()
			{
				Model = deployment,
				MaxTokens = 200,
				ResponseFormat = new ChatCompletionsResponseFormatJsonObject(),
				Messages =
				{
					new ChatRequestSystemMessage(SystemPrompt),
					new ChatRequestUserMessage($"""
                    Name: {name}
                    E-Mail: {email}
                    Nachricht:
                    {message}
                    """)
				}
			};
			// gpt-5 are reasoning models; pin effort low to keep the call fast and cheap.
			options.AdditionalProperties["reasoning_effort"] = BinaryData.FromString("\"low\"");

			ChatCompletions response = await client.CompleteAsync(options, cancellationToken).ConfigureAwait(false);
			string? content = response.Content;

			if (string.IsNullOrWhiteSpace(content))
			{
				_logger.LogWarning("Spam classifier returned no content; treating message as legit.");
				return false;
			}

			return Regex.IsMatch(content, "\"isSpam\"\\s*:\\s*true", RegexOptions.IgnoreCase);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_logger.LogError(ex, "Spam classification failed; treating message as legit.");
			return false;
		}
	}
}
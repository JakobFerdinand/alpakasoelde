using System.ClientModel;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using DashboardApi.Features.Alpakas;
using DashboardApi.Features.Assistant;
using DashboardApi.Features.Events;
using DashboardApi.Features.Gutscheine;
using DashboardApi.Features.Messages;
using DashboardApi.Features.PageViews;
using DashboardApi.Shared;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using OpenAI.Chat;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        string connectionString = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageConnection)
            ?? throw new InvalidOperationException("Environment variable 'StorageConnection' is not set.");
        services.AddSingleton(_ => new TableServiceClient(connectionString));
        services.AddSingleton(_ => new BlobServiceClient(connectionString));

        services.AddScoped<AddAlpaka.Handler>();
        services.AddScoped<GetAlpakas.Handler>();
        services.AddScoped<GetAlpakaById.Handler>();
        services.AddScoped<UpdateAlpaka.Handler>();
        services.AddScoped<GetMessages.Handler>();
        services.AddScoped<GetOldMessageCount.Handler>();
        services.AddScoped<GetMessageStats.Handler>();
        services.AddScoped<DeleteMessage.Handler>();
        services.AddScoped<GetPageViewStats.Handler>();
        services.AddScoped<GetPageViewSessions.Handler>();
        services.AddScoped<Events.GetHandler>();
        services.AddScoped<Events.AddHandler>();
        services.AddScoped<GetGutscheine.Handler>();
        services.AddScoped<AddGutschein.Handler>();
        services.AddScoped<RedeemGutschein.Handler>();
        services.AddScoped<Assistant.Handler>();

        // Resolved lazily so a host without OpenAI settings still serves every other endpoint.
        services.AddSingleton<IChatClient>(sp =>
        {
            IConfiguration configuration = sp.GetRequiredService<IConfiguration>();
            string endpoint = Required(configuration, EnvironmentVariables.OpenAiEndpoint);
            string apiKey = Required(configuration, EnvironmentVariables.OpenAiApiKey);
            string deployment = Required(configuration, EnvironmentVariables.OpenAiAssistantDeployment);

            return new ChatClient(
                    model: deployment,
                    credential: new ApiKeyCredential(apiKey),
                    options: new OpenAIClientOptions { Endpoint = new Uri($"{endpoint.TrimEnd('/')}/openai/v1/") })
                .AsIChatClient()
                .AsBuilder()
                // The round cap lives on the function-invoking client, so it has to be applied here and the
                // agent told not to re-wrap (and thereby discard) it.
                .UseFunctionInvocation(configure: client => client.MaximumIterationsPerRequest = 4)
                .Build();
        });

        // Scoped: the tools depend on the scoped read handlers and collect this request's tool trace.
        services.AddScoped<AssistantTools>();
        services.AddScoped<AIAgent>(sp => new ChatClientAgent(
            sp.GetRequiredService<IChatClient>(),
            new ChatClientAgentOptions
            {
                Name = "alpaka-assistent",
                UseProvidedChatClientAsIs = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = AssistantPrompt.SystemPrompt,
                    Tools = sp.GetRequiredService<AssistantTools>().All,
                    MaxOutputTokens = 800,
                },
            }));

        services.AddScoped<AddAlpaka.IAlpakaWriteStore, AddAlpaka.TableAlpakaWriteStore>();
        services.AddScoped<AddAlpaka.IAlpakaImageStore, AddAlpaka.BlobAlpakaImageStore>();
        services.AddScoped<GetAlpakas.IAlpakaReadStore, GetAlpakas.TableAlpakaReadStore>();
        services.AddScoped<GetAlpakaById.IReadStore, GetAlpakaById.TableReadStore>();
        services.AddScoped<GetAlpakaById.IEventReadStore, GetAlpakaById.TableEventReadStore>();
        services.AddScoped<GetAlpakas.IImageUrlSigner, GetAlpakas.BlobImageUrlSigner>();
        services.AddScoped<UpdateAlpaka.IAlpakaUpdateStore, UpdateAlpaka.TableAlpakaUpdateStore>();
        services.AddScoped<UpdateAlpaka.IAlpakaImageReplacementStore, UpdateAlpaka.BlobAlpakaImageReplacementStore>();
        services.AddScoped<GetMessages.IReadStore, GetMessages.TableReadStore>();
        services.AddScoped<GetPageViewStats.IPageViewReadStore, GetPageViewStats.TablePageViewReadStore>();
        services.AddScoped<DeleteMessage.IStore, DeleteMessage.TableStore>();
        services.AddScoped<Events.IEventStore, Events.TableEventStore>();
        services.AddScoped<Events.IAlpakaLookupStore, Events.TableAlpakaLookupStore>();
        services.AddScoped<IGutscheinStore, TableGutscheinStore>();
    })
    .Build();

await host.RunAsync();

static string Required(IConfiguration configuration, string name) =>
    configuration[name] is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Setting '{name}' is not configured; the assistant cannot run.");

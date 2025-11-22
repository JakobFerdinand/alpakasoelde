using Azure.Data.Tables;
using WebsiteApi.Features.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebsiteApi.Shared;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        string connectionString = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageConnection)
            ?? throw new InvalidOperationException("Environment variable 'StorageConnection' is not set.");
        services.AddSingleton(_ => new TableServiceClient(connectionString));
        services.AddScoped<SendMessageHandler>();
        services.AddScoped<IMessageWriteStore, TableMessageWriteStore>();
        services.AddScoped<IEmailSender, EmailSender>();
    })
    .Build();

await host.RunAsync();

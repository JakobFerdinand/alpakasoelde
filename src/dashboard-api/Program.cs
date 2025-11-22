using Azure.Data.Tables;
using Azure.Storage.Blobs;
using DashboardApi.Features.Alpakas;
using DashboardApi.Features.Events;
using DashboardApi.Features.Messages;
using DashboardApi.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        string connectionString = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageConnection)
            ?? throw new InvalidOperationException("Environment variable 'StorageConnection' is not set.");
        services.AddSingleton(_ => new TableServiceClient(connectionString));
        services.AddSingleton(_ => new BlobServiceClient(connectionString));

        services.AddScoped<AddAlpakaHandler>();
        services.AddScoped<GetAlpakasHandler>();
        services.AddScoped<GetAlpakaByIdHandler>();
        services.AddScoped<UpdateAlpakaHandler>();
        services.AddScoped<GetMessagesHandler>();
        services.AddScoped<GetOldMessageCountHandler>();
        services.AddScoped<DeleteMessageHandler>();
        services.AddScoped<GetEventsHandler>();
        services.AddScoped<AddEventHandler>();

        services.AddScoped<IAlpakaWriteStore, TableAlpakaWriteStore>();
        services.AddScoped<IAlpakaImageStore, BlobAlpakaImageStore>();
        services.AddScoped<IAlpakaReadStore, TableAlpakaReadStore>();
        services.AddScoped<IAlpakaByIdReadStore, TableAlpakaByIdReadStore>();
        services.AddScoped<IImageUrlSigner, BlobImageUrlSigner>();
        services.AddScoped<IAlpakaUpdateStore, TableAlpakaUpdateStore>();
        services.AddScoped<IAlpakaImageReplacementStore, BlobAlpakaImageReplacementStore>();
        services.AddScoped<IMessageReadStore, TableMessageReadStore>();
        services.AddScoped<IMessageDeleteStore, TableMessageDeleteStore>();
        services.AddScoped<IEventStore, TableEventStore>();
        services.AddScoped<IAlpakaLookupStore, TableAlpakaLookupStore>();
    })
    .Build();

await host.RunAsync();

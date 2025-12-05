using Azure.Data.Tables;
using Azure.Storage.Blobs;
using DashboardApi.Features.Alpakas;
using DashboardApi.Features.Events;
using DashboardApi.Features.Gutscheine;
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

        services.AddScoped<AddAlpaka.Handler>();
        services.AddScoped<GetAlpakas.Handler>();
        services.AddScoped<GetAlpakaById.Handler>();
        services.AddScoped<UpdateAlpaka.Handler>();
        services.AddScoped<GetMessages.Handler>();
        services.AddScoped<GetOldMessageCount.Handler>();
        services.AddScoped<DeleteMessage.Handler>();
        services.AddScoped<Events.GetHandler>();
        services.AddScoped<Events.AddHandler>();
        services.AddScoped<Gutscheine.GetHandler>();
        services.AddScoped<Gutscheine.AddHandler>();

        services.AddScoped<AddAlpaka.IAlpakaWriteStore, AddAlpaka.TableAlpakaWriteStore>();
        services.AddScoped<AddAlpaka.IAlpakaImageStore, AddAlpaka.BlobAlpakaImageStore>();
        services.AddScoped<GetAlpakas.IAlpakaReadStore, GetAlpakas.TableAlpakaReadStore>();
        services.AddScoped<GetAlpakaById.IReadStore, GetAlpakaById.TableReadStore>();
        services.AddScoped<GetAlpakaById.IEventReadStore, GetAlpakaById.TableEventReadStore>();
        services.AddScoped<GetAlpakas.IImageUrlSigner, GetAlpakas.BlobImageUrlSigner>();
        services.AddScoped<UpdateAlpaka.IAlpakaUpdateStore, UpdateAlpaka.TableAlpakaUpdateStore>();
        services.AddScoped<UpdateAlpaka.IAlpakaImageReplacementStore, UpdateAlpaka.BlobAlpakaImageReplacementStore>();
        services.AddScoped<GetMessages.IReadStore, GetMessages.TableReadStore>();
        services.AddScoped<DeleteMessage.IStore, DeleteMessage.TableStore>();
        services.AddScoped<Events.IEventStore, Events.TableEventStore>();
        services.AddScoped<Events.IAlpakaLookupStore, Events.TableAlpakaLookupStore>();
        services.AddScoped<Gutscheine.IGutscheinStore, Gutscheine.TableGutscheinStore>();
    })
    .Build();

await host.RunAsync();

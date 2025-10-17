using Azure.Data.Tables;
using Azure.Storage.Blobs;
using DashboardApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        string? connectionString = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageConnection);
        services.AddSingleton(_ => new TableServiceClient(connectionString));
        services.AddSingleton(_ => new BlobServiceClient(connectionString));
    })
    .Build();

await host.RunAsync();

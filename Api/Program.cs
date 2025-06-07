using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        string? connectionString = Environment.GetEnvironmentVariable(Api.EnvironmentVariables.StorageConnection);
        services.AddSingleton(_ => new TableServiceClient(connectionString));
        services.AddSingleton(_ => new BlobServiceClient(connectionString));

        string? senderEmail = Environment.GetEnvironmentVariable(Api.EnvironmentVariables.EmailSenderAddress);
        string[] receiverEmails = Environment.GetEnvironmentVariable(Api.EnvironmentVariables.ReceiverEmailAddresses)?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ??
            Array.Empty<string>();
        string? emailConnection = Environment.GetEnvironmentVariable(Api.EnvironmentVariables.EmailConnection);

        services.AddSingleton(new EmailOptions
        {
            SenderAddress = senderEmail,
            ReceiverAddresses = receiverEmails,
            ConnectionString = emailConnection
        });
    })
    .Build();

await host.RunAsync();

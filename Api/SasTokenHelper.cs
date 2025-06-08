using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System;
using System.IO;

namespace Api;

public static class SasTokenHelper
{
    public static string? GenerateSasUrl(
        BlobContainerClient container,
        string? imageUrl,
        string storageAccountName,
        string storageAccountKey)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        string blobName = Path.GetFileName(new Uri(imageUrl).AbsolutePath);
        BlobClient blob = container.GetBlobClient(blobName);

        var expiresOn = DateTimeOffset.UtcNow.AddMinutes(30);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = container.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = expiresOn
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        StorageSharedKeyCredential credential = new(
            storageAccountName,
            storageAccountKey
        );
        var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
        return $"{blob.Uri}?{sasToken}";
    }
}

using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Azure;

namespace Application.Infrastructure.Pubg;

public interface IPubgStorageClient
{
    Task SaveMatch(string matchId, Models.Match match, CancellationToken cancellationToken = default);
}

public class PubgStorageClient(IAzureClientFactory<BlobServiceClient> azureClientFactory) : IPubgStorageClient
{
    private readonly BlobServiceClient blobServiceClient = azureClientFactory.CreateClient("pubgStorage");
    public async Task SaveMatch(string matchId, Models.Match match, CancellationToken cancellationToken = default)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient("pubg-matches");
        var blobClient = containerClient.GetBlobClient($"{matchId}.json");
        if (await blobClient.ExistsAsync(cancellationToken)) return;

        var json = JsonSerializer.Serialize(match, Models.Converter.Settings);

        var blobUploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json"
            }
        };

        await blobClient.UploadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)), blobUploadOptions, cancellationToken);
    }
}
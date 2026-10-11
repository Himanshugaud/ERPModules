using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ERP.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Services;

/// <summary>Blob storage over Entra ID (managed identity in Azure, az login locally) — no account keys.</summary>
public sealed class AzureBlobStorage : IBlobStorage
{
    private readonly BlobContainerClient _container;
    public string Container { get; }

    public AzureBlobStorage(IConfiguration config)
    {
        var url = config["ERP_AttachmentsStorageUrl"]
            ?? throw new InvalidOperationException("ERP_AttachmentsStorageUrl is not configured.");
        Container = config["ERP_AttachmentsContainer"] ?? "erp-attachments";

        var credentialOptions = new DefaultAzureCredentialOptions();
        var tenantId = config["ERP_AzureTenantId"];
        if (!string.IsNullOrWhiteSpace(tenantId)) credentialOptions.TenantId = tenantId;

        _container = new BlobServiceClient(new Uri(url), new DefaultAzureCredential(credentialOptions)).GetBlobContainerClient(Container);
    }

    public async Task UploadAsync(string path, Stream content, string? contentType, CancellationToken ct = default)
    {
        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType }
        };
        await _container.GetBlobClient(path).UploadAsync(content, options, ct);
    }

    public async Task<BlobDownload?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var result = await _container.GetBlobClient(path).DownloadStreamingAsync(cancellationToken: ct);
            return new BlobDownload(result.Value.Content, result.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default) =>
        await _container.GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: ct);
}

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Infrastructure.Configuration;

namespace Infrastructure.Services
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly StorageConfiguration _config;
        private readonly ILogger<AzureBlobStorageService> _logger;

        public AzureBlobStorageService(
            StorageConfiguration config,
            ILogger<AzureBlobStorageService> logger)
        {
            _config = config;
            _logger = logger;

            var blobServiceClient = new BlobServiceClient(_config.ConnectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(_config.ContainerName);
        }

        public async Task<string> UploadFileAsync(string key, Stream fileStream, string contentType)
        {
            try
            {
                // Ensure container exists
                await _containerClient.CreateIfNotExistsAsync();

                var blobClient = _containerClient.GetBlobClient(key);

                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                };

                await blobClient.UploadAsync(fileStream, uploadOptions);

                _logger.LogInformation("File uploaded to Azure Blob Storage: {Key}", key);

                return blobClient.Uri.AbsoluteUri;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to Azure Blob Storage: {Key}", key);
                throw new InvalidOperationException($"Failed to upload file: {ex.Message}", ex);
            }
        }

        public async Task<Stream> DownloadFileAsync(string key)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(key);
                var download = await blobClient.DownloadAsync();

                var memoryStream = new MemoryStream();
                await download.Value.Content.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                _logger.LogInformation("File downloaded from Azure Blob Storage: {Key}", key);
                return memoryStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file from Azure Blob Storage: {Key}", key);
                throw new InvalidOperationException($"Failed to download file: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteFileAsync(string key)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(key);
                await blobClient.DeleteIfExistsAsync();

                _logger.LogInformation("File deleted from Azure Blob Storage: {Key}", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file from Azure Blob Storage: {Key}", key);
                return false;
            }
        }

        public async Task<bool> FileExistsAsync(string key)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(key);
                var exists = await blobClient.ExistsAsync();
                return exists.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if file exists in Azure Blob Storage: {Key}", key);
                return false;
            }
        }

        public async Task<IEnumerable<string>> ListFilesAsync(string prefix)
        {
            try
            {
                var files = new List<string>();

                await foreach (var blob in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix))
                {
                    files.Add(blob.Name);
                }

                _logger.LogInformation("Listed {Count} files with prefix: {Prefix}", files.Count, prefix);
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files from Azure Blob Storage with prefix: {Prefix}", prefix);
                return Enumerable.Empty<string>();
            }
        }

        public async Task<string> GetBlobUrlWithSasAsync(string key, TimeSpan? expiresIn = null)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(key);

                // Check if blob exists
                var exists = await blobClient.ExistsAsync();
                if (!exists.Value)
                {
                    throw new FileNotFoundException($"Blob {key} not found");
                }

                // Set expiration time (default 1 hour)
                var sasExpiry = expiresIn ?? TimeSpan.FromHours(1);

                // Generate SAS token with read permissions
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = _containerClient.Name,
                    BlobName = key,
                    Resource = "b", // b = blob
                    StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Start 5 minutes ago to account for clock skew
                    ExpiresOn = DateTimeOffset.UtcNow.Add(sasExpiry)
                };

                // Grant read permission
                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                // Generate the SAS token
                var sasToken = blobClient.GenerateSasUri(sasBuilder);

                _logger.LogInformation("Generated SAS URL for blob: {Key}, expires in {Minutes} minutes", key, sasExpiry.TotalMinutes);

                return sasToken.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SAS URL for blob: {Key}", key);
                throw new InvalidOperationException($"Failed to generate SAS URL: {ex.Message}", ex);
            }
        }

        public string GetBlobUrl(string key)
        {
            var blobClient = _containerClient.GetBlobClient(key);
            return blobClient.Uri.AbsoluteUri;
        }

        public Task<string> GetSasUrlFromAbsoluteUrlAsync(string absoluteUrl, TimeSpan? expiresIn = null)
        {
            // Strip the container base URL to get the blob key
            // e.g. "https://<account>.blob.core.windows.net/<container>/payment-proofs/..." → "payment-proofs/..."
            var containerBase = _containerClient.Uri.AbsoluteUri.TrimEnd('/') + "/";
            if (!absoluteUrl.StartsWith(containerBase))
                throw new ArgumentException($"URL does not belong to this container: {absoluteUrl}");

            var key = absoluteUrl.Substring(containerBase.Length);
            return GetBlobUrlWithSasAsync(key, expiresIn);
        }
    }
}

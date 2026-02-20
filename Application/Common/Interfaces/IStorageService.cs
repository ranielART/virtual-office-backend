namespace Application.Common.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(string key, Stream fileStream, string contentType);
        Task<Stream> DownloadFileAsync(string key);
        Task<bool> DeleteFileAsync(string key);
        Task<bool> FileExistsAsync(string key);
        Task<IEnumerable<string>> ListFilesAsync(string prefix);

        /// <summary>
        /// Get the blob URL with a SAS token for temporary direct access
        /// </summary>
        Task<string> GetBlobUrlWithSasAsync(string key, TimeSpan? expiresIn = null);

        /// <summary>
        /// Get the blob URL (without SAS token)
        /// </summary>
        string GetBlobUrl(string key);
    }
}

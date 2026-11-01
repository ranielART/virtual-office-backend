using Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/files")]
    public class FilesController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly ILogger<FilesController> _logger;

        public FilesController(IStorageService storageService, ILogger<FilesController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a file to blob storage
        /// </summary>
        [HttpPost("upload")]
        [Authorize] // Add authentication as needed
        public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] bool generateSasUrl = false, [FromQuery] int? expiryMinutes = 60)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            try
            {
                // Generate a unique file name to prevent collisions
                // customize prefix for better organization (e.g., by user or date) if desired
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var contentType = file.ContentType;

                using var stream = file.OpenReadStream();
                var fileUrl = await _storageService.UploadFileAsync(fileName, stream, contentType);

                var response = new
                {
                    fileName = fileName,
                    originalName = file.FileName,
                    url = fileUrl,
                    size = file.Length,
                    contentType = contentType,
                    // Return a URL that goes through your API for secure access
                    downloadUrl = Url.Action(nameof(DownloadFile), "Files", new { fileName }, Request.Scheme),
                    viewUrl = Url.Action(nameof(ViewFile), "Files", new { fileName }, Request.Scheme),
                    // Optionally include SAS URL for direct access
                    sasUrl = generateSasUrl
                        ? await _storageService.GetBlobUrlWithSasAsync(fileName, TimeSpan.FromMinutes(expiryMinutes ?? 60))
                        : null
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { message = "Error uploading file" });
            }
        }





        /// <summary>
        /// Download a file from blob storage (streams through API)
        /// </summary>
        [HttpGet("download/{fileName}")]
        [AllowAnonymous] // Or add [Authorize] if you want to restrict access
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            try
            {
                var fileExists = await _storageService.FileExistsAsync(fileName);
                if (!fileExists)
                    return NotFound(new { message = "File not found" });

                var stream = await _storageService.DownloadFileAsync(fileName);

                // Determine content type based on file extension
                var contentType = GetContentType(fileName);

                return File(stream, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file: {FileName}", fileName);
                return StatusCode(500, new { message = "Error downloading file" });
            }
        }

        /// <summary>
        /// Get file as inline (for displaying images in browser)
        /// </summary>
        [HttpGet("view/{fileName}")]
        [AllowAnonymous]
        public async Task<IActionResult> ViewFile(string fileName)
        {
            try
            {
                var fileExists = await _storageService.FileExistsAsync(fileName);
                if (!fileExists)
                    return NotFound(new { message = "File not found" });

                var stream = await _storageService.DownloadFileAsync(fileName);
                var contentType = GetContentType(fileName);

                // Return file with inline disposition to display in browser
                return File(stream, contentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing file: {FileName}", fileName);
                return StatusCode(500, new { message = "Error viewing file" });
            }
        }

        /// <summary>
        /// Delete a file from blob storage
        /// </summary>
        [HttpDelete("{fileName}")]
        [Authorize] // Add appropriate authorization
        public async Task<IActionResult> DeleteFile(string fileName)
        {
            try
            {
                var deleted = await _storageService.DeleteFileAsync(fileName);

                if (deleted)
                    return Ok(new { message = "File deleted successfully" });

                return NotFound(new { message = "File not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file: {FileName}", fileName);
                return StatusCode(500, new { message = "Error deleting file" });
            }
        }

        /// <summary>
        /// List all files with optional prefix filter
        /// </summary>
        [HttpGet("list")]
        [Authorize]
        public async Task<IActionResult> ListFiles([FromQuery] string? prefix = "")
        {
            try
            {
                var files = await _storageService.ListFilesAsync(prefix ?? "");

                var fileList = files.Select(fileName => new
                {
                    fileName = fileName,
                    downloadUrl = Url.Action(nameof(DownloadFile), new { fileName }),
                    viewUrl = Url.Action(nameof(ViewFile), new { fileName })
                });

                return Ok(fileList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing files");
                return StatusCode(500, new { message = "Error listing files" });
            }
        }

        /// <summary>
        /// Get a direct URL to the file with SAS token (for temporary direct access)
        /// </summary>
        [HttpGet("url/{fileName}")]
        [Authorize]
        public async Task<IActionResult> GetFileUrl(string fileName, [FromQuery] int? expiryMinutes = 60)
        {
            try
            {
                var fileExists = await _storageService.FileExistsAsync(fileName);
                if (!fileExists)
                    return NotFound(new { message = "File not found" });

                var expiresIn = TimeSpan.FromMinutes(expiryMinutes ?? 60);
                var sasUrl = await _storageService.GetBlobUrlWithSasAsync(fileName, expiresIn);

                return Ok(new
                {
                    fileName = fileName,
                    url = sasUrl,
                    expiresIn = expiresIn.TotalMinutes,
                    expiresAt = DateTimeOffset.UtcNow.Add(expiresIn)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating file URL: {FileName}", fileName);
                return StatusCode(500, new { message = "Error generating file URL" });
            }
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".zip" => "application/zip",
                ".mp4" => "video/mp4",
                ".mp3" => "audio/mpeg",
                _ => "application/octet-stream"
            };
        }
    }
}

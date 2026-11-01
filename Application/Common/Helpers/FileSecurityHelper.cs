namespace Application.Common.Helpers
{
    /// <summary>
    /// Server-side file security checks for public uploads.
    /// Covers: magic bytes verification, extension whitelist, PDF embedded-JS scan.
    ///
    /// NOT covered (require external infra):
    ///   - Antivirus (ClamAV) — add when deploying a ClamAV sidecar
    ///   - EXIF stripping / re-encoding — add ImageSharp when needed
    /// </summary>
    public static class FileSecurityHelper
    {
        // ── Allowed types by category ────────────────────────────────────────

        public static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
        };

        public static readonly HashSet<string> AllowedDocumentContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/gif",
            "image/webp",
            "image/heic",
            "image/heif",
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        };

        public static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic",
            ".pdf", ".doc", ".docx",
        };

        // ── Magic byte signatures ────────────────────────────────────────────

        private static readonly byte[] JpegMagic  = [0xFF, 0xD8, 0xFF];
        private static readonly byte[] PngMagic   = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static readonly byte[] GifMagic   = [0x47, 0x49, 0x46, 0x38];          // GIF8
        private static readonly byte[] WebpRiff   = [0x52, 0x49, 0x46, 0x46];          // RIFF (WebP has WEBP at offset 8)
        private static readonly byte[] WebpMarker = [0x57, 0x45, 0x42, 0x50];          // WEBP at offset 8
        private static readonly byte[] HeicFtyp   = [0x66, 0x74, 0x79, 0x70];          // ftyp at offset 4
        private static readonly byte[] PdfMagic   = [0x25, 0x50, 0x44, 0x46];          // %PDF
        private static readonly byte[] DocMagic   = [0xD0, 0xCF, 0x11, 0xE0];          // OLE2 (old .doc)
        private static readonly byte[] ZipMagic   = [0x50, 0x4B, 0x03, 0x04];          // PK (docx = ZIP)

        /// <summary>
        /// Reads the first 12 bytes of the stream and verifies they match the
        /// expected magic bytes for the declared content type.
        /// Always resets the stream position to 0 before and after the check.
        /// </summary>
        public static bool VerifyMagicBytes(Stream stream, string contentType)
        {
            stream.Position = 0;
            var buf = new byte[12];
            var read = stream.Read(buf, 0, buf.Length);
            stream.Position = 0;

            if (read < 4) return false;

            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg"    => StartsWith(buf, JpegMagic),
                "image/png"     => StartsWith(buf, PngMagic),
                "image/gif"     => StartsWith(buf, GifMagic),
                "image/webp"    => StartsWith(buf, WebpRiff) && read >= 12 && StartsWith(buf, WebpMarker, offset: 8),
                "image/heic"
                or "image/heif" => read >= 8 && StartsWith(buf, HeicFtyp, offset: 4),
                "application/pdf" => StartsWith(buf, PdfMagic),
                "application/msword" => StartsWith(buf, DocMagic),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                                => StartsWith(buf, ZipMagic),
                _               => false,
            };
        }

        /// <summary>
        /// Scans a PDF stream for embedded JavaScript and known macro/action patterns.
        /// Uses Latin-1 encoding so every byte value is preserved without errors.
        /// Returns true if potentially malicious content is found.
        /// Always resets the stream position to 0 after the scan.
        /// </summary>
        public static async Task<bool> ContainsPdfMaliciousContentAsync(
            Stream stream, CancellationToken cancellationToken = default)
        {
            stream.Position = 0;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            stream.Position = 0;

            // Latin-1 can represent every byte value — safe for binary file scanning
            var content = System.Text.Encoding.Latin1.GetString(ms.ToArray());

            return content.Contains("/JavaScript",  StringComparison.OrdinalIgnoreCase)
                || content.Contains("/JS ",         StringComparison.OrdinalIgnoreCase)
                || content.Contains("/Launch",      StringComparison.OrdinalIgnoreCase)
                || content.Contains("/OpenAction",  StringComparison.OrdinalIgnoreCase)
                || content.Contains("/AA ",         StringComparison.OrdinalIgnoreCase)
                || content.Contains("/RichMedia",   StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a sanitized storage key for a file:
        /// "{prefix}/{uuid}_{sanitized-filename}"
        /// The original filename is retained for auditability but stripped of path traversal.
        /// </summary>
        public static string BuildStorageKey(string prefix, string originalFilename)
        {
            var safe = Path.GetFileName(originalFilename)
                .Replace("..", string.Empty)
                .Replace("/", string.Empty)
                .Replace("\\", string.Empty);

            return $"{prefix}/{Guid.NewGuid()}_{safe}";
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static bool StartsWith(byte[] buf, byte[] pattern, int offset = 0)
        {
            if (buf.Length < offset + pattern.Length) return false;
            for (var i = 0; i < pattern.Length; i++)
            {
                if (buf[offset + i] != pattern[i]) return false;
            }
            return true;
        }
    }
}

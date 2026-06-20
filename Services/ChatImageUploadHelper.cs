namespace QuanLyCanTeenHutech.Services;

public sealed record ChatImageUploadResult(bool Success, string? Url, string Message);

public static class ChatImageUploadHelper
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    public static async Task<ChatImageUploadResult> SaveAsync(
        IFormFile? file,
        string webRootPath,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return new(false, null, "Chưa chọn hình ảnh.");

        if (file.Length > MaxFileSize)
            return new(false, null, "Hình ảnh tối đa 5MB.");

        await using var source = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await source.ReadAsync(header.AsMemory(bytesRead, header.Length - bytesRead), cancellationToken);
            if (read == 0) break;
            bytesRead += read;
        }

        var extension = DetectExtension(header.AsSpan(0, bytesRead));
        if (extension == null)
            return new(false, null, "Nội dung file không phải JPG, PNG, GIF hoặc WEBP hợp lệ.");

        var dateFolder = DateTime.UtcNow.ToString("yyyyMMdd");
        var uploadFolder = Path.Combine(webRootPath, "uploads", "chat", dateFolder);
        Directory.CreateDirectory(uploadFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadFolder, fileName);
        await using var destination = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await destination.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);

        return new(true, $"/uploads/chat/{dateFolder}/{fileName}", string.Empty);
    }

    private static string? DetectExtension(ReadOnlySpan<byte> header)
    {
        if (header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return ".png";
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ".jpg";
        if (header.Length >= 6 &&
            (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8)))
            return ".gif";
        if (header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return ".webp";

        return null;
    }
}

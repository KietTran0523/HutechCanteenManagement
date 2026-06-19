using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCanTeenHutech.Models;
using QuanLyCanTeenHutech.Services;

namespace QuanLyCanTeenHutech.Controllers.Api;

[ApiController]
[Route("api/sepay")]
[AllowAnonymous]
public class SepayWebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly SepayPaymentService _sepayPaymentService;
    private readonly ILogger<SepayWebhookController> _logger;

    public SepayWebhookController(
        IConfiguration configuration,
        SepayPaymentService sepayPaymentService,
        ILogger<SepayWebhookController> logger)
    {
        _configuration = configuration;
        _sepayPaymentService = sepayPaymentService;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        using var memoryStream = new MemoryStream();
        await Request.Body.CopyToAsync(memoryStream);
        var rawBodyBytes = memoryStream.ToArray();
        var rawBody = Encoding.UTF8.GetString(rawBodyBytes);

        if (!IsAuthenticated(rawBodyBytes))
        {
            return Unauthorized(new
            {
                success = false,
                message = "Webhook signature/API key không hợp lệ."
            });
        }

        SepayPaymentData? data;
        try
        {
            data = ParseWebhookData(rawBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot parse SePay webhook body: {Body}", rawBody);
            return BadRequest(new
            {
                success = false,
                message = "Payload không hợp lệ."
            });
        }

        if (data == null)
        {
            return BadRequest(new
            {
                success = false,
                message = "Payload rỗng."
            });
        }

        var result = await _sepayPaymentService.ProcessIncomingPaymentAsync(data, rawBody);

        // SePay chỉ cần hệ thống trả 200 + success=true để không gửi lại webhook.
        // Kết quả paid/message vẫn được lưu log và trả về để dễ debug trong dashboard SePay.
        return Ok(new
        {
            success = true,
            paid = result.Paid,
            orderId = result.OrderId,
            message = result.Message
        });
    }

    private bool IsAuthenticated(byte[] rawBodyBytes)
    {
        var signature = Request.Headers["X-SePay-Signature"].FirstOrDefault();
        var timestamp = Request.Headers["X-SePay-Timestamp"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(signature) && !string.IsNullOrWhiteSpace(timestamp))
        {
            return VerifyHmacSignature(rawBodyBytes, timestamp, signature);
        }

        // Dự phòng nếu bạn cấu hình webhook kiểu API Key thay vì HMAC-SHA256.
        var configuredApiKey = _configuration["Sepay:WebhookApiKey"] ?? _configuration["Sepay:ApiToken"];
        var authorization = Request.Headers["Authorization"].ToString();

        if (!string.IsNullOrWhiteSpace(configuredApiKey) && !string.IsNullOrWhiteSpace(authorization))
        {
            return string.Equals(authorization, $"Apikey {configuredApiKey}", StringComparison.Ordinal) ||
                   string.Equals(authorization, $"Bearer {configuredApiKey}", StringComparison.Ordinal);
        }

        return false;
    }

    private bool VerifyHmacSignature(byte[] rawBodyBytes, string timestamp, string signature)
    {
        var secretKey = _configuration["Sepay:HmacSecret"];
        if (string.IsNullOrWhiteSpace(secretKey)) return false;

        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) return false;

        var replayWindowMinutes = _configuration.GetValue<int?>("Sepay:ReplayWindowMinutes") ?? 10;
        if (long.TryParse(timestamp, out var unixSeconds))
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - unixSeconds) > replayWindowMinutes * 60)
            {
                return false;
            }
        }

        var prefixBytes = Encoding.UTF8.GetBytes(timestamp + ".");
        var signedBytes = new byte[prefixBytes.Length + rawBodyBytes.Length];
        Buffer.BlockCopy(prefixBytes, 0, signedBytes, 0, prefixBytes.Length);
        Buffer.BlockCopy(rawBodyBytes, 0, signedBytes, prefixBytes.Length, rawBodyBytes.Length);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(signedBytes);
        var expectedSignature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var actualBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());

        return expectedBytes.Length == actualBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private SepayPaymentData? ParseWebhookData(string rawBody)
    {
        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object) return null;

        return new SepayPaymentData
        {
            SepayId = GetString(root, "id") ?? string.Empty,
            Gateway = GetString(root, "gateway"),
            TransactionDate = ParseDate(GetString(root, "transactionDate") ?? GetString(root, "transaction_date")),
            AccountNumber = GetString(root, "accountNumber") ?? GetString(root, "account_number"),
            SubAccount = GetString(root, "subAccount") ?? GetString(root, "sub_account"),
            Code = GetString(root, "code"),
            Content = GetString(root, "content") ?? GetString(root, "transaction_content"),
            TransferType = GetString(root, "transferType") ?? GetString(root, "transfer_type"),
            Description = GetString(root, "description"),
            TransferAmount = GetDecimal(root, "transferAmount") ?? GetDecimal(root, "amount_in") ?? GetDecimal(root, "amount") ?? 0,
            Accumulated = GetDecimal(root, "accumulated"),
            ReferenceCode = GetString(root, "referenceCode") ?? GetString(root, "reference_number")
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)) return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
            return number;

        if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
            return date;

        if (DateTime.TryParse(value, new CultureInfo("vi-VN"), DateTimeStyles.AssumeLocal, out date))
            return date;

        return null;
    }
}

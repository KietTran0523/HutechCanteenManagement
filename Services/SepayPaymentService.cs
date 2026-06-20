using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Data;
using QuanLyCanTeenHutech.Models;

namespace QuanLyCanTeenHutech.Services;

public class SepayPaymentService
{
    public const string PaymentStatusUnpaid = "Unpaid";
    public const string PaymentStatusPaid = "Paid";
    public const string PaymentStatusExpired = "Expired";

    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SepayPaymentService> _logger;

    public SepayPaymentService(
        ApplicationDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<SepayPaymentService> logger)
    {
        _context = context;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public int GetPaymentExpireMinutes()
    {
        var minutes = _configuration.GetValue<int?>("Sepay:PaymentExpireMinutes") ?? 10;
        return minutes <= 0 ? 10 : minutes;
    }

    public DateTime GetPaymentExpiresAt(Order order)
    {
        return order.OrderDate.AddMinutes(GetPaymentExpireMinutes());
    }

    public int GetPaymentRemainingSeconds(Order order)
    {
        if (order.PaymentStatus == PaymentStatusPaid || order.PaymentStatus == PaymentStatusExpired) return 0;

        var remaining = GetPaymentExpiresAt(order) - DateTime.Now;
        return remaining.TotalSeconds <= 0 ? 0 : (int)Math.Ceiling(remaining.TotalSeconds);
    }

    public bool IsPaymentExpired(Order order)
    {
        return order.PaymentStatus == PaymentStatusExpired ||
               (order.PaymentStatus != PaymentStatusPaid && GetPaymentRemainingSeconds(order) <= 0);
    }

    public async Task<bool> MarkPaymentExpiredAsync(Order order)
    {
        var changed = ApplyExpiredStatus(order);
        if (changed)
        {
            await _context.SaveChangesAsync();
        }

        return changed;
    }

    public async Task<int> MarkExpiredOrdersAsync(IEnumerable<Order> orders)
    {
        var changedCount = 0;

        foreach (var order in orders)
        {
            if (ApplyExpiredStatus(order))
            {
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return changedCount;
    }

    public async Task<int> MarkExpiredOrdersAsync()
    {
        var expireBefore = DateTime.Now.AddMinutes(-GetPaymentExpireMinutes());
        var orders = await _context.Orders
            .Where(o => o.PaymentStatus != PaymentStatusPaid &&
                        o.PaymentStatus != PaymentStatusExpired &&
                        o.OrderDate <= expireBefore)
            .ToListAsync();

        return await MarkExpiredOrdersAsync(orders);
    }

    private bool ApplyExpiredStatus(Order order)
    {
        if (order.PaymentStatus == PaymentStatusPaid || order.PaymentStatus == PaymentStatusExpired)
        {
            return false;
        }

        if (GetPaymentRemainingSeconds(order) > 0)
        {
            return false;
        }

        order.PaymentStatus = PaymentStatusExpired;
        return true;
    }

    public string CreatePaymentCode(int orderId)
    {
        return $"DH{orderId:D6}";
    }

    public async Task EnsurePaymentCodeAsync(Order order)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(order.PaymentMethod))
        {
            order.PaymentMethod = "Sepay";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(order.PaymentStatus))
        {
            order.PaymentStatus = PaymentStatusUnpaid;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(order.PaymentCode))
        {
            order.PaymentCode = CreatePaymentCode(order.Id);
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    public string BuildQrUrl(Order order)
    {
        var accountNumber = _configuration["Sepay:AccountNumber"] ?? string.Empty;
        var bankCode = _configuration["Sepay:BankCode"] ?? "MBBank";
        var template = _configuration["Sepay:QrTemplate"] ?? "compact";
        var amount = decimal.ToInt64(decimal.Round(order.TotalAmount, 0, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);
        var paymentCode = order.PaymentCode ?? CreatePaymentCode(order.Id);

        return "https://qr.sepay.vn/img" +
               $"?acc={Uri.EscapeDataString(accountNumber)}" +
               $"&bank={Uri.EscapeDataString(bankCode)}" +
               $"&amount={Uri.EscapeDataString(amount)}" +
               $"&des={Uri.EscapeDataString(paymentCode)}" +
               $"&template={Uri.EscapeDataString(template)}";
    }

    public async Task<SepayProcessResult> ProcessIncomingPaymentAsync(SepayPaymentData data, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(data.SepayId))
        {
            return new SepayProcessResult { Paid = false, Message = "Thiếu mã giao dịch SePay." };
        }

        var existed = await _context.SepayTransactions.AnyAsync(x => x.SepayId == data.SepayId);
        if (existed)
        {
            return new SepayProcessResult { Paid = false, Message = "Giao dịch đã được xử lý trước đó." };
        }

        var transaction = new SepayTransaction
        {
            SepayId = data.SepayId,
            Gateway = data.Gateway,
            TransactionDate = data.TransactionDate,
            AccountNumber = data.AccountNumber,
            SubAccount = data.SubAccount,
            Code = data.Code,
            Content = data.Content,
            TransferType = data.TransferType,
            Description = data.Description,
            TransferAmount = data.TransferAmount,
            Accumulated = data.Accumulated,
            ReferenceCode = data.ReferenceCode,
            RawBody = rawBody,
            CreatedAt = DateTime.Now
        };

        _context.SepayTransactions.Add(transaction);

        if (!string.Equals(data.TransferType, "in", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(data.TransferType, "credit", StringComparison.OrdinalIgnoreCase))
        {
            await _context.SaveChangesAsync();
            return new SepayProcessResult { Paid = false, Message = "Không phải giao dịch tiền vào." };
        }

        var paymentCode = ExtractPaymentCode(data.Code, data.Content, data.Description, data.ReferenceCode);
        if (string.IsNullOrWhiteSpace(paymentCode))
        {
            await _context.SaveChangesAsync();
            return new SepayProcessResult { Paid = false, Message = "Không tìm thấy mã đơn hàng trong nội dung giao dịch." };
        }

        var order = await _context.Orders.FirstOrDefaultAsync(x => x.PaymentCode == paymentCode);
        if (order == null)
        {
            await _context.SaveChangesAsync();
            return new SepayProcessResult { Paid = false, Message = $"Không tìm thấy đơn hàng có mã {paymentCode}." };
        }

        if (order.PaymentStatus == PaymentStatusPaid)
        {
            await _context.SaveChangesAsync();
            return new SepayProcessResult { Paid = true, Message = "Đơn hàng đã thanh toán trước đó.", OrderId = order.Id };
        }

        var paymentTime = data.TransactionDate ?? DateTime.Now;
        var expiresAt = GetPaymentExpiresAt(order);
        var paidWithinTime = paymentTime <= expiresAt;

        if (!paidWithinTime)
        {
            order.PaymentStatus = PaymentStatusExpired;
            await _context.SaveChangesAsync();
            return new SepayProcessResult
            {
                Paid = false,
                Expired = true,
                OrderId = order.Id,
                Message = $"Đơn hàng đã quá thời gian thanh toán {GetPaymentExpireMinutes()} phút. Giao dịch được ghi nhận nhưng không tự duyệt đơn."
            };
        }

        if (decimal.Round(order.TotalAmount, 0) != decimal.Round(data.TransferAmount, 0))
        {
            await _context.SaveChangesAsync();
            return new SepayProcessResult
            {
                Paid = false,
                Expired = order.PaymentStatus == PaymentStatusExpired,
                OrderId = order.Id,
                Message = $"Sai số tiền. Đơn hàng cần {order.TotalAmount:#,##0} đ nhưng giao dịch nhận {data.TransferAmount:#,##0} đ."
            };
        }

        // Nếu webhook/API đến trễ nhưng thời gian giao dịch ngân hàng nằm trong hạn thanh toán,
        // vẫn cho phép chuyển từ Expired sang Paid để không làm thiệt khách đã trả đúng hạn.
        order.PaymentMethod = "Sepay";
        order.PaymentStatus = PaymentStatusPaid;
        order.PaidAt = DateTime.Now;
        order.SepayTransactionId = data.SepayId;

        await _context.SaveChangesAsync();

        return new SepayProcessResult
        {
            Paid = true,
            Expired = false,
            OrderId = order.Id,
            Message = "Đã xác nhận thanh toán SePay."
        };
    }

    public async Task<SepayProcessResult> RecheckOrderByApiAsync(Order order)
    {
        await EnsurePaymentCodeAsync(order);

        if (order.PaymentStatus == PaymentStatusPaid)
        {
            return new SepayProcessResult { Paid = true, Expired = false, OrderId = order.Id, Message = "Đơn hàng đã thanh toán." };
        }

        var wasExpiredBeforeApi = IsPaymentExpired(order);

        var apiToken = _configuration["Sepay:ApiToken"];
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            if (wasExpiredBeforeApi)
            {
                await MarkPaymentExpiredAsync(order);
                return new SepayProcessResult
                {
                    Paid = false,
                    Expired = true,
                    OrderId = order.Id,
                    Message = $"Đã hết thời gian thanh toán {GetPaymentExpireMinutes()} phút. Vui lòng tạo đơn hàng mới."
                };
            }

            return new SepayProcessResult { Paid = false, Expired = false, OrderId = order.Id, Message = "Chưa cấu hình Sepay:ApiToken." };
        }

        var accountNumber = _configuration["Sepay:AccountNumber"];
        var amount = decimal.ToInt64(decimal.Round(order.TotalAmount, 0, MidpointRounding.AwayFromZero))
            .ToString(CultureInfo.InvariantCulture);

        var query = new List<string>
        {
            "limit=20",
            $"amount_in={Uri.EscapeDataString(amount)}"
        };

        if (!string.IsNullOrWhiteSpace(accountNumber))
        {
            query.Add($"account_number={Uri.EscapeDataString(accountNumber)}");
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var url = "https://my.sepay.vn/userapi/transactions/list?" + string.Join("&", query);
        using var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SePay API recheck failed. Status: {Status}. Body: {Body}", response.StatusCode, body);

            if (wasExpiredBeforeApi)
            {
                await MarkPaymentExpiredAsync(order);
                return new SepayProcessResult
                {
                    Paid = false,
                    Expired = true,
                    OrderId = order.Id,
                    Message = $"Đã hết thời gian thanh toán {GetPaymentExpireMinutes()} phút. Vui lòng tạo đơn hàng mới."
                };
            }

            return new SepayProcessResult { Paid = false, Expired = false, OrderId = order.Id, Message = "SePay API chưa trả về dữ liệu hợp lệ." };
        }

        var matched = FindMatchedTransactionFromApi(body, order.PaymentCode!, order.TotalAmount);
        if (matched == null)
        {
            if (wasExpiredBeforeApi)
            {
                await MarkPaymentExpiredAsync(order);
                return new SepayProcessResult
                {
                    Paid = false,
                    Expired = true,
                    OrderId = order.Id,
                    Message = $"Đã hết thời gian thanh toán {GetPaymentExpireMinutes()} phút. Vui lòng tạo đơn hàng mới."
                };
            }

            return new SepayProcessResult { Paid = false, Expired = false, OrderId = order.Id, Message = "Chưa tìm thấy giao dịch khớp đơn hàng." };
        }

        return await ProcessIncomingPaymentAsync(matched, body);
    }

    public string? ExtractPaymentCode(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            var match = Regex.Match(value, @"DH\d{6,}", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }
        }

        return null;
    }

    private SepayPaymentData? FindMatchedTransactionFromApi(string json, string paymentCode, decimal totalAmount)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var items = GetTransactionArray(doc.RootElement);
            if (items == null) return null;

            foreach (var item in items.Value.EnumerateArray())
            {
                var content = GetString(item, "transaction_content") ?? GetString(item, "content") ?? GetString(item, "description");
                var code = GetString(item, "code");
                var reference = GetString(item, "reference_number") ?? GetString(item, "referenceCode");
                var extractedCode = ExtractPaymentCode(code, content, reference);
                if (!string.Equals(extractedCode, paymentCode, StringComparison.OrdinalIgnoreCase)) continue;

                var amountIn = GetDecimal(item, "amount_in") ?? GetDecimal(item, "transferAmount") ?? GetDecimal(item, "amount");
                if (amountIn == null || decimal.Round(amountIn.Value, 0) != decimal.Round(totalAmount, 0)) continue;

                return new SepayPaymentData
                {
                    SepayId = GetString(item, "id") ?? GetString(item, "transaction_id") ?? reference ?? Guid.NewGuid().ToString("N"),
                    Gateway = GetString(item, "gateway") ?? GetString(item, "bank_brand_name"),
                    TransactionDate = ParseDate(GetString(item, "transaction_date") ?? GetString(item, "transactionDate")),
                    AccountNumber = GetString(item, "account_number") ?? GetString(item, "accountNumber"),
                    Code = code,
                    Content = content,
                    TransferType = "in",
                    Description = GetString(item, "description"),
                    TransferAmount = amountIn.Value,
                    Accumulated = GetDecimal(item, "accumulated"),
                    ReferenceCode = reference
                };
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Cannot parse SePay API response.");
        }

        return null;
    }

    private JsonElement? GetTransactionArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;

        if (root.TryGetProperty("transactions", out var transactions) && transactions.ValueKind == JsonValueKind.Array)
            return transactions;

        if (root.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array) return data;
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("transactions", out var nested) && nested.ValueKind == JsonValueKind.Array)
                return nested;
        }

        return null;
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Data;
using QuanLyCanTeenHutech.Services;

namespace QuanLyCanTeenHutech.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SepayPaymentService _sepayPaymentService;

    public PaymentController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        SepayPaymentService sepayPaymentService)
    {
        _context = context;
        _userManager = userManager;
        _sepayPaymentService = sepayPaymentService;
    }

    [HttpGet]
    public async Task<IActionResult> Sepay(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var order = await _context.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == userId);

        if (order == null) return NotFound();

        await _sepayPaymentService.EnsurePaymentCodeAsync(order);

        if (order.PaymentStatus == SepayPaymentService.PaymentStatusPaid)
        {
            return RedirectToAction(nameof(Success), new { id = order.Id });
        }

        if (_sepayPaymentService.IsPaymentExpired(order))
        {
            await _sepayPaymentService.MarkPaymentExpiredAsync(order);
        }

        var remainingSeconds = _sepayPaymentService.GetPaymentRemainingSeconds(order);
        var isExpired = order.PaymentStatus == SepayPaymentService.PaymentStatusExpired || _sepayPaymentService.IsPaymentExpired(order);

        ViewData["Title"] = $"Thanh toán đơn hàng #{order.Id}";
        ViewBag.QrUrl = _sepayPaymentService.BuildQrUrl(order);
        ViewBag.PaymentExpireMinutes = _sepayPaymentService.GetPaymentExpireMinutes();
        ViewBag.PaymentRemainingSeconds = remainingSeconds;
        ViewBag.PaymentExpiresAt = _sepayPaymentService.GetPaymentExpiresAt(order).ToString("dd/MM/yyyy HH:mm:ss");
        ViewBag.IsPaymentExpired = isExpired;

        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Status(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == userId);

        if (order == null) return NotFound();

        if (order.PaymentStatus != SepayPaymentService.PaymentStatusPaid && _sepayPaymentService.IsPaymentExpired(order))
        {
            await _sepayPaymentService.MarkPaymentExpiredAsync(order);
        }

        var paid = order.PaymentStatus == SepayPaymentService.PaymentStatusPaid;
        var expired = order.PaymentStatus == SepayPaymentService.PaymentStatusExpired || _sepayPaymentService.IsPaymentExpired(order);
        var remainingSeconds = _sepayPaymentService.GetPaymentRemainingSeconds(order);

        return Json(new
        {
            paid,
            expired,
            remainingSeconds,
            paymentStatus = order.PaymentStatus,
            orderStatus = order.Status,
            paidAt = order.PaidAt?.ToString("dd/MM/yyyy HH:mm:ss")
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Expire(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == userId);
        if (order == null) return NotFound();

        if (order.PaymentStatus == SepayPaymentService.PaymentStatusPaid)
        {
            return Json(new
            {
                paid = true,
                expired = false,
                paymentStatus = order.PaymentStatus,
                message = "Đơn hàng đã thanh toán."
            });
        }

        if (_sepayPaymentService.IsPaymentExpired(order))
        {
            await _sepayPaymentService.MarkPaymentExpiredAsync(order);
            return Json(new
            {
                paid = false,
                expired = true,
                paymentStatus = order.PaymentStatus,
                message = $"Đã hết thời gian thanh toán {_sepayPaymentService.GetPaymentExpireMinutes()} phút. Vui lòng tạo đơn hàng mới."
            });
        }

        return Json(new
        {
            paid = false,
            expired = false,
            remainingSeconds = _sepayPaymentService.GetPaymentRemainingSeconds(order),
            paymentStatus = order.PaymentStatus,
            message = "Đơn hàng vẫn còn thời gian thanh toán."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recheck(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == userId);
        if (order == null) return NotFound();

        var result = await _sepayPaymentService.RecheckOrderByApiAsync(order);

        return Json(new
        {
            paid = result.Paid,
            expired = result.Expired || order.PaymentStatus == SepayPaymentService.PaymentStatusExpired || _sepayPaymentService.IsPaymentExpired(order),
            paymentStatus = order.PaymentStatus,
            message = result.Message
        });
    }

    [HttpGet]
    public async Task<IActionResult> Success(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == userId);

        if (order == null) return NotFound();

        if (order.PaymentStatus != SepayPaymentService.PaymentStatusPaid)
        {
            return RedirectToAction(nameof(Sepay), new { id = order.Id });
        }

        ViewData["Title"] = "Thanh toán thành công";
        return View(order);
    }
}

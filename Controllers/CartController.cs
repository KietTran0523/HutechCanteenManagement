using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Common;
using QuanLyCanTeenHutech.Data;
using QuanLyCanTeenHutech.Models;
using QuanLyCanTeenHutech.Services;

namespace QuanLyCanTeenHutech.Controllers;

public class CartController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SepayPaymentService _sepayPaymentService;
    private const string CART_KEY = "CanteenCart";

    public CartController(ApplicationDbContext context, UserManager<IdentityUser> userManager, SepayPaymentService sepayPaymentService)
    {
        _context = context;
        _userManager = userManager;
        _sepayPaymentService = sepayPaymentService;
    }

    private List<CartItem> GetCart()
    {
        return HttpContext.Session.GetObjectFromJson<List<CartItem>>(CART_KEY) ?? new List<CartItem>();
    }

    private void SaveCart(List<CartItem> cart)
    {
        HttpContext.Session.SetObjectAsJson(CART_KEY, cart);
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Giỏ hàng";
        var cart = GetCart();
        return View(cart);
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
    {
        var product = await _context.Products
            .Include(p => p.ProductGalleries)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);

        if (product == null) return NotFound();

        var cart = GetCart();
        var item = cart.FirstOrDefault(i => i.ProductId == productId);

        if (item == null)
        {
            var imageUrl = product.ProductGalleries.FirstOrDefault(g => g.MediaType == "image")?.FilePath ?? "/images/no-image.png";
            cart.Add(new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductNameEn = product.NameEn,
                Price = product.Price,
                ImageUrl = imageUrl,
                Quantity = quantity
            });
        }
        else
        {
            item.Quantity += quantity;
        }

        SaveCart(cart);
        TempData["SuccessMessage"] = $"Đã thêm {product.Name} vào giỏ hàng!";

        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult UpdateCart(int productId, int quantity)
    {
        var cart = GetCart();
        var item = cart.FirstOrDefault(i => i.ProductId == productId);
        if (item != null && quantity > 0)
        {
            item.Quantity = quantity;
            SaveCart(cart);
            return Json(new { 
                success = true, 
                subtotal = item.Subtotal.ToString("#,##0") + " đ", 
                total = cart.Sum(c => c.Subtotal).ToString("#,##0") + " đ" 
            });
        }
        return Json(new { success = false });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoveFromCart(int productId)
    {
        var cart = GetCart();
        var item = cart.FirstOrDefault(i => i.ProductId == productId);
        if (item != null)
        {
            cart.Remove(item);
            SaveCart(cart);
            TempData["SuccessMessage"] = "Đã xóa sản phẩm khỏi giỏ hàng!";
        }
        return RedirectToAction("Index");
    }

    [Authorize]
    public IActionResult Checkout()
    {
        var cart = GetCart();
        if (!cart.Any())
        {
            TempData["ErrorMessage"] = "Giỏ hàng trống! Vui lòng thêm món ăn trước khi thanh toán.";
            return RedirectToAction("Index");
        }

        ViewData["Title"] = "Thanh toán";
        
        var order = new Order
        {
            TotalAmount = cart.Sum(c => c.Subtotal)
        };

        return View(order);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(Order order)
    {
        var cart = GetCart();
        if (!cart.Any())
        {
            TempData["ErrorMessage"] = "Giỏ hàng trống!";
            return RedirectToAction("Index");
        }

        // We bypass ModelState checks on CustomerId since we assign it here manually
        ModelState.Remove("CustomerId");

        if (ModelState.IsValid)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            order.CustomerId = userId;
            order.OrderDate = DateTime.Now;
            order.Status = "Pending";
            order.PaymentMethod = "Sepay";
            order.PaymentStatus = SepayPaymentService.PaymentStatusUnpaid;
            order.TotalAmount = cart.Sum(c => c.Subtotal);

            _context.Add(order);
            await _context.SaveChangesAsync();

            order.PaymentCode = _sepayPaymentService.CreatePaymentCode(order.Id);
            await _context.SaveChangesAsync();

            // Save order details with price/name snapshots
            foreach (var item in cart)
            {
                var detail = new OrderDetail
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName, // snapshot
                    ProductPrice = item.Price, // snapshot
                    Quantity = item.Quantity
                };
                _context.Add(detail);
            }
            await _context.SaveChangesAsync();

            // Clear cart
            SaveCart(new List<CartItem>());

            TempData["SuccessMessage"] = "Đơn hàng đã được tạo. Vui lòng quét QR SePay để thanh toán.";
            return RedirectToAction("Sepay", "Payment", new { id = order.Id });
        }

        ViewData["Title"] = "Thanh toán";
        order.TotalAmount = cart.Sum(c => c.Subtotal);
        return View(order);
    }
}

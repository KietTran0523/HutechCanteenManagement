using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Data;

namespace QuanLyCanTeenHutech.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Employee")]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Trang quản lý Căng tin HUTECH";
        ViewData["ActivePage"] = "Dashboard";

        var productCount = await _context.Products.CountAsync(p => !p.IsDeleted);
        var categoryCount = await _context.Categories.CountAsync(c => !c.IsDeleted);
        var orderCount = await _context.Orders.CountAsync();
        var totalSales = await _context.Orders
            .Where(o => o.Status == "Completed")
            .SumAsync(o => o.TotalAmount);

        ViewBag.ProductCount = productCount;
        ViewBag.CategoryCount = categoryCount;
        ViewBag.OrderCount = orderCount;
        ViewBag.TotalSales = totalSales;

        var recentOrders = await _context.Orders
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .ToListAsync();

        return View(recentOrders);
    }
}

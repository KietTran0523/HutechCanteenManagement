using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TextTemplating;
using QuanLyCanTeenHutech.Common;
using QuanLyCanTeenHutech.Data;
using QuanLyCanTeenHutech.Models;
using System.Diagnostics;

namespace QuanLyCanTeenHutech.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public HomeController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? search, int? categoryId)
    {
        var categories = await _context.Categories.Where(c => !c.IsDeleted).ToListAsync();
        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = categoryId;
        ViewBag.SearchTerm = search;

        var query = _context.Products
            .Include(p => p.ProductGalleries)
            .Where(p => !p.IsDeleted);

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{search}%"));
        }

        var products = await query.ToListAsync();
        return View(products);
    }

    [Route("danh-muc/{slug}")]
    public async Task<IActionResult> CategoryDetails(string slug)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Slug == slug && !c.IsDeleted);
        if (category == null) return NotFound();

        var categories = await _context.Categories.Where(c => !c.IsDeleted).ToListAsync();
        ViewBag.Categories = categories;
        ViewBag.SelectedCategoryId = category.Id;
        ViewBag.CategoryName = category.Name;

        var products = await _context.Products
            .Include(p => p.ProductGalleries)
            .Where(p => p.CategoryId == category.Id && !p.IsDeleted)
            .ToListAsync();

        return View("Index", products);
    }

    [Route("mon-an/{slug}")]
    public async Task<IActionResult> ProductDetails(string slug)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.ProductGalleries)
            .FirstOrDefaultAsync(p => p.Slug == slug && !p.IsDeleted);
        if (product == null) return NotFound();

        return View(product);
    }

    [Authorize]
    [Route("don-hang-cua-toi")]
    public async Task<IActionResult> MyOrders()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var orders = await _context.Orders
            .Include(o => o.OrderDetails)
            .Where(o => o.CustomerId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return View(orders);
    }
    //Autocomplete
    [HttpGet]
    public async Task<IActionResult> AutoComplete(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Json(new List<object>());

        term = term.Trim();

        var keyword = StringHelper
            .RemoveDiacritics(term)
            .ToLower();

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.NameEn,
                p.Slug,
                p.Price,
                Category = p.Category != null ? p.Category.Name : "",
                CategoryEn = p.Category != null ? p.Category.NameEn : "",
                Image = p.ProductGalleries
                    .Where(g => g.MediaType == "image")
                    .Select(g => g.FilePath)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var suggestions = products

            .Select(p => new
            {
                Product = p,

                SearchNameVi = StringHelper
                    .RemoveDiacritics(p.Name)
                    .ToLower(),

                SearchNameEn = StringHelper
                    .RemoveDiacritics(p.NameEn ?? "")
                    .ToLower()
            })

            .Where(x =>
                x.SearchNameVi.Contains(keyword) ||
                x.SearchNameEn.Contains(keyword))

            .OrderByDescending(x =>
                x.SearchNameVi.StartsWith(keyword) ||
                x.SearchNameEn.StartsWith(keyword))

            .ThenBy(x => x.Product.Name)

            .Take(5)

            .Select(x => new
            {
                id = x.Product.Id,

                name = x.Product.Name,

                nameEn = x.Product.NameEn,

                slug = x.Product.Slug,

                category = x.Product.Category,

                categoryEn = x.Product.CategoryEn,

                price = x.Product.Price,

                image = x.Product.Image ?? "/images/default-food.svg"
            })

            .ToList();

        return Json(suggestions);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

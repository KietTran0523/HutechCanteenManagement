using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Common;
using QuanLyCanTeenHutech.Data;
using QuanLyCanTeenHutech.Models;

namespace QuanLyCanTeenHutech.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Employee")]
public class ProductController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _hostEnvironment;

    public ProductController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
    {
        _context = context;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý món ăn";
        ViewData["ActivePage"] = "Products";
        var products = await _context.Products
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted)
            .ToListAsync();
        return View(products);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Thêm món ăn";
        ViewData["ActivePage"] = "Products";
        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(), 
            "Id", "Name"
        );
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(209715200)]
    [RequestFormLimits(MultipartBodyLengthLimit = 209715200)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Product product, List<IFormFile> mediaFiles)
    {
        if (ModelState.IsValid)
        {
            product.Slug = SlugHelper.GenerateSlug(product.Name);
            _context.Add(product);
            await _context.SaveChangesAsync();

            // Save multiple media files
            if (mediaFiles != null && mediaFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                foreach (var file in mediaFiles)
                {
                    if (file.Length > 0)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        // Determine media type
                        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        string mediaType = (ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".webm") ? "video" : "image";

                        var gallery = new ProductGallery
                        {
                            ProductId = product.Id,
                            FilePath = "/uploads/" + uniqueFileName,
                            MediaType = mediaType
                        };
                        _context.Add(gallery);
                    }
                }
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Thêm món ăn mới thành công!";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Thêm món ăn";
        ViewData["ActivePage"] = "Products";
        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(), 
            "Id", "Name", product.CategoryId
        );
        return View(product);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products
            .Include(p => p.ProductGalleries)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product == null || product.IsDeleted) return NotFound();

        ViewData["Title"] = "Chỉnh sửa món ăn";
        ViewData["ActivePage"] = "Products";
        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(), 
            "Id", "Name", product.CategoryId
        );
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(209715200)]
    [RequestFormLimits(MultipartBodyLengthLimit = 209715200)]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, Product product, List<IFormFile> mediaFiles)
    {
        if (id != product.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                product.Slug = SlugHelper.GenerateSlug(product.Name);
                _context.Update(product);
                await _context.SaveChangesAsync();

                // Save multiple media files
                if (mediaFiles != null && mediaFiles.Count > 0)
                {
                    string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "uploads");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    foreach (var file in mediaFiles)
                    {
                        if (file.Length > 0)
                        {
                            string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(fileStream);
                            }

                            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                            string mediaType = (ext == ".mp4" || ext == ".mov" || ext == ".avi" || ext == ".webm") ? "video" : "image";

                            var gallery = new ProductGallery
                            {
                                ProductId = product.Id,
                                FilePath = "/uploads/" + uniqueFileName,
                                MediaType = mediaType
                            };
                            _context.Add(gallery);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "Cập nhật thông tin món ăn thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Chỉnh sửa món ăn";
        ViewData["ActivePage"] = "Products";
        ViewBag.Categories = new SelectList(
            await _context.Categories.Where(c => !c.IsDeleted).ToListAsync(), 
            "Id", "Name", product.CategoryId
        );
        // Reload galleries for validation error page
        product.ProductGalleries = await _context.ProductGalleries.Where(g => g.ProductId == product.Id).ToListAsync();
        return View(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveMedia(int mediaId)
    {
        var media = await _context.ProductGalleries.FindAsync(mediaId);
        if (media == null) return NotFound();

        int productId = media.ProductId;

        // Delete from wwwroot
        var fullPath = Path.Combine(_hostEnvironment.WebRootPath, media.FilePath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }

        _context.ProductGalleries.Remove(media);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var product = await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null || product.IsDeleted) return NotFound();

        ViewData["Title"] = "Xóa món ăn";
        ViewData["ActivePage"] = "Products";
        return View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            // Soft delete
            product.IsDeleted = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa món ăn thành công!";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool ProductExists(int id)
    {
        return _context.Products.Any(e => e.Id == id);
    }
}

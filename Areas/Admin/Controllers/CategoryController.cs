using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Common;
using QuanLyCanTeenHutech.Data;
using QuanLyCanTeenHutech.Models;

namespace QuanLyCanTeenHutech.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Employee")]
public class CategoryController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý danh mục";
        ViewData["ActivePage"] = "Categories";
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted)
            .ToListAsync();
        return View(categories);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm danh mục";
        ViewData["ActivePage"] = "Categories";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(Category category)
    {
        if (ModelState.IsValid)
        {
            category.Slug = SlugHelper.GenerateSlug(category.Name);
            _context.Add(category);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm danh mục mới thành công!";
            return RedirectToAction(nameof(Index));
        }
        ViewData["Title"] = "Thêm danh mục";
        ViewData["ActivePage"] = "Categories";
        return View(category);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var category = await _context.Categories.FindAsync(id);
        if (category == null || category.IsDeleted) return NotFound();

        ViewData["Title"] = "Sửa danh mục";
        ViewData["ActivePage"] = "Categories";
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                category.Slug = SlugHelper.GenerateSlug(category.Name);
                _context.Update(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(category.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewData["Title"] = "Sửa danh mục";
        ViewData["ActivePage"] = "Categories";
        return View(category);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var category = await _context.Categories
            .FirstOrDefaultAsync(m => m.Id == id);
        if (category == null || category.IsDeleted) return NotFound();

        ViewData["Title"] = "Xóa danh mục";
        ViewData["ActivePage"] = "Categories";
        return View(category);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            category.IsDeleted = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa danh mục thành công!";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool CategoryExists(int id)
    {
        return _context.Categories.Any(e => e.Id == id);
    }
}

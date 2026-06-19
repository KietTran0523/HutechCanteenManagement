using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Data;

namespace QuanLyCanTeenHutech.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,Employee")]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;

    public OrderController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý đơn hàng";
        ViewData["ActivePage"] = "Orders";
        var orders = await _context.Orders
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var order = await _context.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (order == null) return NotFound();

        ViewData["Title"] = $"Chi tiết đơn hàng #{order.Id}";
        ViewData["ActivePage"] = "Orders";
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        string[] validStatuses = { "Pending", "Confirmed", "Completed", "Cancelled" };
        if (Array.Exists(validStatuses, s => s == status))
        {
            order.Status = status;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật trạng thái đơn hàng thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = "Trạng thái không hợp lệ!";
        }

        return RedirectToAction(nameof(Details), new { id = id });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        ViewData["Title"] = $"Chỉnh sửa thông tin đơn hàng #{order.Id}";
        ViewData["ActivePage"] = "Orders";
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,PhoneNumber,ShippingAddress,Notes,Status,TotalAmount,OrderDate,CustomerId")] QuanLyCanTeenHutech.Models.Order order)
    {
        if (id != order.Id) return NotFound();

        // CustomerId and TotalAmount and OrderDate are needed for the model state if they are validated, 
        // We will remove validation for Customer as it is not strictly necessary for this edit
        ModelState.Remove("Customer");
        ModelState.Remove("OrderDetails");

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(order);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thông tin nhận hàng thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrderExists(order.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
        
        ViewData["Title"] = $"Chỉnh sửa thông tin đơn hàng #{order.Id}";
        ViewData["ActivePage"] = "Orders";
        TempData["ErrorMessage"] = "Thông tin không hợp lệ, vui lòng kiểm tra lại!";
        return View(order);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var order = await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.Id == id);
        if (order != null)
        {
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đơn hàng đã được xóa vĩnh viễn khỏi hệ thống.";
        }
        else
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng để xóa!";
        }
        
        return RedirectToAction(nameof(Index));
    }

    private bool OrderExists(int id)
    {
        return _context.Orders.Any(e => e.Id == id);
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QuanLyCanTeenHutech.Models;

public class Order
{
    public int Id { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [ForeignKey("CustomerId")]
    [ValidateNever]
    public IdentityUser? Customer { get; set; }

    [Display(Name = "Ngày đặt")]
    public DateTime OrderDate { get; set; } = DateTime.Now;

    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Tổng tiền")]
    public decimal TotalAmount { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Completed, Cancelled

    [Required(ErrorMessage = "Họ tên người nhận là bắt buộc")]
    [StringLength(100)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại là bắt buộc")]
    [StringLength(20)]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vị trí giao hàng (bàn/khu vực) là bắt buộc")]
    [StringLength(200)]
    [Display(Name = "Bàn / Vị trí nhận")]
    public string ShippingAddress { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string? Notes { get; set; }

    [ValidateNever]
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

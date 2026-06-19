using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QuanLyCanTeenHutech.Models;

public class OrderDetail
{
    public int Id { get; set; }

    [Required]
    public int OrderId { get; set; }

    [ValidateNever]
    public Order? Order { get; set; }

    public int? ProductId { get; set; }

    [ValidateNever]
    public Product? Product { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Tên sản phẩm")]
    public string ProductName { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Giá tại thời điểm mua")]
    public decimal ProductPrice { get; set; }

    [Required]
    [Range(1, 1000, ErrorMessage = "Số lượng phải ít nhất là 1")]
    [Display(Name = "Số lượng")]
    public int Quantity { get; set; }
}

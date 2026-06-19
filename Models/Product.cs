using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QuanLyCanTeenHutech.Models;

public class Product
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên món ăn/sản phẩm là bắt buộc")]
    [StringLength(200)]
    [Display(Name = "Tên sản phẩm")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    [Display(Name = "Tên sản phẩm (Tiếng Anh)")]
    public string? NameEn { get; set; }

    [ValidateNever]
    [StringLength(250)]
    public string Slug { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [Display(Name = "Mô tả (Tiếng Anh)")]
    public string? DescriptionEn { get; set; }

    [Required(ErrorMessage = "Giá sản phẩm là bắt buộc")]
    [Range(0, 1000000000, ErrorMessage = "Giá sản phẩm phải lớn hơn hoặc bằng 0")]
    [Column(TypeName = "decimal(18, 2)")]
    [Display(Name = "Giá (VND)")]
    public decimal Price { get; set; }

    [Display(Name = "Danh mục")]
    public int? CategoryId { get; set; }

    [Display(Name = "Danh mục")]
    [ValidateNever]
    public Category? Category { get; set; }

    [Display(Name = "Đã xóa")]
    public bool IsDeleted { get; set; } = false;

    [ValidateNever]
    public ICollection<ProductGallery> ProductGalleries { get; set; } = new List<ProductGallery>();
    
    [ValidateNever]
    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
}

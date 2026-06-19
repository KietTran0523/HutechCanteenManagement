using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QuanLyCanTeenHutech.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên danh mục là bắt buộc")]
    [StringLength(100)]
    [Display(Name = "Tên danh mục")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Tên danh mục (Tiếng Anh)")]
    public string? NameEn { get; set; }

    [ValidateNever]
    [StringLength(150)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [StringLength(500)]
    [Display(Name = "Mô tả (Tiếng Anh)")]
    public string? DescriptionEn { get; set; }

    [Display(Name = "Đã xóa")]
    public bool IsDeleted { get; set; } = false;

    [ValidateNever]
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

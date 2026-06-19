using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QuanLyCanTeenHutech.Models;

public class ProductGallery
{
    public int Id { get; set; }

    [Required]
    public int ProductId { get; set; }

    [ValidateNever]
    public Product? Product { get; set; }

    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string MediaType { get; set; } = "image"; // "image" or "video"
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyCanTeenHutech.Models;

public class SepayPaymentData
{
    public string SepayId { get; set; } = string.Empty;
    public string? Gateway { get; set; }
    public DateTime? TransactionDate { get; set; }
    public string? AccountNumber { get; set; }
    public string? SubAccount { get; set; }
    public string? Code { get; set; }
    public string? Content { get; set; }
    public string? TransferType { get; set; }
    public string? Description { get; set; }
    public decimal TransferAmount { get; set; }
    public decimal? Accumulated { get; set; }
    public string? ReferenceCode { get; set; }
}

public class SepayTransaction
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string SepayId { get; set; } = string.Empty;

    [StringLength(50)]
    public string? Gateway { get; set; }

    public DateTime? TransactionDate { get; set; }

    [StringLength(50)]
    public string? AccountNumber { get; set; }

    [StringLength(50)]
    public string? SubAccount { get; set; }

    [StringLength(100)]
    public string? Code { get; set; }

    public string? Content { get; set; }

    [StringLength(20)]
    public string? TransferType { get; set; }

    public string? Description { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TransferAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Accumulated { get; set; }

    [StringLength(100)]
    public string? ReferenceCode { get; set; }

    public string? RawBody { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class SepayProcessResult
{
    public bool Paid { get; set; }
    public bool Expired { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? OrderId { get; set; }
}

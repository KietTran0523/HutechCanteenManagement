using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuanLyCanTeenHutech.Models;

namespace QuanLyCanTeenHutech.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductGallery> ProductGalleries { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderDetail> OrderDetails { get; set; } = null!;
    public DbSet<SepayTransaction> SepayTransactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);


        builder.Entity<Order>()
            .HasIndex(o => o.PaymentCode)
            .IsUnique()
            .HasFilter("[PaymentCode] IS NOT NULL");

        builder.Entity<SepayTransaction>()
            .HasIndex(t => t.SepayId)
            .IsUnique();

        // Configure Category -> Product (SetNull on Delete)
        builder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure Product -> OrderDetail (SetNull on Delete)
        builder.Entity<OrderDetail>()
            .HasOne(od => od.Product)
            .WithMany(p => p.OrderDetails)
            .HasForeignKey(od => od.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // Configure Order -> OrderDetail (Cascade on Delete, since if an order itself is deleted, its details should be deleted)
        builder.Entity<OrderDetail>()
            .HasOne(od => od.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(od => od.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

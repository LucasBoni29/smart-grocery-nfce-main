using Microsoft.EntityFrameworkCore;
using SmartGrocery.Api.Models;

namespace SmartGrocery.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Product> Products { get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<PurchaseItem> PurchaseItems { get; set; }
    public DbSet<CanonicalProduct> CanonicalProducts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Barcode).HasMaxLength(50);
            // Nao e unique: o "codigo" que a nota expoe e interno de cada loja (PLU/SKU),
            // nao um EAN/GTIN universal. Duas lojas diferentes podem repetir o mesmo codigo
            // pra produtos diferentes - ver _memoria/estrategia.md (Gargalo atual).
            entity.HasIndex(e => e.Barcode);

            entity.HasOne(e => e.CanonicalProduct)
                .WithMany(c => c.Products)
                .HasForeignKey(e => e.CanonicalProductId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CanonicalProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Category).HasMaxLength(100);
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StoreName).HasMaxLength(200);
            entity.Property(e => e.NfceAccessKey).HasMaxLength(44);
            entity.HasIndex(e => e.NfceAccessKey).IsUnique();
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PurchaseItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
            entity.Property(e => e.Unit).HasMaxLength(20);

            entity.HasOne(e => e.Purchase)
                .WithMany(p => p.Items)
                .HasForeignKey(e => e.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                .WithMany(p => p.PurchaseItems)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PesticideShop.Models;

namespace PesticideShop.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<CustomerTransaction> CustomerTransactions { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<ActivityLog> ActivityLogs { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<ReturnTracking> ReturnTrackings { get; set; }
    public DbSet<ExchangeTracking> ExchangeTrackings { get; set; }
    
    // Daily Inventory Tables
    public DbSet<DailyInventory> DailyInventories { get; set; }
    public DbSet<DailySaleTransaction> DailySaleTransactions { get; set; }
    public DbSet<DailyProductSummary> DailyProductSummaries { get; set; }
    public DbSet<DailyCustomerSummary> DailyCustomerSummaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure CustomerTransaction foreign keys
        modelBuilder.Entity<CustomerTransaction>()
            .HasOne(ct => ct.Customer)
            .WithMany(c => c.Transactions)
            .HasForeignKey(ct => ct.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<CustomerTransaction>()
            .HasOne(ct => ct.Product)
            .WithMany(p => p.CustomerTransactions)
            .HasForeignKey(ct => ct.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure Product-Category relationship
        modelBuilder.Entity<Product>()
            .HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Invoice relationships
        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.Customer)
            .WithMany()
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne(ii => ii.Product)
            .WithMany()
            .HasForeignKey(ii => ii.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Color and Size columns for InvoiceItem
        modelBuilder.Entity<InvoiceItem>()
            .Property(ii => ii.Color)
            .HasMaxLength(50);

        modelBuilder.Entity<InvoiceItem>()
            .Property(ii => ii.Size)
            .HasMaxLength(50);

        // Configure Color and Size columns for CustomerTransaction
        modelBuilder.Entity<CustomerTransaction>()
            .Property(ct => ct.Color)
            .HasMaxLength(50);

        modelBuilder.Entity<CustomerTransaction>()
            .Property(ct => ct.Size)
            .HasMaxLength(50);

        // Configure Product additional properties
        modelBuilder.Entity<Product>()
            .Property(p => p.AvailableColors)
            .HasMaxLength(200);

        modelBuilder.Entity<Product>()
            .Property(p => p.AvailableSizes)
            .HasMaxLength(200);
            
        // Configure PaymentMethod column for Invoice
        modelBuilder.Entity<Invoice>()
            .Property(i => i.PaymentMethod)
            .HasMaxLength(50);

        // Configure ExchangeTracking relationships
        modelBuilder.Entity<ExchangeTracking>()
            .HasOne(et => et.OldProduct)
            .WithMany()
            .HasForeignKey(et => et.OldProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ExchangeTracking>()
            .HasOne(et => et.NewProduct)
            .WithMany()
            .HasForeignKey(et => et.NewProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Daily Inventory relationships
        modelBuilder.Entity<DailySaleTransaction>()
            .HasOne(dst => dst.DailyInventory)
            .WithMany(di => di.SaleTransactions)
            .HasForeignKey(dst => dst.DailyInventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DailySaleTransaction>()
            .HasOne(dst => dst.Customer)
            .WithMany()
            .HasForeignKey(dst => dst.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailySaleTransaction>()
            .HasOne(dst => dst.Product)
            .WithMany()
            .HasForeignKey(dst => dst.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyProductSummary>()
            .HasOne(dps => dps.DailyInventory)
            .WithMany(di => di.ProductSummaries)
            .HasForeignKey(dps => dps.DailyInventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DailyProductSummary>()
            .HasOne(dps => dps.Product)
            .WithMany()
            .HasForeignKey(dps => dps.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DailyCustomerSummary>()
            .HasOne(dcs => dcs.DailyInventory)
            .WithMany(di => di.CustomerSummaries)
            .HasForeignKey(dcs => dcs.DailyInventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DailyCustomerSummary>()
            .HasOne(dcs => dcs.Customer)
            .WithMany()
            .HasForeignKey(dcs => dcs.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure unique constraints
        modelBuilder.Entity<DailyInventory>()
            .HasIndex(di => di.InventoryDate)
            .IsUnique();

        modelBuilder.Entity<DailyProductSummary>()
            .HasIndex(dps => new { dps.DailyInventoryId, dps.ProductId })
            .IsUnique();

        modelBuilder.Entity<DailyCustomerSummary>()
            .HasIndex(dcs => new { dcs.DailyInventoryId, dcs.CustomerId })
            .IsUnique();
    }
}

using Microsoft.EntityFrameworkCore;
using StationeryShop.Models;

namespace StationeryShop.Data
{
    public class StationeryDbContext : DbContext
    {
        public StationeryDbContext(DbContextOptions<StationeryDbContext> options)
           : base(options)
        { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Product
            modelBuilder.Entity<Product>()
                .HasKey(e => e.ProductID);

            modelBuilder.Entity<Product>()
                .Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Product>()
                .Property(e => e.Price)
                .HasPrecision(18, 2);

            // Category
            modelBuilder.Entity<Category>()
                .HasKey(e => e.CategoryID);

            modelBuilder.Entity<Category>()
                .Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            // Customer
            modelBuilder.Entity<Customer>()
                .HasKey(e => e.CustomerID);

            modelBuilder.Entity<Customer>()
                .Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Customer>()
                .Property(e => e.Email)
                .IsRequired();

            // Order
            modelBuilder.Entity<Order>()
                .HasKey(e => e.OrderID);

            modelBuilder.Entity<Order>()
                .Property(e => e.TotalAmount)
                .HasPrecision(18, 2);

            // OrderItem
            modelBuilder.Entity<OrderItem>()
                .HasKey(e => e.OrderItemID);

            // Связи
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerID);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderID);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductID);
        }
    }
}   
using Microsoft.EntityFrameworkCore;
using PoultrySlaughterPOS.Models;
using System.Reflection;

namespace PoultrySlaughterPOS.Data
{
    public class PoultryDbContext : DbContext
    {
        public PoultryDbContext(DbContextOptions<PoultryDbContext> options) : base(options)
        {
        }

        // DbSets for all entities
        public DbSet<Truck> Trucks { get; set; }
        public DbSet<TruckLoad> TruckLoads { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<DailyReconciliation> DailyReconciliations { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure entity relationships and constraints
            ConfigureEntities(modelBuilder);

            // Seed initial data
            SeedData(modelBuilder);
        }

        private void ConfigureEntities(ModelBuilder modelBuilder)
        {
            // Truck Configuration
            modelBuilder.Entity<Truck>(entity =>
            {
                entity.HasKey(e => e.TruckId);
                entity.HasIndex(e => e.TruckNumber).IsUnique();
                entity.Property(e => e.TruckNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.DriverName).IsRequired().HasMaxLength(100);
            });

            // TruckLoad Configuration
            modelBuilder.Entity<TruckLoad>(entity =>
            {
                entity.HasKey(e => e.LoadId);
                entity.HasOne(e => e.Truck)
                      .WithMany(t => t.TruckLoads)
                      .HasForeignKey(e => e.TruckId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.TotalWeight).HasPrecision(10, 2);
                entity.Property(e => e.Status).HasDefaultValue("LOADED");
            });

            // Customer Configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.CustomerId);
                entity.HasIndex(e => e.CustomerName);
                entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.TotalDebt).HasPrecision(12, 2).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // Invoice Configuration
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasKey(e => e.InvoiceId);
                entity.HasIndex(e => e.InvoiceNumber).IsUnique();
                entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(20);

                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.Invoices)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Truck)
                      .WithMany(t => t.Invoices)
                      .HasForeignKey(e => e.TruckId)
                      .OnDelete(DeleteBehavior.Restrict);

                // Decimal precision configuration
                entity.Property(e => e.GrossWeight).HasPrecision(10, 2);
                entity.Property(e => e.CagesWeight).HasPrecision(10, 2);
                entity.Property(e => e.NetWeight).HasPrecision(10, 2);
                entity.Property(e => e.UnitPrice).HasPrecision(8, 2);
                entity.Property(e => e.TotalAmount).HasPrecision(12, 2);
                entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
                entity.Property(e => e.FinalAmount).HasPrecision(12, 2);
                entity.Property(e => e.PreviousBalance).HasPrecision(12, 2);
                entity.Property(e => e.CurrentBalance).HasPrecision(12, 2);
            });

            // Payment Configuration
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.PaymentId);

                entity.HasOne(e => e.Customer)
                      .WithMany(c => c.Payments)
                      .HasForeignKey(e => e.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Invoice)
                      .WithMany(i => i.Payments)
                      .HasForeignKey(e => e.InvoiceId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(e => e.Amount).HasPrecision(12, 2);
                entity.Property(e => e.PaymentMethod).HasDefaultValue("CASH");
            });

            // DailyReconciliation Configuration
            modelBuilder.Entity<DailyReconciliation>(entity =>
            {
                entity.HasKey(e => e.ReconciliationId);

                entity.HasOne(e => e.Truck)
                      .WithMany(t => t.DailyReconciliations)
                      .HasForeignKey(e => e.TruckId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.LoadWeight).HasPrecision(10, 2);
                entity.Property(e => e.SoldWeight).HasPrecision(10, 2);
                entity.Property(e => e.WastageWeight).HasPrecision(10, 2);
                entity.Property(e => e.WastagePercentage).HasPrecision(5, 2);
                entity.Property(e => e.Status).HasDefaultValue("PENDING");

                // Unique constraint for truck and date
                entity.HasIndex(e => new { e.TruckId, e.ReconciliationDate }).IsUnique();
            });

            // AuditLog Configuration
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(e => e.AuditId);
                entity.HasIndex(e => new { e.TableName, e.CreatedDate });
                entity.Property(e => e.TableName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Operation).IsRequired().HasMaxLength(10);
            });
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            // Seed initial trucks
            modelBuilder.Entity<Truck>().HasData(
                new Truck
                {
                    TruckId = 1,
                    TruckNumber = "TR-001",
                    DriverName = "أحمد محمد",
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                },
                new Truck
                {
                    TruckId = 2,
                    TruckNumber = "TR-002",
                    DriverName = "محمد علي",
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                },
                new Truck
                {
                    TruckId = 3,
                    TruckNumber = "TR-003",
                    DriverName = "علي حسن",
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                }
            );

            // Seed initial customers
            modelBuilder.Entity<Customer>().HasData(
                new Customer
                {
                    CustomerId = 1,
                    CustomerName = "سوق الجملة المركزي",
                    PhoneNumber = "07901234567",
                    Address = "بغداد - الكرادة",
                    TotalDebt = 0,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                },
                new Customer
                {
                    CustomerId = 2,
                    CustomerName = "مطعم الأصالة",
                    PhoneNumber = "07801234567",
                    Address = "بغداد - الجادرية",
                    TotalDebt = 0,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                },
                new Customer
                {
                    CustomerId = 3,
                    CustomerName = "متجر الطازج للدواجن",
                    PhoneNumber = "07901234568",
                    Address = "بغداد - الأعظمية",
                    TotalDebt = 0,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                }
            );
        }

        // Override SaveChanges to implement audit logging
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await AddAuditLogs();
            return await base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            AddAuditLogs().Wait();
            return base.SaveChanges();
        }

        private async Task AddAuditLogs()
        {
            var auditEntries = new List<AuditLog>();

            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditLog = new AuditLog
                {
                    TableName = entry.Entity.GetType().Name,
                    Operation = entry.State.ToString(),
                    CreatedDate = DateTime.Now,
                    UserId = "SYSTEM" // TODO: Replace with actual user ID when authentication is implemented
                };

                if (entry.State == EntityState.Modified)
                {
                    auditLog.OldValues = GetOriginalValues(entry);
                    auditLog.NewValues = GetCurrentValues(entry);
                }
                else if (entry.State == EntityState.Added)
                {
                    auditLog.NewValues = GetCurrentValues(entry);
                }
                else if (entry.State == EntityState.Deleted)
                {
                    auditLog.OldValues = GetOriginalValues(entry);
                }

                auditEntries.Add(auditLog);
            }

            foreach (var auditLog in auditEntries)
            {
                AuditLogs.Add(auditLog);
            }
        }

        private string GetOriginalValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            var values = new Dictionary<string, object?>();
            foreach (var property in entry.OriginalValues.Properties)
            {
                values[property.Name] = entry.OriginalValues[property];
            }
            return System.Text.Json.JsonSerializer.Serialize(values);
        }

        private string GetCurrentValues(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
        {
            var values = new Dictionary<string, object?>();
            foreach (var property in entry.CurrentValues.Properties)
            {
                values[property.Name] = entry.CurrentValues[property];
            }
            return System.Text.Json.JsonSerializer.Serialize(values);
        }
    }
}
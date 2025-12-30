using Microsoft.EntityFrameworkCore;
using HotelApp.Models;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using static Android.Provider.DocumentsContract;


namespace HotelApp.Data
{
    // DbContext: Cổng giao tiếp với DB, chứa DbSet cho từng bảng
    public class HotelDbContext : DbContext
    {
        public HotelDbContext(DbContextOptions<HotelDbContext> options) : base(options) { }

        // DbSet tương đương các bảng
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<RoomType> RoomTypes => Set<RoomType>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Stay> Stays => Set<Stay>();
        // Fixed types for services
        public DbSet<Service> Services => Set<Service>();
        public DbSet<ServiceUsage> ServiceUsages => Set<ServiceUsage>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Explicit table mapping to match your DB
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<User>().ToTable("Users");

            // Map services tables explicitly (use plural names used elsewhere)
            modelBuilder.Entity<Service>().ToTable("Services");
            modelBuilder.Entity<ServiceUsage>().ToTable("ServiceUsages");

            // Index unique cho số phòng
            modelBuilder.Entity<Room>().HasIndex(r => r.RoomNumber).IsUnique();

            // Quan hệ Room - RoomType (1-n)
            modelBuilder.Entity<Room>()
                .HasOne(r => r.RoomType)
                .WithMany(rt => rt.Rooms)
                .HasForeignKey(r => r.RoomTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Quan hệ Booking - Customer/Room
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Customer)
                .WithMany(c => c.Bookings)
                .HasForeignKey(b => b.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Room)
                .WithMany(r => r.Bookings)
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // Quan hệ Stay - Booking/Customer/Room
            modelBuilder.Entity<Stay>()
                .HasOne(s => s.Booking)
                .WithMany(b => b.Stays)
                .HasForeignKey(s => s.BookingId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Stay>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Stays)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Stay>()
                .HasOne(s => s.Room)
                .WithMany(r => r.Stays)
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // Quan hệ ServiceUsage - Stay/Service
            modelBuilder.Entity<ServiceUsage>()
                .HasOne(su => su.Stay)
                .WithMany(s => s.ServiceUsages)
                .HasForeignKey(su => su.StayId);

            modelBuilder.Entity<ServiceUsage>()
                .HasOne(su => su.Service)
                .WithMany(s => s.ServiceUsages)
                .HasForeignKey(su => su.ServiceId);

            // Quan hệ Invoice - Stay, Payment - Invoice
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Stay)
                .WithMany(s => s.Invoices)
                .HasForeignKey(i => i.StayId);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId);

            // Quan hệ User - Role, AuditLog - User
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId);
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId);
        }
    }
}

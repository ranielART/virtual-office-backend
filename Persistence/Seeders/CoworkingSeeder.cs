using Domain.Entity.Coworking;
using Domain.Enum;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Persistence.Seeders
{
    public class CoworkingSeeder
    {
        private readonly ApplicationDbContext _context;

        public CoworkingSeeder(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedAsync()
        {
            await SeedResourcesAsync();
            await SeedServicePackagesAsync();
        }

        private async Task SeedResourcesAsync()
        {
            if (await _context.CoworkingResources.AnyAsync())
                return;

            var resources = new List<CoworkingResource>();

            // 20 individual coworking seats
            for (int i = 1; i <= 20; i++)
            {
                resources.Add(new CoworkingResource
                {
                    Id = Guid.NewGuid(),
                    Name = $"Seat {i:D2}",
                    ResourceType = ResourceType.CoworkingSeats,
                    Capacity = 1,
                    Description = $"Individual coworking seat #{i:D2}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Meeting rooms
            resources.Add(new CoworkingResource
            {
                Id = Guid.NewGuid(),
                Name = "Executive Room",
                ResourceType = ResourceType.ExecutiveRoom,
                Capacity = 3,
                Description = "Private executive meeting room. Maximum 3 pax.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            resources.Add(new CoworkingResource
            {
                Id = Guid.NewGuid(),
                Name = "Collaboration Room",
                ResourceType = ResourceType.CollaborationRoom,
                Capacity = 20,
                Description = "Large collaboration meeting room. Maximum 20 pax.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.CoworkingResources.AddRangeAsync(resources);
            await _context.SaveChangesAsync();
        }

        private async Task SeedServicePackagesAsync()
        {
            if (await _context.ServicePackages.AnyAsync())
                return;

            var packages = new List<ServicePackage>
            {
                // ── Coworking Seats ──────────────────────────────────────────────
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Hourly — Regular",
                    ResourceType = ResourceType.CoworkingSeats,
                    PackageType = PackageType.Hourly,
                    PriceAmount = 50.00m,
                    DurationHours = null,
                    IsStudentEligible = false,
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Hourly — Student",
                    ResourceType = ResourceType.CoworkingSeats,
                    PackageType = PackageType.Hourly,
                    PriceAmount = 40.00m,
                    DurationHours = null,
                    IsStudentEligible = false, // Rate IS the student rate; no separate discount applied
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Day Pass",
                    ResourceType = ResourceType.CoworkingSeats,
                    PackageType = PackageType.DayPass,
                    PriceAmount = 600.00m,
                    DurationHours = 10,
                    IsStudentEligible = true,
                    StudentDiscountPercent = 10,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Weekly Pass",
                    ResourceType = ResourceType.CoworkingSeats,
                    PackageType = PackageType.WeeklyPass,
                    PriceAmount = 2700.00m,
                    DurationDays = 5,
                    IsStudentEligible = true,
                    StudentDiscountPercent = 10,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Monthly Dedicated Desk",
                    ResourceType = ResourceType.CoworkingSeats,
                    PackageType = PackageType.MonthlyDedicatedDesk,
                    PriceAmount = 8500.00m,
                    DurationDays = 30,
                    IsStudentEligible = true,
                    StudentDiscountPercent = 10,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },

                // ── Executive Room ───────────────────────────────────────────────
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Executive Room — Hourly",
                    ResourceType = ResourceType.ExecutiveRoom,
                    PackageType = PackageType.Hourly,
                    PriceAmount = 800.00m,
                    DurationHours = null,
                    IsStudentEligible = false,
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Executive Room — Half Day",
                    ResourceType = ResourceType.ExecutiveRoom,
                    PackageType = PackageType.HalfDay,
                    PriceAmount = 3000.00m,
                    DurationHours = 5,
                    IsStudentEligible = false,
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Executive Room — Full Day",
                    ResourceType = ResourceType.ExecutiveRoom,
                    PackageType = PackageType.FullDay,
                    PriceAmount = 5500.00m,
                    DurationHours = 8,
                    IsStudentEligible = false,
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },

                // ── Collaboration Room ───────────────────────────────────────────
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Collaboration Room — Hourly",
                    ResourceType = ResourceType.CollaborationRoom,
                    PackageType = PackageType.Hourly,
                    PriceAmount = 1500.00m,
                    DurationHours = null,
                    IsStudentEligible = false,
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Collaboration Room — Half Day",
                    ResourceType = ResourceType.CollaborationRoom,
                    PackageType = PackageType.HalfDay,
                    PriceAmount = 5500.00m,
                    DurationHours = 4,
                    IsStudentEligible = false,
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new ServicePackage
                {
                    Id = Guid.NewGuid(),
                    Name = "Collaboration Room — Full Day",
                    ResourceType = ResourceType.CollaborationRoom,
                    PackageType = PackageType.FullDay,
                    PriceAmount = 9500.00m,
                    DurationHours = 8,
                    IsStudentEligible = false,
                    StudentDiscountPercent = 0,
                    DownpaymentPercent = 50,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await _context.ServicePackages.AddRangeAsync(packages);
            await _context.SaveChangesAsync();
        }
    }
}

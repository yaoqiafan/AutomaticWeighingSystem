using AWS.Core.Entities;
using AWS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace AWS.Data;

public class AwsDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<GoodsCategory> GoodsCategories => Set<GoodsCategory>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WeighingQueue> WeighingQueues => Set<WeighingQueue>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DeliveryRecord> DeliveryRecords => Set<DeliveryRecord>();
    public DbSet<DeliveryItem> DeliveryItems => Set<DeliveryItem>();

    public AwsDbContext(DbContextOptions<AwsDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemSetting>().HasKey(e => e.Key);

        modelBuilder.Entity<DeliveryRecord>()
            .HasMany(r => r.Items)
            .WithOne(i => i.DeliveryRecord)
            .HasForeignKey(i => i.DeliveryRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<WeighingQueue>()
            .Property(q => q.Status)
            .HasConversion<int>();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<int>();

        SeedDefaultData(modelBuilder);
    }

    private static void SeedDefaultData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemSetting>().HasData(
            new SystemSetting { Key = SettingKeys.SerialPortName, Value = "COM1" },
            new SystemSetting { Key = SettingKeys.BaudRate, Value = "9600" },
            new SystemSetting { Key = SettingKeys.CompanyName, Value = "绿鑫资源" },
            new SystemSetting { Key = SettingKeys.SkinType, Value = "Dark" },
            new SystemSetting { Key = SettingKeys.CloudSyncEnabled, Value = "false" },
            new SystemSetting { Key = SettingKeys.CloudSyncUrl, Value = "" },
            new SystemSetting { Key = SettingKeys.DefaultPricePerKg, Value = "0" },
            new SystemSetting { Key = SettingKeys.SerialPortEnabled, Value = "false" }
        );

        // 默认管理员账号，密码 Admin@123 的 SHA256
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "6d7fce9fee471194aa8b5b6e47267f03fbe8a7d774ae87c5d4b2fa0bcabee7a9",
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = new DateTime(2025, 1, 1)
        });
    }
}

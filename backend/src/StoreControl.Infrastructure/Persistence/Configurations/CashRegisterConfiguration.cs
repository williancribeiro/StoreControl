using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StoreControl.Domain.Cash;

namespace StoreControl.Infrastructure.Persistence.Configurations;

internal sealed class CashRegisterConfiguration : IEntityTypeConfiguration<CashRegister>
{
    public void Configure(EntityTypeBuilder<CashRegister> builder)
    {
        builder.ToTable("CashRegisters", "cash", table =>
        {
            table.HasCheckConstraint("CK_CashRegisters_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
            table.HasCheckConstraint("CK_CashRegisters_Name_NotBlank", "LEN(LTRIM(RTRIM([Name]))) > 0");
        });
        builder.HasKey(register => register.Id);
        builder.Property(register => register.Id).ValueGeneratedOnAdd();
        builder.Property(register => register.Code).HasMaxLength(30);
        builder.Property(register => register.Name).HasMaxLength(100);
        builder.Property(register => register.CreatedAtUtc).HasPrecision(3);
        builder.Property(register => register.UpdatedAtUtc).HasPrecision(3);
        builder.Property(register => register.RowVersion).IsRowVersion();
        builder.HasIndex(register => new { register.StoreId, register.Code }).IsUnique();
        builder.HasOne(register => register.Store)
            .WithMany()
            .HasForeignKey(register => register.StoreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

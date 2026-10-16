using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Librarians have no plan, so the FK is optional.
        // A plan that still has users cannot be deleted.
        builder.HasOne(u => u.MembershipPlan)
            .WithMany(p => p.Users)
            .HasForeignKey(u => u.MembershipPlanId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

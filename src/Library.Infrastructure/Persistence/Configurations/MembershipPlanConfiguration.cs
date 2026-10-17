using Library.Domain.Entities;
using Library.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder.HasKey(p => p.Id);

        // Seed rows use explicit Ids 1-3; the identity starts at 100 to avoid duplicate keys.
        builder.Property(p => p.Id).HasIdentityOptions(startValue: 100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.MaxConcurrentLoans)
            .IsRequired();

        builder.Property(p => p.Price)
            .HasPrecision(10, 2);

        builder.HasData(SeedData.MembershipPlans);
    }
}

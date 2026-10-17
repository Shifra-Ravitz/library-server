using Library.Domain.Entities;
using Library.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);

        // Seed rows use explicit Ids 1-5; the identity starts at 100 to avoid duplicate keys.
        builder.Property(c => c.Id).HasIdentityOptions(startValue: 100);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.Name)
            .IsUnique();

        builder.HasData(SeedData.Categories);
    }
}

using Library.Domain.Entities;
using Library.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class BookCopyConfiguration : IEntityTypeConfiguration<BookCopy>
{
    public void Configure(EntityTypeBuilder<BookCopy> builder)
    {
        builder.HasKey(c => c.Id);

        // Seed rows use explicit Ids 1-25; the identity starts at 100 to avoid duplicate keys.
        builder.Property(c => c.Id).HasIdentityOptions(startValue: 100);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Optimistic concurrency: Version is regenerated in AppDbContext.UpdateVersions().
        builder.Property(c => c.Version)
            .IsConcurrencyToken();

        builder.HasOne(c => c.Book)
            .WithMany(b => b.Copies)
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(SeedData.BookCopies);
    }
}

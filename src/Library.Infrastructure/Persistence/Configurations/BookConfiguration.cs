using Library.Domain.Entities;
using Library.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.HasKey(b => b.Id);

        // Seed rows use explicit Ids 1-10; the identity starts at 100 to avoid duplicate keys.
        builder.Property(b => b.Id).HasIdentityOptions(startValue: 100);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Author)
            .IsRequired()
            .HasMaxLength(150);

        // Soft delete. No global query filter: repositories filter explicitly,
        // because loan history must still show deleted books.
        builder.Property(b => b.IsDeleted)
            .IsRequired();

        // Many-to-many with Category through a shared-type join entity.
        // Column names are fixed explicitly because the join table is seeded with HasData.
        builder.HasMany(b => b.Categories)
            .WithMany(c => c.Books)
            .UsingEntity<Dictionary<string, object>>(
                "BookCategory",
                right => right
                    .HasOne<Category>()
                    .WithMany()
                    .HasForeignKey("CategoryId")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne<Book>()
                    .WithMany()
                    .HasForeignKey("BookId")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.ToTable("BookCategories");
                    join.HasKey("BookId", "CategoryId");
                    join.HasData(SeedData.BookCategories);
                });

        builder.HasData(SeedData.Books);
    }
}

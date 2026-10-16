using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.Author)
            .IsRequired()
            .HasMaxLength(150);

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
                });
    }
}

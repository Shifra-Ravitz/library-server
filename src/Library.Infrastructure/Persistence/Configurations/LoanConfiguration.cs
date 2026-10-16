using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.LoanedAtUtc)
            .IsRequired();

        builder.Property(l => l.DueAtUtc)
            .IsRequired();

        // Null while the loan is still active.
        builder.Property(l => l.ReturnedAtUtc)
            .IsRequired(false);

        // Loan history must never be deleted automatically.
        builder.HasOne(l => l.User)
            .WithMany(u => u.Loans)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.BookCopy)
            .WithMany(c => c.Loans)
            .HasForeignKey(l => l.BookCopyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

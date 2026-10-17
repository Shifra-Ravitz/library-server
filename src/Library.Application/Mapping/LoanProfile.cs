using AutoMapper;
using Library.Application.DTOs.Loans;
using Library.Domain.Entities;

namespace Library.Application.Mapping;

public sealed class LoanProfile : Profile
{
    public LoanProfile()
    {
        // UserName is flattened from User.Name. MapFrom is null-safe, so a missing navigation gives null/0.
        CreateMap<Loan, LoanResponse>()
            .ForMember(d => d.BookId, o => o.MapFrom(s => s.BookCopy.BookId))
            .ForMember(d => d.BookTitle, o => o.MapFrom(s => s.BookCopy.Book.Title));
    }
}

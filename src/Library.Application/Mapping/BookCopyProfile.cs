using AutoMapper;
using Library.Application.DTOs.Copies;
using Library.Domain.Entities;

namespace Library.Application.Mapping;

public sealed class BookCopyProfile : Profile
{
    public BookCopyProfile()
    {
        // BookTitle is flattened from Book.Title.
        CreateMap<BookCopy, BookCopyResponse>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
    }
}

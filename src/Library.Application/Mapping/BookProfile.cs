using AutoMapper;
using Library.Application.DTOs.Books;
using Library.Domain.Entities;
using Library.Domain.Enums;

namespace Library.Application.Mapping;

public sealed class BookProfile : Profile
{
    public BookProfile()
    {
        // The repository loads only non-Retired copies, so Available is counted here explicitly.
        CreateMap<Book, BookResponse>()
            .ForMember(d => d.AvailableCopiesCount,
                o => o.MapFrom(s => s.Copies.Count(c => c.Status == CopyStatus.Available)));

        // Categories are attached by the service from CategoryIds; Id and IsDeleted are never set by the client.
        CreateMap<CreateBookRequest, Book>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.Categories, o => o.Ignore())
            .ForMember(d => d.Copies, o => o.Ignore());

        CreateMap<UpdateBookRequest, Book>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.IsDeleted, o => o.Ignore())
            .ForMember(d => d.Categories, o => o.Ignore())
            .ForMember(d => d.Copies, o => o.Ignore());
    }
}

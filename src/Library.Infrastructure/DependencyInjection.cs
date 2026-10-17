using Library.Application.Interfaces;
using Library.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the repositories and the unit of work. All are scoped, so within one HTTP request
    /// they share the same scoped <see cref="Persistence.AppDbContext"/>.
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IBookRepository, BookRepository>();
        services.AddScoped<IBookCopyRepository, BookCopyRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IMembershipPlanRepository, MembershipPlanRepository>();

        return services;
    }
}

using Library.Application.Interfaces;
using Library.Application.Services;
using Library.Infrastructure.Persistence.Repositories;
using Library.Infrastructure.Security;
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

    /// <summary>
    /// Registers the application services. Scoped, to match the repositories they depend on.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBookService, BookService>();
        services.AddScoped<IBookCopyService, BookCopyService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IMembershipPlanService, MembershipPlanService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ILoanService, LoanService>();

        return services;
    }

    /// <summary>
    /// Registers password hashing and token creation.
    /// </summary>
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        // Singletons: both are stateless, and their only dependency is IOptions<JwtOptions>, which is itself
        // a singleton. So no singleton here captures a scoped service (such as AppDbContext).
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}

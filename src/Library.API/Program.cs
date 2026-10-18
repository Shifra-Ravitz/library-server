using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using AutoMapper;
using Library.Application.Mapping;
using Library.Application.Options;
using Library.Infrastructure;
using Library.Infrastructure.Options;
using Library.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOptions<LoanOptions>()
    .Bind(builder.Configuration.GetSection(LoanOptions.SectionName))
    .Validate(o => o.LoanPeriodDays > 0, "LoanSettings:LoanPeriodDays must be greater than 0.")
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => o.Key.Length >= 32, "Jwt:Key is missing or shorter than 32 characters. Set it with dotnet user-secrets.")
    .Validate(o => o.ExpiryMinutes > 0, "Jwt:ExpiryMinutes must be greater than 0.")
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing. Set it with dotnet user-secrets.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();
builder.Services.AddScoped<DbInitializer>();

builder.Services.AddAutoMapper(
    cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"],
    typeof(BookProfile).Assembly);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // Enums as names ("Available"), and Hebrew messages written as-is instead of \uXXXX escapes.
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Hebrew);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Library API", Version = "v1" });
});

var app = builder.Build();

// Fail fast in development if a destination member has no source and was not ignored.
if (app.Environment.IsDevelopment())
{
    app.Services.GetRequiredService<IMapper>().ConfigurationProvider.AssertConfigurationIsValid();
}

// Apply migrations and seed demo users before the app starts handling requests.
await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using AutoMapper;
using Library.API.Middleware;
using Library.Application.Mapping;
using Library.Application.Options;
using Library.Infrastructure;
using Library.Infrastructure.Options;
using Library.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOptions<LoanOptions>()
    .Bind(builder.Configuration.GetSection(LoanOptions.SectionName))
    .Validate(o => o.LoanPeriodDays > 0, "LoanSettings:LoanPeriodDays must be greater than 0.")
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer חסר.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience חסר.")
    // HS256 needs a key of at least 256 bits; bytes are counted, not characters.
    .Validate(o => o.Key is not null && Encoding.UTF8.GetByteCount(o.Key) >= 32,
        "Jwt:Key חסר או קצר מ-32 בתים. יש להגדיר אותו עם dotnet user-secrets.")
    .Validate(o => o.ExpiryMinutes > 0, "Jwt:ExpiryMinutes חייב להיות גדול מ-0.")
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is missing. Set it with dotnet user-secrets.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();
builder.Services.AddScoped<DbInitializer>();
builder.Services.AddSecurity();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Configured through IOptions<JwtOptions>, so the key comes from configuration (user-secrets) and not from code.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddAutoMapper(
    cfg => cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"],
    typeof(BookProfile).Assembly);

// Hebrew messages written as-is instead of \uXXXX escapes.
var jsonEncoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.Hebrew);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // Enums as names ("Available").
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.Encoder = jsonEncoder;
});

// The same encoder for JSON written outside MVC: ProblemDetails from the middleware and from UseStatusCodePages.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = jsonEncoder);

// Every ProblemDetails gets the correlation id: ToProblem, the automatic 400 of [ApiController],
// UseStatusCodePages and ExceptionHandlingMiddleware.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        var correlationId = ctx.HttpContext.GetCorrelationId();
        if (correlationId is not null)
        {
            ctx.ProblemDetails.Extensions["correlationId"] = correlationId;
        }
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Library API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "הדביקי רק את ה-token, בלי המילה Bearer.",
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
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

// First, so it catches exceptions from every middleware after it; the correlation id is read from Items.
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

// Writes a ProblemDetails body for an error status that has no body: 401/403 from JwtBearer, 404/405 from routing.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// app.UseCors(...) ← שלב 18

// Authentication first: it builds HttpContext.User from the token, which authorization then checks.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

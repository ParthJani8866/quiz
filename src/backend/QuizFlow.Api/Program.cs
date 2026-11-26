using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuizFlow.Api.Services;
using QuizFlow.Infrastructure.Identity;
using QuizFlow.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres must be configured.");
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 bytes.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddDbContext<QuizFlowDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
})
    .AddEntityFrameworkStores<QuizFlowDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(30));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
        };
    })
    .AddOpenIdConnect("Google", options =>
    {
        options.Authority = "https://accounts.google.com";
        options.ClientId = builder.Configuration["Google:ClientId"] ?? "not-configured";
        options.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? "not-configured";
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.CallbackPath = "/signin-google";
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false;
        options.MapInboundClaims = false;
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        options.Events.OnTokenValidated = context =>
        {
            if (context.Principal?.FindFirst("email_verified")?.Value != "true")
                context.Fail("Google account email must be verified.");
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IAccountEmailSender, SmtpAccountEmailSender>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/health/ready", async (QuizFlowDbContext db, CancellationToken cancellationToken) =>
{
    if (!await db.Database.CanConnectAsync(cancellationToken)) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    return Results.Ok(new { status = "ready" });
});
app.Run();

public partial class Program;

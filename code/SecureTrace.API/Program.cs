using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureTrace.API.Data;
using SecureTrace.API.Repositories;
using SecureTrace.API.Repositories.Interfaces;
using SecureTrace.API.Services;
using SecureTrace.API.Services.Interfaces;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database: PostgreSQL (EF Core) ────────────────────────────────────────────
var rawConnStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string not found.");

// Render provides connection string in postgresql:// URL format
// Npgsql requires Host=...;Port=...;Database=... format — convert here
string pgConnStr;
if (rawConnStr.StartsWith("postgresql://") || rawConnStr.StartsWith("postgres://"))
{
    var uri = new Uri(rawConnStr);
    var userInfo = uri.UserInfo.Split(':');
    pgConnStr = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}
else
{
    pgConnStr = rawConnStr;
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(pgConnStr));

// ── Database: MongoDB (Audit Ledger) ─────────────────────────────────────────
builder.Services.AddSingleton<MongoDbContext>();

// ── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICaseRepository,     CaseRepository>();
builder.Services.AddScoped<IEvidenceRepository, EvidenceRepository>();

// ── Application Services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IJwtService,        JwtService>();
builder.Services.AddScoped<IAuthService,       AuthService>();
builder.Services.AddScoped<ICryptographyService,  CryptographyService>();
builder.Services.AddScoped<IAuditService,         AuditService>();
builder.Services.AddScoped<IVerificationService,  VerificationService>();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// ── CORS (for React frontend) ─────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")   // Vite default port
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Controllers & Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SecureTrace API", Version = "v1" });

    // Add JWT bearer input to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.ApiKey,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter: Bearer {your token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto-apply EF Core migrations on startup (convenient for dev; fine for faculty project)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();

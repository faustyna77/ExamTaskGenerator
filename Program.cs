using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using ExamCreateApp.Data;
using ExamCreateApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. DATABASE - PostgreSQL + pgvector
// ============================================
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("NeonDb"),
        o => o.UseVector()
    )
);

// ============================================
// 2. SERVICES - Dependency Injection
// ============================================
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<RAGService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<RAGService>();


// ============================================
// 3. JWT AUTHENTICATION - Tokeny
// ============================================
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("Brak SecretKey w appsettings.json");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ============================================
// 4. CONTROLLERS
// ============================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ============================================
// 5. SWAGGER - z supportem JWT
// ============================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Exam Create API",
        Version = "v1",
        Description = "API do generowania zadañ maturalnych z fizyki"
    });

    // Konfiguracja JWT w Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================
// 6. CORS - Dostêp z frontendu
// ============================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ============================================
// 7. AUTO-MIGRACJA BAZY przy starcie
// ============================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        Console.WriteLine("?? Sprawdzam migracje bazy...");
        await db.Database.MigrateAsync();
        Console.WriteLine("? Baza danych gotowa!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"? B³¹d migracji bazy: {ex.Message}");
    }
}

// ============================================
// 8. MIDDLEWARE PIPELINE
// ============================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");          // CORS - przed UseRouting
app.UseHttpsRedirection();
app.UseAuthentication();          // Sprawdza JWT token
app.UseAuthorization();           // Sprawdza uprawnienia (role)
app.MapControllers();

Console.WriteLine("?? Aplikacja uruchomiona!");
app.Run();
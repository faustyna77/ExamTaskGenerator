using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using ExamCreateApp.Data;
using ExamCreateApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// DEBUG - JWT CONFIGURATION CHECK
// ============================================
Console.WriteLine("\n====================================");
Console.WriteLine("?? CHECKING JWT CONFIGURATION:");

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];
var issuer = jwtSettings["Issuer"];
var audience = jwtSettings["Audience"];

Console.WriteLine($"?? SecretKey exists: {!string.IsNullOrEmpty(secretKey)}");
Console.WriteLine($"?? SecretKey length: {secretKey?.Length ?? 0} chars");
Console.WriteLine($"?? Issuer: '{issuer}'");
Console.WriteLine($"?? Audience: '{audience}'");
Console.WriteLine("====================================\n");

if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("? Brak SecretKey w appsettings.json");
}

if (secretKey.Length < 32)
{
    throw new InvalidOperationException($"? SecretKey jest za krótki ({secretKey.Length} chars). Wymagane minimum 32!");
}

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
// ? DODAJ:
builder.Services.AddScoped<PdfImportService>();
builder.Services.AddScoped<PdfExportService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<StripeService>();

builder.Services.AddScoped<MockExamService>();
// ============================================
// 3. JWT AUTHENTICATION - Tokeny
// ============================================
Console.WriteLine("?? Configuring JWT Authentication...");

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
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // ? DEBUGGING EVENTS - POKA¯E CO SIÊ DZIEJE!
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("\n? JWT AUTHENTICATION FAILED!");
            Console.WriteLine($"   Exception Type: {context.Exception.GetType().Name}");
            Console.WriteLine($"   Message: {context.Exception.Message}");

            if (context.Exception.InnerException != null)
            {
                Console.WriteLine($"   Inner Exception: {context.Exception.InnerException.Message}");
            }

            return Task.CompletedTask;
        },

        OnTokenValidated = context =>
        {
            var userId = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var email = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            Console.WriteLine($"\n? JWT TOKEN VALIDATED!");
            Console.WriteLine($"   User ID: {userId}");
            Console.WriteLine($"   Email: {email}");

            return Task.CompletedTask;
        },

        OnChallenge = context =>
        {
            Console.WriteLine("\n?? JWT CHALLENGE (Unauthorized)!");
            Console.WriteLine($"   Error: {context.Error}");
            Console.WriteLine($"   Error Description: {context.ErrorDescription}");

            return Task.CompletedTask;
        },

        OnMessageReceived = context =>
        {
            var token = context.Token;
            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine($"\n?? JWT Token received (length: {token.Length} chars)");
            }
            return Task.CompletedTask;
        }
    };
});

Console.WriteLine("? JWT Authentication configured\n");

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

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();  // WA¯NE: Przed UseAuthorization!
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("?? Aplikacja uruchomiona!");
Console.WriteLine("?? Swagger: https://localhost:7013/swagger\n");

app.Run();
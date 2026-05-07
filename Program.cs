using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 🔥 Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ CORS (ONLY ONCE)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition"); // 🔥 IMPORTANT
    });
});

// 🔐 JWT (FIXED)
var key = builder.Configuration["Jwt:Key"]
          ?? throw new Exception("JWT Key missing");

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1073741824; // 1 GB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 1073741824; // 1 GB
});

builder.Services.AddAuthentication("Bearer")
.AddJwtBearer("Bearer", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(key))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// 🔥 Middleware order (IMPORTANT)
app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();
app.UseHttpsRedirection();

app.UseAuthentication(); // MUST be here
app.UseAuthorization();

app.MapControllers();

app.Run();
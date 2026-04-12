using System.Text;
using FluentValidation;
using JiraFit.API.BackgroundServices;
using JiraFit.API.Middlewares;
using JiraFit.Application.Interfaces;
using JiraFit.Infrastructure.Data;
using JiraFit.Infrastructure.Repositories;
using JiraFit.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Read PORT from environment (for Railway.app)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure - EF Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL"); // Fallback for Railway

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

// Authentication (JWT)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "superSecretKey_must_be_changed_in_prod!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "JiraFit",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "JiraFitUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// Dependency Injection
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMealRepository, MealRepository>();
builder.Services.AddScoped<IAIService, GeminiAIService>();
builder.Services.AddScoped<IMessagingService, TwilioMessagingService>();

// Validators
builder.Services.AddValidatorsFromAssembly(typeof(JiraFit.Application.DTOs.UserRegistrationDto).Assembly);

// Background Services & Channels
// WebhookChannel acts as a Singleton because it's the bridge
var channel = new WebhookChannel();
builder.Services.AddSingleton<IWebhookProcessorService>(channel);
builder.Services.AddSingleton(channel); // Also expose concrete for HostedService

builder.Services.AddHostedService<WebhookBackgroundService>();

var app = builder.Build();

// Automatically apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

// Apply Twilio Signature Middleware specific to the webhook
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api/webhook"), appBuilder =>
{
    appBuilder.UseMiddleware<TwilioSignatureMiddleware>();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

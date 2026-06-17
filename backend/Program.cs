using Microsoft.EntityFrameworkCore;
using Aquarius.Api.Data;
using Aquarius.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// Listen on all interfaces for mobile testing (port 4200 already firewall-allowed)
builder.WebHost.UseUrls("http://0.0.0.0:4200");

// ── Database ──────────────────────────────────────────────
builder.Services.AddDbContext<AquariusDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("Default") 
        ?? "Data Source=Aquarius.db"));

// ── Controllers ───────────────────────────────────────────
builder.Services.AddControllers();

// ── CORS (dev: allow Angular dev server from any local addr) ──
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(p =>
        p.SetIsOriginAllowed(_ => true)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// ── Swagger / OpenAPI ─────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Auto-migrate & seed ───────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AquariusDbContext>();
    db.Database.EnsureCreated();
}

// ── Middleware pipeline ───────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve Angular static files from wwwroot (production mode)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// SPA fallback — return index.html for any non-API route
app.MapFallbackToFile("index.html");

app.Run();

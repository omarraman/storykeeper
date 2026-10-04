using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Data;
using Storykeeper.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Storykeeper")
    ?? throw new InvalidOperationException("The Storykeeper database connection string is not configured.");

builder.Services.AddDbContext<StorykeeperDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<ICampaignEntityRepository, CampaignEntityRepository>();
builder.Services.AddScoped<ICampaignService, CampaignService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StorykeeperDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Data;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Storykeeper")
    ?? throw new InvalidOperationException("The Storykeeper database connection string is not configured.");

builder.Services.AddDbContext<StorykeeperDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<ICampaignEntityRepository, CampaignEntityRepository>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StorykeeperDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/api/campaigns", async (ICampaignService campaigns, CancellationToken cancellationToken) =>
    Results.Ok((await campaigns.ListAsync(cancellationToken)).Select(CampaignResponse.From)));

app.MapGet("/api/campaign-briefs", async (ICampaignBriefService briefs, CancellationToken cancellationToken) =>
    Results.Ok((await briefs.ListAsync(cancellationToken)).Select(CampaignBriefResponse.From)));

app.MapPost("/api/campaign-briefs", async (
    CampaignBriefRequest? request,
    ICampaignBriefService briefs,
    CancellationToken cancellationToken) =>
{
    var errors = CampaignBriefRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var brief = await briefs.CreateAsync(request!.ToDomain(), cancellationToken);
    return Results.Created($"/api/campaign-briefs/{brief.Id}", CampaignBriefResponse.From(brief));
});

app.MapGet("/api/campaign-briefs/{briefId:guid}", async (
    Guid briefId,
    ICampaignBriefService briefs,
    CancellationToken cancellationToken) =>
{
    var brief = await briefs.GetAsync(briefId, cancellationToken);
    return brief is null ? Results.NotFound() : Results.Ok(CampaignBriefResponse.From(brief));
});

app.MapPut("/api/campaign-briefs/{briefId:guid}", async (
    Guid briefId,
    CampaignBriefRequest? request,
    ICampaignBriefService briefs,
    CancellationToken cancellationToken) =>
{
    var errors = CampaignBriefRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var brief = await briefs.UpdateAsync(briefId, request!.ToDomain(), cancellationToken);
    return brief is null ? Results.NotFound() : Results.Ok(CampaignBriefResponse.From(brief));
});

app.MapDelete("/api/campaign-briefs/{briefId:guid}", async (
    Guid briefId,
    ICampaignBriefService briefs,
    CancellationToken cancellationToken) =>
    await briefs.DeleteAsync(briefId, cancellationToken) ? Results.NoContent() : Results.NotFound());

app.MapPost("/api/campaigns", async (
    CreateCampaignRequest? request,
    ICampaignService campaigns,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateCampaignRequest(request?.Name, request?.Description);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var campaign = await campaigns.CreateAsync(request!.Name!, request.Description, cancellationToken);
    return Results.Created($"/api/campaigns/{campaign.Id}", CampaignResponse.From(campaign));
});

app.MapGet("/api/campaigns/{campaignId:guid}", async (
    Guid campaignId,
    ICampaignService campaigns,
    CancellationToken cancellationToken) =>
{
    var campaign = await campaigns.GetAsync(campaignId, cancellationToken);
    return campaign is null
        ? Results.NotFound()
        : Results.Ok(CampaignResponse.From(campaign));
}).WithName("GetCampaign");

app.MapPost("/api/campaigns/{campaignId:guid}/complete", async (
    Guid campaignId,
    ICampaignService campaigns,
    CancellationToken cancellationToken) =>
    await campaigns.CompleteAsync(campaignId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound());

app.MapPost("/api/campaigns/{campaignId:guid}/archive", async (
    Guid campaignId,
    ICampaignService campaigns,
    CancellationToken cancellationToken) =>
    await campaigns.ArchiveAsync(campaignId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound());

app.MapDelete("/api/campaigns/{campaignId:guid}", async (
    Guid campaignId,
    ICampaignService campaigns,
    CancellationToken cancellationToken) =>
    await campaigns.DeleteAsync(campaignId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound());

app.Run();

static Dictionary<string, string[]> ValidateCampaignRequest(string? name, string? description)
{
    var errors = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(name))
    {
        errors["name"] = ["A campaign name is required."];
    }
    else if (name.Trim().Length > 120)
    {
        errors["name"] = ["Campaign names cannot exceed 120 characters."];
    }

    if (description?.Trim().Length > 2000)
    {
        errors["description"] = ["Premises cannot exceed 2000 characters."];
    }

    return errors;
}

public sealed record CreateCampaignRequest(string? Name, string? Description);

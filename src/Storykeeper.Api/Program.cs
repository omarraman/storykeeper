using Microsoft.EntityFrameworkCore;
using Storykeeper.Api.Data;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Storykeeper")
    ?? throw new InvalidOperationException("The Storykeeper database connection string is not configured.");

builder.Services.AddDbContext<StorykeeperDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<ICampaignEntityRepository, CampaignEntityRepository>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IGameRulesService, GameRulesService>();
builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();
builder.Services.Configure<StorykeeperAiOptions>(builder.Configuration.GetSection("Storykeeper:Ai"));
builder.Services.AddHttpClient<ICampaignDraftGenerator, OpenAiCompatibleCampaignDraftGenerator>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<StorykeeperAiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds is >= 10 and <= 180 ? options.TimeoutSeconds : 60);
});
builder.Services.AddScoped<ICampaignDraftService, CampaignDraftService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StorykeeperDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/api/campaigns", async (ICampaignService campaigns, CancellationToken cancellationToken) =>
    Results.Ok((await campaigns.ListAsync(cancellationToken)).Select(CampaignResponse.From)));

app.MapPost("/api/campaigns/{campaignId:guid}/sessions", async (
    Guid campaignId,
    IGameRulesService rules,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await rules.StartSessionAsync(campaignId, cancellationToken);
        return result.Session is null
            ? Results.NotFound()
            : Results.Ok(SessionStartResponse.From(result.Session, result.Heroes));
    }
    catch (RuleConflictException exception)
    {
        return Results.Problem(statusCode: 409, title: "Session cannot be started", detail: exception.Message);
    }
});

app.MapPost("/api/campaigns/{campaignId:guid}/sessions/{sessionId:guid}/heroes/{heroId:guid}/checks", async (
    Guid campaignId,
    Guid sessionId,
    Guid heroId,
    CheckRequest? request,
    IGameRulesService rules,
    CancellationToken cancellationToken) =>
{
    var errors = CheckRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var resolution = await rules.ResolveCheckAsync(
            campaignId,
            sessionId,
            heroId,
            request!.Roll,
            request.Difficulty,
            request.Strength,
            request.SpendSparkleToken,
            request.Risky,
            cancellationToken);
        return resolution is null
            ? Results.NotFound()
            : Results.Ok(CheckResolutionResponse.From(resolution));
    }
    catch (RuleValidationException exception)
    {
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { ["check"] = [exception.Message] });
    }
    catch (RuleConflictException exception)
    {
        return Results.Problem(statusCode: 409, title: "Check cannot be resolved", detail: exception.Message);
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/campaigns/{campaignId:guid}/sessions/{sessionId:guid}/heroes/{heroId:guid}/checks/test", async (
        Guid campaignId,
        Guid sessionId,
        Guid heroId,
        TestCheckRequest? request,
        IGameRulesService rules,
        CancellationToken cancellationToken) =>
    {
        var errors = CheckRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            var resolution = await rules.ResolveTestCheckAsync(
                campaignId,
                sessionId,
                heroId,
                request!.Difficulty,
                request.Strength,
                request.SpendSparkleToken,
                request.Risky,
                cancellationToken);
            return resolution is null
                ? Results.NotFound()
                : Results.Ok(CheckResolutionResponse.From(resolution));
        }
        catch (RuleValidationException exception)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["check"] = [exception.Message] });
        }
        catch (RuleConflictException exception)
        {
            return Results.Problem(statusCode: 409, title: "Check cannot be resolved", detail: exception.Message);
        }
    });
}

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

app.MapGet("/api/campaign-drafts", async (
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
    Results.Ok((await drafts.ListAsync(cancellationToken)).Select(CampaignDraftResponse.From)));

app.MapPost("/api/campaign-briefs/{briefId:guid}/drafts", async (
    Guid briefId,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    try
    {
        var draft = await drafts.GenerateAsync(briefId, cancellationToken);
        return draft is null
            ? Results.NotFound()
            : Results.Created($"/api/campaign-drafts/{draft.Id}", CampaignDraftResponse.From(draft));
    }
    catch (CampaignDraftGenerationException exception)
    {
        return Results.Problem(statusCode: exception.StatusCode, title: "Campaign generation failed", detail: exception.Message);
    }
    catch (CampaignDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Campaign draft rejected", detail: exception.Message);
    }
});

app.MapGet("/api/campaign-drafts/{draftId:guid}", async (
    Guid draftId,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    var draft = await drafts.GetAsync(draftId, cancellationToken);
    return draft is null ? Results.NotFound() : Results.Ok(CampaignDraftResponse.From(draft));
});

app.MapPost("/api/campaign-drafts/{draftId:guid}/regenerate", async (
    Guid draftId,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await drafts.RegenerateAsync(draftId, cancellationToken);
        return result.Status switch
        {
            CampaignDraftOperationStatus.NotFound => Results.NotFound(),
            CampaignDraftOperationStatus.AlreadyActivated => Results.Problem(
                statusCode: 409, title: "Draft already activated", detail: "Activated drafts cannot be regenerated."),
            _ => Results.Ok(CampaignDraftResponse.From(result.Draft!))
        };
    }
    catch (CampaignDraftGenerationException exception)
    {
        return Results.Problem(statusCode: exception.StatusCode, title: "Campaign generation failed", detail: exception.Message);
    }
    catch (CampaignDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Campaign draft rejected", detail: exception.Message);
    }
});

app.MapPut("/api/campaign-drafts/{draftId:guid}", async (
    Guid draftId,
    CampaignDraftContent? content,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    var errors = CampaignDraftValidator.Validate(content);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors, statusCode: 422, title: "Campaign draft rejected");
    }

    var result = await drafts.UpdateAsync(draftId, content!, cancellationToken);
    return result.Status switch
    {
        CampaignDraftOperationStatus.NotFound => Results.NotFound(),
        CampaignDraftOperationStatus.AlreadyActivated => Results.Problem(
            statusCode: 409, title: "Draft already activated", detail: "Activated drafts cannot be edited."),
        _ => Results.Ok(CampaignDraftResponse.From(result.Draft!))
    };
});

app.MapPost("/api/campaign-drafts/{draftId:guid}/approve", async (
    Guid draftId,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await drafts.ApproveAsync(draftId, cancellationToken);
        return result.Status switch
        {
            CampaignDraftOperationStatus.NotFound => Results.NotFound(),
            CampaignDraftOperationStatus.AlreadyActivated => Results.Problem(
                statusCode: 409, title: "Draft already activated", detail: "Activated drafts cannot be re-approved."),
            _ => Results.Ok(CampaignDraftResponse.From(result.Draft!))
        };
    }
    catch (CampaignDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Campaign draft rejected", detail: exception.Message);
    }
});

app.MapPost("/api/campaign-drafts/{draftId:guid}/activate", async (
    Guid draftId,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    var result = await drafts.ActivateAsync(draftId, cancellationToken);
    return result.Status switch
    {
        CampaignDraftActivationStatus.NotFound => Results.NotFound(),
        CampaignDraftActivationStatus.ApprovalRequired => Results.Problem(
            statusCode: 409, title: "Approval required", detail: "A parent must approve the draft before activation."),
        CampaignDraftActivationStatus.AlreadyActivated => Results.Problem(
            statusCode: 409, title: "Draft already activated", detail: "This draft has already created a story world."),
        _ => Results.Created($"/api/campaigns/{result.Campaign!.Id}", CampaignResponse.From(result.Campaign))
    };
});

app.MapDelete("/api/campaign-drafts/{draftId:guid}", async (
    Guid draftId,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    var result = await drafts.DeleteAsync(draftId, cancellationToken);
    return result.Status switch
    {
        CampaignDraftOperationStatus.NotFound => Results.NotFound(),
        CampaignDraftOperationStatus.AlreadyActivated => Results.Problem(
            statusCode: 409, title: "Draft already activated", detail: "Activated drafts cannot be discarded."),
        _ => Results.NoContent()
    };
});

app.MapGet("/api/campaigns/{campaignId:guid}/bible-versions", async (
    Guid campaignId,
    ICampaignDraftService drafts,
    CancellationToken cancellationToken) =>
{
    var versions = await drafts.ListBibleVersionsAsync(campaignId, cancellationToken);
    return versions is null
        ? Results.NotFound()
        : Results.Ok(versions.Select(CampaignBibleVersionResponse.From));
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

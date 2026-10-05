using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Storykeeper.Api.Data;
using Storykeeper.Api.Contracts;
using Storykeeper.Api.Domain;
using Storykeeper.Api.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("parent-pin", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var connectionString = builder.Configuration.GetConnectionString("Storykeeper")
    ?? throw new InvalidOperationException("The Storykeeper database connection string is not configured.");

builder.Services.AddDbContext<StorykeeperDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<ICampaignEntityRepository, CampaignEntityRepository>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<CampaignArchiveService>();
builder.Services.AddScoped<IGameRulesService, GameRulesService>();
builder.Services.AddScoped<ICampaignBriefService, CampaignBriefService>();
builder.Services.AddScoped<ICampaignContinuityService, CampaignContinuityService>();
builder.Services.Configure<StorykeeperAiOptions>(builder.Configuration.GetSection("Storykeeper:Ai"));
builder.Services.AddHttpClient<IAdventureDraftGenerator, OpenAiCompatibleAdventureDraftGenerator>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<StorykeeperAiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds is >= 10 and <= 180 ? options.TimeoutSeconds : 60);
});
builder.Services.AddScoped<IAdventureDraftService, AdventureDraftService>();
builder.Services.AddHttpClient<ICampaignDraftGenerator, OpenAiCompatibleCampaignDraftGenerator>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<StorykeeperAiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds is >= 10 and <= 180 ? options.TimeoutSeconds : 60);
});
builder.Services.AddScoped<ICampaignDraftService, CampaignDraftService>();
builder.Services.AddHttpClient<IStoryTurnGenerator, OpenAiCompatibleStoryTurnGenerator>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<StorykeeperAiOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds is >= 10 and <= 180 ? options.TimeoutSeconds : 60);
});
builder.Services.AddScoped<IStoryTurnService, StoryTurnService>();

var app = builder.Build();
app.UseRateLimiter();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StorykeeperDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));

var archiveJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
};
archiveJsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));

app.MapPost("/api/campaigns/import", async (
    JsonElement request,
    CampaignArchiveService archives,
    CancellationToken cancellationToken) =>
{
    CampaignArchiveDocument? archive;
    try
    {
        archive = request.Deserialize<CampaignArchiveDocument>(archiveJsonOptions);
    }
    catch (JsonException exception)
    {
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { ["archive"] = [$"The archive JSON is invalid: {exception.Message}"] },
            title: "Campaign archive could not be read");
    }

    var result = await archives.ImportAsync(archive, cancellationToken);
    if (result.Errors.Count > 0)
    {
        return Results.ValidationProblem(
            result.Errors,
            statusCode: result.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
            title: result.Conflict ? "Campaign archive conflicts with saved data" : "Campaign archive is invalid");
    }

    return Results.Created($"/api/campaigns/{result.CampaignId}", new { campaignId = result.CampaignId });
});

app.MapGet("/api/campaigns/{campaignId:guid}/export", async (
    Guid campaignId,
    CampaignArchiveService archives,
    CancellationToken cancellationToken) =>
{
    var archive = await archives.ExportAsync(campaignId, cancellationToken);
    if (archive is null)
    {
        return Results.NotFound();
    }

    var fileName = string.Concat(
        archive.Campaign.Name.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-'))
        .Trim('-');
    return Results.File(
        JsonSerializer.SerializeToUtf8Bytes(archive, archiveJsonOptions),
        "application/json",
        $"{(string.IsNullOrEmpty(fileName) ? "campaign" : fileName)}-storykeeper.json");
});

app.MapGet("/api/campaigns/{campaignId:guid}/storybook", async (
    Guid campaignId,
    CampaignArchiveService archives,
    CancellationToken cancellationToken) =>
{
    var storybook = await archives.GetStorybookAsync(campaignId, cancellationToken);
    return storybook is null ? Results.NotFound() : Results.Ok(storybook);
});

app.MapPost("/api/parent-controls/verify", (HttpRequest httpRequest, IConfiguration configuration) =>
{
    var pinError = ValidateParentPin(httpRequest, configuration);
    return pinError ?? Results.NoContent();
}).RequireRateLimiting("parent-pin");

app.MapGet("/api/campaigns", async (ICampaignService campaigns, CancellationToken cancellationToken) =>
    Results.Ok((await campaigns.ListAsync(cancellationToken)).Select(CampaignResponse.From)));

app.MapPut("/api/campaigns/{campaignId:guid}/parent-controls", async (
    Guid campaignId,
    ParentControlsRequest? request,
    HttpRequest httpRequest,
    IConfiguration configuration,
    StorykeeperDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var pinError = ValidateParentPin(httpRequest, configuration);
    if (pinError is not null) return pinError;

    var errors = ParentControlsRequestValidator.Validate(request?.SafetySettings);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var campaign = await dbContext.Campaigns
        .Include(item => item.Settings)
        .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
    if (campaign is null)
    {
        return Results.NotFound();
    }

    if (campaign.Status == CampaignStatus.Archived || campaign.Settings is null)
    {
        return Results.Problem(statusCode: 409, title: "Parent controls cannot be changed",
            detail: "Controls can only be changed for a campaign that is not archived.");
    }

    campaign.Settings.SafetySettings = request!.SafetySettings! with
    {
        ExcludedContent = request.SafetySettings.ExcludedContent
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
    };
    campaign.UpdatedAtUtc = DateTimeOffset.UtcNow;
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(campaign.Settings.SafetySettings);
}).RequireRateLimiting("parent-pin");

app.MapPost("/api/campaigns/{campaignId:guid}/sessions/{sessionId:guid}/parent-actions", async (
    Guid campaignId,
    Guid sessionId,
    ParentActionRequest? request,
    HttpRequest httpRequest,
    IConfiguration configuration,
    StorykeeperDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var pinError = ValidateParentPin(httpRequest, configuration);
    if (pinError is not null) return pinError;

    var errors = ParentControlsRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var session = await dbContext.Sessions.SingleOrDefaultAsync(
        item => item.CampaignId == campaignId && item.Id == sessionId, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    if (session.EndedAtUtc is not null)
    {
        return Results.Problem(statusCode: 409, title: "Session has ended");
    }

    var campaignStatus = await dbContext.Campaigns
        .Where(item => item.Id == campaignId)
        .Select(item => item.Status)
        .SingleOrDefaultAsync(cancellationToken);
    var latestSessionId = await dbContext.Sessions
        .Where(item => item.CampaignId == campaignId)
        .OrderByDescending(item => item.SessionNumber)
        .Select(item => (Guid?)item.Id)
        .FirstOrDefaultAsync(cancellationToken);
    if (campaignStatus != CampaignStatus.Active || latestSessionId != session.Id)
    {
        return Results.Problem(statusCode: 409, title: "Parent controls require the current active session");
    }

    session.ParentInstruction = request!.Action switch
    {
        "makeEasier" => "Make the next challenge easier and ensure the heroes make useful progress.",
        "addClue" => "Offer a clear, useful clue now without requiring a roll.",
        "skipScene" => "Skip the current scene and move directly to a fresh, player-led moment.",
        "moveTowardEnding" => "Gently move the story toward a satisfying stopping point.",
        "endSession" => "Bring the story to a gentle stopping point now, with no new cliffhanger.",
        "pause" => null,
        "resume" => null,
        _ => throw new InvalidOperationException("Validated parent action was not recognized.")
    };
    if (request.Action is "pause" or "endSession")
    {
        session.IsPaused = true;
    }
    else if (request.Action == "resume")
    {
        session.IsPaused = false;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(new
    {
        session.IsPaused,
        endSessionRequested = request.Action == "endSession"
    });
}).RequireRateLimiting("parent-pin");

app.MapGet("/api/campaigns/{campaignId:guid}/adventure-drafts", async (
    Guid campaignId,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    var drafts = await adventures.ListAsync(campaignId, cancellationToken);
    return drafts is null
        ? Results.NotFound()
        : Results.Ok(drafts.Select(AdventureDraftResponse.From));
});

app.MapGet("/api/adventure-drafts/{draftId:guid}", async (
    Guid draftId,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    var draft = await adventures.GetAsync(draftId, cancellationToken);
    return draft is null ? Results.NotFound() : Results.Ok(AdventureDraftResponse.From(draft));
});

app.MapPost("/api/campaigns/{campaignId:guid}/adventure-drafts", async (
    Guid campaignId,
    AdventureDraftRequest? request,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    var errors = AdventureDraftRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var result = await adventures.GenerateAsync(campaignId, request!, cancellationToken);
        return result.Status switch
        {
            AdventureDraftOperationStatus.NotFound => Results.NotFound(),
            AdventureDraftOperationStatus.Conflict => Results.Problem(
                statusCode: 409, title: "Adventure cannot be generated",
                detail: "Adventure drafts can only be generated for an active campaign between sessions."),
            _ => Results.Created($"/api/adventure-drafts/{result.Draft!.Id}", AdventureDraftResponse.From(result.Draft))
        };
    }
    catch (AdventureDraftGenerationException exception)
    {
        return Results.Problem(statusCode: exception.StatusCode, title: "Adventure generation failed", detail: exception.Message);
    }
    catch (AdventureDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Adventure draft rejected", detail: exception.Message);
    }
});

app.MapPost("/api/adventure-drafts/{draftId:guid}/regenerate", async (
    Guid draftId,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await adventures.RegenerateAsync(draftId, cancellationToken);
        return result.Status switch
        {
            AdventureDraftOperationStatus.NotFound => Results.NotFound(),
            AdventureDraftOperationStatus.Conflict => Results.Problem(
                statusCode: 409, title: "Adventure cannot be regenerated",
                detail: "Adventure drafts can only be regenerated for an active campaign between sessions."),
            AdventureDraftOperationStatus.AlreadyActivated => Results.Problem(
                statusCode: 409, title: "Adventure already activated", detail: "Activated adventures cannot be regenerated."),
            _ => Results.Ok(AdventureDraftResponse.From(result.Draft!))
        };
    }
    catch (AdventureDraftGenerationException exception)
    {
        return Results.Problem(statusCode: exception.StatusCode, title: "Adventure generation failed", detail: exception.Message);
    }
    catch (AdventureDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Adventure draft rejected", detail: exception.Message);
    }
});

app.MapPut("/api/adventure-drafts/{draftId:guid}", async (
    Guid draftId,
    AdventureDraftContent? content,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await adventures.UpdateAsync(draftId, content!, cancellationToken);
        return result.Status switch
        {
            AdventureDraftOperationStatus.NotFound => Results.NotFound(),
            AdventureDraftOperationStatus.AlreadyActivated => Results.Problem(
                statusCode: 409, title: "Adventure already activated", detail: "Activated adventures cannot be edited."),
            _ => Results.Ok(AdventureDraftResponse.From(result.Draft!))
        };
    }
    catch (AdventureDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Adventure draft rejected");
    }
});

app.MapPost("/api/adventure-drafts/{draftId:guid}/approve", async (
    Guid draftId,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await adventures.ApproveAsync(draftId, cancellationToken);
        return result.Status switch
        {
            AdventureDraftOperationStatus.NotFound => Results.NotFound(),
            AdventureDraftOperationStatus.AlreadyActivated => Results.Problem(
                statusCode: 409, title: "Adventure already activated", detail: "Activated adventures cannot be re-approved."),
            _ => Results.Ok(AdventureDraftResponse.From(result.Draft!))
        };
    }
    catch (AdventureDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Adventure draft rejected", detail: exception.Message);
    }
});

app.MapPost("/api/adventure-drafts/{draftId:guid}/activate", async (
    Guid draftId,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await adventures.ActivateAsync(draftId, cancellationToken);
        return result.Status switch
        {
            AdventureDraftOperationStatus.NotFound => Results.NotFound(),
            AdventureDraftOperationStatus.Conflict => Results.Problem(
                statusCode: 409, title: "Adventure cannot be activated",
                detail: "Adventures can only be activated for an active campaign between sessions."),
            AdventureDraftOperationStatus.AlreadyActivated => Results.Problem(
                statusCode: 409, title: "Adventure already activated", detail: "This adventure has already been added to the campaign."),
            _ => Results.Ok(AdventureDraftResponse.From(result.Draft!))
        };
    }
    catch (RuleConflictException exception)
    {
        return Results.Problem(statusCode: 409, title: "Adventure cannot be activated", detail: exception.Message);
    }
    catch (AdventureDraftRejectedException exception)
    {
        return Results.ValidationProblem(exception.Errors, statusCode: 422, title: "Adventure draft rejected", detail: exception.Message);
    }
});

app.MapDelete("/api/adventure-drafts/{draftId:guid}", async (
    Guid draftId,
    IAdventureDraftService adventures,
    CancellationToken cancellationToken) =>
{
    var result = await adventures.DeleteAsync(draftId, cancellationToken);
    return result.Status switch
    {
        AdventureDraftOperationStatus.NotFound => Results.NotFound(),
        AdventureDraftOperationStatus.AlreadyActivated => Results.Problem(
            statusCode: 409, title: "Adventure already activated", detail: "Activated adventures cannot be discarded."),
        _ => Results.NoContent()
    };
});

app.MapPost("/api/campaigns/{campaignId:guid}/actions", async (
    Guid campaignId,
    StoryTurnRequest? request,
    IStoryTurnService storyTurns,
    CancellationToken cancellationToken) =>
{
    var errors = StoryTurnRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var result = await storyTurns.SubmitActionAsync(campaignId, request!, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
    catch (RuleValidationException exception)
    {
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { ["action"] = [exception.Message] });
    }
    catch (RuleConflictException exception)
    {
        return Results.Problem(statusCode: 409, title: "Story action cannot be submitted", detail: exception.Message);
    }
    catch (StoryTurnGenerationException exception)
    {
        return Results.Json(new
        {
            type = "error",
            message = exception.SafeMessage,
            retryable = true
        }, statusCode: exception.StatusCode);
    }
});

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

app.MapGet("/api/campaigns/{campaignId:guid}/continuity", async (
    Guid campaignId,
    ICampaignContinuityService continuity,
    CancellationToken cancellationToken) =>
{
    var result = await continuity.GetAsync(campaignId, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

app.MapGet("/api/campaigns/{campaignId:guid}/facts/{factId:guid}", async (
    Guid campaignId,
    Guid factId,
    ICampaignContinuityService continuity,
    CancellationToken cancellationToken) =>
{
    var fact = await continuity.GetFactAsync(campaignId, factId, cancellationToken);
    return fact is null ? Results.NotFound() : Results.Ok(fact);
});

app.MapPost("/api/campaigns/{campaignId:guid}/facts", async (
    Guid campaignId,
    CreateCampaignFactRequest? request,
    ICampaignContinuityService continuity,
    CancellationToken cancellationToken) =>
{
    var errors = CampaignContinuityRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var fact = await continuity.CreateFactAsync(campaignId, request!, cancellationToken);
        return fact is null ? Results.NotFound() : Results.Created(
            $"/api/campaigns/{campaignId}/facts/{fact.Id}", fact);
    }
    catch (RuleValidationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fact"] = [exception.Message] });
    }
    catch (RuleConflictException exception)
    {
        return Results.Problem(statusCode: 409, title: "Fact cannot be created", detail: exception.Message);
    }
});

app.MapPut("/api/campaigns/{campaignId:guid}/facts/{factId:guid}", async (
    Guid campaignId,
    Guid factId,
    UpdateCampaignFactRequest? request,
    ICampaignContinuityService continuity,
    CancellationToken cancellationToken) =>
{
    var errors = CampaignContinuityRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var fact = await continuity.UpdateFactAsync(campaignId, factId, request!, cancellationToken);
        return fact is null ? Results.NotFound() : Results.Ok(fact);
    }
    catch (RuleValidationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["fact"] = [exception.Message] });
    }
    catch (RuleConflictException exception)
    {
        return Results.Problem(statusCode: 409, title: "Fact cannot be updated", detail: exception.Message);
    }
});

app.MapPut("/api/campaigns/{campaignId:guid}/sessions/{sessionId:guid}/summary", async (
    Guid campaignId,
    Guid sessionId,
    SaveSessionSummaryRequest? request,
    HttpRequest httpRequest,
    IConfiguration configuration,
    StorykeeperDbContext dbContext,
    ICampaignContinuityService continuity,
    CancellationToken cancellationToken) =>
{
    var session = await dbContext.Sessions.AsNoTracking().SingleOrDefaultAsync(
        item => item.CampaignId == campaignId && item.Id == sessionId, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    if (session.EndedAtUtc is null)
    {
        var pinError = ValidateParentPin(httpRequest, configuration);
        if (pinError is not null) return pinError;
    }

    var errors = CampaignContinuityRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    try
    {
        var summary = await continuity.SaveSummaryAsync(campaignId, sessionId, request!, cancellationToken);
        return summary is null ? Results.NotFound() : Results.Ok(summary);
    }
    catch (RuleValidationException exception)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["summary"] = [exception.Message] });
    }
    catch (RuleConflictException exception)
    {
        return Results.Problem(statusCode: 409, title: "Session cannot be ended", detail: exception.Message);
    }
}).RequireRateLimiting("parent-pin");

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
    HttpRequest httpRequest,
    IConfiguration configuration,
    ICampaignBriefService briefs,
    CancellationToken cancellationToken) =>
{
    var pinError = ValidateParentPin(httpRequest, configuration);
    if (pinError is not null) return pinError;

    var errors = CampaignBriefRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var brief = await briefs.CreateAsync(request!.ToDomain(), cancellationToken);
    return Results.Created($"/api/campaign-briefs/{brief.Id}", CampaignBriefResponse.From(brief));
}).RequireRateLimiting("parent-pin");

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
    HttpRequest httpRequest,
    IConfiguration configuration,
    ICampaignBriefService briefs,
    CancellationToken cancellationToken) =>
{
    var pinError = ValidateParentPin(httpRequest, configuration);
    if (pinError is not null) return pinError;

    var errors = CampaignBriefRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var brief = await briefs.UpdateAsync(briefId, request!.ToDomain(), cancellationToken);
    return brief is null ? Results.NotFound() : Results.Ok(CampaignBriefResponse.From(brief));
}).RequireRateLimiting("parent-pin");

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

app.MapPost("/api/campaigns/{campaignId:guid}/heroes", async (
    Guid campaignId,
    CreateHeroRequest? request,
    HttpRequest httpRequest,
    IConfiguration configuration,
    StorykeeperDbContext dbContext,
    ICampaignEntityRepository entities,
    CancellationToken cancellationToken) =>
{
    var pinError = ValidateParentPin(httpRequest, configuration);
    if (pinError is not null) return pinError;

    var errors = CreateHeroRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var campaign = await dbContext.Campaigns
        .Include(item => item.Party)
        .SingleOrDefaultAsync(item => item.Id == campaignId, cancellationToken);
    if (campaign is null)
    {
        return Results.NotFound();
    }

    if (campaign.Status != CampaignStatus.Active || campaign.Party is null)
    {
        return Results.Problem(
            statusCode: 409,
            title: "Hero cannot be added",
            detail: "Heroes can only be added to an active campaign.");
    }

    var hasActiveSession = await dbContext.Sessions.AsNoTracking()
        .AnyAsync(item => item.CampaignId == campaignId && item.EndedAtUtc == null, cancellationToken);
    if (hasActiveSession)
    {
        return Results.Problem(
            statusCode: 409,
            title: "Adventure is in progress",
            detail: "End the current adventure before changing the party.");
    }

    var hero = new Hero
    {
        CampaignId = campaignId,
        PartyId = campaign.Party.Id,
        Name = request!.Name!.Trim(),
        Description = request.Description!.Trim(),
        Role = request.Role!.Trim(),
        Strengths = (request.Strengths ?? [])
            .Select(strength => strength!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
    };
    campaign.UpdatedAtUtc = DateTimeOffset.UtcNow;
    await entities.AddAsync(campaignId, hero, cancellationToken);

    return Results.Ok(HeroResponse.From(hero));
}).RequireRateLimiting("parent-pin");

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

static IResult? ValidateParentPin(HttpRequest request, IConfiguration configuration)
{
    if (!ParentPinAuthorization.IsConfigured(configuration))
    {
        return Results.Problem(statusCode: 503, title: "Parent controls are unavailable",
            detail: "Configure the server-side Storykeeper:ParentPin setting first.");
    }

    return ParentPinAuthorization.IsAuthorized(request, configuration)
        ? null
        : Results.Unauthorized();
}

public sealed record CreateCampaignRequest(string? Name, string? Description);

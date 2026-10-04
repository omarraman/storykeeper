using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CampaignArchiveDocument(
    string Format,
    int SchemaVersion,
    DateTimeOffset ExportedAtUtc,
    CampaignArchiveCampaign Campaign,
    CampaignArchiveSettings Settings,
    CampaignArchiveBible Bible,
    CampaignArchiveParty Party,
    IReadOnlyList<CampaignArchiveHero> Heroes,
    IReadOnlyList<CampaignArchiveInventoryItem> Inventory,
    IReadOnlyList<CampaignArchiveLocation> Locations,
    IReadOnlyList<CampaignArchiveNpc> Npcs,
    IReadOnlyList<CampaignArchiveQuest> Quests,
    IReadOnlyList<CampaignArchiveSession> Sessions,
    IReadOnlyList<CampaignArchiveCheck> Checks,
    IReadOnlyList<CampaignArchiveFact> Facts,
    IReadOnlyList<CampaignArchiveRevision> Revisions,
    IReadOnlyList<CampaignArchiveRelationship> Relationships,
    IReadOnlyList<CampaignArchiveReward> Rewards,
    IReadOnlyList<CampaignArchiveBibleVersion> BibleVersions,
    IReadOnlyList<CampaignArchiveAdventureDraft> AdventureDrafts);

public sealed record CampaignArchiveCampaign(
    Guid Id,
    string Name,
    string? Description,
    CampaignStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record CampaignArchiveSettings(
    Guid Id,
    string Theme,
    string Tone,
    bool LowFright,
    ParentSafetySettings SafetySettings);

public sealed record CampaignArchiveBible(
    Guid Id,
    string WorldDescription,
    string? CurrentSituation,
    int Version);

public sealed record CampaignArchiveParty(Guid Id, string Name);

public sealed record CampaignArchiveHero(
    Guid Id,
    Guid PartyId,
    string Name,
    string Description,
    string Role,
    IReadOnlyList<string> Strengths,
    int Hearts,
    int SparkleTokens);

public sealed record CampaignArchiveInventoryItem(
    Guid Id,
    Guid HeroId,
    string Name,
    string Description,
    int Quantity);

public sealed record CampaignArchiveLocation(Guid Id, string Name, string Description);

public sealed record CampaignArchiveNpc(
    Guid Id,
    Guid? LocationId,
    string Name,
    string Description,
    string Disposition);

public sealed record CampaignArchiveQuest(
    Guid Id,
    string Title,
    string Description,
    QuestStatus Status,
    int? SessionLengthMinutes,
    string? AdventurePlanJson);

public sealed record CampaignArchiveSession(
    Guid Id,
    int SessionNumber,
    string Title,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? Summary,
    bool IsPaused,
    string? ParentInstruction);

public sealed record CampaignArchiveCheck(
    Guid Id,
    Guid SessionId,
    Guid HeroId,
    int Roll,
    CheckRollSource RollSource,
    CheckDifficulty Difficulty,
    int Target,
    string? Strength,
    int StrengthBonus,
    int SparkleBonus,
    int Total,
    CheckOutcome Outcome,
    bool ForwardProgressRequired,
    ConsequenceCategory ConsequenceCategory,
    bool Risky,
    bool SparkleTokenSpent,
    int HeartsBefore,
    int HeartsAfter,
    int SparkleTokensBefore,
    int SparkleTokensAfter,
    DateTimeOffset CreatedAtUtc);

public sealed record CampaignArchiveFact(
    Guid Id,
    Guid? SourceSessionId,
    string Category,
    string Statement,
    CampaignFactStatus Status,
    int Importance);

public sealed record CampaignArchiveRevision(
    Guid Id,
    ContinuityRecordType RecordType,
    Guid RecordId,
    Guid? SourceSessionId,
    string? PreviousContent,
    string NewContent,
    string ChangedBy,
    DateTimeOffset ChangedAtUtc);

public sealed record CampaignArchiveRelationship(
    Guid Id,
    RelationshipParticipantType SubjectType,
    Guid SubjectId,
    RelationshipParticipantType TargetType,
    Guid TargetId,
    string Description);

public sealed record CampaignArchiveReward(
    Guid Id,
    Guid? HeroId,
    string Name,
    string Description,
    bool IsClaimed);

public sealed record CampaignArchiveBibleVersion(
    Guid Id,
    int Version,
    string Title,
    string ContentJson,
    DateTimeOffset ActivatedAtUtc);

public sealed record CampaignArchiveAdventureDraft(
    Guid Id,
    int SessionLengthMinutes,
    string? ParentPreferences,
    AdventureDraftStatus Status,
    int GenerationNumber,
    string ContentJson,
    Guid? ActivatedQuestId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ActivatedAtUtc);

public sealed record CampaignStorybookResponse(
    IReadOnlyList<CampaignStorybookSession> Sessions,
    IReadOnlyList<CampaignStorybookReward> Rewards);

public sealed record CampaignStorybookSession(
    Guid Id,
    int SessionNumber,
    string Title,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? Summary,
    IReadOnlyList<CampaignStorybookDiscovery> Discoveries,
    IReadOnlyList<CampaignStorybookAchievement> HeroAchievements);

public sealed record CampaignStorybookDiscovery(string Category, string Statement);

public sealed record CampaignStorybookAchievement(string HeroName, string Description);

public sealed record CampaignStorybookReward(string Name, string Description, string? HeroName, bool IsClaimed);

public sealed record CampaignArchiveImportResult(
    Guid? CampaignId,
    IReadOnlyDictionary<string, string[]> Errors,
    bool Conflict);

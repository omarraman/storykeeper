using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Storykeeper.Api.Domain;
using System.Text.Json;

namespace Storykeeper.Api.Data;

public sealed class StorykeeperDbContext(DbContextOptions<StorykeeperDbContext> options) : DbContext(options)
{
    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (left, right) => left!.SequenceEqual(right!),
        value => value!.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        value => value!.ToList());
    private static readonly ValueComparer<ParentSafetySettings> ParentSafetySettingsComparer = new(
        (left, right) => left!.FearLevel == right!.FearLevel &&
                         left.CombatMode == right.CombatMode &&
                         left.MaxNarrationWords == right.MaxNarrationWords &&
                         left.SessionLengthMinutes == right.SessionLengthMinutes &&
                         left.ExcludedContent.SequenceEqual(right.ExcludedContent),
        value => HashCode.Combine(
            value!.FearLevel,
            value.CombatMode,
            value.MaxNarrationWords,
            value.SessionLengthMinutes,
            value.ExcludedContent.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode()))),
        value => CloneParentSafetySettings(value!));
    private static readonly JsonSerializerOptions SafetySettingsJsonOptions = new(JsonSerializerDefaults.Web);

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignBrief> CampaignBriefs => Set<CampaignBrief>();
    public DbSet<CampaignDraft> CampaignDrafts => Set<CampaignDraft>();
    public DbSet<AdventureDraft> AdventureDrafts => Set<AdventureDraft>();
    public DbSet<CampaignBibleVersion> CampaignBibleVersions => Set<CampaignBibleVersion>();
    public DbSet<CampaignSettings> CampaignSettings => Set<CampaignSettings>();
    public DbSet<CampaignBible> CampaignBibles => Set<CampaignBible>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Hero> Heroes => Set<Hero>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Npc> Npcs => Set<Npc>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<CheckResolution> CheckResolutions => Set<CheckResolution>();
    public DbSet<CampaignFact> CampaignFacts => Set<CampaignFact>();
    public DbSet<CampaignContinuityRevision> CampaignContinuityRevisions => Set<CampaignContinuityRevision>();
    public DbSet<Relationship> Relationships => Set<Relationship>();
    public DbSet<Reward> Rewards => Set<Reward>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampaignBrief>(entity =>
        {
            entity.HasKey(brief => brief.Id);
            entity.Property(brief => brief.Title).HasMaxLength(120).IsRequired();
            entity.Property(brief => brief.Genre).HasMaxLength(100).IsRequired();
            entity.Property(brief => brief.Tone).HasMaxLength(300).IsRequired();
            entity.Property(brief => brief.StoryIdea).HasMaxLength(2000);
            entity.Property(brief => brief.Inclusions)
                .HasConversion(values => JsonSerializer.Serialize(values, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.Property(brief => brief.Exclusions)
                .HasConversion(values => JsonSerializer.Serialize(values, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.Property(brief => brief.SafetyBoundaries)
                .HasConversion(values => JsonSerializer.Serialize(values, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>())
                .Metadata.SetValueComparer(StringListComparer);
            entity.Property(brief => brief.SafetySettings)
                .HasConversion(
                    settings => JsonSerializer.Serialize(settings, SafetySettingsJsonOptions),
                    value => JsonSerializer.Deserialize<ParentSafetySettings>(value, SafetySettingsJsonOptions) ?? ParentSafetySettings.Defaults)
                .Metadata.SetValueComparer(ParentSafetySettingsComparer);
        });

        modelBuilder.Entity<CampaignDraft>(entity =>
        {
            entity.HasKey(draft => draft.Id);
            entity.Property(draft => draft.ContentJson).HasMaxLength(30000).IsRequired();
            entity.HasOne(draft => draft.CampaignBrief)
                .WithMany(brief => brief.Drafts)
                .HasForeignKey(draft => draft.CampaignBriefId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Campaign>()
                .WithMany()
                .HasForeignKey(draft => draft.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(draft => draft.CampaignBriefId);
        });

        modelBuilder.Entity<AdventureDraft>(entity =>
        {
            entity.HasKey(draft => draft.Id);
            entity.Property(draft => draft.ContentJson).HasMaxLength(16000).IsRequired();
            entity.Property(draft => draft.ParentPreferences).HasMaxLength(500);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_AdventureDrafts_SessionLength", "SessionLengthMinutes BETWEEN 15 AND 180");
                table.HasCheckConstraint("CK_AdventureDrafts_GenerationNumber", "GenerationNumber >= 1");
            });
            entity.HasOne<Campaign>()
                .WithMany()
                .HasForeignKey(draft => draft.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(draft => new { draft.CampaignId, draft.UpdatedAtUtc });
            entity.HasIndex(draft => draft.ActivatedQuestId)
                .IsUnique()
                .HasFilter("ActivatedQuestId IS NOT NULL");
        });

        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(campaign => campaign.Id);
            entity.Property(campaign => campaign.Name).HasMaxLength(120).IsRequired();
            entity.Property(campaign => campaign.Description).HasMaxLength(2000);

            entity.HasOne(campaign => campaign.Settings)
                .WithOne()
                .HasForeignKey<CampaignSettings>(settings => settings.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(campaign => campaign.Bible)
                .WithOne()
                .HasForeignKey<CampaignBible>(bible => bible.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(campaign => campaign.Party)
                .WithOne()
                .HasForeignKey<Party>(party => party.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ConfigureCampaignEntity<CampaignSettings>(modelBuilder, entity =>
        {
            entity.Property(settings => settings.Theme).HasMaxLength(100).IsRequired();
            entity.Property(settings => settings.Tone).HasMaxLength(300).IsRequired();
            entity.Property(settings => settings.SafetySettings)
                .HasConversion(
                    safetySettings => JsonSerializer.Serialize(safetySettings, SafetySettingsJsonOptions),
                    value => JsonSerializer.Deserialize<ParentSafetySettings>(value, SafetySettingsJsonOptions) ?? ParentSafetySettings.Defaults)
                .Metadata.SetValueComparer(ParentSafetySettingsComparer);
        }, configureCampaignRelationship: false);
        ConfigureCampaignEntity<CampaignBible>(modelBuilder, entity =>
        {
            entity.Property(bible => bible.WorldDescription).HasMaxLength(4000).IsRequired();
            entity.Property(bible => bible.CurrentSituation).HasMaxLength(2000);
            entity.Property(bible => bible.Version).HasDefaultValue(1);
            entity.ToTable(table => table.HasCheckConstraint("CK_CampaignBibles_Version", "Version >= 1"));
        }, configureCampaignRelationship: false);
        ConfigureCampaignEntity<Party>(modelBuilder, entity =>
        {
            entity.Property(party => party.Name).HasMaxLength(120).IsRequired();
            entity.HasAlternateKey(party => new { party.CampaignId, party.Id });
        }, configureCampaignRelationship: false);
        ConfigureCampaignEntity<Hero>(modelBuilder, entity =>
        {
            entity.Property(hero => hero.Name).HasMaxLength(120).IsRequired();
            entity.Property(hero => hero.Description).HasMaxLength(2000).IsRequired();
            entity.Property(hero => hero.Role).HasMaxLength(80).IsRequired();
            entity.Property(hero => hero.Strengths)
                .HasConversion(values => JsonSerializer.Serialize(values, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null) ?? new List<string>())
                .HasDefaultValueSql("'[]'")
                .Metadata.SetValueComparer(StringListComparer);
            entity.HasAlternateKey(hero => new { hero.CampaignId, hero.Id });
            entity.HasOne<Party>()
                .WithMany(party => party.Heroes)
                .HasForeignKey(hero => new { hero.CampaignId, hero.PartyId })
                .HasPrincipalKey(party => new { party.CampaignId, party.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
        ConfigureCampaignEntity<InventoryItem>(modelBuilder, entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(120).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000).IsRequired();
            entity.HasOne<Hero>()
                .WithMany(hero => hero.Inventory)
                .HasForeignKey(item => new { item.CampaignId, item.HeroId })
                .HasPrincipalKey(hero => new { hero.CampaignId, hero.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
        ConfigureCampaignEntity<Location>(modelBuilder, entity =>
        {
            entity.Property(location => location.Name).HasMaxLength(120).IsRequired();
            entity.Property(location => location.Description).HasMaxLength(2000).IsRequired();
            entity.HasAlternateKey(location => new { location.CampaignId, location.Id });
        }, campaignNavigation: campaign => campaign.Locations);
        ConfigureCampaignEntity<Npc>(modelBuilder, entity =>
        {
            entity.Property(npc => npc.Name).HasMaxLength(120).IsRequired();
            entity.Property(npc => npc.Description).HasMaxLength(2000).IsRequired();
            entity.Property(npc => npc.Disposition).HasMaxLength(300).IsRequired();
            entity.HasOne<Location>()
                .WithMany()
                .HasForeignKey(npc => new { npc.CampaignId, npc.LocationId })
                .HasPrincipalKey(location => new { location.CampaignId, location.Id })
                .OnDelete(DeleteBehavior.Restrict);
        }, campaignNavigation: campaign => campaign.Npcs);
        ConfigureCampaignEntity<Quest>(modelBuilder, entity =>
        {
            entity.Property(quest => quest.Title).HasMaxLength(160).IsRequired();
            entity.Property(quest => quest.Description).HasMaxLength(2000).IsRequired();
            entity.Property(quest => quest.AdventurePlanJson).HasMaxLength(12000);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Quests_SessionLength", "SessionLengthMinutes IS NULL OR SessionLengthMinutes BETWEEN 15 AND 180"));
        }, campaignNavigation: campaign => campaign.Quests);
        ConfigureCampaignEntity<Session>(modelBuilder, entity =>
        {
            entity.Property(session => session.Summary).HasMaxLength(4000);
            entity.Property(session => session.ParentInstruction).HasMaxLength(300);
            entity.HasIndex(session => new { session.CampaignId, session.SessionNumber }).IsUnique();
            entity.HasAlternateKey(session => new { session.CampaignId, session.Id });
        }, campaignNavigation: campaign => campaign.Sessions);
        ConfigureCampaignEntity<CheckResolution>(modelBuilder, entity =>
        {
            entity.Property(check => check.Strength).HasMaxLength(80);
            entity.HasOne<Session>()
                .WithMany(session => session.CheckResolutions)
                .HasForeignKey(check => new { check.CampaignId, check.SessionId })
                .HasPrincipalKey(session => new { session.CampaignId, session.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Hero>()
                .WithMany()
                .HasForeignKey(check => new { check.CampaignId, check.HeroId })
                .HasPrincipalKey(hero => new { hero.CampaignId, hero.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_CheckResolutions_Roll", "Roll BETWEEN 1 AND 20");
                table.HasCheckConstraint("CK_CheckResolutions_Target", "Target IN (8, 12, 16)");
            });
        });
        ConfigureCampaignEntity<CampaignFact>(modelBuilder, entity =>
        {
            entity.Property(fact => fact.Category).HasMaxLength(80).IsRequired();
            entity.Property(fact => fact.Statement).HasMaxLength(2000).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_CampaignFacts_Importance", "Importance BETWEEN 1 AND 5"));
            entity.HasOne<Session>()
                .WithMany(session => session.Facts)
                .HasForeignKey(fact => new { fact.CampaignId, fact.SourceSessionId })
                .HasPrincipalKey(session => new { session.CampaignId, session.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureCampaignEntity<CampaignContinuityRevision>(modelBuilder, entity =>
        {
            entity.Property(revision => revision.PreviousContent).HasMaxLength(4000);
            entity.Property(revision => revision.NewContent).HasMaxLength(4000).IsRequired();
            entity.Property(revision => revision.ChangedBy).HasMaxLength(80).IsRequired();
            entity.HasIndex(revision => new { revision.CampaignId, revision.RecordType, revision.RecordId });
            entity.HasIndex(revision => new { revision.CampaignId, revision.ChangedAtUtc });
        });
        ConfigureCampaignEntity<Relationship>(modelBuilder, entity =>
        {
            entity.Property(relationship => relationship.Description).HasMaxLength(1000).IsRequired();
        });
        ConfigureCampaignEntity<Reward>(modelBuilder, entity =>
        {
            entity.Property(reward => reward.Name).HasMaxLength(120).IsRequired();
            entity.Property(reward => reward.Description).HasMaxLength(1000).IsRequired();
            entity.HasOne<Hero>()
                .WithMany()
                .HasForeignKey(reward => new { reward.CampaignId, reward.HeroId })
                .HasPrincipalKey(hero => new { hero.CampaignId, hero.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureCampaignEntity<CampaignBibleVersion>(modelBuilder, entity =>
        {
            entity.Property(version => version.Title).HasMaxLength(120).IsRequired();
            entity.Property(version => version.ContentJson).HasMaxLength(30000).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_CampaignBibleVersions_Version", "Version >= 1"));
            entity.HasIndex(version => new { version.CampaignId, version.Version }).IsUnique();
            entity.HasIndex(version => version.SourceDraftId)
                .IsUnique()
                .HasFilter("SourceDraftId IS NOT NULL");
            entity.HasOne<CampaignDraft>()
                .WithMany()
                .HasForeignKey(version => version.SourceDraftId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureCampaignEntity<TEntity>(
        ModelBuilder modelBuilder,
        Action<EntityTypeBuilder<TEntity>> configure,
        bool configureCampaignRelationship = true,
        System.Linq.Expressions.Expression<Func<Campaign, IEnumerable<TEntity>?>>? campaignNavigation = null)
        where TEntity : CampaignEntity
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.HasKey(item => item.Id);
        if (configureCampaignRelationship)
        {
            entity.HasOne<Campaign>()
                .WithMany(campaignNavigation)
                .HasForeignKey(item => item.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        }
        configure(entity);
    }

    private static ParentSafetySettings CloneParentSafetySettings(ParentSafetySettings value) => new()
    {
        FearLevel = value.FearLevel,
        CombatMode = value.CombatMode,
        ExcludedContent = value.ExcludedContent.ToList(),
        MaxNarrationWords = value.MaxNarrationWords,
        SessionLengthMinutes = value.SessionLengthMinutes
    };
}

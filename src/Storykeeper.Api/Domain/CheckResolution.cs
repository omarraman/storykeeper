namespace Storykeeper.Api.Domain;

public enum CheckDifficulty
{
    Easy,
    Tricky,
    Heroic
}

public enum CheckOutcome
{
    StrongSuccess,
    Success,
    SuccessWithComplication,
    SetbackWithProgress
}

public enum ConsequenceCategory
{
    None,
    ExtraBenefit,
    GentleComplication,
    GentleSetback,
    GentleSetbackWithHeartLoss
}

public enum CheckRollSource
{
    Physical,
    ServerTest
}

public sealed class CheckResolution : CampaignEntity
{
    public Guid SessionId { get; set; }
    public Guid HeroId { get; set; }
    public int Roll { get; set; }
    public CheckRollSource RollSource { get; set; }
    public CheckDifficulty Difficulty { get; set; }
    public int Target { get; set; }
    public string? Strength { get; set; }
    public int StrengthBonus { get; set; }
    public int SparkleBonus { get; set; }
    public int Total { get; set; }
    public CheckOutcome Outcome { get; set; }
    public bool ForwardProgressRequired { get; set; }
    public ConsequenceCategory ConsequenceCategory { get; set; }
    public bool Risky { get; set; }
    public bool SparkleTokenSpent { get; set; }
    public int HeartsBefore { get; set; }
    public int HeartsAfter { get; set; }
    public int SparkleTokensBefore { get; set; }
    public int SparkleTokensAfter { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

using Storykeeper.Api.Domain;

namespace Storykeeper.Api.Contracts;

public sealed record CampaignNarratorGuideResponse(
    string? ActiveText,
    string PendingText,
    bool HasPendingRevision,
    int ActiveRevision,
    int PendingRevision,
    DateTimeOffset? ActiveApprovedAtUtc,
    DateTimeOffset? PendingUpdatedAtUtc)
{
    public static CampaignNarratorGuideResponse From(CampaignNarratorGuide? guide) =>
        guide is null
            ? new CampaignNarratorGuideResponse(null, string.Empty, false, 0, 0, null, null)
            : new CampaignNarratorGuideResponse(
                guide.ActiveText,
                guide.PendingText,
                guide.HasPendingRevision,
                guide.ActiveRevision,
                guide.PendingRevision,
                guide.ActiveApprovedAtUtc,
                guide.PendingUpdatedAtUtc);
}

public sealed record SaveNarratorGuideRequest(string? Text);

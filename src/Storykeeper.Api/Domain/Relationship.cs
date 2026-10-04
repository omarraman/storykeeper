namespace Storykeeper.Api.Domain;

public enum RelationshipParticipantType
{
    Hero,
    Npc
}

public sealed class Relationship : CampaignEntity
{
    public RelationshipParticipantType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public RelationshipParticipantType TargetType { get; set; }
    public Guid TargetId { get; set; }
    public string Description { get; set; } = string.Empty;
}

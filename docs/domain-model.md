# Domain model

Each campaign is an independent persistence boundary. A campaign owns its
settings, bible, party, heroes, inventory, locations, NPCs, quests, sessions,
facts, relationships, and rewards. Campaign-owned rows carry a `CampaignId`.
Repositories always scope reads by both campaign ID and entity ID, and
composite foreign keys prevent child records from linking to another
campaign's party, hero, location, or session.

## Entities

- `Campaign` stores its name, description, lifecycle status, and UTC creation,
  update, and archive timestamps. Active campaigns may be completed or
  archived, and completed campaigns may be archived. Completed campaigns
  remain resumable; archived campaigns preserve their saved data for
  read-only viewing.
- `CampaignSettings` stores theme, tone, the low-fright safety default, and
  the parent-selected fright/combat modes, excluded content, narration limit,
  and session pacing target.
- `CampaignBible` stores durable world description, current situation, and
  current version number.
- `Party` groups the campaign's `Hero` records. Heroes track description,
  role, listed strengths, current-session hearts and sparkle tokens, and their
  inventory.
- Parents can add heroes to an active campaign between sessions. The API
  validates the hero details and requires the parent PIN; new heroes are
  stored in that campaign's party and receive session resources when a session
  starts.
- `InventoryItem` belongs to one hero and stores its name, description, and
  quantity.
- `Location` and `Npc` store campaign-scoped world details. An NPC may
  reference a location in the same campaign.
- `Quest` stores its title, description, and lifecycle status. Activated
  adventure quests also retain their validated structured episode plan and
  selected 15-180-minute target.
- `AdventureDraft` stores one campaign-scoped episode plan, its parent pacing
  preferences, generation number, review status, and the quest created when
  activated. Drafts and their activated quest links cannot cross campaigns.
- `Session` stores its sequence number, title, UTC start/end times, and optional
  factual summary. Its title is the active quest title at session start, or
  `Adventure N` when no quest is active. Starting a session resets each hero
  to 3 hearts and 1 sparkle token. It also stores a parent pause flag and
  one-turn narration direction. A running session must be summarized before
  another session can start.
- `CheckResolution` records a server-calculated d20 result, its session and
  hero, the difficulty and bonuses, the outcome band, the forward-progress
  requirement, and before/after heart and sparkle-token counts. Composite
  foreign keys keep both the hero and session within the owning campaign.
- `StoryBeat` stores validated narration with backward-compatible optional
  submitted action, acting hero ID, NPC dialogue JSON, and check-resolution ID.
  A per-session sequence gives accepted turns deterministic chronology. Legacy
  beats are assigned a best-effort sequence from `CreatedAtUtc` with an ID
  tie-break and marked as estimated; exact ordering cannot be recovered when
  legacy timestamps tie.
- `CampaignFact` stores a durable statement, category, status (`Proposed`,
  `Active`, `Resolved`, `Superseded`, or `Discarded`), importance from 1 to 5,
  and an optional source session from the same campaign. Only active facts
  inform future narration; proposed facts await parent review.
- `CampaignContinuityRevision` is an append-only audit entry for parent-created
  or corrected facts and session summaries. It records prior and replacement
  content, the campaign-scoped source session, editor label, and UTC edit time.
- `Relationship` stores a campaign-scoped relationship between typed hero or
  NPC IDs. The entity repository verifies both participants belong to that
  campaign before persisting the relationship.
- `CampaignBrief` stores a parent-authored campaign idea and safety settings
  before generation:
  title, genre, tone, intended campaign and session lengths, optional
  inclusions and exclusions, and a free-text story idea. Briefs are saved
  independently of campaigns and have creation/update timestamps.
- `CampaignDraft` stores a validated, structured generated draft linked to
  its brief. It tracks generation number, pending-review/approved/activated
  status, and the activated campaign when present.
- `CampaignBibleVersion` stores a campaign-scoped immutable snapshot of each
  activated bible, including its source draft and version number.
- `Reward` stores a campaign reward and may associate it with a hero from the
  same campaign.
- `AdventureDraftContent` is a structured episode with an opening, 2-4 scenes,
  at least two solution paths, 2-5 clues, a featured NPC, a gentle finale,
  celebration or reward, and standalone/season-arc framing.

Fact metadata is distinct from narration: a session summary is a compact
recap, while a campaign fact is an individually categorized claim that can be
activated, resolved, superseded, or discarded and traced to its source
session. Continuity revisions are removed only when their campaign is
permanently deleted.

Live turn context keeps three memory types distinct: active facts are
parent-reviewed durable canon, recent accepted StoryBeats are bounded
campaign-and-session-scoped events, and a reviewed adventure plan is private
facilitator material rather than evidence of player discovery or completion.
Recent-turn context defaults to 8 beats and 12,000 aggregate text characters,
with server-configured hard bounds of 1-50 beats and 200-50,000 characters.
This character budget is not a token limit. Recent history improves narrative
continuity but does not replace explicit adventure state or reviewed facts.

Campaign archival preserves all campaign data. Archived campaigns remain
loadable for save/resume history but cannot be updated through the campaign
service.

Storybook history is derived from campaign sessions and their source-linked
facts and check resolutions, together with campaign rewards and celebration
rewards from completed adventure plans. JSON campaign archives preserve the
campaign-owned continuity entities and their identifiers; campaign brief
authoring drafts and provider/server configuration are deliberately excluded.

Campaign deletion is permanent and removes its campaign-owned records. The
selector requires an explicit confirmation before requesting deletion.

Parent safety settings are validated at the API boundary and copied from an
approved campaign brief to its activated campaign. Generated adventure pacing
uses the campaign's saved session-length target, and campaign settings never
cross campaign boundaries.

## Current defaults and constraints

New campaigns receive default cozy-fantasy, warm/adventurous, low-fright
settings, an empty bible, and an empty party. The schema applies required
fields, bounded text lengths, one party per campaign, unique session numbers
within a campaign, and fact importance validation.

Relationship participants are currently heroes or NPCs. Add new participant
types explicitly when the domain adds more character categories.

Every campaign brief receives server-owned safety boundaries: no gore or
cruelty, mature themes, permanent character death, or mandatory tactical
combat, and low-fright, age-appropriate content. These boundaries are not
editable through the brief API. Campaign and session lengths are validated
server-side to 1-30 sessions and 15-180 minutes respectively.

Campaign drafts require 3-6 world rules, 3-5 unique NPCs, 3-6 unique
locations, and 1-4 adventure hooks. Generated and parent-edited drafts pass
the same server-side validation before persistence. Only approved drafts can
be activated. Activation writes the new campaign and its version 1 bible
snapshot atomically.

Adventure drafts require parent review and approval. Editing or regenerating
clears approval. Activation creates the featured NPC and in-progress quest
with its complete plan in one transaction, and marks any prior in-progress
quest completed. Active facts and recent summaries used for generation are
read only from the owning campaign.

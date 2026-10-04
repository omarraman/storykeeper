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
- `CampaignSettings` stores theme, tone, and the low-fright safety default.
- `CampaignBible` stores durable world description, current situation, and
  current version number.
- `Party` groups the campaign's `Hero` records. Heroes track description,
  role, listed strengths, current-session hearts and sparkle tokens, and their
  inventory.
- `InventoryItem` belongs to one hero and stores its name, description, and
  quantity.
- `Location` and `Npc` store campaign-scoped world details. An NPC may
  reference a location in the same campaign.
- `Quest` stores its title, description, and lifecycle status.
- `Session` stores its sequence number, UTC start/end times, and optional
  summary. Starting a session resets each hero to 3 hearts and 1 sparkle
  token.
- `CheckResolution` records a server-calculated d20 result, its session and
  hero, the difficulty and bonuses, the outcome band, the forward-progress
  requirement, and before/after heart and sparkle-token counts. Composite
  foreign keys keep both the hero and session within the owning campaign.
- `CampaignFact` stores a durable statement, category, status, importance
  from 1 to 5, and an optional source session from the same campaign.
- `Relationship` stores a campaign-scoped relationship between typed hero or
  NPC IDs. The entity repository verifies both participants belong to that
  campaign before persisting the relationship.
- `CampaignBrief` stores a parent-authored campaign idea before generation:
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

Fact metadata is distinct from narration: a session summary is a recap, while
a campaign fact is an individually categorized claim that can be confirmed,
superseded, or discarded and traced to its source session.

Campaign archival preserves all campaign data. Archived campaigns remain
loadable for save/resume history but cannot be updated through the campaign
service.

Campaign deletion is permanent and removes its campaign-owned records. The
selector requires an explicit confirmation before requesting deletion.

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

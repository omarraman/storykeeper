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
- `CampaignBible` stores durable world description and current situation.
- `Party` groups the campaign's `Hero` records. Heroes track description,
  role, hearts, sparkle tokens, and their inventory.
- `InventoryItem` belongs to one hero and stores its name, description, and
  quantity.
- `Location` and `Npc` store campaign-scoped world details. An NPC may
  reference a location in the same campaign.
- `Quest` stores its title, description, and lifecycle status.
- `Session` stores its sequence number, UTC start/end times, and optional
  summary.
- `CampaignFact` stores a durable statement, category, status, importance
  from 1 to 5, and an optional source session from the same campaign.
- `Relationship` stores a campaign-scoped relationship between typed hero or
  NPC IDs. The entity repository verifies both participants belong to that
  campaign before persisting the relationship.
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

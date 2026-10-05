# AI Storykeeper turns

## Turn API

`POST /api/campaigns/{campaignId}/actions` accepts an action of up to 500
characters plus the current `sessionId` and an optional campaign-owned
`heroId`. The API requires an active campaign and its latest, unended session.
Campaign, session, and hero are checked on the server; browser-provided story
context is never trusted.

The server sends only that campaign's settings, bible, party (or selected
hero), current quest and recent completed quests, up to 20 active facts, the
three latest ended-session summaries, unresolved promise/thread facts,
relationships, rewards, NPCs, and locations to the AI provider. Proposed,
resolved, superseded, and discarded facts are not used as active continuity.
Inventory and completed quests are loaded from their saved campaign records.
If the current quest came from a reviewed adventure draft, its stored opening,
scenes, solution paths, clues, featured NPC, finale, reward, and pacing target
are included so narration can follow the approved episode structure.
Server-loaded parent safety settings and the current one-turn parent
direction are included as well. Paused sessions are rejected before any
provider request is made.
The provider receives the player's action and saved story context as untrusted
content under a stable child-safety narrator prompt.

The API requests up to 2,400 completion tokens in text mode, then parses
`message.content` as strict typed JSON with no unknown properties. Reasoning
channels are not used as story output. A response is either `roll_required`
with a server-validated difficulty, optional listed
hero strength, prompt, and risk flag, or `story_beat` with narration, optional
speaker and NPC dialogue, up to four suggested choices, and current campaign
state. Suggested choices do not constrain the player's next free-text action.
Narration follows the campaign's configured word limit (40-150 words), which
the API enforces before returning a turn.

The API validates response lengths, choice identifiers, NPC attribution,
roll difficulty and strength, fact categories and importance, and child-safety
content, including campaign-excluded topics, before applying anything. A roll
request does not persist state.
Validated fact proposals are saved only as `Proposed` campaign facts, scoped
to the current campaign and source session; duplicate statements are not
re-added. A parent can create facts or promote, correct, resolve, supersede,
or discard proposals. Only `Active` facts enter future prompts. Parent
corrections append an audit record containing the prior and updated values.
The model cannot write directly to the database or alter quest status, hero
resources, rules, or dice results. A malformed, unsafe, or unavailable
provider response returns a safe, retryable structured error and is not saved.

`GET /api/campaigns/{campaignId}/continuity` returns campaign facts, saved
session summaries, and their revision history. `GET
/api/campaigns/{campaignId}/facts/{factId}` reads one campaign-owned fact.
`POST /api/campaigns/{campaignId}/facts` creates a parent-approved fact;
`PUT /api/campaigns/{campaignId}/facts/{factId}` corrects its content and
status. `PUT
/api/campaigns/{campaignId}/sessions/{sessionId}/summary` saves a compact
parent-curated recap and ends the current session, or corrects a prior recap.
Every save or correction is audited. Summaries are bounded and
child-safety validated; transcripts are not required to continue a campaign.

After a physical check, the web client submits the original action with the
check-resolution ID. The API verifies that this is the latest check for the
selected hero in the current session, then supplies its server-calculated
outcome and resource totals as authoritative narration context.

The adventure screen sends submitted actions to this API for campaigns with
server-owned heroes. Empty-world preview campaigns continue to use the local
demo adapter and never save preview actions or state.

## Provider configuration

The turn generator reuses the server-only
`Storykeeper__Ai__BaseUrl`, `Storykeeper__Ai__Model`,
`Storykeeper__Ai__ApiKey`, and `Storykeeper__Ai__TimeoutSeconds` settings
documented in the [development guide](development.md). Only HTTPS provider
URLs are accepted except loopback development endpoints. The browser never
receives the API key.

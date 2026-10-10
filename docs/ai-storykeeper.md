# AI Storykeeper turns

## Turn API

`POST /api/campaigns/{campaignId}/actions` accepts an action of up to 500
characters plus the current `sessionId` and an optional campaign-owned
`heroId`. The API requires an active campaign and its latest, unended session.
Campaign, session, and hero are checked on the server; browser-provided story
context is never trusted.

The server sends only that campaign's settings, bible, the whole party and a
separate `actingHero` for the selected campaign-owned hero, current quest and
recent completed quests, up to 20 active facts, the three latest ended-session
summaries, unresolved promise/thread facts, relationships, rewards, NPCs, and
locations to the AI provider. It also sends bounded `recentTurns` from
accepted StoryBeats in the same campaign and active session. Each turn carries
its available submitted action, acting-hero identity, accepted narration,
NPC dialogue, and the associated server-resolved check outcome when available.
The latest submitted action remains separate and is not duplicated as history.
Proposed, resolved, superseded, and discarded facts are not used as active
continuity. Inventory and completed quests are loaded from their saved
campaign records.
If the current quest came from a reviewed adventure draft, its stored opening,
scenes, solution paths, clues, featured NPC, finale, reward, and pacing target
are included so narration can follow the approved episode structure.
Server-loaded parent safety settings and the current one-turn parent
direction are included as well. Paused sessions are rejected before any
provider request is made.
Recent play is transient narrator context, not reviewed canon. Only active
facts are reviewed durable canon; generated proposals remain proposed until a
parent reviews them. A reviewed `adventurePlan` is private facilitator
material and distinguishes planned content from what the players have
observed; plan presence does not mean that a clue was discovered or a scene
completed. Recent turns are provider-only context and are not returned as
player-visible hidden-guide content. The provider receives the player's
current action and saved story context as untrusted content under a stable
child-safety narrator prompt.

Accepted StoryBeats persist the trimmed action, optional acting hero ID,
validated NPC dialogue JSON, optional check-resolution ID, and a per-session
sequence alongside narration. Only a validated `story_beat` is persisted;
roll requests and rejected provider responses are not completed turns.
Recent-turn defaults are 8 turns and 12,000 aggregate text characters, bounded
to 1-50 turns and 200-50,000 characters by server configuration. The character
budget counts turn text, not JSON framing and not model tokens. The retained
suffix is sent oldest-to-newest, choosing the newest turns first when trimming;
if the newest alone is over budget, its text is safely truncated and its
`truncated` flag is set. Historical StoryBeats are migrated
with an estimated sequence from creation time and a deterministic ID tie
break; their chronology is explicitly marked estimated because exact event
order cannot be recovered for legacy ties.

The API requests up to 2,400 completion tokens in text mode, then parses
`message.content` as strict typed JSON with no unknown properties. Reasoning
channels are never used as story output. If a provider returns reasoning but
no user-facing content, the API retries once with llama.cpp's
`chat_template_kwargs.enable_thinking=false`; it still rejects the response
if the retry has no content. A response is either `roll_required`
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
`Storykeeper__Ai__ApiKey`, `Storykeeper__Ai__TimeoutSeconds`,
`Storykeeper__Ai__RecentTurnLimit`, and
`Storykeeper__Ai__RecentTurnCharacterBudget` settings documented in the
[development guide](development.md). Only HTTPS provider URLs are accepted
except loopback development endpoints. The browser never receives the API
key.

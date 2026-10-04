# AI Storykeeper turns

## Turn API

`POST /api/campaigns/{campaignId}/actions` accepts an action of up to 500
characters plus the current `sessionId` and an optional campaign-owned
`heroId`. The API requires an active campaign and its latest, unended session.
Campaign, session, and hero are checked on the server; browser-provided story
context is never trusted.

The server sends only that campaign's settings, bible, party (or selected
hero), current quest, up to 20 active facts, the three latest non-empty session
summaries, NPCs, and locations to the AI provider. Active facts exclude
superseded and discarded records. The provider receives the player's action
as untrusted text under a stable child-safety narrator prompt.

Responses use strict typed JSON with no unknown properties. A response is
either `roll_required` with a server-validated difficulty, optional listed
hero strength, prompt, and risk flag, or `story_beat` with narration, optional
speaker and NPC dialogue, up to four suggested choices, and current campaign
state. Suggested choices do not constrain the player's next free-text action.
Narration is prompted to be 60-120 words and rejected above 150 words.

The API validates response lengths, choice identifiers, NPC attribution,
roll difficulty and strength, fact categories and importance, and child-safety
content before applying anything. A roll request does not persist state.
Validated fact proposals are saved only as `Proposed` campaign facts, scoped
to the current campaign and source session; duplicate active statements are
not added. The model cannot write directly to the database or alter quest
status, hero resources, rules, or dice results. A malformed, unsafe, or
unavailable provider response returns a safe, retryable structured error and
is not saved.

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

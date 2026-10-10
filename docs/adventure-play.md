# Adventure play screen

The web app's adventure-play screen is a landscape-first play surface for a
campaign's current session. It presents short narration, a speaker, the current
quest, hero resources, clues, inventory, up to four suggested actions, and an
always-available free-text action path. A physical d20 result is entered by
the player and submitted to the server rules API.

## Turn-client boundary

React consumes the typed `AdventureTurnClient` interface and the
`AdventureTurnResponse` discriminated union (`story_beat`, `roll_required`, or
`error`). The view depends on shared story, hero, quest, clue, inventory, and
roll-resolution summaries—not on fixture-specific properties. For campaigns
with server-owned heroes, submitted suggested or free-text actions use the
server AI turn API. Their opening scene uses the saved current situation and,
when the active quest has an approved adventure plan, its opening and featured
NPC. The campaign response deliberately omits future scenes, clues, paths, and
the finale. Empty-world preview campaigns alone use the local deterministic
demo adapter, whose scene is explicitly labeled as a preview. The browser
never receives provider credentials and never persists AI-proposed state.

## Authoritative session and roll state

Starting an adventure calls `POST /api/campaigns/{campaignId}/sessions`. An
already-active latest session is resumed rather than replaced, including after
a page refresh. Physical die results for campaigns with server-owned heroes
are sent to the existing campaign/session/hero checks endpoint. The API
validates the hero, session, die result, strength, sparkle token, and outcome,
then persists the resource changes. The UI refreshes and displays those
server-returned values; it does not resolve the check.

The server titles a new session with the active quest title, or `Adventure N`
if there is no active quest. The campaign room's Storybook displays sessions
chronologically with their factual summaries, high-importance discoveries,
strong-success hero moments, and campaign or completed-adventure rewards.
Campaign Storybook history and the versioned JSON backup/restore contract are
documented in [Storybook history and campaign export](storybook-and-campaign-export.md).

An activated, parent-reviewed adventure plan is saved with its current quest.
The server includes that plan in live narration context to guide the episode's
opening, scenes, clues, routes, and gentle finale without limiting player
actions. A parent can add the next episode after the current session is
summarized; activation starts its quest and completes the prior in-progress
quest.

At session end, a parent enters a compact factual recap in the play screen.
Saving the recap with the parent PIN ends the current session; a new session
cannot silently close an unfinished one. The campaign room's continuity controls let a parent add
or correct facts, mark them active/resolved/superseded/discarded, revise
summaries, and inspect the preserved correction history.

The current campaign authoring UI may produce a campaign with no heroes. For
that case the demo adapter supplies a clearly marked, in-memory preview hero
and sample state so the screen's interaction flow can be explored. Its
physical die entry advances only the preview story: it does not calculate a
rules outcome, spend resources, create campaign facts, or save any preview
content. The preview scene, clues, and progression reset when the screen is
reopened. The preview introduces Mira as part of its sample scene; she is not
an NPC in the campaign unless that campaign separately defines her. Preview
content is not campaign continuity.

In the campaign room, a parent can add heroes before starting a session. Hero
creation is PIN-gated and unavailable while a session is active; an empty
party cannot start a new session. If a demo session is already active, the
parent must end it before adding a saved hero and starting real play.

Hero cards select which party member takes the next action. The first hero is
selected by default; when there is only one hero, the screen says it is already
selected. In empty-world preview mode, the card's “Demo hero” tag is only a
status label, not a separate preview action.

## Optional narrated playback

The parent chooses Off, On demand, or Autoplay after a newly received story
beat in the PIN-gated campaign settings; Off is the default. The scene always
keeps the validated narration visible, and typed/tap actions and physical d20
entry remain unchanged. Playback uses large Listen, Pause, Stop, Replay, and
Mute controls. It never autoplays on initial page load; autoplay applies only
when a new server StoryBeat replaces the current one.

Listen requests audio by the current campaign, session, and server-issued
StoryBeat ID. Only normalized narration already validated and saved by the API
can be synthesized. The opening scene is assembled client-side from approved
campaign data, not saved as an AI StoryBeat, so it remains text-only. Preview
beats also remain text-only. Provider errors, unsupported audio playback, and
network failures announce a recoverable message and leave the narration
available to read. Starting a new action, replacing a beat, or leaving the
screen stops existing playback.

The API is configured with a single explicit server provider (Piper or
ElevenLabs); no provider key reaches the browser and there is no automatic
local-to-cloud fallback. Parents can choose the playback preference but cannot
override server provider configuration. See [Text-to-speech](text-to-speech.md)
for configuration, endpoint, and cache behavior.

Microphone capture and speech-to-text are deferred to Phase 2. The existing
browser dictation code is not changed by this narrated-playback phase.

## Resilience and accessibility

Network errors resolving a real check are not blindly retried, because the
server may have committed the check before a connection failed. The recovery
action reloads saved campaign state instead. Retry is offered for recoverable
story API errors. The screen uses keyboard-operable controls,
visible focus, labels and live status/error announcements, and keeps the
suggested-choice count capped at four. On narrow displays the panels reflow and
remain available through normal page scrolling; on tablet landscape the
essential action area stays visible while the party/discovery panel can scroll
independently.

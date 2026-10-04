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
server AI turn API. Empty-world preview campaigns continue using the local
deterministic demo adapter. The browser never receives provider credentials
and never persists AI-proposed state.

## Authoritative session and roll state

Starting an adventure calls `POST /api/campaigns/{campaignId}/sessions`. An
already-active latest session is resumed rather than replaced, including after
a page refresh. Physical die results for campaigns with server-owned heroes
are sent to the existing campaign/session/hero checks endpoint. The API
validates the hero, session, die result, strength, sparkle token, and outcome,
then persists the resource changes. The UI refreshes and displays those
server-returned values; it does not resolve the check.

The current campaign authoring UI may produce a campaign with no heroes. For
that case the demo adapter supplies a clearly marked, in-memory preview hero
and sample state so the screen's interaction flow can be explored. Its
physical die entry advances only the preview story: it does not calculate a
rules outcome, spend resources, create campaign facts, or save any preview
content. The preview scene, clues, and progression reset when the screen is
reopened. They are not campaign continuity.

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

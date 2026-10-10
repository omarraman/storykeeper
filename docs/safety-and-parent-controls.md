# Safety and parent controls

## Fixed safety boundaries

The server always retains Storykeeper's child-safety rules: age-appropriate,
low-fright narration; no gore, cruelty, mature themes, permanent character
death, or mandatory tactical combat; and forward progress after a failed roll.
Parent preferences may make the story gentler, exclude additional topics or
creatures, and choose optional non-graphic combat styles, but cannot weaken
these fixed boundaries.

## Campaign settings

Each saved campaign has parent safety settings for fright level, combat mode,
excluded topics or creatures, narration word limit, and target session length.
The campaign brief carries these settings before world generation; activation
copies them into that campaign. Empty campaigns receive the same safe defaults.
Settings are returned with campaign data so adults can review them before play.

The API validates settings when they are saved. Campaign and adventure
generation receive the server-saved preferences, and generated text is checked
for excluded phrases before it is persisted or shown. Live narration receives
the campaign settings as server-loaded context; the API enforces the narration
word limit and rejects excluded content before applying proposed facts.
Session duration is used as the pacing target for generated episodes.
Fright is either `None` or `Low`; combat is `Avoid`, `Silly`, or
`StoryOnly`. Narration is bounded to 40-150 words and session pacing to
15-180 minutes. Exclusions use case-insensitive whole-word or phrase matching
against generated text; adults should still review generated drafts and turns.
Session length is a pacing target, not an automatic timer.

Narrated playback has an enable flag, a provider choice, and a campaign
preference with three values: `Off` (the default), `On demand`, and
`Autoplay after a new story beat`. Only a parent with unlocked PIN-gated
controls can change them. Provider choices are restricted to the one
server-configured provider; the provider status is shown without exposing
credentials.
Children can continue using visible narration, text actions, tap choices, and
physical dice if playback is off, unavailable, or denied by the browser.

Microphone capture and speech-to-text are not part of the Phase 1 narrated
playback implementation and are deferred to Phase 2.

## Parent PIN

Set `Storykeeper__ParentPin` on the API server to a private value of 6-64
characters. Docker Compose reads `STORYKEEPER_PARENT_PIN` from the untracked
`.env` file. No default PIN is installed: parent-control writes fail closed
until one is configured. The browser asks for the PIN when an adult opens the
parent panel, sends it only in the `X-Parent-Pin` request header, and keeps it
only in component memory. The server compares it in constant time and limits
failed attempts per client IP. Do not place this value in web configuration,
local storage, or source control. Production deployments must use HTTPS.

## Live story controls

PIN-gated parent actions can make the next challenge easier, add a clue, skip a
scene, move toward a gentle ending, pause or resume the story, or request a
session wrap-up. Directions are stored with the active campaign session and
loaded by the API for the next narration turn; the browser cannot inject or
persist these directions. Paused sessions reject story actions and check
resolution server-side. Ending a session still requires a parent-curated
factual summary and the parent PIN.

The PIN is a household access gate, not a user account or identity system.
Third-party parental-control integration is not provided.

The API verifies with `POST /api/parent-controls/verify`; saves campaign
settings with `PUT /api/campaigns/{campaignId}/parent-controls`; and applies
live actions with `POST
/api/campaigns/{campaignId}/sessions/{sessionId}/parent-actions`. Creating or
updating a campaign brief also requires the PIN so unactivated campaign
settings cannot be changed around the parent gate.

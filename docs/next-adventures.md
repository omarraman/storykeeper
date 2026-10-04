# Next playable adventures

## Parent review flow

From an active campaign room, a parent chooses an approximate session length
(30, 45, or 60 minutes) and may add preferences up to 500 characters. The
server generates one episode and saves it as a campaign-scoped draft. A
running session must first be summarized. Parents can preview, edit,
regenerate, approve, activate, or discard a draft; editing and regeneration
return it to pending review. Only an approved draft can be activated.

The plan contains a premise and opening, 2-4 scenes, at least two solution
paths, 2-5 clues, a featured NPC, a gentle finale, a celebration or reward,
and whether it stands alone or advances a season arc. The featured NPC is
created in the same campaign when the plan is activated. Activation atomically
finishes the previously active quest, stores the complete plan and selected
session length on the new in-progress quest, and links the draft to that quest.
The confirmation explains that activating the next episode finishes any
currently in-progress quest.

## API and continuity

- `GET /api/campaigns/{campaignId}/adventure-drafts` lists that campaign's
  saved drafts.
- `POST /api/campaigns/{campaignId}/adventure-drafts` generates one draft from
  `sessionLengthMinutes` and optional `parentPreferences`.
- `GET /api/adventure-drafts/{draftId}` loads one saved draft.
- `POST /api/adventure-drafts/{draftId}/regenerate` replaces an unactivated
  draft with a new generated plan while retaining its requested length and
  preferences.
- `PUT /api/adventure-drafts/{draftId}` validates and saves parent edits.
- `POST /api/adventure-drafts/{draftId}/approve` and
  `POST /api/adventure-drafts/{draftId}/activate` enforce review before
  activation.
- `DELETE /api/adventure-drafts/{draftId}` discards an unactivated draft.

Generation runs on the API server with the existing Storykeeper AI provider
configuration. Its context is loaded from the requested campaign only:
campaign theme and tone, bible and current situation, up to 30 active facts
ordered by importance, the three most recent saved session summaries, existing
quest titles, NPC names, and locations. Proposed or resolved facts, other
campaigns, and browser-supplied story context are excluded. Parent preferences
are untrusted input and cannot override child-safety rules.

Generated and edited content is strict typed JSON. The API bounds the
session length and text, requires every plan component and a featured NPC used
in a scene, limits scene/path/clue counts, and rejects unsafe or mandatory
combat content. Active facts are included in the generation prompt to avoid
contradictions; parent review is required before activation. Provider failures
or invalid output do not replace an existing valid draft. The provider key
remains server-side.

Activated quest plans are included in the campaign-scoped context for live
Storykeeper turns. A later episode can be generated after session wrap-up,
using the refreshed facts, summaries, and quest history.

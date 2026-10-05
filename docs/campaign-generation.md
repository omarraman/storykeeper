# Campaign generation

## Campaign brief wizard

The parent-led wizard collects a title, genre, tone, optional story idea,
campaign length in sessions, session length in minutes, and optional
inclusions and exclusions. It saves a `CampaignBrief` draft independently of
campaigns; saving a brief does not create a playable campaign or start AI
generation. Saved briefs can be reopened and edited from the campaign library,
or discarded there. Leaving the editor without saving discards the in-progress
changes.

The API owns brief persistence. `GET /api/campaign-briefs` lists saved drafts,
`POST /api/campaign-briefs` creates one, `GET
/api/campaign-briefs/{briefId}` loads one, `PUT
/api/campaign-briefs/{briefId}` updates it, and `DELETE
/api/campaign-briefs/{briefId}` discards it. The API validates required text,
length bounds, list sizes, and optional idea size before writing to SQLite.
Inclusions and exclusions are stored as JSON lists in the brief row.

The server supplies the fixed safety boundaries on every new or updated brief:
no gore or cruelty, no mature themes, no permanent character death, no
mandatory tactical combat, and low-fright, age-appropriate content. These
cannot be removed by browser input. The wizard displays them separately from
the parent's optional exclusions.
Campaign briefs also store parent-selected fright and combat modes, excluded
topics or creatures, a narration word limit, and the session pacing target.
Brief creation and updates require the server-configured parent PIN.

Campaign length is bounded to 1-30 sessions and session length to 15-180
minutes. The wizard currently offers common choices of 3, 6, or 10 sessions
and 30, 45, 60, or 90 minutes. The campaign library also retains a separate
option to create an empty world without a brief.

## Generated campaign drafts

The API sends a saved brief to a server-side OpenAI-compatible Chat Completions
endpoint. Its system prompt targets a warm, funny, low-fright adventure for
children aged 8 and 10, and preserves player agency and forward-moving
setbacks. The API requests text output for compatibility with local providers,
then requires a JSON object deserializable to `CampaignDraftContent`; unknown
fields are rejected. A draft must contain a
title, premise, central mystery, 3-6 world rules, 3-5 distinct recurring NPCs,
3-6 distinct locations, and 1-4 adventure hooks. NPCs may name a location
only if it matches a location in the same draft. Text lengths are bounded.

Every generated or parent-edited draft is checked on the server before it is
returned or stored. All required safety declarations must be true, and a
conservative content check rejects explicit unsafe terms and parent-excluded
topics. The server prompt receives the safety profile. Invalid output is
not persisted or shown for review; a failed regeneration leaves the previous
valid draft intact. The parent can edit a valid draft, regenerate it, approve
it, and then activate it. Editing or regenerating clears approval. Only an
approved draft can be activated, and an activated draft cannot be changed or
activated again.

Activation creates a new isolated campaign and writes its settings, bible,
party, generated locations, recurring NPCs, and adventure hooks as available
quests in one database transaction. It records a version 1
`CampaignBibleVersion` snapshot. `GET /api/campaigns/{campaignId}/bible-versions`
returns the campaign-scoped version history. A discarded or invalid draft
never creates a playable campaign.

Draft workflow endpoints are `GET /api/campaign-drafts`,
`POST /api/campaign-briefs/{briefId}/drafts`,
`GET /api/campaign-drafts/{draftId}`,
`POST /api/campaign-drafts/{draftId}/regenerate`,
`PUT /api/campaign-drafts/{draftId}`,
`POST /api/campaign-drafts/{draftId}/approve`,
`POST /api/campaign-drafts/{draftId}/activate`, and
`DELETE /api/campaign-drafts/{draftId}`. The UI only exposes approval and
activation after parent review; the API also enforces draft validation,
approval state, and activation idempotency.

Configure generation on the API server with `Storykeeper__Ai__BaseUrl`,
`Storykeeper__Ai__Model`, and `Storykeeper__Ai__ApiKey`. The base URL is the
provider's API root (for example, ending in `/v1`); only HTTPS is accepted
except for loopback development endpoints. `Storykeeper__Ai__TimeoutSeconds`
sets a 10-180 second request timeout. The key is never returned to the browser.
Without these settings, brief saving and the rest of the app work normally,
but requesting generation returns a configuration error.

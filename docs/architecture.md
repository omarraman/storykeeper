# Architecture

## API and persistence

The Minimal API is the server-side authority for campaign data. EF Core maps
the domain model to SQLite through `StorykeeperDbContext`. The database is
created and brought up to the current schema with EF Core migrations when the
API starts.

`ICampaignService` provides campaign-level create, list, load, update,
complete, archive, and delete operations over `ICampaignRepository`.
`ICampaignEntityRepository` reads and adds campaign-owned records only when
the supplied campaign ID matches the record's owner. Entity foreign keys that
point to another campaign-owned record include the campaign ID, so SQLite
also rejects cross-campaign links.

The browser uses the Minimal API campaign endpoints; it never accesses the
database directly. `GET /api/campaigns` and `GET /api/campaigns/{id}` return
campaign summaries and the saved party, quest, and session state. The
selector creates campaigns with `POST /api/campaigns`, marks them completed
with `POST /api/campaigns/{id}/complete`, archives them with
`POST /api/campaigns/{id}/archive`, and permanently deletes them with
`DELETE /api/campaigns/{id}`. Request data is validated at the API boundary.
The selected campaign ID is a browser preference in local storage; campaign
facts and saved story state remain server-side in SQLite.

The game-rules service owns session resource resets and check resolution.
`POST /api/campaigns/{id}/sessions` starts a session and resets heroes to 3
hearts and 1 sparkle token. Checks are submitted to
`POST /api/campaigns/{id}/sessions/{sessionId}/heroes/{heroId}/checks`; the API
validates campaign, session, hero, strength, and token state, computes and
records the result in one transaction, and returns typed, child-readable
outcome data. The browser supplies the physical d20 value but does not
calculate or persist the outcome.

A running session cannot be silently closed by starting another one. The
parent saves a factual summary through the continuity API to end it. Parent
fact and summary corrections are stored in campaign-scoped append-only
continuity revisions, including prior and replacement content.

The tablet adventure screen consumes a typed `AdventureTurnClient` interface.
Its current `DemoAdventureTurnClient` supplies deterministic local story beats
and sample clues; fixture narration does not call an AI provider or persist
story state. Session creation/resume and real-hero d20 checks use the existing
server endpoints. When live gameplay narration is implemented, an HTTP turn
client can replace the demo adapter without coupling fixture content to React views.
If a campaign has no server-owned heroes, a visibly marked in-memory preview
party supports UI exploration only; its die entry does not resolve game rules
or persist resources.

Campaign-generation inputs are saved as independent `CampaignBrief` drafts
through `/api/campaign-briefs`. The API supports list, create, read, update,
and delete operations with server-side request validation and server-owned
safety boundaries. A brief is not a campaign and is not converted into
campaign state until a later generation and approval workflow.

## Storage configuration

`ConnectionStrings:Storykeeper` selects the SQLite connection string and can
be overridden with the standard `ConnectionStrings__Storykeeper` environment
variable. Local execution defaults to `storykeeper.db` in the API working
directory. Docker Compose mounts the database under `/data` in a named volume
so it survives container recreation.

## AI boundary

Campaign draft generation runs only on the API server through
`ICampaignDraftGenerator`, implemented with an OpenAI-compatible Chat
Completions client. `Storykeeper__Ai__BaseUrl`, `Storykeeper__Ai__Model`, and
`Storykeeper__Ai__ApiKey` configure that client; the API key is never serialized
to browser responses. The URL is restricted to HTTPS except loopback
development endpoints. Generation requests use JSON-object mode, strict
typed deserialization, and server-side structural and safety validation.
Invalid output is not persisted or displayed. A parent must approve a valid,
editable draft before activation.

Draft activation is performed by server-side logic in one database
transaction. It creates a new isolated campaign with generated settings,
campaign bible, locations, NPCs, and quests, and stores a versioned bible
snapshot. The browser cannot directly persist generated campaign state.

Live gameplay narration uses the server-side
`POST /api/campaigns/{campaignId}/actions` endpoint, detailed in
[AI Storykeeper](ai-storykeeper.md). The API loads a campaign-scoped context
and validates strict structured output before it is returned or applied.
Validated fact proposals are persisted as proposed facts by server-side logic;
AI output cannot write state directly or change quest status, hero resources,
rules, or dice outcomes. The server-side d20 rules engine remains authoritative.

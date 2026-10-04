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

Campaign rooms load chronological Storybook entries from
`GET /api/campaigns/{campaignId}/storybook`. `GET
/api/campaigns/{campaignId}/export` serializes an explicit campaign-data
allowlist as the versioned JSON archive documented in
[Storybook history and campaign export](storybook-and-campaign-export.md).
`POST /api/campaigns/import` validates the format, safety, lengths, IDs, and
campaign-scoped references before restoring all included rows in one
transaction. The archive excludes provider credentials and server
configuration; import is a separate copy, not a merge.

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
Campaigns with server-owned heroes use the server AI turn API and authoritative
session and d20 endpoints. The deterministic local demo adapter is retained
only for empty-world UI preview; its narration and die entry do not save state
or resolve rules. This keeps fixture content out of React views without
exposing provider credentials to the browser.

Campaign-generation inputs are saved as independent `CampaignBrief` drafts
through `/api/campaign-briefs`. The API supports list, create, read, update,
and delete operations with server-side request validation and server-owned
safety boundaries. A brief is not a campaign and is not converted into
campaign state until a later generation and approval workflow.

Parents can generate campaign-scoped adventure drafts from the campaign room.
The API builds continuity context from that campaign's bible, active facts,
recent saved session summaries, quests, NPCs, and locations. Reviewed,
approved plans activate transactionally as in-progress quests; each quest
stores its structured plan and approximate session length. See
[Next playable adventures](next-adventures.md) for the endpoints and lifecycle.

Campaign safety settings are stored with the campaign (and with its source
brief before activation). The API validates and supplies them to campaign
generation, adventure generation, and live narration; excluded content and
narration length are checked server-side before generated output is accepted.
Parent writes and live redirects require the API-configured
`Storykeeper__ParentPin`, sent in the request header and never persisted by the
browser. A session pause is saved server-side and blocks both narration and
check resolution.

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
For an activated adventure, that context includes the server-saved episode
plan so narration follows the reviewed opening, scenes, clues, and finale.
Validated fact proposals are persisted as proposed facts by server-side logic;
AI output cannot write state directly or change quest status, hero resources,
rules, or dice outcomes. The server-side d20 rules engine remains authoritative.

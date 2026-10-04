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

## Storage configuration

`ConnectionStrings:Storykeeper` selects the SQLite connection string and can
be overridden with the standard `ConnectionStrings__Storykeeper` environment
variable. Local execution defaults to `storykeeper.db` in the API working
directory. Docker Compose mounts the database under `/data` in a named volume
so it survives container recreation.

## AI boundary

AI integration is not implemented in this slice. Campaign facts are durable,
structured records distinct from session summaries or generated narration.
Future AI features must propose schema-validated changes through server-side
logic; AI output must not write to persistence directly or determine rules or
dice outcomes.

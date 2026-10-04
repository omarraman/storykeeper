# Development

## Prerequisites

- Node.js 22 or newer and npm
- .NET 10 SDK
- Docker Engine with Docker Compose v2 for the containerized setup

## Local API database

The API uses SQLite and applies committed EF Core migrations at startup. The
default connection string is `Data Source=storykeeper.db`; the database file
is ignored by Git. Override it with `ConnectionStrings__Storykeeper`, for
example `Data Source=C:\data\storykeeper.db` on Windows or
`Data Source=/data/storykeeper.db` on Linux.

The web app's campaign library uses the API to create, list, load, complete,
archive, and delete story worlds. The selected campaign ID is stored in the
browser so a refresh can reopen its saved campaign state; campaign data itself
is persisted by the API in SQLite. Campaign briefs and generated campaign
drafts are also saved by the API; provider credentials are read only by the
server and must not be placed in web configuration.

## AI campaign generation

Campaign generation is optional. Configure the API process with
`Storykeeper__Ai__BaseUrl`, `Storykeeper__Ai__Model`, and
`Storykeeper__Ai__ApiKey` to use an OpenAI-compatible Chat Completions service.
The base URL should include any API prefix such as `/v1`. `Storykeeper__Ai__TimeoutSeconds`
is optional and defaults to 60; valid values are 10-180. Non-loopback
endpoints must use HTTPS. Store the API key in a server environment or secret
manager, never in Vite variables or browser storage.

For Docker Compose, set `STORYKEEPER_AI_BASE_URL`, `STORYKEEPER_AI_MODEL`,
`STORYKEEPER_AI_API_KEY`, and optionally `STORYKEEPER_AI_TIMEOUT_SECONDS` in
the untracked `.env` file. Generation remains unavailable if the provider
settings are blank; no key is required to run the rest of the application.

Docker Compose uses the named `storykeeper-data` volume mounted at `/data`,
which keeps the database when the API container is recreated. Use
`docker compose down -v` only when intentionally deleting that local data.

## Tests and migrations

Run the web component tests with:

```sh
npm run test --workspace @storykeeper/web
```

Run backend tests with:

```sh
dotnet test Storykeeper.slnx
```

EF Core tools are used to create schema migrations. The `dotnet-ef` tool must
match the EF Core major version. To add a migration after changing the model:

```sh
dotnet ef migrations add <MigrationName> --project src/Storykeeper.Api/Storykeeper.Api.csproj --startup-project src/Storykeeper.Api/Storykeeper.Api.csproj --output-dir Data/Migrations
```

Commit generated migration files with the model change. Do not commit local
SQLite databases.

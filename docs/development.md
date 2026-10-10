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

For LM Studio's local server, use a base URL such as
`http://localhost:1234/v1` and the exact model ID shown by its `/v1/models`
endpoint. The API requests text output and parses and validates the expected
JSON shape server-side, which supports compatible servers that reject the
`json_object` response format. LM Studio may accept a placeholder API key such
as `local-dev` when authentication is disabled; the API still requires a
non-empty key setting.

## Optional narrated playback

Narrated playback is disabled by default and does not add a development or
test dependency on Piper or ElevenLabs. Set the server-side `TextToSpeech__*`
variables documented in [.env.example](../.env.example) to enable it. For
Docker Compose, use the corresponding `TEXT_TO_SPEECH_*` names in `.env`.
Choose exactly one provider; `Piper` calls the configured local HTTP endpoint,
while `ElevenLabs` calls its HTTPS API with `TextToSpeech__ApiKey`. The parent
then selects Off, On demand, or Autoplay after a new story beat for each
campaign. The audio cache defaults to `/data/tts-cache` in Compose; customize
it with `TEXT_TO_SPEECH_CACHE_DIRECTORY`. Cache files are server data and are
excluded from Git. See [Text-to-speech](text-to-speech.md) for exact provider
payloads and the campaign-scoped audio API.

For Docker Compose, set `STORYKEEPER_AI_BASE_URL`, `STORYKEEPER_AI_MODEL`,
`STORYKEEPER_AI_API_KEY`, and optionally `STORYKEEPER_AI_TIMEOUT_SECONDS` in
the untracked `.env` file. Generation remains unavailable if the provider
settings are blank; no key is required to run the rest of the application.

Configure `Storykeeper__ParentPin` on the API server (or
`STORYKEEPER_PARENT_PIN` in Docker Compose) to enable PIN-gated parent
settings and live controls. Use a private value of 6-64 characters. Parent
control writes fail closed when the setting is absent or too short. The PIN
is sent only in the `X-Parent-Pin` request header and is never stored by the
web app. Production deployments must serve the app over HTTPS. The repository's
`docker-compose.production.yml` uses Caddy to obtain and renew a certificate
for `DOMAIN`, requiring public DNS to point at the host and inbound ports 80
and 443 to be reachable. Set `ACME_EMAIL` for certificate notices. The
production web/API containers share one public origin; Caddy sends `/api`
requests to the API and serves the static PWA for all other requests. See
[PWA installation and delivery](pwa-delivery.md) for install and offline
behavior.

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

## Creating a Perplexity context archive

`Storykeeper.Archiver` creates a ZIP containing the solution files, source,
tests, documentation, and configuration needed to understand the codebase.
Run it from the repository root to create `Storykeeper-Perplexity.zip` beside
the repository:

```sh
dotnet run --project src/Storykeeper.Archiver/Storykeeper.Archiver.csproj
```

You can pass a source directory and an output ZIP path:

```sh
dotnet run --project src/Storykeeper.Archiver/Storykeeper.Archiver.csproj -- "C:\path\to\source" "C:\path\to\output.zip"
```

The archive preserves relative paths and skips Git and IDE metadata,
dependencies, `bin`, `obj`, frontend build output, DLLs, other compiled
binaries, local databases, and local environment files. `.env.example` is
included. Output defaults to a ZIP beside the source directory; the output
file itself is never added if an explicit output path is inside the source.

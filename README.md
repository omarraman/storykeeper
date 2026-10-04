# Storykeeper

A family-friendly, AI-guided tabletop adventure platform for ongoing story worlds.
Storykeeper lets children play warm, imaginative fantasy adventures on a tablet,
while a parent stays in control of safety, pacing, rules, and campaign creation.

## Prerequisites

- Node.js 22 or newer and npm
- .NET 10 SDK
- Docker Engine with Docker Compose v2 (for the containerized setup)

## Run locally

Install the frontend dependencies from the repository root:

```sh
npm install
```

In one terminal, start the API:

```sh
npm run dev:api
```

In another terminal, start the web app:

```sh
npm run dev:web
```

Open <http://localhost:5173>. The web app proxies `/api` requests to the API at
<http://localhost:5080>; its status in the header confirms the health check.
The campaign library can save and edit parent-authored campaign briefs,
generate and review child-safe campaign drafts, activate approved drafts as
isolated story worlds, create blank worlds, and organize campaigns. Reopening a
selected world restores its server-saved party, quests, and session summaries.

## Run with Docker Compose

```sh
cp .env.example .env
docker compose up --build
```

Open <http://localhost:5173>. The API is also available at
<http://localhost:5080/api/health>. To stop the services, press `Ctrl+C` and
run `docker compose down`.

`WEB_PORT` and `API_PORT` in `.env` can change the host ports. The compose
services use the API's internal Docker network address for web-to-API requests.

## Production HTTPS deployment

Point a public DNS name at the Docker host and allow inbound ports 80 and 443.
Set `DOMAIN` and `ACME_EMAIL` in `.env`, then start the production stack:

```sh
docker compose -f docker-compose.production.yml up --build -d
```

Caddy obtains and renews the HTTPS certificate. The static web app and API are
served on the same HTTPS origin; `/api` is reverse-proxied to the API, and the
API and web containers are not exposed directly to the internet. The API uses
the persistent `storykeeper-production-data` volume. Stop the stack with
`docker compose -f docker-compose.production.yml down`; do not add `-v` unless
you intend to delete its saved campaigns and certificates. Configure optional
AI settings and `STORYKEEPER_PARENT_PIN` in the same untracked `.env` file.

The PWA and service worker require HTTPS in production. Plain HTTP on
`localhost` is permitted for local development.

## Build and smoke test

Build both projects:

```sh
npm run build
```

Run the API persistence tests:

```sh
dotnet test Storykeeper.slnx
```

With the API running locally or through Compose, check its health endpoint:

```sh
curl --fail http://localhost:5080/api/health
```

The response should be `{"status":"Healthy"}`. The Vite development server
serves the web app and proxies its health request to the API.

## Configuration and secrets

Configuration uses environment variables. ASP.NET Core's standard environment
configuration provider reads API settings from `ASPNETCORE_*` and other
environment variables. Do not commit credentials; keep local values in an
untracked `.env` file or use your deployment platform's secret manager. The
checked-in `.env.example` contains non-secret port defaults and blank optional
AI settings. The API uses SQLite at `storykeeper.db` by default; set
`ConnectionStrings__Storykeeper` to change its connection string. Docker
Compose stores its database in the persistent `storykeeper-data` volume.
Campaign generation and live story narration use server-side
`Storykeeper__Ai__BaseUrl`, `Storykeeper__Ai__Model`, and
`Storykeeper__Ai__ApiKey` settings; the key is never sent to the browser.
Without them, other campaign features remain available while AI features
return a safe configuration error.

# PWA installation and delivery

Storykeeper is served as a tablet-first installable web app. Its web manifest
sets standalone display, a landscape orientation preference, app colors, and
the scalable Storykeeper icon. Android browsers that support installation can
show their native install prompt; the persistent **Install app** control also
explains how to use Chrome or Samsung Internet's **Install app** or **Add to
Home screen** browser-menu action.

## Offline app shell

The root-scoped service worker precaches the app entry page, manifest, icon,
and the JavaScript and CSS bundles referenced by the production entry page.
It uses network-first navigation with the cached entry page as fallback, and
caches additional same-origin Vite assets on first use. API requests and
campaign data are never cached: campaign loading, saving, and story play still
require the API connection. A previously loaded app can therefore open during
a brief connection interruption, but this is not offline gameplay.

Service workers and Android PWA installation require a secure context.
Production must use HTTPS; local development on `localhost` is allowed over
HTTP. The service worker file and app shell are served at the site root so
their scope covers the whole app.

## Production delivery

`docker-compose.production.yml` builds the web app as static assets and the API
as a .NET release image. Caddy is the only internet-facing service; it obtains
and renews HTTPS certificates, routes `/api` to the API, and routes other
requests to the web app. The API and web containers are private to the Compose
network, and the SQLite database and Caddy certificate state use named volumes.

Set these variables in an untracked `.env` file before starting the production
stack:

| Variable | Requirement | Purpose |
|---|---|---|
| `DOMAIN` | Required | Public DNS name for Storykeeper, pointing to the Docker host |
| `ACME_EMAIL` | Required | Contact address for certificate notices |
| `STORYKEEPER_PARENT_PIN` | Recommended | Private PIN for parent controls |
| `STORYKEEPER_AI_BASE_URL` | Optional | HTTPS OpenAI-compatible API base URL |
| `STORYKEEPER_AI_MODEL` | Optional | Model name for campaign generation and narration |
| `STORYKEEPER_AI_API_KEY` | Optional secret | Server-only provider credential; never add it to web build variables |
| `STORYKEEPER_AI_TEMPERATURE` | Optional | Sampling temperature for compatible providers; omitted for Anthropic |
| `STORYKEEPER_AI_TIMEOUT_SECONDS` | Optional | AI request timeout; defaults to 60 seconds |

Ensure public DNS is configured and inbound TCP ports 80 and 443 are reachable
before starting the stack. Do not expose the API or web container ports
directly, and do not delete the production named volumes during routine
updates.

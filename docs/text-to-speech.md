# Narrated text-to-speech playback

## Scope and gameplay fallback

Phase 1 adds optional narration playback for saved, server-approved StoryBeats.
The narration text remains the authoritative gameplay output and stays visible.
Players can always use typed actions, suggested choices, and physical d20
checks without audio. Playback does not start on initial page load.

Parents choose Off, On demand, or Autoplay after a new story beat in the
PIN-protected campaign controls. Off is the default. Autoplay applies only
after a new server StoryBeat arrives; it does not autoplay a client-assembled
campaign opening or a preview beat. Listen, Pause, Stop, Replay, and Mute
controls are keyboard accessible and tablet-sized. A new action, replacement
beat, or leaving the screen stops the current audio.
The parent settings also store an enable flag and selected provider. Provider
choices are limited to the single provider selected in server configuration;
the campaign setting must match it before audio can be requested.

Microphone capture, recording, and speech-to-text are deferred to Phase 2.
This change does not add or alter those flows.

## Server configuration

TTS options are server-only and use ASP.NET Core environment variable binding:

| Setting | Meaning |
|---|---|
| `TextToSpeech__Enabled` | Master server switch; defaults to `false`. |
| `TextToSpeech__Provider` | `Disabled` (default), `Piper`, or `ElevenLabs`. |
| `TextToSpeech__BaseUrl` | Exact Piper HTTP endpoint or provider API base URL. |
| `TextToSpeech__ApiKey` | Server-only ElevenLabs key; unused by Piper. |
| `TextToSpeech__Model` | Provider model ID; leave blank if the configured Piper service does not use one. |
| `TextToSpeech__NarratorVoice` | Required one server-configured narrator voice; no AI-selected or child-selected voices. |
| `TextToSpeech__OutputFormat` | Provider-compatible format; for example `wav` for Piper or `mp3_44100_128` for ElevenLabs. |
| `TextToSpeech__TimeoutSeconds` | Provider timeout, 1-180 seconds; defaults to 30. |
| `TextToSpeech__CacheEnabled` | Enables successful-audio disk caching; defaults to `true`. |
| `TextToSpeech__CacheDirectory` | Server cache path; defaults to `tts-cache` beside the API application. |

`.env.example` lists placeholder `TEXT_TO_SPEECH_*` variables. Docker Compose
maps these only into the API container and defaults the cache path to
`/data/tts-cache` on the persistent API data volume. The cache directory is
ignored by Git. Keep API keys in an untracked `.env` file or deployment secret
manager; do not place them in Vite variables, appsettings files, browser
storage, API responses, or campaign exports.

Selecting a provider is explicit. The default disabled provider returns a
typed unavailable result. Piper uses the configured URL as-is and sends a
JSON POST with `text`, `model`, `voice`, and `output_format`; the endpoint and
Piper service contract are deployment-owned, with no hard-coded image, port,
or voice model. Piper may use HTTP for a deployment-owned local service URL;
the URL is server configuration, never browser input, and must point only to
a trusted local service. ElevenLabs uses the configured base URL's
`/v1/text-to-speech/{voice}` route, an `xi-api-key` server header, model ID,
and output format. It requires an HTTPS base URL and does not send the key in
the request body. Selecting Piper never falls back to ElevenLabs, or vice
versa.

The parent controls display which provider the server selected, without
revealing credentials, and let the parent explicitly enable that provider for
the campaign. A campaign cannot enable a provider that has not been selected
and configured on the server.

## StoryBeat persistence and API

When live narration passes schema and safety validation, the API saves its
normalized narration and generated StoryBeat ID with the campaign and session
before returning it. The endpoint
`GET /api/campaigns/{campaignId}/sessions/{sessionId}/story-beats/{storyBeatId}/audio`
looks up this server-owned record using all three IDs and never accepts
browser-supplied text. Audio bytes are streamed with the provider's audio
content type. Requests are campaign-scoped and rate limited.

Disabled-by-server, disabled-by-parent, unconfigured, failed, and timed-out
playback return a typed JSON error (`type`, `code`, `message`, `retryable`)
with an appropriate HTTP status. The client announces the recoverable
message; it never hides or replaces the narration text.

The API saves audio only after successful synthesis. The cache key is a
SHA-256 digest over the narration text hash, selected provider, model, voice,
and output format. Provider failures and timeouts are not cached. Cached audio
stores its content type alongside the bytes.

## Testing

Automated tests use fake providers and stub HTTP handlers; they never call a
Piper, ElevenLabs, or other external service. Coverage includes disabled
results, successful synthesis, cache hits and key variation, timeout and
failure mapping, server-only ElevenLabs credentials, Piper endpoint
configuration, saved StoryBeat association, and browser text fallback.

# Storykeeper documentation index

## Purpose

Storykeeper is a family-friendly, AI-guided tabletop adventure platform.
It supports many independent campaign worlds—such as cozy fantasy, space,
pirates, mysteries, or dragon schools—through a reusable rules engine,
campaign data model, and AI Storykeeper.

The first target device is a Samsung Galaxy Tab running the app as an
installable web PWA.

## Current status

Early development.

Current implementation focus:

- Parent-led campaign brief wizard with saved, editable, discardable drafts and
  server-enforced child-safety boundaries.
- Structured AI campaign drafts with parent review, editing, approval,
  isolated activation, and versioned campaign bibles.
- Campaign-scoped next-adventure plans with selected pacing, parent review,
  editable regeneration, and activation into saved quests.
- Tablet-friendly campaign library with create, list, complete, archive,
  delete, and saved campaign resume.
- Installable landscape Android PWA with HTTPS production delivery and a
  cached app shell for brief connection interruptions.
- Tablet landscape adventure-play screen with suggested and free-text actions,
  physical-d20 entry, and server-backed AI narration for playable campaigns.
- Reusable campaign/domain model with SQLite persistence and campaign isolation.
- Server-authoritative, child-friendly d20 checks with session hearts, sparkle
  tokens, and forward-progress outcomes.
- Campaign-scoped continuity with parent-curated session summaries, reviewable
  facts, relevance-selected prompt context, and an edit audit trail.
- Chronological campaign storybooks with discoveries, hero milestones, rewards,
  and validated JSON backup/restore.
- PIN-gated parent safety settings and live story redirects, enforced by the
  API across generation and narration.
- Text-first, tablet-first play before optional voice interaction.

## Core principles

- Children make the choices; AI supports narration and improvisation.
- The server is authoritative for rules, dice, campaign facts, and saves.
- AI output is structured, validated, and never directly persists game state.
- Every campaign is isolated from every other campaign.
- Default content is warm, funny, adventurous, low-fright, and suitable for
  children aged 8 and 10.
- A failed roll must move the story forward rather than block it.

## Documentation map

| Document | Purpose | Status |
|---|---|---|
| [README.md](README.md) | Repository overview and local quick start | Planned |
| [Development guide](docs/development.md) | Setup, run, test, configuration, and deployment guidance | Available |
| [PWA installation and delivery](docs/pwa-delivery.md) | Android installation, app-shell caching, and production HTTPS delivery | Available |
| [Architecture](docs/architecture.md) | Web PWA, API, persistence, and AI integration boundaries | Available |
| [Domain model](docs/domain-model.md) | Campaigns, heroes, NPCs, facts, quests, sessions, and inventory | Available |
| [Game rules](docs/game-rules.md) | Child-friendly d20 rules, hearts, sparkle tokens, and consequences | Available |
| [Adventure play](docs/adventure-play.md) | Play screen, session wrap-up, parent continuity controls, typed turn-client boundary, and authoritative roll integration | Available |
| [Storybook history and campaign export](docs/storybook-and-campaign-export.md) | Campaign storybook history, versioned JSON archive format, and safe restore behavior | Available |
| [Campaign generation](docs/campaign-generation.md) | CampaignBrief wizard, validated generation, approval, activation, and bible versions | Available |
| [Next playable adventures](docs/next-adventures.md) | Session-length preferences, continuity-aware episode plans, parent review, and quest activation | Available |
| [AI Storykeeper](docs/ai-storykeeper.md) | Prompting, schemas, turn processing, validation, and relevance-selected continuity | Available |
| [Safety and parent controls](docs/safety-and-parent-controls.md) | Safety defaults, campaign settings, parent PIN, pacing, and live redirects | Available |
| [ADR directory](docs/decisions/) | Long-lived architecture decisions and their rationale | Planned |

## Issue roadmap

| Issue | Area |
|---:|---|
| #1 | Monorepo and local development foundation |
| #3 | Domain model and SQLite persistence |
| #4 | Campaign selector and save/resume |
| #5 | Campaign-generation wizard |
| #6 | Structured campaign-draft generation and approval |
| #7 | Child-friendly d20 rules engine |
| #8 | Tablet adventure-play screen |
| #9 | Secure AI Storykeeper turn API |
| #10 | Campaign continuity and end-of-session summaries |
| #11 | Next playable adventure generation |
| #12 | Parent controls and child-safety settings |
| #13 | Android PWA installation and delivery |
| #14 | Storybook history and campaign export |
| #15 | Optional voice input and narrated playback |

## Change protocol

When implementing an issue:

1. Read this file and the relevant linked documents.
2. Update the relevant topic documentation with durable implementation facts.
3. Update this index if a document, system capability, architecture decision,
   or roadmap entry changes.
4. Mention documentation changes in the pull request summary.

# Storybook history and campaign export

## Storybook

Each campaign room loads `GET /api/campaigns/{campaignId}/storybook`. Entries
are ordered by session number and show the saved title, start date, factual
summary, major discoveries, and hero achievements. New sessions take their
title from the active quest, or use `Adventure N` when there is no active
quest. Existing session records receive an `Adventure N` title during the
schema migration.

Major discoveries are reviewed campaign facts with importance 4 or 5 that have
a source session and are active, resolved, or superseded. Proposed and
discarded facts are not shown. Hero achievements are the saved strong
success check results, attributed to their hero. Rewards include campaign
reward records and celebration rewards from completed, activated adventure
plans. These are read-only history; game rules and session state remain
server-owned.

## Campaign archive format

`GET /api/campaigns/{campaignId}/export` downloads a UTF-8 JSON file. Its
version 1 envelope has `format: "storykeeper-campaign"`,
`schemaVersion: 1`, an `exportedAtUtc` timestamp, and campaign data grouped by
entity collection. It includes campaign settings and safety choices, the
current bible, party and heroes, inventory, locations, NPCs, quests and
adventure plans, sessions and summaries, check resolutions, facts, continuity
revisions, relationships, rewards, bible versions, and adventure drafts.
Campaign IDs and references are retained so foreign-key relationships can be
validated on restore. The separate campaign-brief and draft-authoring workflow
is not included; version snapshots retain their title and content, without a
link to an authoring draft.

Archives use camel-case JSON names and camel-case string enums. Imports reject
unknown fields, unsupported versions, malformed JSON, unsafe or overlong text,
invalid enum values, inconsistent saved roll outcomes, duplicate IDs, and
references outside the exported campaign. A valid archive is restored in one
database transaction. An archive
whose IDs already exist in the database is rejected with a conflict rather
than overwritten or partially merged.

The format is an allowlist of campaign data, not a database dump. It never
contains provider API credentials, server configuration, database connection
strings, or the parent PIN. `POST /api/campaigns/import` accepts the exported
JSON body and returns the restored campaign ID. Exporting and importing
creates a separate backup copy; it does not merge campaigns.

Ordinary campaign export and storybook output deliberately exclude full
narrator guides, including active, pending, and draft source text. No private
guide backup option is implemented in this version, so imports do not restore
guides. Parents who need to preserve authored source should keep their own
private copy.

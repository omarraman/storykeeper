# Storykeeper agent instructions

## Start here

Before making changes:

1. Read `index.md`.
2. Read the documentation linked from `index.md` that applies to the task.
3. Read the full GitHub issue, including its acceptance criteria and out-of-scope section.
4. Inspect the existing implementation before choosing an approach.
5. Prefer the smallest change that fully satisfies the issue.

`index.md` is the documentation entry point and must remain an accurate map of the project.

## Documentation is part of the work

For every issue:

1. Update the relevant documentation when a change affects:
  - Architecture or project structure.
  - Setup, configuration, environment variables, or deployment.
  - Data/domain model, persistence, migrations, or API contracts.
  - Game rules, safety boundaries, parent controls, or AI behaviour.
  - User-visible flows, screens, or campaign behaviour.
2. Update `index.md` when:
  - You add, remove, rename, or substantially change a documentation file.
  - You change a documented system capability or implementation status.
  - You add a new major subsystem or important project decision.
3. Do not make cosmetic `index.md` edits solely to satisfy this instruction.
   If no documentation is affected, state that in the pull request summary.

## Project principles

- Storykeeper is a family-friendly, AI-guided tabletop adventure platform,
  designed initially for children aged 8 and 10.
- The app is a tablet-first web PWA, initially targeted at a Samsung Galaxy Tab.
- The browser must never receive a long-lived AI provider API key.
- The server owns authoritative game state, rules, dice results, and persistence.
- AI may narrate and propose structured state changes, but must not directly
  persist state or decide roll results.
- Support multiple isolated campaigns. Never allow campaign facts, heroes,
  inventory, NPC relationships, or quests to leak between campaigns.
- Defaults must be warm, funny, adventurous, age-appropriate, and low-fright.
- Never introduce gore, cruelty, mature themes, permanent character death,
  or mandatory tactical combat.
- A failed roll must not create a dead end. Use progress with a complication,
  a new clue, a gentle setback, or another route forward.
- Preserve player agency: support free-text actions, not only prewritten choices.

## Engineering expectations

- Keep changes focused on the current issue.
- Follow existing naming, formatting, and project conventions.
- Add or update automated tests for changed backend logic and rules.
- Validate inputs at the API boundary.
- Use typed, schema-validated contracts for AI-generated data.
- Do not commit secrets, production credentials, local databases, or generated
  build artifacts.
- Use environment variables for configuration and document every new variable.
- Run the relevant build, lint, and test commands before finishing.
- Report what was changed, tests run, documentation updated, and any remaining
  assumptions or limitations in the pull request summary.

## Documentation style

- Use clear Markdown with concise headings.
- Keep durable facts in topic documents under `docs/`.
- Keep `index.md` concise: it should point to the right documents, not duplicate them.
- Record significant, long-lived architectural choices as ADRs in `docs/decisions/`.
- Use file names in `kebab-case.md`.

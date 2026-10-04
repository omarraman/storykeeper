# Child-friendly d20 rules

The API is the authority for check calculations, recorded outcomes, and
session resources. The browser supplies a physical d20 result and displays the
typed response. AI may narrate that response but cannot change the roll,
bonuses, target, outcome, hearts, tokens, or persisted check record.

## When to roll

Roll only when the outcome is uncertain, both success and failure would be
interesting, and the action has a meaningful story consequence. Ordinary,
safe, creative, kind, and obvious actions do not need a roll. Reward creative
ideas by lowering the difficulty, using an applicable listed strength, finding
a clue, or avoiding a roll.

## Check calculation

The server calculates:

```text
total = d20 + applicable strength bonus + optional sparkle bonus
```

The d20 result must be between 1 and 20. Easy, Tricky, and Heroic targets are
8, 12, and 16. At most one listed hero strength may apply, adding +2; strengths
do not stack. A sparkle token adds +3. Natural 1 and 20 have no automatic
mechanical effect.

A player may spend their own available token after seeing their die result and
before the outcome is resolved. The UI should ask when the roll plus any
strength bonus is below the target and a token is available. The check request
must state whether the token is spent; the server validates eligibility and
records the spend atomically with the outcome. Tokens cannot be shared or
refunded and cannot lower difficulty.
The play screen offers a listed hero strength and only presents the sparkle
option when the entered die plus any selected strength is below the target.
These UI hints do not resolve the check; the server remains authoritative and
rejects an ineligible token request.

## Outcome bands

The bands are evaluated from the final total, after bonuses:

| Result | Outcome | Guidance |
|---|---|---|
| Target + 5 or more | Strong success | Achieve the goal and add a helpful detail, clue, advantage, or positive narrative effect. Do not invent permanent mechanical bonuses. |
| Target or more | Success | Achieve the attempted goal. |
| 1-2 below target | Success with complication | Make meaningful progress or achieve the main goal, with a gentle, reversible complication. |
| 3 or more below target | Setback with progress | Still reveal a clue, partial result, new route, or changed situation, alongside a gentle setback. Never create a dead end. |

Complications may include taking longer, making a funny noise, dropping an
item nearby, attracting attention, using a simple resource, getting muddy, or
creating a small problem to solve. Never force loss, permanent item loss,
permanent harm, or character death.

## Hearts and sessions

Starting a session resets every hero to 3 hearts and 1 sparkle token. A check
can remove at most one heart. Only a risky action with a setback can cost a
heart; social, exploration, puzzle, and creative checks normally use a
non-heart complication. A hero at 0 hearts is tired, tangled, temporarily
separated, or needs help, but can still act and cannot lose further hearts.
Starting a new session is the only recovery rule in this version; mid-session
healing is not implemented.

A parent can pause an active session. The API rejects both narration actions
and check resolution while paused; only a PIN-authorized parent resume or
session wrap-up can continue the flow.

## API and persistence

- `POST /api/campaigns/{campaignId}/sessions` starts the next session and
  resets campaign heroes' resources.
- `POST /api/campaigns/{campaignId}/sessions/{sessionId}/heroes/{heroId}/checks`
  accepts a physical die result, difficulty, optional listed strength,
  sparkle-token choice, and a risky-action flag. It returns the roll, bonuses,
  total, target, outcome band, progress requirement, consequence category,
  child-readable message, and before/after resources.
- In Development only, `POST
  /api/campaigns/{campaignId}/sessions/{sessionId}/heroes/{heroId}/checks/test`
  asks the server to generate a test d20 value and resolves it through the
  same rules service. The response identifies this as a `ServerTest` roll.
- Each resolved check is stored against its campaign, session, and hero.
  Campaign-scoped foreign keys prevent cross-campaign session or hero use.
  Heart and token updates and the check record are committed in one database
  transaction.

The initial version does not include tactical combat, enemy rules, damage
values beyond hearts, advantage/disadvantage, stacked modifiers, class
systems, spells, or a full D&D ruleset.

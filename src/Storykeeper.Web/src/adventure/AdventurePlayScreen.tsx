import { useState } from 'react'
import type {
  AdventureCampaign,
  AdventureTurnClient,
  AdventureTurnResponse,
  CheckDifficulty,
  HeroStatus,
  StoryBeat,
} from './contracts'
import './AdventurePlayScreen.css'

const difficultyTargets: Record<CheckDifficulty, number> = { Easy: 8, Tricky: 12, Heroic: 16 }

interface AdventurePlayScreenProps {
  campaign: AdventureCampaign
  sessionId: string
  client: AdventureTurnClient
  onCampaignChange: (campaign: AdventureCampaign) => void
  onBack: () => void
}

export function AdventurePlayScreen({
  campaign,
  sessionId,
  client,
  onCampaignChange,
  onBack,
}: AdventurePlayScreenProps) {
  const [initialTurn] = useState(() => client.openTurn(campaign, sessionId))
  const [turn, setTurn] = useState<AdventureTurnResponse>(initialTurn)
  const [storyBeat, setStoryBeat] = useState<StoryBeat>(
    initialTurn.type === 'story_beat' ? initialTurn.storyBeat : openingBeatFallback,
  )
  const [state, setState] = useState(
    initialTurn.type === 'story_beat' ? initialTurn.state : emptyState(campaign),
  )
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [lastAction, setLastAction] = useState<{ action: string; choiceId: string | null; heroId: string } | null>(null)
  const [retryableAction, setRetryableAction] = useState(false)
  const [selectedHeroId, setSelectedHeroId] = useState(campaign.heroes[0]?.id ?? 'demo-hero-scout')
  const [showFreeText, setShowFreeText] = useState(false)
  const [actionText, setActionText] = useState('')
  const [roll, setRoll] = useState('')
  const [strength, setStrength] = useState('')
  const [spendSparkleToken, setSpendSparkleToken] = useState(false)

  const pendingRoll = turn.type === 'roll_required' ? turn.rollRequired : null
  const selectedHero = state.heroes.find((hero) => hero.id === selectedHeroId) ?? state.heroes[0]
  const rollValue = Number(roll)
  const requestedStrength = strength || pendingRoll?.strength || ''
  const selectedStrength = selectedHero?.strengths.find((item) => item.toLowerCase() === requestedStrength.toLowerCase()) ?? ''
  const strengthBonus = selectedStrength ? 2 : 0
  const sparkleTokenAvailable = Boolean(
    pendingRoll &&
    selectedHero &&
    state.mode === 'campaign' &&
    selectedHero.sparkleTokens > 0 &&
    roll.trim() !== '' &&
    Number.isInteger(rollValue) &&
    rollValue >= 1 &&
    rollValue <= 20 &&
    rollValue + strengthBonus < difficultyTargets[pendingRoll.difficulty],
  )

  const submitAction = async (action: string, choiceId: string | null): Promise<boolean> => {
    if (!selectedHero) return false
    const request = { action, choiceId, heroId: selectedHero.id }
    setLastAction(request)
    setRetryableAction(false)
    setBusy(true)
    setError('')
    setSpendSparkleToken(false)
    setStrength('')
    try {
      const response = await client.submitAction({ ...request, campaign, sessionId })
      applyTurn(response)
      if (response.type === 'error') setRetryableAction(response.retryable)
      else {
        setRetryableAction(false)
        setShowFreeText(false)
      }
      return response.type !== 'error'
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'The story did not answer. Please try again.')
      setRetryableAction(true)
      return false
    } finally {
      setBusy(false)
    }
  }

  const submitFreeText = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const action = actionText.trim()
    if (!action) {
      setError('Type an idea before sending it to the Storykeeper.')
      return
    }

    if (await submitAction(action, null)) setActionText('')
  }

  const submitRoll = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!pendingRoll || !selectedHero) return

    if (!Number.isInteger(rollValue) || rollValue < 1 || rollValue > 20) {
      setError('Enter the number showing on your d20, from 1 to 20.')
      return
    }
    if (spendSparkleToken && !sparkleTokenAvailable) {
      setError('A sparkle token can only be spent when your roll is below the target.')
      return
    }

    setBusy(true)
    setError('')
    try {
      const result = await client.resolveRoll({
        campaign,
        sessionId,
        heroId: selectedHero.id,
        roll: rollValue,
        difficulty: pendingRoll.difficulty,
        strength: selectedStrength || null,
        spendSparkleToken,
        risky: pendingRoll.risky,
      })
      onCampaignChange(result.campaign)
      applyTurn(result.turn)
      setRoll('')
      setStrength('')
      setSpendSparkleToken(false)
    } catch (rollError) {
      setError(rollError instanceof Error ? rollError.message : 'The check could not be saved. Refresh party status before continuing.')
    } finally {
      setBusy(false)
    }
  }

  const retryLastAction = async () => {
    if (lastAction) await submitAction(lastAction.action, lastAction.choiceId)
  }

  const refreshPartyStatus = async () => {
    setBusy(true)
    setError('')
    try {
      const refreshedCampaign = await client.refreshCampaign(campaign.id)
      onCampaignChange(refreshedCampaign)
      applyTurn(client.openTurn(refreshedCampaign, sessionId))
      setRoll('')
      setStrength('')
      setSpendSparkleToken(false)
    } catch (refreshError) {
      setError(refreshError instanceof Error ? refreshError.message : 'Could not refresh saved party status.')
    } finally {
      setBusy(false)
    }
  }

  const applyTurn = (response: AdventureTurnResponse) => {
    setTurn(response)
    if (response.type === 'story_beat') {
      setStoryBeat(response.storyBeat)
      setState(response.state)
      setError('')
    } else if (response.type === 'error') {
      setError(response.message)
      setRetryableAction(response.retryable)
    } else {
      setError('')
      setRetryableAction(false)
    }
  }

  return (
    <section className="adventure-screen" aria-labelledby="adventure-title">
      <div className="adventure-screen-heading">
        <button className="text-button" onClick={onBack} disabled={busy}>
          <span aria-hidden="true">←</span> Story world
        </button>
        <div>
          <p className="eyebrow">Adventure {campaign.latestSession?.sessionNumber ?? 1}</p>
          <h1 id="adventure-title">{campaign.name}</h1>
        </div>
        {state.mode === 'preview' && <span className="demo-badge">Preview · not saved</span>}
      </div>

      <div className="adventure-layout">
        <main className="adventure-scene" aria-labelledby="scene-heading">
          <p className="card-kicker" id="scene-heading">THE STORY SO FAR</p>
          {storyBeat.speaker && <p className="adventure-speaker">{storyBeat.speaker}</p>}
          <p className="adventure-narration" aria-live="polite">{storyBeat.narration}</p>
          {turn.type === 'story_beat' && turn.checkResolution && (
            <div className={`check-result ${turn.checkResolution.source === 'preview' ? 'demo-result' : ''}`} role="status">
              <strong>{turn.checkResolution.source === 'preview' ? `Preview die: ${turn.checkResolution.roll}` : `${turn.checkResolution.outcome} · ${turn.checkResolution.total}`}</strong>
              <span>{turn.checkResolution.childReadableMessage}</span>
              {turn.checkResolution.source === 'server' && <span>{turn.checkResolution.heartsAfter} hearts · {turn.checkResolution.sparkleTokensAfter} sparkle tokens</span>}
            </div>
          )}
          {state.currentQuest && (
            <div className="adventure-current-quest">
              <span className="card-kicker">CURRENT QUEST</span>
              <strong>{state.currentQuest.title}</strong>
              <span>{state.currentQuest.description}</span>
            </div>
          )}
          {!state.currentQuest && (
            <div className="adventure-current-quest">
              <span className="card-kicker">CURRENT QUEST</span>
              <strong>No active quest yet</strong>
              <span>No quest has been added to this story world yet.</span>
            </div>
          )}
        </main>

        <aside className="adventure-sidebar" aria-label="Party and discoveries">
          <section className="adventure-panel" aria-labelledby="party-heading">
            <p className="card-kicker">YOUR PARTY</p>
            <h2 id="party-heading">Choose a hero</h2>
            {state.heroes.length ? (
              <div className="adventure-heroes">
                {state.heroes.map((hero) => (
                  <HeroCard
                    hero={hero}
                    key={hero.id}
                    preview={state.mode === 'preview'}
                    selected={hero.id === selectedHero?.id}
                    onSelect={() => { setSelectedHeroId(hero.id); setStrength(''); setSpendSparkleToken(false) }}
                  />
                ))}
              </div>
            ) : <p className="adventure-muted">Your heroes will appear here.</p>}
          </section>

          <section className="adventure-panel" aria-labelledby="clues-heading">
            <p className="card-kicker">THINGS YOU HAVE NOTICED</p>
            <h2 id="clues-heading">Clues</h2>
            {state.clues.length
              ? <ul className="adventure-clues">{state.clues.map((clue) => <li key={clue.id}>{clue.text}</li>)}</ul>
              : <p className="adventure-muted">No clues yet. Keep exploring!</p>}
          </section>

          <details className="adventure-panel inventory-panel">
            <summary>Inventory</summary>
            {state.heroes.some((hero) => hero.inventory.length) ? (
              <ul className="adventure-inventory">
                {state.heroes.flatMap((hero) => hero.inventory.map((item) => (
                  <li key={`${hero.id}-${item.name}`}>
                    <strong>{item.name}</strong> <span>×{item.quantity}</span>
                    <small>{hero.name} · {item.description}</small>
                  </li>
                )))}
              </ul>
            ) : <p className="adventure-muted">Your pack is ready for its first treasure.</p>}
          </details>
        </aside>
      </div>

      <section className="adventure-actions" aria-labelledby="actions-heading">
        <div className="adventure-actions-heading">
          <div>
            <p className="card-kicker">WHAT WOULD YOU LIKE TO DO?</p>
            <h2 id="actions-heading">{pendingRoll ? 'Roll your d20' : 'Choose a path, or make up your own'}</h2>
          </div>
          {state.heroes.length > 1 && (
            <label className="hero-picker">
              Hero
              <select value={selectedHero?.id ?? ''} onChange={(event) => {
                setSelectedHeroId(event.target.value)
                setStrength('')
                setSpendSparkleToken(false)
              }}>
                {state.heroes.map((hero) => <option key={hero.id} value={hero.id}>{hero.name}</option>)}
              </select>
            </label>
          )}
        </div>

        {pendingRoll ? (
          <form className="d20-form" onSubmit={(event) => void submitRoll(event)}>
            <p>{pendingRoll.prompt}</p>
            <label htmlFor="physical-d20">Your physical d20 result</label>
            <input
              id="physical-d20"
              type="number"
              min={1}
              max={20}
              step={1}
              required
              inputMode="numeric"
              value={roll}
              onChange={(event) => { setRoll(event.target.value); setSpendSparkleToken(false) }}
              disabled={busy}
            />
            {selectedHero && selectedHero.strengths.length > 0 && state.mode === 'campaign' && (
              <label className="roll-strength-picker">
                Hero strength
                <select value={selectedStrength} onChange={(event) => { setStrength(event.target.value); setSpendSparkleToken(false) }} disabled={busy}>
                  <option value="">No strength</option>
                  {selectedHero.strengths.map((item) => <option key={item} value={item}>{item} (+2)</option>)}
                </select>
              </label>
            )}
            {sparkleTokenAvailable && (
              <label className="sparkle-option">
                <input type="checkbox" checked={spendSparkleToken} onChange={(event) => setSpendSparkleToken(event.target.checked)} disabled={busy} />
                Spend 1 sparkle token
              </label>
            )}
            <button className="button button-primary" type="submit" disabled={busy || !roll}>
              {busy ? 'Saving check…' : 'Resolve check'}
            </button>
            <button
              className="button button-secondary free-text-toggle"
              type="button"
              disabled={busy}
              onClick={() => {
                setTurn({ type: 'story_beat', storyBeat, state })
                setShowFreeText(true)
                setError('')
                setRoll('')
                setStrength('')
                setSpendSparkleToken(false)
              }}
            >
              We have another idea!
            </button>
          </form>
        ) : (
          <>
            <div className="suggested-choices">
              {storyBeat.suggestedChoices.slice(0, 4).map((choice) => (
                <button
                  className="button button-secondary suggestion-button"
                  key={choice.id}
                  disabled={busy || !selectedHero}
                  onClick={() => void submitAction(choice.text, choice.id)}
                >
                  {choice.text}
                </button>
              ))}
              <button
                className="button button-secondary free-text-toggle"
                aria-expanded={showFreeText}
                onClick={() => { setShowFreeText((visible) => !visible); setError('') }}
                disabled={busy}
              >
                We have another idea!
              </button>
            </div>
            {showFreeText && (
              <form className="free-text-form" onSubmit={(event) => void submitFreeText(event)}>
                <label htmlFor="free-text-action">Tell the Storykeeper your idea</label>
                <textarea
                  id="free-text-action"
                  rows={2}
                  maxLength={500}
                  value={actionText}
                  onChange={(event) => setActionText(event.target.value)}
                  disabled={busy}
                  autoFocus
                />
                <button className="button button-primary" type="submit" disabled={busy || !actionText.trim()}>
                  {busy ? 'Thinking…' : 'Try our idea'}
                </button>
              </form>
            )}
          </>
        )}

        {error && (
          <div className="adventure-error" role="alert">
            <span>{error}</span>
            {lastAction && retryableAction && turn.type !== 'roll_required' && (
              <button className="button button-secondary" onClick={() => void retryLastAction()} disabled={busy}>Retry story action</button>
            )}
            {turn.type === 'roll_required' && (
              <button className="button button-secondary" onClick={() => void refreshPartyStatus()} disabled={busy}>Refresh saved party status</button>
            )}
          </div>
        )}
        {busy && <p className="adventure-loading" role="status">The story is catching up…</p>}
      </section>
    </section>
  )
}

function HeroCard({ hero, preview, selected, onSelect }: { hero: HeroStatus; preview: boolean; selected: boolean; onSelect: () => void }) {
  return (
    <button className={`adventure-hero ${selected ? 'selected' : ''}`} onClick={onSelect} aria-pressed={selected}>
      <span className="adventure-hero-avatar" aria-hidden="true">{hero.name.slice(0, 1).toUpperCase()}</span>
      <span className="adventure-hero-details">
        <strong>{hero.name}</strong>
        <span>{hero.role}</span>
        <span>{'♥ '.repeat(hero.hearts)}{hero.hearts === 0 ? 'Tired, but still part of the adventure' : `${hero.hearts} hearts`} · ✦ {hero.sparkleTokens} sparkle</span>
      </span>
      {preview && <span className="demo-hero-label">Preview</span>}
    </button>
  )
}

function emptyState(campaign: AdventureCampaign) {
  return {
    mode: 'campaign' as const,
    heroes: campaign.heroes,
    currentQuest: campaign.currentQuest,
    clues: [],
  }
}

const openingBeatFallback: StoryBeat = {
  id: 'adventure-loading',
  narration: 'Getting the adventure ready…',
  speaker: null,
  suggestedChoices: [],
}

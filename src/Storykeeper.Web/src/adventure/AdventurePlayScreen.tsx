import { useEffect, useRef, useState } from 'react'
import type {
  AdventureCampaign,
  AdventureTurnClient,
  AdventureTurnResponse,
  CheckDifficulty,
  HeroStatus,
  StoryBeat,
} from './contracts'
import { campaignContinuityClient, type CampaignContinuityClient } from './continuityClient'
import { ParentControlsPanel } from './ParentControlsPanel'
import { useVoiceInput } from './voiceInput'
import { fetchNarrationAudio, isAbortError } from './narrationAudio'
import './AdventurePlayScreen.css'

const difficultyTargets: Record<CheckDifficulty, number> = { Easy: 8, Tricky: 12, Heroic: 16 }

interface AdventurePlayScreenProps {
  campaign: AdventureCampaign
  sessionId: string
  client: AdventureTurnClient
  continuityClient?: CampaignContinuityClient
  onCampaignChange: (campaign: AdventureCampaign) => void
  onBack: () => void
}

export function AdventurePlayScreen({
  campaign,
  sessionId,
  client,
  continuityClient = campaignContinuityClient,
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
  const [sessionSummary, setSessionSummary] = useState('')
  const [sessionParentPin, setSessionParentPin] = useState('')
  const [savingSummary, setSavingSummary] = useState(false)
  const [summaryError, setSummaryError] = useState('')
  const [showWrapUp, setShowWrapUp] = useState(false)
  const [storyPaused, setStoryPaused] = useState(campaign.latestSession?.isPaused ?? false)
  const [narrationState, setNarrationState] = useState<'idle' | 'loading' | 'speaking' | 'paused'>('idle')
  const [playbackMessage, setPlaybackMessage] = useState('')
  const [narrationMuted, setNarrationMuted] = useState(false)
  const audioRef = useRef<HTMLAudioElement | null>(null)
  const audioUrlRef = useRef<string | null>(null)
  const audioRequestRef = useRef<AbortController | null>(null)
  const previousStoryBeatIdRef = useRef(storyBeat.id)

  const voiceEnabled = state.mode === 'campaign' && (campaign.safetySettings?.voiceEnabled ?? false)
  const narrationPlayback = campaign.safetySettings?.narrationPlayback ?? 'off'
  const narrationPlaybackEnabled = state.mode === 'campaign' &&
    (campaign.safetySettings?.textToSpeechEnabled ?? false) &&
    campaign.safetySettings?.narrationProvider !== 'disabled' &&
    narrationPlayback !== 'off'
  const voiceInput = useVoiceInput(voiceEnabled, setActionText)
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

  const stopNarration = () => {
    audioRequestRef.current?.abort()
    audioRequestRef.current = null
    const audio = audioRef.current
    if (audio) {
      audio.pause()
      audio.currentTime = 0
    }
    setNarrationState('idle')
  }

  const clearNarration = () => {
    stopNarration()
    audioRef.current?.removeAttribute('src')
    if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current)
    audioUrlRef.current = null
    setPlaybackMessage('')
  }

  const playNarration = async (replay = false) => {
    const audio = audioRef.current
    if (!audio) return
    setPlaybackMessage('')

    if (!audio.src) {
      if (!/^(?:[0-9a-f]{32}|[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12})$/i.test(storyBeat.id)) {
        setPlaybackMessage('Narrated playback is not available for this opening scene. The story text is still here to read.')
        return
      }

      const controller = new AbortController()
      audioRequestRef.current = controller
      setNarrationState('loading')
      try {
        const blob = await fetchNarrationAudio(campaign.id, sessionId, storyBeat.id, controller.signal)
        if (controller.signal.aborted) return
        const url = URL.createObjectURL(blob)
        if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current)
        audioUrlRef.current = url
        audio.src = url
        audio.load()
      } catch (loadError) {
        if (!isAbortError(loadError)) {
          setPlaybackMessage(loadError instanceof Error
            ? `${loadError.message} The story text is still here to read.`
            : 'Narrated playback could not be loaded. The story text is still here to read.')
        }
        setNarrationState('idle')
        return
      } finally {
        if (audioRequestRef.current === controller) audioRequestRef.current = null
      }
    }

    if (replay) audio.currentTime = 0
    try {
      await audio.play()
      setNarrationState('speaking')
    } catch {
      setNarrationState('idle')
      setPlaybackMessage('Narrated playback could not start in this browser. The story text is still here to read.')
    }
  }

  const pauseNarration = () => {
    audioRef.current?.pause()
    setNarrationState('paused')
  }

  const toggleNarrationMute = () => {
    const muted = !narrationMuted
    setNarrationMuted(muted)
    if (audioRef.current) audioRef.current.muted = muted
  }

  useEffect(() => {
    if (previousStoryBeatIdRef.current === storyBeat.id) return
    previousStoryBeatIdRef.current = storyBeat.id
    clearNarration()
    if (narrationPlayback === 'autoplayAfterNewStoryBeat') {
      void playNarration()
    }
  }, [storyBeat.id, narrationPlayback])

  useEffect(() => () => {
    audioRequestRef.current?.abort()
    audioRef.current?.pause()
    if (audioUrlRef.current) URL.revokeObjectURL(audioUrlRef.current)
  }, [])

  const handleAudioEnded = () => {
    setNarrationState('idle')
  }

  const handleAudioError = () => {
    setNarrationState('idle')
    setPlaybackMessage('Narrated playback stopped. The story text is still here to read.')
  }

  const onNarrationSettingOff = !narrationPlaybackEnabled
  useEffect(() => {
    if (onNarrationSettingOff) stopNarration()
  }, [onNarrationSettingOff])

  /*
   * Keep the opening narration text-first: only IDs issued by the server for
   * validated StoryBeats can request audio.
   */
  const renderNarrationControls = () => {
    if (!narrationPlaybackEnabled) return null
    return (
      <div className="narration-voice-controls" aria-label="Narration playback controls">
        <button type="button" className="button button-secondary" onClick={() => void playNarration()}
          disabled={narrationState === 'loading'} aria-label="Listen to narration">
          {narrationState === 'loading' ? 'Loading audio…' : 'Listen'}
        </button>
        <button type="button" className="button button-secondary" onClick={pauseNarration}
          disabled={narrationState !== 'speaking'} aria-label="Pause narration">
          Pause
        </button>
        <button type="button" className="button button-secondary" onClick={stopNarration}
          disabled={narrationState === 'idle'} aria-label="Stop narration">
          Stop
        </button>
        <button type="button" className="button button-secondary" onClick={() => void playNarration(true)}
          disabled={narrationState === 'loading' || !audioRef.current?.src} aria-label="Replay narration">
          Replay
        </button>
        <button type="button" className="button button-secondary" onClick={toggleNarrationMute}
          aria-pressed={narrationMuted} aria-label={narrationMuted ? 'Unmute narration' : 'Mute narration'}>
          {narrationMuted ? 'Unmute' : 'Mute'}
        </button>
        {playbackMessage && <span role="status">{playbackMessage}</span>}
        <audio ref={audioRef} muted={narrationMuted} onEnded={handleAudioEnded} onError={handleAudioError} />
      </div>
    )
  }

  const submitAction = async (action: string, choiceId: string | null): Promise<boolean> => {
    if (storyPaused) {
      setError('The story is paused by a parent.')
      return false
    }

    if (!selectedHero) return false
    stopNarration()
    voiceInput.stopListening()
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
    if (storyPaused) {
      setError('The story is paused by a parent.')
      return
    }

    if (!pendingRoll || !selectedHero) return

    if (!Number.isInteger(rollValue) || rollValue < 1 || rollValue > 20) {
      setError('Enter the number showing on your d20, from 1 to 20.')
      return
    }
    if (spendSparkleToken && !sparkleTokenAvailable) {
      setError('A sparkle token can only be spent when your roll is below the target.')
      return
    }

    stopNarration()
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

  const finishSession = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const summary = sessionSummary.trim()
    if (!summary) {
      setSummaryError('Add a short factual recap before ending the adventure.')
      return
    }
    if (!sessionParentPin.trim()) {
      setSummaryError('Enter the parent PIN before ending the adventure.')
      return
    }

    setSavingSummary(true)
    setSummaryError('')
    try {
      const updatedCampaign = await continuityClient.endSession({
        campaignId: campaign.id,
        sessionId,
        summary,
        parentPin: sessionParentPin,
      })
      onCampaignChange(updatedCampaign)
      onBack()
    } catch (saveError) {
      setSummaryError(saveError instanceof Error ? saveError.message : 'The adventure could not be ended.')
    } finally {
      setSavingSummary(false)
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

      {state.mode === 'campaign' && (
        <ParentControlsPanel
          campaignId={campaign.id}
          settings={campaign.safetySettings ?? {
            fearLevel: 'low',
            combatMode: 'avoid',
            voiceEnabled: false,
            textToSpeechEnabled: false,
            narrationProvider: 'disabled',
            narrationPlayback: 'off',
            excludedContent: [],
            maxNarrationWords: 120,
            sessionLengthMinutes: 45,
          }}
          sessionId={sessionId}
          isPaused={storyPaused}
          onSettingsSaved={(safetySettings) => onCampaignChange({ ...campaign, safetySettings })}
          onPausedChange={setStoryPaused}
          onEndSessionRequested={() => setShowWrapUp(true)}
        />
      )}
      {storyPaused && (
        <p className="adventure-paused" role="status">
          The story is paused. A grown-up can resume it or write a recap to end this session.
        </p>
      )}

      <div className="adventure-layout">
        <main className="adventure-scene" aria-labelledby="scene-heading">
          <p className="card-kicker" id="scene-heading">
            {state.mode === 'preview' ? 'DEMO PREVIEW - NOT SAVED' : 'THE STORY SO FAR'}
          </p>
          {storyBeat.speaker && <p className="adventure-speaker">{storyBeat.speaker}</p>}
          <p className="adventure-narration" aria-live="polite">{storyBeat.narration}</p>
          {renderNarrationControls()}
          {storyBeat.npcDialogue?.map((line, index) => (
            <p className="adventure-speaker" key={`${line.npcName}-${index}`}>
              <strong>{line.npcName}:</strong> {line.text}
            </p>
          ))}
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
              {state.currentQuest.sessionLengthMinutes && <span>About {state.currentQuest.sessionLengthMinutes} minutes</span>}
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
            {state.heroes.length === 1 && (
              <p className="adventure-muted">
                {state.mode === 'preview'
                  ? 'This demo hero is already selected for the preview.'
                  : `${selectedHero?.name} is your only available hero and is already selected.`}
              </p>
            )}
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
            <button className="button button-primary" type="submit" disabled={busy || storyPaused || !roll}>
              {busy ? 'Saving check…' : 'Resolve check'}
            </button>
            <button
              className="button button-secondary free-text-toggle"
              type="button"
              disabled={busy || storyPaused}
              onClick={() => {
                stopNarration()
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
                  disabled={busy || storyPaused || !selectedHero}
                  onClick={() => void submitAction(choice.text, choice.id)}
                >
                  {choice.text}
                </button>
              ))}
              <button
                className="button button-secondary free-text-toggle"
                aria-expanded={showFreeText}
                onClick={() => {
                  stopNarration()
                  setShowFreeText((visible) => !visible)
                  setError('')
                }}
                disabled={busy || storyPaused}
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
                  disabled={busy || storyPaused}
                  autoFocus
                />
                {voiceEnabled && (
                  <div className="voice-input-controls">
                    <button
                      className={`button ${voiceInput.listening ? 'button-primary' : 'button-secondary'}`}
                      type="button"
                      aria-label={voiceInput.listening ? 'Listening, release to finish' : 'Hold to speak'}
                      aria-pressed={voiceInput.listening}
                      disabled={busy || storyPaused}
                      onPointerDown={(event) => {
                        event.preventDefault()
                        event.currentTarget.setPointerCapture?.(event.pointerId)
                        stopNarration()
                        setPlaybackMessage('')
                        voiceInput.startListening()
                      }}
                      onPointerUp={voiceInput.stopListening}
                      onPointerLeave={voiceInput.stopListening}
                      onPointerCancel={voiceInput.stopListening}
                      onLostPointerCapture={voiceInput.stopListening}
                      onKeyDown={(event) => {
                        if ((event.key === ' ' || event.key === 'Enter') && !event.repeat) {
                          event.preventDefault()
                          stopNarration()
                          voiceInput.startListening()
                        }
                      }}
                      onKeyUp={(event) => {
                        if (event.key === ' ' || event.key === 'Enter') {
                          event.preventDefault()
                          voiceInput.stopListening()
                        }
                      }}
                      onContextMenu={(event) => event.preventDefault()}
                    >
                      {voiceInput.listening ? 'Listening… release to finish' : 'Hold to speak'}
                    </button>
                    <p className="voice-input-guidance">
                      Hold the button while you speak. Your browser may ask for microphone permission and may use its own speech service. You can always type your idea instead.
                    </p>
                    {voiceInput.message && <p className="voice-input-message" role="status">{voiceInput.message}</p>}
                  </div>
                )}
                <button className="button button-primary" type="submit" disabled={busy || storyPaused || !actionText.trim()}>
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

      <details className="adventure-wrap-up" open={showWrapUp}>
        <summary>Parent: wrap up this adventure</summary>
        <form onSubmit={(event) => void finishSession(event)}>
          <p>Keep it brief and factual: note discoveries, rewards, important relationships, and promises still open. The full play-by-play is not saved as continuity.</p>
          <label htmlFor="session-summary">Session summary</label>
          <textarea id="session-summary" required maxLength={1200} rows={3}
            value={sessionSummary} onChange={(event) => setSessionSummary(event.target.value)}
            disabled={savingSummary} />
          <label htmlFor="session-parent-pin">Parent PIN</label>
          <input id="session-parent-pin" type="password" inputMode="numeric" autoComplete="current-password"
            value={sessionParentPin} onChange={(event) => setSessionParentPin(event.target.value)}
            disabled={savingSummary} required />
          {summaryError && <p className="adventure-error" role="alert">{summaryError}</p>}
          <button className="button button-primary" type="submit" disabled={savingSummary || !sessionSummary.trim()}>
            {savingSummary ? 'Saving summary…' : 'Save summary and end adventure'}
          </button>
        </form>
      </details>
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
      {preview && <span className="demo-hero-label">Demo hero</span>}
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

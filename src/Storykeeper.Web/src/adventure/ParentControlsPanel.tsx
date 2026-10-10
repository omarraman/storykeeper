import { useEffect, useState } from 'react'
import { request } from '../api/request'
import type { NarrationProvider, ParentSafetySettings } from './contracts'
import './ParentControlsPanel.css'
export type { ParentSafetySettings } from './contracts'

interface ParentControlsPanelProps {
  campaignId: string
  settings: ParentSafetySettings
  sessionId?: string
  isPaused?: boolean
  onSettingsSaved: (settings: ParentSafetySettings) => void
  onPausedChange?: (isPaused: boolean) => void
  onEndSessionRequested?: () => void
}

export function ParentControlsPanel({
  campaignId,
  settings,
  sessionId,
  isPaused = false,
  onSettingsSaved,
  onPausedChange,
  onEndSessionRequested,
}: ParentControlsPanelProps) {
  const [expanded, setExpanded] = useState(false)
  const [pin, setPin] = useState('')
  const [unlocked, setUnlocked] = useState(false)
  const [form, setForm] = useState(settings)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const [paused, setPaused] = useState(isPaused)
  const [ttsProviderStatus, setTtsProviderStatus] = useState('Checking server configuration…')
  const [ttsServerInfo, setTtsServerInfo] = useState({
    configured: false,
    provider: 'disabled' as NarrationProvider,
  })

  useEffect(() => setPaused(isPaused), [isPaused])

  useEffect(() => {
    if (!unlocked) return
    let current = true
    void request<{ enabled: boolean; configured: boolean; provider: string }>('/api/text-to-speech/status')
      .then((status) => {
        if (current) {
          const provider: NarrationProvider = status.provider === 'Piper'
            ? 'piper'
            : status.provider === 'ElevenLabs'
              ? 'elevenLabs'
              : 'disabled'
          setTtsServerInfo({ configured: status.configured, provider })
          setTtsProviderStatus(status.enabled
            ? `${status.provider} is configured`
            : status.configured
              ? `${status.provider} is configured but disabled on the server`
              : `${status.provider} is not available`)
        }
      })
      .catch(() => {
        if (current) setTtsProviderStatus('Server provider status is unavailable')
      })
    return () => { current = false }
  }, [unlocked])

  const parentHeaders = { 'X-Parent-Pin': pin }

  const unlock = async () => {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await request<void>('/api/parent-controls/verify', {
        method: 'POST',
        headers: parentHeaders,
      })
      setUnlocked(true)
    } catch (unlockError) {
      setUnlocked(false)
      setError(unlockError instanceof Error ? unlockError.message : 'The parent PIN could not be checked.')
    } finally {
      setBusy(false)
    }
  }

  const saveSettings = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const saved = await request<ParentSafetySettings>(
        `/api/campaigns/${encodeURIComponent(campaignId)}/parent-controls`,
        {
          method: 'PUT',
          headers: parentHeaders,
          body: JSON.stringify({ safetySettings: form }),
        },
      )
      setForm(saved)
      onSettingsSaved(saved)
      setMessage('Parent settings saved for this story world.')
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'Parent settings could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  const sendAction = async (action: string) => {
    if (!sessionId) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const result = await request<{ isPaused: boolean; endSessionRequested: boolean }>(
        `/api/campaigns/${encodeURIComponent(campaignId)}/sessions/${encodeURIComponent(sessionId)}/parent-actions`,
        {
          method: 'POST',
          headers: parentHeaders,
          body: JSON.stringify({ action }),
        },
      )
      setPaused(result.isPaused)
      onPausedChange?.(result.isPaused)
      if (result.endSessionRequested) onEndSessionRequested?.()
      else setMessage(action === 'pause' ? 'The story is paused.' : action === 'resume' ? 'The story is ready to continue.' : 'The Storykeeper will follow your direction.')
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : 'That parent control could not be applied.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="parent-controls">
      <button
        type="button"
        className="parent-controls-toggle"
        aria-expanded={expanded}
        onClick={() => setExpanded((value) => !value)}
      >
        <span>
          <span className="card-kicker">GROWN-UP TOOLS</span>
          <strong>Parent controls</strong>
        </span>
        <span aria-hidden="true">{expanded ? '−' : '+'}</span>
      </button>
      {expanded && (
        <div className="parent-controls-body">
          {!unlocked ? (
            <form onSubmit={(event) => { event.preventDefault(); void unlock() }}>
              <label htmlFor={`parent-pin-${campaignId}`}>Parent PIN</label>
              <div className="parent-pin-row">
                <input
                  id={`parent-pin-${campaignId}`}
                  type="password"
                  inputMode="numeric"
                  autoComplete="current-password"
                  value={pin}
                  onChange={(event) => setPin(event.target.value)}
                  required
                />
                <button className="button button-primary" disabled={busy}>Unlock</button>
              </div>
              <p className="parent-controls-hint">The PIN is checked by the story server and is not saved in this browser.</p>
            </form>
          ) : (
            <>
              <form className="parent-settings-form" onSubmit={(event) => void saveSettings(event)}>
                <div className="parent-settings-grid">
                  <label>
                    Fright level
                    <select value={form.fearLevel} onChange={(event) => setForm({ ...form, fearLevel: event.target.value as ParentSafetySettings['fearLevel'] })}>
                      <option value="none">None</option>
                      <option value="low">Low</option>
                    </select>
                  </label>
                  <label>
                    Combat
                    <select value={form.combatMode} onChange={(event) => setForm({ ...form, combatMode: event.target.value as ParentSafetySettings['combatMode'] })}>
                      <option value="avoid">Avoid</option>
                      <option value="silly">Silly, non-graphic</option>
                      <option value="storyOnly">Story-only, no tactics</option>
                    </select>
                  </label>
                  <label>
                    Narration word limit
                    <input type="number" min={40} max={150} value={form.maxNarrationWords}
                      onChange={(event) => setForm({ ...form, maxNarrationWords: Number(event.target.value) })} />
                  </label>
                  <label>
                    Session length
                    <input type="number" min={15} max={180} value={form.sessionLengthMinutes}
                      onChange={(event) => setForm({ ...form, sessionLengthMinutes: Number(event.target.value) })} />
                  </label>
                  <label>
                    Narration provider
                    <select
                      value={form.narrationProvider}
                      onChange={(event) => {
                        const narrationProvider = event.target.value as NarrationProvider
                        const enabled = narrationProvider !== 'disabled'
                        setForm({
                          ...form,
                          narrationProvider,
                          textToSpeechEnabled: enabled,
                          narrationPlayback: enabled
                            ? form.narrationPlayback === 'off' ? 'onDemand' : form.narrationPlayback
                            : 'off',
                        })
                      }}
                    >
                      <option value="disabled">Disabled</option>
                      {ttsServerInfo.configured && ttsServerInfo.provider !== 'disabled' && (
                        <option value={ttsServerInfo.provider}>
                          {ttsServerInfo.provider === 'elevenLabs' ? 'ElevenLabs' : 'Piper'}
                        </option>
                      )}
                    </select>
                  </label>
                  <label>
                    Narrated playback
                    <select
                      value={form.narrationPlayback}
                      onChange={(event) => {
                        const narrationPlayback = event.target.value as ParentSafetySettings['narrationPlayback']
                        const narrationProvider = narrationPlayback !== 'off' && form.narrationProvider === 'disabled'
                          ? ttsServerInfo.provider
                          : form.narrationProvider
                        setForm({
                          ...form,
                          narrationPlayback,
                          narrationProvider,
                          textToSpeechEnabled: narrationPlayback !== 'off' &&
                            ttsServerInfo.configured && narrationProvider !== 'disabled',
                        })
                      }}
                    >
                      <option value="off">Off</option>
                      <option value="onDemand" disabled={!ttsServerInfo.configured}>On demand</option>
                      <option value="autoplayAfterNewStoryBeat" disabled={!ttsServerInfo.configured}>
                        Autoplay after a new story beat
                      </option>
                    </select>
                  </label>
                </div>
                <label className="parent-voice-setting">
                  <input
                    type="checkbox"
                    checked={form.textToSpeechEnabled}
                    disabled={!ttsServerInfo.configured}
                    onChange={(event) => setForm({
                      ...form,
                      textToSpeechEnabled: event.target.checked,
                      narrationProvider: event.target.checked
                        ? form.narrationProvider === 'disabled' ? ttsServerInfo.provider : form.narrationProvider
                        : form.narrationProvider,
                      narrationPlayback: event.target.checked
                        ? form.narrationPlayback === 'off' ? 'onDemand' : form.narrationPlayback
                        : 'off',
                    })}
                  />
                  <span>
                    <strong>Enable narrated playback</strong>
                    <span>Server provider: {ttsProviderStatus}. Keys never leave the server.</span>
                  </span>
                </label>
                <p className="parent-controls-hint">
                  Provider choices are limited to the server-configured provider; local Piper never falls back to cloud speech.
                </p>
                <label className="parent-voice-setting">
                  <input
                    type="checkbox"
                    checked={form.voiceEnabled}
                    onChange={(event) => setForm({ ...form, voiceEnabled: event.target.checked })}
                  />
                  <span>
                    <strong>Allow browser voice input</strong>
                    <span>Off by default. Browser dictation may use its own speech service.</span>
                  </span>
                </label>
                <label htmlFor={`excluded-content-${campaignId}`}>Topics or creatures to exclude <span>(one per line, up to 20)</span></label>
                <textarea id={`excluded-content-${campaignId}`} rows={3} maxLength={2000}
                  value={form.excludedContent.join('\n')}
                  onChange={(event) => setForm({
                    ...form,
                    excludedContent: event.target.value.split(/\r?\n/).map((line) => line.trim()).filter(Boolean),
                  })} />
                <button className="button button-primary" disabled={busy}>Save settings</button>
              </form>
              {sessionId && (
                <div className="parent-live-actions" aria-label="Live parent story controls">
                  <p className="card-kicker">REDIRECT THIS SCENE</p>
                  <div className="parent-live-action-grid">
                    <button type="button" disabled={busy} onClick={() => void sendAction('makeEasier')}>Make easier</button>
                    <button type="button" disabled={busy} onClick={() => void sendAction('addClue')}>Add a clue</button>
                    <button type="button" disabled={busy} onClick={() => void sendAction('skipScene')}>Skip scene</button>
                    <button type="button" disabled={busy} onClick={() => void sendAction('moveTowardEnding')}>Move toward ending</button>
                    <button type="button" disabled={busy} onClick={() => void sendAction(paused ? 'resume' : 'pause')}>
                      {paused ? 'Resume story' : 'Pause story'}
                    </button>
                    <button type="button" disabled={busy} onClick={() => void sendAction('endSession')}>End session</button>
                  </div>
                </div>
              )}
              <button className="text-button parent-controls-lock" type="button" onClick={() => { setUnlocked(false); setPin('') }}>
                Lock parent controls
              </button>
            </>
          )}
          {error && <p className="alert" role="alert">{error}</p>}
          {message && <p className="parent-controls-message" role="status">{message}</p>}
        </div>
      )}
    </section>
  )
}

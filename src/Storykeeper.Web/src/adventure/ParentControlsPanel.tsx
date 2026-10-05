import { useEffect, useState } from 'react'
import { request } from '../api/request'
import type { ParentSafetySettings } from './contracts'
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

  useEffect(() => setPaused(isPaused), [isPaused])

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
                </div>
                <label className="parent-voice-setting">
                  <input
                    type="checkbox"
                    checked={form.voiceEnabled}
                    onChange={(event) => setForm({ ...form, voiceEnabled: event.target.checked })}
                  />
                  <span>
                    <strong>Allow voice input and spoken narration</strong>
                    <span>Off by default. The browser may ask for microphone permission and may use its own speech service.</span>
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

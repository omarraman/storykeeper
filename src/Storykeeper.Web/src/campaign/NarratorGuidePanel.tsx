import { useEffect, useState } from 'react'
import { request } from '../api/request'
import { NarratorGuideTextEditor } from './NarratorGuideTextEditor'
import './NarratorGuidePanel.css'

interface GuideState {
  activeText: string | null
  pendingText: string
  hasPendingRevision: boolean
  activeRevision: number
  pendingRevision: number
}

const emptyGuide: GuideState = {
  activeText: null,
  pendingText: '',
  hasPendingRevision: false,
  activeRevision: 0,
  pendingRevision: 0,
}

interface NarratorGuidePanelProps {
  campaignId: string
  activeSession: boolean
  onDirtyChange: (dirty: boolean) => void
}

export function NarratorGuidePanel({ campaignId, activeSession, onDirtyChange }: NarratorGuidePanelProps) {
  const [expanded, setExpanded] = useState(false)
  const [pin, setPin] = useState('')
  const [unlocked, setUnlocked] = useState(false)
  const [guide, setGuide] = useState<GuideState>(emptyGuide)
  const [text, setText] = useState('')
  const [savedText, setSavedText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const headers = { 'X-Parent-Pin': pin }

  useEffect(() => {
    const dirty = text !== savedText
    onDirtyChange(dirty)
    if (!dirty) return
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', warnBeforeLeaving)
    return () => window.removeEventListener('beforeunload', warnBeforeLeaving)
  }, [onDirtyChange, savedText, text])

  const lock = () => {
    if (text !== savedText && !window.confirm('Lock and discard unsaved narrator guide edits?')) return
    setUnlocked(false)
    setPin('')
  }

  const unlock = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      await request<void>('/api/parent-controls/verify', { method: 'POST', headers })
      const loaded = await request<GuideState>(`/api/campaigns/${encodeURIComponent(campaignId)}/narrator-guide`, { headers })
      setGuide(loaded)
      const editorText = loaded.hasPendingRevision ? loaded.pendingText : loaded.activeText ?? ''
      setText(editorText)
      setSavedText(editorText)
      setUnlocked(true)
    } catch (loadError) {
      setUnlocked(false)
      setError(loadError instanceof Error ? loadError.message : 'The narrator guide could not be loaded.')
    } finally {
      setBusy(false)
    }
  }

  const savePending = async () => {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const saved = await request<GuideState>(
        `/api/campaigns/${encodeURIComponent(campaignId)}/narrator-guide/pending`,
        { method: 'PUT', headers, body: JSON.stringify({ text }) },
      )
      setGuide(saved)
      setSavedText(text)
      setMessage('Saved as a pending revision. It will not affect the narrator until approved.')
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'The guide could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  const approve = async () => {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const saved = await request<GuideState>(
        `/api/campaigns/${encodeURIComponent(campaignId)}/narrator-guide/approve`,
        { method: 'POST', headers },
      )
      setGuide(saved)
      const activeText = saved.activeText ?? ''
      setText(activeText)
      setSavedText(activeText)
      setMessage('The reviewed guide is now active for future story turns.')
    } catch (approvalError) {
      setError(approvalError instanceof Error ? approvalError.message : 'The guide could not be approved.')
    } finally {
      setBusy(false)
    }
  }

  const remove = async () => {
    if (!window.confirm('Remove the active and pending narrator guide? This does not end or change a session.')) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await request<void>(`/api/campaigns/${encodeURIComponent(campaignId)}/narrator-guide`, {
        method: 'DELETE',
        headers,
      })
      setGuide(emptyGuide)
      setText('')
      setSavedText('')
      setMessage('The narrator guide was removed.')
    } catch (removeError) {
      setError(removeError instanceof Error ? removeError.message : 'The guide could not be removed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="narrator-guide-panel">
      <button type="button" className="parent-controls-toggle" aria-expanded={expanded}
        onClick={() => setExpanded((current) => !current)}>
        <span><span className="card-kicker">PARENT-ONLY STORY MATERIAL</span><strong>Full narrator guide (private)</strong></span>
        <span aria-hidden="true">{expanded ? '−' : '+'}</span>
      </button>
      {expanded && (
        <div className="narrator-guide-body">
          {activeSession && (
            <p className="alert" role="status">
              Guide changes are unavailable while an adventure session is running. End the session normally before editing; Storykeeper will not end it for you.
            </p>
          )}
          {!unlocked ? (
            <form onSubmit={(event) => void unlock(event)}>
              <label htmlFor={`narrator-guide-pin-${campaignId}`}>Parent PIN</label>
              <div className="parent-pin-row">
                <input id={`narrator-guide-pin-${campaignId}`} type="password" autoComplete="current-password"
                  value={pin} onChange={(event) => setPin(event.target.value)} required />
                <button className="button button-primary" disabled={busy}>Unlock</button>
              </div>
              <p className="field-hint">The guide is only loaded after parent PIN authorization.</p>
            </form>
          ) : (
            <>
              <p className="field-hint">
                {guide.activeText === null ? 'No approved guide is active.' : `Active approved revision ${guide.activeRevision}.`}
                {guide.hasPendingRevision ? ` Pending revision ${guide.pendingRevision} is not used during play.` : ''}
              </p>
              <NarratorGuideTextEditor value={text} onChange={setText} disabled={busy || activeSession} />
              <div className="narrator-guide-actions">
                <button type="button" className="button button-secondary" disabled={busy || activeSession || !text.trim()}
                  onClick={() => void savePending()}>Save pending revision</button>
                <button type="button" className="button button-primary"
                  disabled={busy || activeSession || !guide.hasPendingRevision || text !== savedText}
                  onClick={() => void approve()}>Approve and apply</button>
                {(guide.activeText !== null || guide.hasPendingRevision) && (
                  <button type="button" className="text-button delete-action" disabled={busy || activeSession}
                    onClick={() => void remove()}>Remove guide</button>
                )}
                <button type="button" className="text-button" onClick={lock}>Lock parent editor</button>
              </div>
            </>
          )}
          {error && <p className="alert" role="alert">{error}</p>}
          {message && <p className="parent-controls-message" role="status">{message}</p>}
        </div>
      )}
    </section>
  )
}

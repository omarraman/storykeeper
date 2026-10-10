import { useEffect, useState } from 'react'
import { request } from '../api/request'
import { NarratorGuideTextEditor } from './NarratorGuideTextEditor'
import './NarratorGuidePanel.css'

interface CampaignDraftState {
  id: string
  status: 'PendingReview' | 'Approved' | 'Activated'
}

interface DraftNarratorGuidePanelProps {
  draft: CampaignDraftState
  onSaved: (status: 'PendingReview') => void
  onDirtyChange: (dirty: boolean) => void
}

export function DraftNarratorGuidePanel({ draft, onSaved, onDirtyChange }: DraftNarratorGuidePanelProps) {
  const [pin, setPin] = useState('')
  const [unlocked, setUnlocked] = useState(false)
  const [text, setText] = useState('')
  const [savedText, setSavedText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')

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
      await request<void>('/api/parent-controls/verify', {
        method: 'POST',
        headers: { 'X-Parent-Pin': pin },
      })
      const result = await request<{ text: string | null }>(
        `/api/campaign-drafts/${encodeURIComponent(draft.id)}/narrator-guide`,
        { headers: { 'X-Parent-Pin': pin } },
      )
      const loaded = result.text ?? ''
      setText(loaded)
      setSavedText(loaded)
      setUnlocked(true)
    } catch (loadError) {
      setUnlocked(false)
      setError(loadError instanceof Error ? loadError.message : 'The full narrator guide could not be loaded.')
    } finally {
      setBusy(false)
    }
  }

  const save = async () => {
    if (savedText.trim() && !text.trim() && !window.confirm('Remove this narrator guide from the draft?')) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await request(`/api/campaign-drafts/${encodeURIComponent(draft.id)}/narrator-guide`, {
        method: 'PUT',
        headers: { 'X-Parent-Pin': pin },
        body: JSON.stringify({ text }),
      })
      setSavedText(text)
      onSaved('PendingReview')
      setMessage('Guide saved verbatim. Review and approve the complete draft again before activation.')
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'The full narrator guide could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="draft-guide-review room-panel">
      <p className="card-kicker">PARENT-ONLY AUTHORED SOURCE</p>
      <h2>Full narrator guide (private)</h2>
      {!unlocked ? (
        <form onSubmit={(event) => void unlock(event)}>
          <label htmlFor={`draft-guide-pin-${draft.id}`}>Parent PIN to review the private guide</label>
          <div className="parent-pin-row">
            <input id={`draft-guide-pin-${draft.id}`} type="password" autoComplete="current-password"
              value={pin} onChange={(event) => setPin(event.target.value)} required />
            <button className="button button-primary" disabled={busy}>Load guide</button>
          </div>
        </form>
      ) : (
        <>
          <p>This authored source is kept separately from generated scaffolding. Generation does not guarantee fidelity; correct any conflicts below before approving.</p>
          <NarratorGuideTextEditor value={text} onChange={setText} disabled={busy || draft.status === 'Activated'} />
          <div className="narrator-guide-actions">
            <button type="button" className="button button-primary"
              disabled={busy || draft.status === 'Activated' || text === savedText}
              onClick={() => void save()}>Save guide and reset approval</button>
            <button type="button" className="text-button" onClick={lock}>
              Lock guide
            </button>
          </div>
        </>
      )}
      {error && <p className="alert" role="alert">{error}</p>}
      {message && <p className="parent-controls-message" role="status">{message}</p>}
    </section>
  )
}

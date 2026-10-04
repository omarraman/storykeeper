import { useEffect, useState } from 'react'
import { request } from '../api/request'
import type { AdventureDraft, AdventureDraftContent, AdventureArcType } from './contracts'

interface AdventureDraftPanelProps {
  campaignId: string
  campaignActive: boolean
  sessionActive: boolean
  onActivated: () => Promise<void>
}

function copyContent(content: AdventureDraftContent): AdventureDraftContent {
  return {
    ...content,
    scenes: content.scenes.map((scene) => ({ ...scene })),
    solutionPaths: [...content.solutionPaths],
    clues: content.clues.map((clue) => ({ ...clue })),
    featuredNpc: { ...content.featuredNpc },
  }
}

export function AdventureDraftPanel({
  campaignId,
  campaignActive,
  sessionActive,
  onActivated,
}: AdventureDraftPanelProps) {
  const [drafts, setDrafts] = useState<AdventureDraft[]>([])
  const [selected, setSelected] = useState<AdventureDraft | null>(null)
  const [form, setForm] = useState<AdventureDraftContent | null>(null)
  const [editing, setEditing] = useState(false)
  const [sessionLength, setSessionLength] = useState(45)
  const [preferences, setPreferences] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let mounted = true
    setLoading(true)
    request<AdventureDraft[]>(`/api/campaigns/${encodeURIComponent(campaignId)}/adventure-drafts`)
      .then((result) => { if (mounted) setDrafts(result) })
      .catch((loadError: unknown) => {
        if (mounted) setError(loadError instanceof Error ? loadError.message : 'Adventure drafts could not be loaded.')
      })
      .finally(() => { if (mounted) setLoading(false) })
    return () => { mounted = false }
  }, [campaignId])

  const saveDraft = (draft: AdventureDraft) => {
    setSelected(draft)
    setForm(copyContent(draft.content))
    setDrafts((current) => [draft, ...current.filter((item) => item.id !== draft.id)])
    setEditing(false)
  }

  const generate = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      const draft = await request<AdventureDraft>(
        `/api/campaigns/${encodeURIComponent(campaignId)}/adventure-drafts`,
        {
          method: 'POST',
          body: JSON.stringify({
            sessionLengthMinutes: sessionLength,
            parentPreferences: preferences.trim() || null,
          }),
        },
      )
      saveDraft(draft)
      setPreferences('')
    } catch (generationError) {
      setError(generationError instanceof Error ? generationError.message : 'The next adventure could not be generated.')
    } finally {
      setSaving(false)
    }
  }

  const regenerate = async () => {
    if (!selected || !window.confirm('Regenerate this adventure? Its current draft will be replaced.')) return
    setSaving(true)
    setError('')
    try {
      const draft = await request<AdventureDraft>(
        `/api/adventure-drafts/${encodeURIComponent(selected.id)}/regenerate`,
        { method: 'POST' },
      )
      saveDraft(draft)
    } catch (generationError) {
      setError(generationError instanceof Error ? generationError.message : 'The adventure could not be regenerated.')
    } finally {
      setSaving(false)
    }
  }

  const saveEdits = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!selected || !form) return
    setSaving(true)
    setError('')
    try {
      const draft = await request<AdventureDraft>(
        `/api/adventure-drafts/${encodeURIComponent(selected.id)}`,
        { method: 'PUT', body: JSON.stringify(form) },
      )
      saveDraft(draft)
    } catch (editError) {
      setError(editError instanceof Error ? editError.message : 'The adventure edits could not be saved.')
    } finally {
      setSaving(false)
    }
  }

  const approve = async () => {
    if (!selected) return
    setSaving(true)
    setError('')
    try {
      saveDraft(await request<AdventureDraft>(
        `/api/adventure-drafts/${encodeURIComponent(selected.id)}/approve`,
        { method: 'POST' },
      ))
    } catch (approvalError) {
      setError(approvalError instanceof Error ? approvalError.message : 'The adventure could not be approved.')
    } finally {
      setSaving(false)
    }
  }

  const activate = async () => {
    if (!selected || !window.confirm(
      `Add “${selected.content.title}” as the current quest? This finishes any quest currently in progress.`,
    )) return
    setSaving(true)
    setError('')
    try {
      const draft = await request<AdventureDraft>(
        `/api/adventure-drafts/${encodeURIComponent(selected.id)}/activate`,
        { method: 'POST' },
      )
      saveDraft(draft)
      await onActivated()
    } catch (activationError) {
      setError(activationError instanceof Error ? activationError.message : 'The adventure could not be activated.')
    } finally {
      setSaving(false)
    }
  }

  const discard = async () => {
    if (!selected || !window.confirm(`Discard “${selected.content.title}”?`)) return
    setSaving(true)
    setError('')
    try {
      await request<void>(`/api/adventure-drafts/${encodeURIComponent(selected.id)}`, { method: 'DELETE' })
      setDrafts((current) => current.filter((draft) => draft.id !== selected.id))
      setSelected(null)
      setForm(null)
      setEditing(false)
    } catch (discardError) {
      setError(discardError instanceof Error ? discardError.message : 'The adventure draft could not be discarded.')
    } finally {
      setSaving(false)
    }
  }

  const update = <K extends keyof AdventureDraftContent>(key: K, value: AdventureDraftContent[K]) => {
    setForm((current) => current ? { ...current, [key]: value } : current)
  }

  const updateScene = (index: number, key: 'title' | 'description' | 'npcName', value: string) => {
    setForm((current) => current ? {
      ...current,
      scenes: current.scenes.map((scene, sceneIndex) => sceneIndex === index
        ? { ...scene, [key]: key === 'npcName' && value.trim() === '' ? null : value }
        : scene),
    } : current)
  }

  const updateClue = (index: number, key: 'title' | 'description', value: string) => {
    setForm((current) => current ? {
      ...current,
      clues: current.clues.map((clue, clueIndex) => clueIndex === index ? { ...clue, [key]: value } : clue),
    } : current)
  }

  return (
    <section className="room-panel adventure-draft-panel" aria-labelledby="next-adventure-title">
      <div className="adventure-draft-heading">
        <div>
          <p className="card-kicker">PARENT ADVENTURE BUILDER</p>
          <h2 id="next-adventure-title">Plan the next adventure</h2>
          <p>Generate one episode from this world's story, then review it before it becomes the current quest.</p>
        </div>
      </div>
      {error && <p className="alert" role="alert">{error}</p>}
      {!campaignActive && <p className="adventure-builder-note">Only active story worlds can add an adventure.</p>}
      {campaignActive && sessionActive && (
        <p className="adventure-builder-note">Finish and summarize the current session before planning the next episode.</p>
      )}
      {campaignActive && !sessionActive && (
        <form className="adventure-generate-form" onSubmit={(event) => void generate(event)}>
          <label>Approximate session length
            <select value={sessionLength} onChange={(event) => setSessionLength(Number(event.target.value))} disabled={saving}>
              <option value={30}>About 30 minutes</option>
              <option value={45}>About 45 minutes</option>
              <option value={60}>About 60 minutes</option>
            </select>
          </label>
          <label>Parent preferences <span>(optional)</span>
            <textarea maxLength={500} rows={2} value={preferences}
              onChange={(event) => setPreferences(event.target.value)} disabled={saving}
              placeholder="More puzzles, a quieter pace, include the moon gardener…" />
          </label>
          <button className="button button-primary" type="submit" disabled={saving}>
            {saving ? 'Generating…' : 'Generate next adventure'}
          </button>
        </form>
      )}
      {loading ? <p role="status">Loading adventure drafts…</p> : drafts.length > 0 && (
        <div className="adventure-draft-list" aria-label="Saved adventure drafts">
          {drafts.map((draft) => (
            <button className={`adventure-draft-choice ${selected?.id === draft.id ? 'selected' : ''}`}
              key={draft.id} onClick={() => {
                setSelected(draft)
                setForm(copyContent(draft.content))
                setEditing(false)
                setError('')
              }}>
              <span>{draft.status.replace(/([A-Z])/g, ' $1').trim()} · {draft.sessionLengthMinutes} min · v{draft.generationNumber}</span>
              <strong>{draft.content.title}</strong>
            </button>
          ))}
        </div>
      )}
      {selected && form && (
        <div className="adventure-draft-review">
          <div className="adventure-draft-tools">
            <span className="adventure-builder-note">
              {selected.status === 'Activated' ? 'Added to the campaign' : `${selected.sessionLengthMinutes}-minute episode`}
            </span>
            {selected.status !== 'Activated' && (
              <>
                <button className="button button-secondary" disabled={saving || !campaignActive} onClick={() => {
                  setForm(copyContent(selected.content))
                  setEditing((current) => !current)
                }}>{editing ? 'Preview draft' : 'Edit draft'}</button>
                <button className="button button-secondary" disabled={saving || !campaignActive || sessionActive}
                  onClick={() => void regenerate()}>
                  Regenerate
                </button>
                <button className="text-button delete-action" disabled={saving} onClick={() => void discard()}>Discard</button>
              </>
            )}
          </div>
          {editing ? (
            <form className="adventure-edit-form" onSubmit={(event) => void saveEdits(event)}>
              <label>Adventure title
                <input required maxLength={120} value={form.title} onChange={(event) => update('title', event.target.value)} />
              </label>
              <label>Premise
                <textarea required maxLength={800} rows={3} value={form.premise} onChange={(event) => update('premise', event.target.value)} />
              </label>
              <label>Story shape
                <select value={form.arcType} onChange={(event) => update('arcType', event.target.value as AdventureArcType)}>
                  <option value="standalone">Standalone episode</option>
                  <option value="seasonArc">Advances the season arc</option>
                </select>
              </label>
              {form.arcType === 'seasonArc' && (
                <label>How it advances the arc
                  <textarea required maxLength={600} rows={2} value={form.arcConnection ?? ''}
                    onChange={(event) => update('arcConnection', event.target.value)} />
                </label>
              )}
              <label>Opening
                <textarea required maxLength={800} rows={3} value={form.opening} onChange={(event) => update('opening', event.target.value)} />
              </label>
              <fieldset>
                <legend>Scenes</legend>
                {form.scenes.map((scene, index) => (
                  <div className="adventure-edit-record" key={`scene-${index}`}>
                    <label>Scene {index + 1} title
                      <input required maxLength={120} value={scene.title} onChange={(event) => updateScene(index, 'title', event.target.value)} />
                    </label>
                    <label>What happens
                      <textarea required maxLength={800} rows={3} value={scene.description}
                        onChange={(event) => updateScene(index, 'description', event.target.value)} />
                    </label>
                    <label>Featured or campaign character
                      <input maxLength={100} value={scene.npcName ?? ''}
                        onChange={(event) => updateScene(index, 'npcName', event.target.value)} />
                    </label>
                  </div>
                ))}
              </fieldset>
              <fieldset>
                <legend>Different ways forward</legend>
                {form.solutionPaths.map((path, index) => (
                  <label key={`path-${index}`}>Path {index + 1}
                    <textarea required maxLength={400} rows={2} value={path}
                      onChange={(event) => update('solutionPaths', form.solutionPaths.map((item, pathIndex) => pathIndex === index ? event.target.value : item))} />
                  </label>
                ))}
              </fieldset>
              <fieldset>
                <legend>Clues</legend>
                {form.clues.map((clue, index) => (
                  <div className="adventure-edit-record" key={`clue-${index}`}>
                    <label>Clue {index + 1} title
                      <input required maxLength={120} value={clue.title} onChange={(event) => updateClue(index, 'title', event.target.value)} />
                    </label>
                    <label>What it reveals
                      <textarea required maxLength={400} rows={2} value={clue.description}
                        onChange={(event) => updateClue(index, 'description', event.target.value)} />
                    </label>
                  </div>
                ))}
              </fieldset>
              <fieldset>
                <legend>Featured character</legend>
                <label>Name
                  <input required maxLength={100} value={form.featuredNpc.name}
                    onChange={(event) => update('featuredNpc', { ...form.featuredNpc, name: event.target.value })} />
                </label>
                <label>Description
                  <textarea required maxLength={500} rows={2} value={form.featuredNpc.description}
                    onChange={(event) => update('featuredNpc', { ...form.featuredNpc, description: event.target.value })} />
                </label>
                <label>Personality
                  <input required maxLength={240} value={form.featuredNpc.disposition}
                    onChange={(event) => update('featuredNpc', { ...form.featuredNpc, disposition: event.target.value })} />
                </label>
              </fieldset>
              <label>Gentle finale
                <textarea required maxLength={800} rows={3} value={form.finale} onChange={(event) => update('finale', event.target.value)} />
              </label>
              <label>Celebration or reward
                <textarea required maxLength={400} rows={2} value={form.celebrationReward}
                  onChange={(event) => update('celebrationReward', event.target.value)} />
              </label>
              <div className="adventure-draft-tools">
                <button className="button button-secondary" type="button" disabled={saving} onClick={() => {
                  setForm(copyContent(selected.content))
                  setEditing(false)
                }}>Cancel edits</button>
                <button className="button button-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save edits'}</button>
              </div>
            </form>
          ) : (
            <article className="adventure-preview">
              <p className="card-kicker">{form.arcType === 'standalone' ? 'STANDALONE STORY' : 'SEASON ARC'}</p>
              <h3>{selected.content.title}</h3>
              <p>{selected.content.premise}</p>
              {selected.content.arcConnection && <p><strong>Season connection:</strong> {selected.content.arcConnection}</p>}
              <h4>Opening</h4><p>{selected.content.opening}</p>
              <h4>Scenes</h4>
              <ol>{selected.content.scenes.map((scene) => (
                <li key={scene.title}><strong>{scene.title}</strong> — {scene.description}
                  {scene.npcName && <span> Character: {scene.npcName}.</span>}
                </li>
              ))}</ol>
              <h4>Other ways forward</h4>
              <ul>{selected.content.solutionPaths.map((path, index) => <li key={index}>{path}</li>)}</ul>
              <h4>Clues</h4>
              <ul>{selected.content.clues.map((clue) => <li key={clue.title}><strong>{clue.title}:</strong> {clue.description}</li>)}</ul>
              <h4>Featured character</h4>
              <p><strong>{selected.content.featuredNpc.name}</strong> — {selected.content.featuredNpc.description} {selected.content.featuredNpc.disposition}</p>
              <h4>Gentle finale</h4><p>{selected.content.finale}</p>
              <h4>Celebration or reward</h4><p>{selected.content.celebrationReward}</p>
              <div className="adventure-draft-approval">
                {selected.status === 'PendingReview' && (
                  <>
                    <span>Review the episode before making it the current quest.</span>
                    <button className="button button-primary" disabled={saving} onClick={() => void approve()}>Approve adventure</button>
                  </>
                )}
                {selected.status === 'Approved' && (
                  <>
                    <span>Parent approved · ready to add to this campaign.</span>
                    <button className="button button-primary" disabled={saving || !campaignActive || sessionActive}
                      onClick={() => void activate()}>Activate adventure</button>
                  </>
                )}
                {selected.status === 'Activated' && <span>This adventure is part of the campaign's saved quest history.</span>}
              </div>
            </article>
          )}
        </div>
      )}
    </section>
  )
}

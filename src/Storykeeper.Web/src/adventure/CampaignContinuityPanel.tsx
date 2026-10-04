import { useEffect, useState } from 'react'
import {
  campaignContinuityClient,
  type CampaignContinuity,
  type CampaignContinuityClient,
  type CampaignFact,
  type CampaignFactStatus,
} from './continuityClient'

interface CampaignContinuityPanelProps {
  campaignId: string
  sourceSessionId: string | null
  readOnly?: boolean
  client?: CampaignContinuityClient
}

const factStatuses: Exclude<CampaignFactStatus, 'Proposed'>[] = [
  'Active',
  'Resolved',
  'Superseded',
  'Discarded',
]

export function CampaignContinuityPanel({
  campaignId,
  sourceSessionId,
  readOnly = false,
  client = campaignContinuityClient,
}: CampaignContinuityPanelProps) {
  const [continuity, setContinuity] = useState<CampaignContinuity | null>(null)
  const [category, setCategory] = useState('clue')
  const [statement, setStatement] = useState('')
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const reload = async () => {
    setContinuity(await client.load(campaignId))
  }

  useEffect(() => {
    let mounted = true
    void client.load(campaignId).then((result) => {
      if (mounted) setContinuity(result)
    }).catch((loadError: unknown) => {
      if (mounted) setError(loadError instanceof Error ? loadError.message : 'Saved continuity could not be loaded.')
    })
    return () => { mounted = false }
  }, [campaignId, client])

  const createFact = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!statement.trim()) return
    setSaving(true)
    setError('')
    try {
      await client.createFact({
        campaignId,
        category,
        statement: statement.trim(),
        status: 'Active',
        importance: 3,
        sourceSessionId,
      })
      setStatement('')
      await reload()
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'The fact could not be saved.')
    } finally {
      setSaving(false)
    }
  }

  const saveFact = async (fact: CampaignFact) => {
    setSaving(true)
    setError('')
    try {
      await client.updateFact({ campaignId, fact })
      await reload()
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'The fact could not be updated.')
    } finally {
      setSaving(false)
    }
  }

  const saveSummary = async (summary: string, sessionId: string) => {
    setSaving(true)
    setError('')
    try {
      await client.saveSummary({ campaignId, sessionId, summary })
      await reload()
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'The session summary could not be saved.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <details className="room-panel continuity-panel">
      <summary>
        <span>
          <span className="card-kicker">PARENT CONTROLS</span>
          <strong>Campaign continuity</strong>
        </span>
        <span>Edit saved facts and session summaries</span>
      </summary>
      {error && <p className="alert" role="alert">{error}</p>}
      {!continuity ? <p className="adventure-muted">Loading saved continuity…</p> : (
        <>
          <section aria-labelledby="saved-facts-heading">
            <h3 id="saved-facts-heading">Saved facts</h3>
            {continuity.facts.length === 0 && <p className="adventure-muted">No campaign facts have been recorded.</p>}
            <div className="continuity-facts">
              {continuity.facts.map((fact) => (
                <FactEditor
                  key={fact.id}
                  fact={fact}
                  revisions={continuity.revisions.filter((revision) =>
                    revision.recordType === 'Fact' && revision.recordId === fact.id)}
                  saving={saving}
                  readOnly={readOnly}
                  onSave={(updated) => void saveFact(updated)}
                />
              ))}
            </div>
            {!readOnly && (
              <form className="continuity-create-form" onSubmit={(event) => void createFact(event)}>
                <h4>Add a campaign fact</h4>
                <label>Type
                  <select value={category} onChange={(event) => setCategory(event.target.value)} disabled={saving}>
                    {['clue', 'promise', 'thread', 'relationship', 'reward', 'quest', 'npc', 'world', 'other']
                      .map((item) => <option key={item} value={item}>{item}</option>)}
                  </select>
                </label>
                <label>What should the Storykeeper remember?
                  <textarea required maxLength={2000} value={statement}
                    onChange={(event) => setStatement(event.target.value)} disabled={saving} />
                </label>
                <button className="button button-secondary" type="submit" disabled={saving || !statement.trim()}>
                  Add active fact
                </button>
              </form>
            )}
          </section>
          <section aria-labelledby="session-summaries-heading">
            <h3 id="session-summaries-heading">Session summaries</h3>
            {continuity.summaries.length === 0 && <p className="adventure-muted">Finished adventures will appear here.</p>}
            <div className="continuity-summaries">
              {continuity.summaries.map((summary) => (
                <SummaryEditor
                  key={summary.sessionId}
                  sessionNumber={summary.sessionNumber}
                  value={summary.summary}
                  revisions={continuity.revisions.filter((revision) =>
                    revision.recordType === 'Summary' && revision.recordId === summary.sessionId)}
                  saving={saving}
                  readOnly={readOnly}
                  onSave={(value) => void saveSummary(value, summary.sessionId)}
                />
              ))}
            </div>
          </section>
        </>
      )}
    </details>
  )
}

function FactEditor({
  fact,
  revisions,
  saving,
  readOnly,
  onSave,
}: {
  fact: CampaignFact
  revisions: CampaignContinuity['revisions']
  saving: boolean
  readOnly: boolean
  onSave: (fact: CampaignFact) => void
}) {
  const [category, setCategory] = useState(fact.category)
  const [statement, setStatement] = useState(fact.statement)
  const [status, setStatus] = useState<CampaignFactStatus>(fact.status)
  const [importance, setImportance] = useState(fact.importance)
  const changed = category !== fact.category || statement !== fact.statement ||
    status !== fact.status || importance !== fact.importance

  return (
    <article className="continuity-record">
      <p className="card-kicker">{fact.status} · {fact.category} · importance {fact.importance}</p>
      <label>Category
        <input maxLength={80} value={category} disabled={readOnly || saving}
          onChange={(event) => setCategory(event.target.value)} />
      </label>
      <label>Fact
        <textarea maxLength={2000} value={statement} disabled={readOnly || saving}
          onChange={(event) => setStatement(event.target.value)} />
      </label>
      <div className="continuity-fact-controls">
        <label>Status
          <select value={status} disabled={readOnly || saving}
            onChange={(event) => setStatus(event.target.value as CampaignFactStatus)}>
            {factStatuses.map((item) => <option key={item} value={item}>{item}</option>)}
            {fact.status === 'Proposed' && <option value="Proposed">Proposed</option>}
          </select>
        </label>
        <label>Importance
          <select value={importance} disabled={readOnly || saving}
            onChange={(event) => setImportance(Number(event.target.value))}>
            {[1, 2, 3, 4, 5].map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
        </label>
      </div>
      {fact.sourceSessionId && <small>Source session: {fact.sourceSessionId}</small>}
      {changed && !readOnly && (
        <button className="button button-secondary" disabled={saving || !statement.trim()}
          onClick={() => onSave({ ...fact, category, statement, status, importance })}>
          Save fact correction
        </button>
      )}
      {revisions.length > 0 && (
        <details className="continuity-history">
          <summary>Correction history ({revisions.length})</summary>
          {revisions.map((revision) => (
            <article key={revision.id}>
              <small>{new Date(revision.changedAtUtc).toLocaleString()} · {revision.changedBy}</small>
              <p>Before: {revision.previousContent ?? 'New fact'}</p>
              <p>After: {revision.newContent}</p>
            </article>
          ))}
        </details>
      )}
    </article>
  )
}

function SummaryEditor({
  sessionNumber,
  value,
  revisions,
  saving,
  readOnly,
  onSave,
}: {
  sessionNumber: number
  value: string
  revisions: CampaignContinuity['revisions']
  saving: boolean
  readOnly: boolean
  onSave: (summary: string) => void
}) {
  const [summary, setSummary] = useState(value)
  const changed = summary !== value
  return (
    <article className="continuity-record">
      <h4>Adventure {sessionNumber}</h4>
      <label>Factual recap
        <textarea maxLength={1200} value={summary} disabled={readOnly || saving}
          onChange={(event) => setSummary(event.target.value)} />
      </label>
      {!readOnly && changed && (
        <button className="button button-secondary" disabled={saving || !summary.trim()}
          onClick={() => onSave(summary.trim())}>
          Save summary correction
        </button>
      )}
      {revisions.length > 0 && (
        <details className="continuity-history">
          <summary>Correction history ({revisions.length})</summary>
          {revisions.map((revision) => (
            <article key={revision.id}>
              <small>{new Date(revision.changedAtUtc).toLocaleString()} · {revision.changedBy}</small>
              <p>Before: {revision.previousContent ?? 'No summary'}</p>
              <p>After: {revision.newContent}</p>
            </article>
          ))}
        </details>
      )}
    </article>
  )
}

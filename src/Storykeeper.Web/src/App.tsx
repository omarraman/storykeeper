import { useEffect, useMemo, useState } from 'react'
import './App.css'

type ApiStatus = 'checking' | 'connected' | 'unavailable'
type CampaignStatus = 'Active' | 'Completed' | 'Archived'
type CampaignTab = CampaignStatus
type CampaignBriefInput = {
  title: string
  genre: string
  tone: string
  campaignLengthSessions: number
  sessionLengthMinutes: number
  inclusions: string[]
  exclusions: string[]
  storyIdea: string | null
}
type CampaignBrief = CampaignBriefInput & {
  id: string
  safetyBoundaries: string[]
  createdAtUtc: string
  updatedAtUtc: string
}
const briefSteps = ['The world', 'The adventure', 'Your guardrails']

function newBrief(): CampaignBriefInput {
  return {
    title: '',
    genre: 'Cozy fantasy',
    tone: 'Warm, funny, and adventurous',
    campaignLengthSessions: 6,
    sessionLengthMinutes: 45,
    inclusions: [],
    exclusions: [],
    storyIdea: '',
  }
}

type Campaign = {
  id: string
  name: string
  description: string | null
  status: CampaignStatus
  createdAtUtc: string
  updatedAtUtc: string
  archivedAtUtc: string | null
  theme: string
  tone: string
  lowFright: boolean
  worldDescription: string
  currentSituation: string | null
  partyName: string
  heroes: Hero[]
  quests: Quest[]
  sessions: Session[]
  currentQuest: Quest | null
  latestSession: Session | null
}

type Hero = {
  id: string
  name: string
  description: string
  role: string
  hearts: number
  sparkleTokens: number
  inventory: { name: string; description: string; quantity: number }[]
}

type Quest = { id: string; title: string; description: string; status: string }
type Session = {
  id: string
  sessionNumber: number
  startedAtUtc: string
  endedAtUtc: string | null
  summary: string | null
}

const selectedCampaignKey = 'storykeeper.selectedCampaignId'
const tabs: CampaignTab[] = ['Active', 'Completed', 'Archived']

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const details = await response.json().catch(() => null) as { title?: string; errors?: Record<string, string[]> } | null
    const message = details?.errors
      ? Object.values(details.errors).flat().join(' ')
      : details?.title ?? `The story server returned ${response.status}.`
    throw new Error(message)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

function App() {
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking')
  const [campaigns, setCampaigns] = useState<Campaign[]>([])
  const [briefs, setBriefs] = useState<CampaignBrief[]>([])
  const [selectedCampaign, setSelectedCampaign] = useState<Campaign | null>(null)
  const [activeTab, setActiveTab] = useState<CampaignTab>('Active')
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
  const [showWizard, setShowWizard] = useState(false)
  const [editingBriefId, setEditingBriefId] = useState<string | null>(null)
  const [briefForm, setBriefForm] = useState<CampaignBriefInput>(newBrief)
  const [wizardStep, setWizardStep] = useState(0)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const visibleCampaigns = useMemo(
    () => campaigns.filter((campaign) => campaign.status === activeTab),
    [activeTab, campaigns],
  )

  useEffect(() => {
    let stopped = false
    let retryTimer: ReturnType<typeof setTimeout>
    let controller: AbortController

    const checkApi = async () => {
      controller = new AbortController()
      try {
        const response = await fetch('/api/health', { signal: controller.signal })
        const result: { status?: string } = await response.json()
        if (!response.ok || result.status !== 'Healthy') throw new Error('The API is not healthy')
        if (!stopped) setApiStatus('connected')
      } catch {
        if (stopped) return
        setApiStatus('unavailable')
        retryTimer = setTimeout(checkApi, 5000)
      }
    }

    void checkApi()
    return () => {
      stopped = true
      controller.abort()
      clearTimeout(retryTimer)
    }
  }, [])

  useEffect(() => {
    let stopped = false

    const load = async () => {
      setLoading(true)
      try {
        const storedId = localStorage.getItem(selectedCampaignKey)
        const result = await request<Campaign[]>('/api/campaigns')
        const savedBriefs = await request<CampaignBrief[]>('/api/campaign-briefs')
        if (stopped) return
        setCampaigns(result)
        setBriefs(savedBriefs)
        if (storedId && result.some((campaign) => campaign.id === storedId)) {
          const storedCampaign = await request<Campaign>(`/api/campaigns/${encodeURIComponent(storedId)}`)
          if (stopped) return
          setSelectedCampaign(storedCampaign)
          setActiveTab(storedCampaign.status)
        } else if (storedId) {
          localStorage.removeItem(selectedCampaignKey)
        }
      } catch (loadError) {
        if (!stopped) setError(loadError instanceof Error ? loadError.message : 'Could not load your story worlds.')
      } finally {
        if (!stopped) setLoading(false)
      }
    }

    void load()
    return () => { stopped = true }
  }, [])

  const selectCampaign = (campaign: Campaign) => {
    setSelectedCampaign(campaign)
    localStorage.setItem(selectedCampaignKey, campaign.id)
    setError('')
  }

  const refreshCampaigns = async (selectedId?: string) => {
    const result = await request<Campaign[]>('/api/campaigns')
    setCampaigns(result)
    if (selectedId) {
      const selected = await request<Campaign>(`/api/campaigns/${encodeURIComponent(selectedId)}`)
      selectCampaign(selected)
    }
  }

  const startBrief = (brief?: CampaignBrief) => {
    setError('')
    setEditingBriefId(brief?.id ?? null)
    setBriefForm(brief ? {
      title: brief.title,
      genre: brief.genre,
      tone: brief.tone,
      campaignLengthSessions: brief.campaignLengthSessions,
      sessionLengthMinutes: brief.sessionLengthMinutes,
      inclusions: [...brief.inclusions],
      exclusions: [...brief.exclusions],
      storyIdea: brief.storyIdea,
    } : newBrief())
    setWizardStep(0)
    setShowWizard(true)
  }

  const createCampaign = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const formData = new FormData(event.currentTarget)
    setSaving(true)
    setError('')
    try {
      const campaign = await request<Campaign>('/api/campaigns', {
        method: 'POST',
        body: JSON.stringify({
          name: formData.get('name'),
          description: formData.get('description') || null,
        }),
      })
      await refreshCampaigns(campaign.id)
      setActiveTab('Active')
      setShowCreate(false)
    } catch (createError) {
      setError(createError instanceof Error ? createError.message : 'Could not create this story world.')
    } finally {
      setSaving(false)
    }
  }

  const discardBrief = async (brief: CampaignBrief) => {
    if (!window.confirm(`Discard the saved brief “${brief.title}”? This cannot be undone.`)) return
    setError('')
    try {
      await request<void>(`/api/campaign-briefs/${encodeURIComponent(brief.id)}`, { method: 'DELETE' })
      setBriefs((current) => current.filter((item) => item.id !== brief.id))
    } catch (discardError) {
      setError(discardError instanceof Error ? discardError.message : 'Could not discard this campaign brief.')
    }
  }

  const saveBrief = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSaving(true)
    setError('')
    try {
      const saved = await request<CampaignBrief>(
        editingBriefId ? `/api/campaign-briefs/${encodeURIComponent(editingBriefId)}` : '/api/campaign-briefs',
        {
          method: editingBriefId ? 'PUT' : 'POST',
          body: JSON.stringify({ ...briefForm, storyIdea: briefForm.storyIdea || null }),
        },
      )
      setBriefs((current) => [saved, ...current.filter((brief) => brief.id !== saved.id)])
      setShowWizard(false)
      setEditingBriefId(null)
      setWizardStep(0)
      setError('')
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'Could not save this campaign brief.')
    } finally {
      setSaving(false)
    }
  }

  const continueWizard = () => {
    if (wizardStep === 0 && (!briefForm.title.trim() || !briefForm.genre.trim() || !briefForm.tone.trim())) {
      setError('Add a story world title, genre, and tone before continuing.')
      return
    }
    setError('')
    setWizardStep((step) => Math.min(step + 1, briefSteps.length - 1))
  }

  const leaveWizard = () => {
    setShowWizard(false)
    setEditingBriefId(null)
    setWizardStep(0)
    setError('')
  }

  const updateBrief = <K extends keyof CampaignBriefInput>(key: K, value: CampaignBriefInput[K]) => {
    setBriefForm((current) => ({ ...current, [key]: value }))
  }

  const editLines = (value: string) => value.split(/\r?\n/).map((line) => line.trim()).filter(Boolean)

  const changeStatus = async (campaign: Campaign, action: 'complete' | 'archive') => {
    const label = action === 'complete' ? 'complete' : 'archive'
    if (!window.confirm(`Move “${campaign.name}” to ${label}d story worlds?`)) return
    setError('')
    try {
      await request<void>(`/api/campaigns/${encodeURIComponent(campaign.id)}/${action}`, { method: 'POST' })
      await refreshCampaigns(campaign.id)
      setActiveTab(action === 'complete' ? 'Completed' : 'Archived')
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : `Could not ${label} this story world.`)
    }
  }

  const deleteCampaign = async (campaign: Campaign) => {
    if (!window.confirm(`Permanently delete “${campaign.name}” and all of its saved story data? This cannot be undone.`)) return
    setError('')
    try {
      await request<void>(`/api/campaigns/${encodeURIComponent(campaign.id)}`, { method: 'DELETE' })
      const result = await request<Campaign[]>('/api/campaigns')
      setCampaigns(result)
      if (selectedCampaign?.id === campaign.id) {
        setSelectedCampaign(null)
        localStorage.removeItem(selectedCampaignKey)
      }
    } catch (deleteError) {
      setError(deleteError instanceof Error ? deleteError.message : 'Could not delete this story world.')
    }
  }

  return (
    <main className="page-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="Storykeeper home">
          <img src="/storykeeper.svg" alt="" />
          <span>Storykeeper</span>
        </a>
        <span className={`service-status ${apiStatus}`} role="status" aria-live="polite">
          <span className="status-dot" aria-hidden="true" />
          {apiStatus === 'connected'
            ? 'Story server ready'
            : apiStatus === 'unavailable'
              ? 'Reconnecting to story server'
              : 'Waking up the story server'}
        </span>
      </header>

      {showWizard ? (
        <section className="brief-wizard" aria-labelledby="brief-title">
          <button className="text-button back-button" onClick={leaveWizard} disabled={saving}>
            <span aria-hidden="true">←</span> Story worlds and saved briefs
          </button>
          <div className="wizard-heading">
            <div>
              <p className="eyebrow">Parent-led campaign setup</p>
              <h1 id="brief-title">{editingBriefId ? 'Shape your story brief.' : 'Imagine a new world.'}</h1>
              <p className="intro">A few gentle choices help set the scene. You can edit this saved brief any time before generation.</p>
            </div>
            <div className="wizard-steps" aria-label="Campaign brief steps">
              {briefSteps.map((step, index) => (
                <span className={`wizard-step ${index === wizardStep ? 'current' : ''} ${index < wizardStep ? 'complete' : ''}`}
                  aria-current={index === wizardStep ? 'step' : undefined} key={step}>
                  <span>{index + 1}</span>{step}
                </span>
              ))}
            </div>
          </div>
          {error && <p className="alert" role="alert">{error}</p>}
          <form className="wizard-panel" onSubmit={(event) => void saveBrief(event)}>
            {wizardStep === 0 && (
              <div className="wizard-fields">
                <div className="wizard-copy">
                  <p className="card-kicker">STEP 1 · THE WORLD</p>
                  <h2>Where will your story begin?</h2>
                  <p>Start with a name, then choose a genre and a feeling for the adventure.</p>
                </div>
                <div className="field-stack">
                  <label htmlFor="brief-title-input">Story world title</label>
                  <input id="brief-title-input" autoFocus maxLength={120} required value={briefForm.title}
                    onChange={(event) => updateBrief('title', event.target.value)} placeholder="The Moonlit Woods" />
                  <label htmlFor="brief-genre">Genre</label>
                  <input id="brief-genre" maxLength={100} required value={briefForm.genre}
                    onChange={(event) => updateBrief('genre', event.target.value)} placeholder="Cozy fantasy, space explorers…" />
                  <label htmlFor="brief-tone">Tone</label>
                  <input id="brief-tone" maxLength={300} required value={briefForm.tone}
                    onChange={(event) => updateBrief('tone', event.target.value)} placeholder="Warm, funny, adventurous…" />
                  <label htmlFor="brief-idea">Your story spark <span>(optional)</span></label>
                  <textarea id="brief-idea" maxLength={2000} rows={3} value={briefForm.storyIdea ?? ''}
                    onChange={(event) => updateBrief('storyIdea', event.target.value)} placeholder="A tiny dragon is looking for a place to belong…" />
                </div>
              </div>
            )}
            {wizardStep === 1 && (
              <div className="wizard-fields">
                <div className="wizard-copy">
                  <p className="card-kicker">STEP 2 · THE ADVENTURE</p>
                  <h2>Make room for the fun.</h2>
                  <p>Choose a comfortable length. The story can always find a kind way forward.</p>
                </div>
                <div className="field-stack">
                  <label htmlFor="brief-campaign-length">Campaign length</label>
                  <select id="brief-campaign-length" value={briefForm.campaignLengthSessions}
                    onChange={(event) => updateBrief('campaignLengthSessions', Number(event.target.value))}>
                    <option value={3}>A short tale · 3 sessions</option>
                    <option value={6}>A story season · 6 sessions</option>
                    <option value={10}>A grand adventure · 10 sessions</option>
                  </select>
                  <label htmlFor="brief-session-length">Session length</label>
                  <select id="brief-session-length" value={briefForm.sessionLengthMinutes}
                    onChange={(event) => updateBrief('sessionLengthMinutes', Number(event.target.value))}>
                    <option value={30}>About 30 minutes</option>
                    <option value={45}>About 45 minutes</option>
                    <option value={60}>About an hour</option>
                    <option value={90}>About 90 minutes</option>
                  </select>
                </div>
              </div>
            )}
            {wizardStep === 2 && (
              <div className="wizard-fields">
                <div className="wizard-copy">
                  <p className="card-kicker">STEP 3 · YOUR GUARDRAILS</p>
                  <h2>Keep the story just right.</h2>
                  <p>Add favorite ingredients or anything you would rather leave out. These safety promises always stay in place.</p>
                  <ul className="safety-list" aria-label="Always included safety boundaries">
                    {(briefs.find((brief) => brief.id === editingBriefId)?.safetyBoundaries ?? [
                      'No gore or cruelty',
                      'No mature themes',
                      'No permanent character death',
                      'No mandatory tactical combat',
                      'Keep the adventure low-fright and age-appropriate',
                    ]).map((boundary) => <li key={boundary}>{boundary}</li>)}
                  </ul>
                </div>
                <div className="field-stack">
                  <label htmlFor="brief-inclusions">Things to include <span>(one per line, optional)</span></label>
                  <textarea id="brief-inclusions" maxLength={2000} rows={5} value={briefForm.inclusions.join('\n')}
                    onChange={(event) => updateBrief('inclusions', editLines(event.target.value))} placeholder={'Friendly dragons\nPuzzles and hidden gardens\nA helpful talking fox'} />
                  <label htmlFor="brief-exclusions">Things to avoid <span>(one per line, optional)</span></label>
                  <textarea id="brief-exclusions" maxLength={2000} rows={4} value={briefForm.exclusions.join('\n')}
                    onChange={(event) => updateBrief('exclusions', editLines(event.target.value))} placeholder={'Spiders\nStorms'} />
                </div>
              </div>
            )}
            <div className="wizard-actions">
              <button className="text-button" type="button" onClick={leaveWizard} disabled={saving}>Discard changes</button>
              <span className="wizard-action-spacer" />
              {wizardStep > 0 && <button className="button button-secondary" type="button" onClick={() => { setError(''); setWizardStep((step) => step - 1) }} disabled={saving}>Back</button>}
              {wizardStep < briefSteps.length - 1 ? (
                <button className="button button-primary" type="button" onClick={continueWizard}>Next step <span aria-hidden="true">→</span></button>
              ) : (
                <button className="button button-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save campaign brief'}</button>
              )}
            </div>
          </form>
        </section>
      ) : selectedCampaign ? (
        <section className="campaign-room" aria-labelledby="room-title">
          <button className="text-button back-button" onClick={() => setSelectedCampaign(null)}>
            <span aria-hidden="true">←</span> All story worlds
          </button>
          <div className="room-heading">
            <div>
              <p className="eyebrow">{selectedCampaign.theme || 'Your story world'} · {selectedCampaign.status}</p>
              <h1 id="room-title">{selectedCampaign.name}</h1>
              <p className="intro">{selectedCampaign.description || 'A new adventure is ready to begin.'}</p>
            </div>
            <div className="room-actions">
              {selectedCampaign.status === 'Active' && (
                <button className="text-button" onClick={() => void changeStatus(selectedCampaign, 'complete')}>Mark complete</button>
              )}
            </div>
          </div>
          {error && <p className="alert" role="alert">{error}</p>}
          <div className="room-grid">
            <section className="room-panel">
              <p className="card-kicker">LAST TIME IN YOUR STORY</p>
              <h2>{selectedCampaign.latestSession
                ? `Adventure ${selectedCampaign.latestSession.sessionNumber}`
                : 'The first page is waiting'}</h2>
              <p>{selectedCampaign.latestSession?.summary || selectedCampaign.currentSituation || 'Your world is saved here, ready whenever your family wants to play.'}</p>
              {selectedCampaign.currentQuest && (
                <div className="quest-note">
                  <span className="card-kicker">CURRENT QUEST</span>
                  <strong>{selectedCampaign.currentQuest.title}</strong>
                  <span>{selectedCampaign.currentQuest.description}</span>
                </div>
              )}
            </section>
            <section className="room-panel party-panel">
              <p className="card-kicker">{selectedCampaign.partyName || 'YOUR PARTY'}</p>
              <h2>{selectedCampaign.heroes.length ? 'Your brave heroes' : 'Heroes gather here'}</h2>
              {selectedCampaign.heroes.length ? (
                <div className="hero-list">
                  {selectedCampaign.heroes.map((hero) => (
                    <article className="hero-row" key={hero.id}>
                      <span className="hero-avatar" aria-hidden="true">{hero.name.slice(0, 1).toUpperCase()}</span>
                      <span className="hero-info">
                        <strong>{hero.name}</strong>
                        <span>{hero.role} · {hero.hearts} hearts · {hero.sparkleTokens} sparkle</span>
                      </span>
                    </article>
                  ))}
                </div>
              ) : (
                <p>Your party and its treasures will be saved with this story world.</p>
              )}
            </section>
          </div>
        </section>
      ) : (
        <section className="library" aria-labelledby="library-title">
          <div className="library-heading">
            <div>
              <p className="eyebrow">A little wonder, just around the corner</p>
              <h1 id="library-title">Your story <em>worlds.</em></h1>
              <p className="intro">Every adventure stays safe in its own storybook. Pick up where you left off, or start somewhere new.</p>
            </div>
            <div className="library-heading-actions">
              <button className="button button-primary create-button" onClick={() => startBrief()}>
                <span className="button-plus" aria-hidden="true">+</span> New campaign brief
              </button>
              <button className="button button-secondary create-button" onClick={() => { setError(''); setShowCreate(true) }}>
                Create blank world
              </button>
            </div>
          </div>

          {error && <p className="alert" role="alert">{error}</p>}

          {briefs.length > 0 && (
            <section className="saved-briefs" aria-labelledby="saved-briefs-title">
              <div className="saved-briefs-heading">
                <div>
                  <p className="card-kicker">READY WHEN YOU ARE</p>
                  <h2 id="saved-briefs-title">Campaign briefs</h2>
                </div>
                <span>{briefs.length} saved {briefs.length === 1 ? 'brief' : 'briefs'}</span>
              </div>
              <div className="brief-grid">
                {briefs.map((brief) => (
                  <article className="brief-card" key={brief.id}>
                    <div>
                      <p className="card-kicker">{brief.genre} · {brief.campaignLengthSessions} sessions</p>
                      <h3>{brief.title}</h3>
                      <p>{brief.tone}</p>
                    </div>
                    <div className="brief-card-actions">
                      <button className="button button-secondary" onClick={() => startBrief(brief)}>Edit brief</button>
                      <button className="text-button delete-action" onClick={() => void discardBrief(brief)}>Discard</button>
                    </div>
                  </article>
                ))}
              </div>
            </section>
          )}

          <div className="library-toolbar">
            <div className="tabs" role="tablist" aria-label="Story world status">
              {tabs.map((tab) => (
                <button
                  className={`tab ${activeTab === tab ? 'selected' : ''}`}
                  id={`tab-${tab.toLowerCase()}`}
                  key={tab}
                  role="tab"
                  aria-selected={activeTab === tab}
                  aria-controls="campaign-list"
                  onClick={() => setActiveTab(tab)}
                >
                  {tab} <span className="tab-count">{campaigns.filter((campaign) => campaign.status === tab).length}</span>
                </button>
              ))}
            </div>
            <span className="save-note"><span aria-hidden="true">✦</span> Your stories save as you go</span>
          </div>

          {loading ? (
            <div className="empty-state" role="status">Finding your storybooks…</div>
          ) : (
            <div className="campaign-grid" id="campaign-list" role="tabpanel" aria-labelledby={`tab-${activeTab.toLowerCase()}`}>
              {visibleCampaigns.length ? visibleCampaigns.map((campaign) => (
                <CampaignCard
                  campaign={campaign}
                  key={campaign.id}
                  onOpen={() => selectCampaign(campaign)}
                  onComplete={() => void changeStatus(campaign, 'complete')}
                  onArchive={() => void changeStatus(campaign, 'archive')}
                  onDelete={() => void deleteCampaign(campaign)}
                />
              )) : (
                <div className="empty-state">
                  <span className="empty-star" aria-hidden="true">✧</span>
                  <h2>{activeTab === 'Active' ? 'A fresh page awaits' : `No ${activeTab.toLowerCase()} worlds yet`}</h2>
                  <p>{activeTab === 'Active'
                    ? 'Create a cozy world and gather your adventurers.'
                    : 'Story worlds will appear here when you need them.'}</p>
                  {activeTab === 'Active' && (
                    <button className="button button-secondary" onClick={() => startBrief()}>Start a campaign brief</button>
                  )}
                </div>
              )}
            </div>
          )}
        </section>
      )}

      <footer className="footer">
        <span>Kind stories. Brave little heroes. Happy endings.</span>
        <span className="footer-mark">✦ &nbsp; The adventure is yours</span>
      </footer>

      {showCreate && (
        <div className="dialog-backdrop" onMouseDown={(event) => {
          if (event.target === event.currentTarget && !saving) setShowCreate(false)
        }}>
          <section className="create-dialog" role="dialog" aria-modal="true" aria-labelledby="create-title">
            <button className="dialog-close" aria-label="Close" disabled={saving} onClick={() => setShowCreate(false)}>×</button>
            <p className="eyebrow">A brand-new beginning</p>
            <h2 id="create-title">Name your story world</h2>
            <p className="dialog-intro">Create an empty story world without a campaign-generation brief.</p>
            <form onSubmit={(event) => void createCampaign(event)}>
              <label htmlFor="campaign-name">Story world name</label>
              <input id="campaign-name" name="name" maxLength={120} required autoFocus placeholder="The Moonlit Woods" />
              <label htmlFor="campaign-premise">A little about this world <span>(optional)</span></label>
              <textarea id="campaign-premise" name="description" maxLength={2000} rows={3} placeholder="A friendly forest where the stars sometimes fall…" />
              <div className="dialog-actions">
                <button className="button button-secondary" type="button" disabled={saving} onClick={() => setShowCreate(false)}>Not yet</button>
                <button className="button button-primary" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Create world'}</button>
              </div>
            </form>
          </section>
        </div>
      )}
    </main>
  )
}

function CampaignCard({
  campaign,
  onOpen,
  onComplete,
  onArchive,
  onDelete,
}: {
  campaign: Campaign
  onOpen: () => void
  onComplete: () => void
  onArchive: () => void
  onDelete: () => void
}) {
  return (
    <article className="campaign-card">
      <div className="campaign-art" aria-hidden="true">
        <span className="art-moon" />
        <span className="art-star art-star-one">✦</span>
        <span className="art-star art-star-two">✧</span>
        <span className="art-hill art-hill-back" />
        <span className="art-hill art-hill-front" />
        <span className="art-status">{campaign.status}</span>
      </div>
      <div className="campaign-content">
        <p className="card-kicker">{campaign.theme || 'STORY WORLD'}</p>
        <h2>{campaign.name}</h2>
        <p className="campaign-premise">{campaign.description || 'A new adventure is ready to begin.'}</p>
        <div className="campaign-meta">
          <span><span aria-hidden="true">◷</span> {campaign.latestSession
            ? `Last played · Adventure ${campaign.latestSession.sessionNumber}`
            : 'No adventures yet'}</span>
          {campaign.latestSession?.summary && (
            <span className="latest-summary"><span aria-hidden="true">“</span>{campaign.latestSession.summary}</span>
          )}
          {campaign.currentQuest && <span><span aria-hidden="true">✧</span> {campaign.currentQuest.title}</span>}
          <span><span aria-hidden="true">♙</span> {campaign.heroes.length
            ? campaign.heroes.map((hero) => hero.name).join(', ')
            : 'Heroes gathering'}</span>
        </div>
        <div className="card-actions">
          <button className="button button-primary card-open" onClick={onOpen}>
            {campaign.status === 'Archived' ? 'View story' : 'Continue'} <span aria-hidden="true">→</span>
          </button>
          <div className="more-actions">
            {campaign.status === 'Active' && (
              <button className="text-button" onClick={onComplete}>Complete</button>
            )}
            {campaign.status !== 'Archived' && (
              <button className="text-button" onClick={onArchive}>Archive</button>
            )}
            <button className="text-button delete-action" onClick={onDelete}>Delete</button>
          </div>
        </div>
      </div>
    </article>
  )
}

export default App

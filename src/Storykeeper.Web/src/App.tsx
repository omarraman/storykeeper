import { useEffect, useMemo, useState } from 'react'
import './App.css'

type ApiStatus = 'checking' | 'connected' | 'unavailable'
type CampaignStatus = 'Active' | 'Completed' | 'Archived'
type CampaignTab = CampaignStatus

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
  const [selectedCampaign, setSelectedCampaign] = useState<Campaign | null>(null)
  const [activeTab, setActiveTab] = useState<CampaignTab>('Active')
  const [loading, setLoading] = useState(true)
  const [showCreate, setShowCreate] = useState(false)
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
        if (stopped) return
        setCampaigns(result)
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

      {selectedCampaign ? (
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
            <button className="button button-primary create-button" onClick={() => { setError(''); setShowCreate(true) }}>
              <span className="button-plus" aria-hidden="true">+</span> New story world
            </button>
          </div>

          {error && <p className="alert" role="alert">{error}</p>}

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
                    <button className="button button-secondary" onClick={() => setShowCreate(true)}>Create a story world</button>
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
            <p className="dialog-intro">Give your adventure a name and a little hint of what makes it special.</p>
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

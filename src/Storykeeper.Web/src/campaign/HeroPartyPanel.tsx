import { useState } from 'react'
import { request } from '../api/request'
import type { HeroStatus } from '../adventure/contracts'
import './HeroPartyPanel.css'

export interface PartyHero extends HeroStatus {
  description: string
}

interface HeroPartyPanelProps {
  campaignId: string
  partyName: string
  heroes: PartyHero[]
  canManageHeroes: boolean
  activeSession: boolean
  onHeroAdded: (hero: PartyHero) => void
}

export function HeroPartyPanel({
  campaignId,
  partyName,
  heroes,
  canManageHeroes,
  activeSession,
  onHeroAdded,
}: HeroPartyPanelProps) {
  const [expanded, setExpanded] = useState(heroes.length === 0 && canManageHeroes && !activeSession)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [role, setRole] = useState('')
  const [strengthText, setStrengthText] = useState('')
  const [parentPin, setParentPin] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')

  const addHero = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setBusy(true)
    setError('')
    try {
      const hero = await request<PartyHero>(`/api/campaigns/${encodeURIComponent(campaignId)}/heroes`, {
        method: 'POST',
        headers: { 'X-Parent-Pin': parentPin },
        body: JSON.stringify({
          name,
          description,
          role,
          strengths: strengthText.split(/\r?\n/).map((strength) => strength.trim()).filter(Boolean),
        }),
      })
      onHeroAdded(hero)
      setName('')
      setDescription('')
      setRole('')
      setStrengthText('')
      setParentPin('')
      setExpanded(false)
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : 'The hero could not be saved.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="room-panel party-panel" aria-labelledby="party-title">
      <p className="card-kicker">{partyName || 'YOUR PARTY'}</p>
      <h2 id="party-title">{heroes.length ? 'Your brave heroes' : 'Heroes gather here'}</h2>
      {heroes.length ? (
        <div className="hero-list">
          {heroes.map((hero) => (
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
        <p>Add a saved hero here before starting a real adventure. Demo heroes are only for previews.</p>
      )}

      {activeSession && (
        <p className="hero-party-hint">End the current adventure before changing your party.</p>
      )}

      {canManageHeroes && !activeSession && (
        <>
          {heroes.length > 0 && (
            <button
              className="text-button hero-party-toggle"
              type="button"
              aria-expanded={expanded}
              aria-controls={`hero-form-${campaignId}`}
              onClick={() => setExpanded((value) => !value)}
            >
              {expanded ? 'Cancel adding a hero' : 'Add another hero'}
            </button>
          )}
          {expanded && (
            <form className="hero-create-form" id={`hero-form-${campaignId}`} onSubmit={(event) => void addHero(event)}>
              <label>
                Hero name
                <input required maxLength={120} value={name} onChange={(event) => setName(event.target.value)} />
              </label>
              <label>
                What are they like?
                <textarea required maxLength={500} rows={2} value={description}
                  onChange={(event) => setDescription(event.target.value)} />
              </label>
              <label>
                Role
                <input required maxLength={80} placeholder="Curious scout" value={role}
                  onChange={(event) => setRole(event.target.value)} />
              </label>
              <label>
                Strengths <span>(optional, one per line, up to 6)</span>
                <textarea maxLength={650} rows={3} placeholder={'Noticing tiny details\nKindness'}
                  value={strengthText} onChange={(event) => setStrengthText(event.target.value)} />
              </label>
              <label>
                Parent PIN
                <input required type="password" inputMode="numeric" autoComplete="current-password"
                  value={parentPin} onChange={(event) => setParentPin(event.target.value)} />
              </label>
              <p className="hero-party-hint">The server checks your PIN. It is not saved in this browser.</p>
              {error && <p className="alert" role="alert">{error}</p>}
              <button className="button button-primary" type="submit" disabled={busy}>
                {busy ? 'Saving hero…' : 'Save hero'}
              </button>
            </form>
          )}
        </>
      )}
    </section>
  )
}

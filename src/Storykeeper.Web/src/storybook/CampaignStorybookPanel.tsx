import { useEffect, useState } from 'react'
import { request } from '../api/request'

type Storybook = {
  sessions: {
    id: string
    sessionNumber: number
    title: string
    startedAtUtc: string
    endedAtUtc: string | null
    summary: string | null
    discoveries: { category: string; statement: string }[]
    heroAchievements: { heroName: string; description: string }[]
  }[]
  rewards: { name: string; description: string; heroName: string | null; isClaimed: boolean }[]
}

type CampaignStorybookPanelProps = {
  campaignId: string
}

export function CampaignStorybookPanel({ campaignId }: CampaignStorybookPanelProps) {
  const [storybook, setStorybook] = useState<Storybook | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    let stopped = false
    request<Storybook>(`/api/campaigns/${encodeURIComponent(campaignId)}/storybook`)
      .then((result) => {
        if (!stopped) setStorybook(result)
      })
      .catch((loadError: unknown) => {
        if (!stopped) setError(loadError instanceof Error ? loadError.message : 'Could not load this storybook.')
      })
    return () => { stopped = true }
  }, [campaignId])

  return (
    <section className="storybook-panel" aria-labelledby="storybook-title">
      <div className="storybook-heading">
        <div>
          <p className="card-kicker">CAMPAIGN MEMORY</p>
          <h2 id="storybook-title">Storybook</h2>
          <p>Every wrapped-up adventure, discovery, and brave hero moment in one place.</p>
        </div>
        <a className="button button-secondary" href={`/api/campaigns/${encodeURIComponent(campaignId)}/export`}
          download>
          Export campaign JSON
        </a>
      </div>
      {error && <p className="alert" role="alert">{error}</p>}
      {!storybook ? (
        !error && <p className="storybook-empty" role="status">Gathering your campaign memories…</p>
      ) : (
        <>
          {storybook.sessions.length ? (
            <ol className="storybook-sessions">
              {storybook.sessions.map((session) => (
                <li className="storybook-session" key={session.id}>
                  <article>
                    <div className="storybook-session-heading">
                      <div>
                        <p className="card-kicker">ADVENTURE {session.sessionNumber}</p>
                        <h3>{session.title || `Adventure ${session.sessionNumber}`}</h3>
                      </div>
                      <time dateTime={session.startedAtUtc}>
                        {new Date(session.startedAtUtc).toLocaleDateString()}
                      </time>
                    </div>
                    <p className="storybook-summary">{session.summary || 'This adventure is still unfolding.'}</p>
                    {(session.discoveries.length > 0 || session.heroAchievements.length > 0) && (
                      <div className="storybook-highlights">
                        {session.discoveries.length > 0 && (
                          <section aria-label="Major discoveries">
                            <h4>Major discoveries</h4>
                            <ul>{session.discoveries.map((discovery, index) => (
                              <li key={`${discovery.category}-${index}`}>
                                <strong>{discovery.category}</strong> · {discovery.statement}
                              </li>
                            ))}</ul>
                          </section>
                        )}
                        {session.heroAchievements.length > 0 && (
                          <section aria-label="Hero achievements">
                            <h4>Hero achievements</h4>
                            <ul>{session.heroAchievements.map((achievement, index) => (
                              <li key={`${achievement.heroName}-${index}`}>
                                <strong>{achievement.heroName}</strong> · {achievement.description}
                              </li>
                            ))}</ul>
                          </section>
                        )}
                      </div>
                    )}
                  </article>
                </li>
              ))}
            </ol>
          ) : (
            <p className="storybook-empty">Your first adventure will be remembered here.</p>
          )}
          {storybook.rewards.length > 0 && (
            <section className="storybook-rewards" aria-labelledby="storybook-rewards-title">
              <h3 id="storybook-rewards-title">Campaign rewards</h3>
              <ul>{storybook.rewards.map((reward, index) => (
                <li key={`${reward.name}-${index}`}>
                  <strong>{reward.name}</strong>
                  {reward.heroName && <span> · {reward.heroName}</span>}
                  <p>{reward.description}</p>
                  <span>{reward.isClaimed ? 'Collected' : 'Waiting to be claimed'}</span>
                </li>
              ))}</ul>
            </section>
          )}
        </>
      )}
    </section>
  )
}

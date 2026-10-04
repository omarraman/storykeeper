import { useEffect, useState } from 'react'
import './App.css'

type ApiStatus = 'checking' | 'connected' | 'unavailable'

function App() {
  const [apiStatus, setApiStatus] = useState<ApiStatus>('checking')

  useEffect(() => {
    let retryTimer: ReturnType<typeof setTimeout>
    let stopped = false
    let controller: AbortController

    const checkApi = async () => {
      controller = new AbortController()

      try {
        const response = await fetch('/api/health', { signal: controller.signal })
        const result: { status?: string } = await response.json()

        if (!response.ok || result.status !== 'Healthy') {
          throw new Error('The API is not healthy')
        }

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

      <section className="welcome" aria-labelledby="welcome-title">
        <div className="welcome-copy">
          <p className="eyebrow">A little wonder, just around the corner</p>
          <h1 id="welcome-title">Every great story starts <em>together.</em></h1>
          <p className="intro">
            Make a cozy world, gather your adventurers, and see where your
            imagination takes you.
          </p>
          <div className="welcome-note">
            <span className="sparkle" aria-hidden="true">✦</span>
            <span>Made for curious kids and the grown-ups who guide them.</span>
          </div>
        </div>

        <div className="story-card" aria-label="An invitation to begin an adventure">
          <div className="moon" aria-hidden="true" />
          <div className="hill hill-back" aria-hidden="true" />
          <div className="hill hill-front" aria-hidden="true" />
          <div className="card-caption">
            <span className="card-kicker">YOUR STORYBOOK AWAITS</span>
            <span className="card-title">What will you discover?</span>
          </div>
          <span className="card-star star-one" aria-hidden="true">✦</span>
          <span className="card-star star-two" aria-hidden="true">✧</span>
          <span className="card-star star-three" aria-hidden="true">✦</span>
        </div>
      </section>

      <footer className="footer">
        <span>Kind stories. Brave little heroes. Happy endings.</span>
        <span className="footer-mark">✦ &nbsp; The adventure is yours</span>
      </footer>
    </main>
  )
}

export default App

import { useEffect, useState } from 'react'

type InstallPrompt = Event & {
  prompt: () => Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed'; platform: string }>
}

export function PwaInstallControl() {
  const [installPrompt, setInstallPrompt] = useState<InstallPrompt | null>(null)
  const [helpOpen, setHelpOpen] = useState(false)
  const [installed, setInstalled] = useState(false)
  const [prompting, setPrompting] = useState(false)
  const [promptError, setPromptError] = useState('')

  useEffect(() => {
    if (window.matchMedia?.('(display-mode: standalone)').matches) setInstalled(true)

    const onBeforeInstallPrompt = (event: Event) => {
      event.preventDefault()
      setInstallPrompt(event as InstallPrompt)
    }
    const onInstalled = () => {
      setInstalled(true)
      setInstallPrompt(null)
    }

    window.addEventListener('beforeinstallprompt', onBeforeInstallPrompt)
    window.addEventListener('appinstalled', onInstalled)
    return () => {
      window.removeEventListener('beforeinstallprompt', onBeforeInstallPrompt)
      window.removeEventListener('appinstalled', onInstalled)
    }
  }, [])

  useEffect(() => {
    if (!helpOpen) return
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setHelpOpen(false)
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [helpOpen])

  const install = () => {
    if (!installPrompt) {
      setHelpOpen(true)
      return
    }

    const prompt = installPrompt
    setInstallPrompt(null)
    setPromptError('')
    setPrompting(true)
    void prompt.prompt()
      .then(() => prompt.userChoice)
      .then((choice) => {
        if (choice.outcome === 'accepted') setInstalled(true)
        else setHelpOpen(true)
      })
      .catch(() => {
        setPromptError('The install prompt could not be opened. You can still install from your browser menu.')
        setHelpOpen(true)
      })
      .finally(() => setPrompting(false))
  }

  return (
    <>
      <button className="button button-secondary install-button" onClick={install} disabled={installed || prompting}>
        {installed ? 'App installed' : prompting ? 'Opening…' : 'Install app'}
      </button>
      {helpOpen && (
        <div className="install-dialog-backdrop" onMouseDown={(event) => {
          if (event.target === event.currentTarget) setHelpOpen(false)
        }}>
          <section className="install-dialog" role="dialog" aria-modal="true" aria-labelledby="install-title">
            <button className="dialog-close" aria-label="Close install help" onClick={() => setHelpOpen(false)}>×</button>
            <p className="eyebrow">Take the story with you</p>
            <h2 id="install-title">Install Storykeeper</h2>
            {promptError && <p className="install-error" role="alert">{promptError}</p>}
            <p>On Android, open this page in Chrome or Samsung Internet, then choose <strong>Install app</strong> or <strong>Add to Home screen</strong> from the browser menu. The exact wording depends on your browser.</p>
            <p>For installation and offline loading, open Storykeeper over HTTPS. During a brief connection interruption, the app shell and previously loaded static files remain available; saved campaigns and story play still need the story server.</p>
            <div className="dialog-actions">
              <button className="button button-primary" onClick={() => setHelpOpen(false)}>Got it</button>
            </div>
          </section>
        </div>
      )}
    </>
  )
}

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

if ('serviceWorker' in navigator) {
  void navigator.serviceWorker.register('/service-worker.js')
    .catch((error: unknown) => console.error('Storykeeper could not register offline support.', error))
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

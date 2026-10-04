const SHELL_CACHE = 'storykeeper-shell-v1'
const STATIC_CACHE = 'storykeeper-static-v1'
const APP_SHELL = ['/', '/index.html', '/manifest.webmanifest', '/storykeeper.svg']

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(SHELL_CACHE)
      .then(async (cache) => {
        await cache.addAll(APP_SHELL)
        const appEntry = await cache.match('/index.html')
        if (!appEntry) throw new Error('Storykeeper app entry was not precached.')

        const html = await appEntry.text()
        const assets = Array.from(
          html.matchAll(/(?:src|href)=["']([^"']*\/assets\/[^"']+)["']/g),
          (match) => match[1],
        )
        if (assets.length === 0) throw new Error('Storykeeper build contains no precacheable assets.')
        await caches.open(STATIC_CACHE).then((staticCache) => staticCache.addAll(assets))
      })
      .then(() => self.skipWaiting()),
  )
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys
        .filter((key) => key.startsWith('storykeeper-') && ![SHELL_CACHE, STATIC_CACHE].includes(key))
        .map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const request = event.request
  const url = new URL(request.url)
  if (request.method !== 'GET' || url.origin !== self.location.origin) return

  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then(async (response) => {
          if (response.ok && response.headers.get('content-type')?.includes('text/html')) {
            const cache = await caches.open(SHELL_CACHE)
            await cache.put('/', response.clone())
          }
          return response
        })
        .catch(async () => (await caches.match('/')) ?? Response.error()),
    )
    return
  }

  const isStaticAsset = url.pathname.startsWith('/assets/')
    || url.pathname === '/manifest.webmanifest'
    || url.pathname === '/storykeeper.svg'
  if (!isStaticAsset) return

  event.respondWith(
    caches.open(STATIC_CACHE).then(async (cache) => {
      const cached = await cache.match(request)
      if (cached) return cached

      const response = await fetch(request)
      if (response.ok) await cache.put(request, response.clone())
      return response
    }),
  )
})

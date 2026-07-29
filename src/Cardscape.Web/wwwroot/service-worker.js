// Cardscape service worker — PWA shell caching with a
// network-first policy for /api/* (so the client always
// gets fresh data when online) and a cache-first policy
// for the static app shell (HTML, CSS, JS, images). Falls
// back to a cached shell when the network is unreachable
// so the app can be launched from the home-screen icon
// while offline.

const CACHE_VERSION = 'cardscape-v1';
const APP_SHELL_CACHE = `${CACHE_VERSION}-shell`;
const RUNTIME_CACHE = `${CACHE_VERSION}-runtime`;

// Pre-cache the minimal app shell. The Blazor WASM
// boot resource (_framework/dotnet.js) and the static
// CSS/JS bundles are versioned by the build (fingerprint
// in the URL) so they always hit the network and never
// need a long-lived cache.
const APP_SHELL_URLS = [
    '/',
    '/index.html',
    '/manifest.webmanifest',
    '/favicon.png',
    '/css/app.css'
];

self.addEventListener('install', (event) => {
    event.waitUntil((async () => {
        const cache = await caches.open(APP_SHELL_CACHE);
        await cache.addAll(APP_SHELL_URLS);
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', (event) => {
    event.waitUntil((async () => {
        const keys = await caches.keys();
        await Promise.all(
            keys
                .filter((k) => k.startsWith('cardscape-') && k !== APP_SHELL_CACHE && k !== RUNTIME_CACHE)
                .map((k) => caches.delete(k))
        );
        await self.clients.claim();
    })());
});

self.addEventListener('fetch', (event) => {
    const request = event.request;

    if (request.method !== 'GET') {
        return;
    }

    const url = new URL(request.url);

    // ── API: network-first with no caching of responses.
    // The Cardscape API is the source of truth; we never
    // want to serve a stale board / card / comment from
    // a previous session.
    if (url.pathname.startsWith('/api/')
        || url.pathname.startsWith('/hubs/')
        || url.pathname.startsWith('/_blazor/')) {
        event.respondWith(fetch(request).catch(() => new Response('', {
            status: 503,
            statusText: 'Service Unavailable (offline)'
        })));
        return;
    }

    // ── Static app shell / SPA navigation: cache-first,
    // fall back to network, fall back to /index.html so
    // the Blazor client-side router can take over.
    if (request.mode === 'navigate') {
        event.respondWith((async () => {
            const cached = await caches.match(request);
            if (cached) {
                return cached;
            }
            try {
                const fresh = await fetch(request);
                const cache = await caches.open(RUNTIME_CACHE);
                cache.put(request, fresh.clone());
                return fresh;
            } catch (err) {
                const shell = await caches.match('/index.html');
                if (shell) {
                    return shell;
                }
                return new Response('Offline and no cached shell.', {
                    status: 503,
                    statusText: 'Service Unavailable'
                });
            }
        })());
        return;
    }

    // ── Other GETs (CSS/JS/images/icons/manifest): try
    // cache first, then network. The runtime cache holds
    // anything we have seen.
    event.respondWith((async () => {
        const cached = await caches.match(request);
        if (cached) {
            return cached;
        }
        try {
            const fresh = await fetch(request);
            if (fresh && fresh.status === 200 && fresh.type === 'basic') {
                const cache = await caches.open(RUNTIME_CACHE);
                cache.put(request, fresh.clone());
            }
            return fresh;
        } catch (err) {
            return new Response('', { status: 504, statusText: 'Gateway Timeout (offline)' });
        }
    })());
});

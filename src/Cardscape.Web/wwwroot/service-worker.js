// Cardscape service worker — PWA shell caching with a
// network-first policy for /api/* (so the client always
// gets fresh data when online) and a network-first policy
// for the static app shell (HTML, CSS, JS, images) so a
// new build is picked up on the next visit instead of being
// masked by a cached pre-rebuild copy. The cache is only
// used as the offline fallback, so the app can still be
// launched from the home-screen icon when the network is
// unreachable.

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

    // ── Static app shell / SPA navigation: network-first,
    // fall back to the cache, fall back to /index.html so
    // the Blazor client-side router can take over.
    //
    // Why network-first: a previous version of this handler
    // served cached /index.html (and the cached
    // _framework/blazor.webassembly.js) on every visit. After
    // any deploy that changes the framework assets, the boot
    // manifest inside the cached JS became incompatible with
    // the new fingerprinted .wasm files on the server, and
    // the app failed with "An unhandled error has occurred"
    // until the user did a Ctrl+Shift+R. The cache version
    // (CACHE_VERSION above) is hardcoded, so the activate
    // handler can't tell old caches from new ones, and the
    // install event only re-fires when the SW file itself
    // changes — neither path picks up new content otherwise.
    // Going network-first means every visit always sees the
    // current index.html and the current framework assets;
    // the cache is only used when the network is unreachable,
    // which preserves the offline launch from the home-screen
    // icon.
    if (request.mode === 'navigate') {
        event.respondWith((async () => {
            try {
                const fresh = await fetch(request);
                const cache = await caches.open(RUNTIME_CACHE);
                cache.put(request, fresh.clone());
                return fresh;
            } catch (err) {
                const cached = await caches.match(request);
                if (cached) {
                    return cached;
                }
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

    // ── Framework files under _framework/*: always network-first.
    // These are either content-hashed (Cardscape.Web.<hash>.wasm,
    // dotnet.runtime.<hash>.js, …) or are stable entry points
    // (dotnet.js, blazor.webassembly.js) that change shape between
    // SDK versions. A stale cached copy from a previous build will
    // throw a startup error (e.g. "Blazor detected a change in the
    // application's culture…") when the new boot manifest is
    // loaded against the old runtime. Cache only the network result
    // for offline use, never serve a stale one when online.
    if (url.pathname.startsWith('/_framework/')) {
        event.respondWith((async () => {
            try {
                const fresh = await fetch(request);
                if (fresh && fresh.status === 200 && fresh.type === 'basic') {
                    const cache = await caches.open(RUNTIME_CACHE);
                    cache.put(request, fresh.clone());
                }
                return fresh;
            } catch (err) {
                const cached = await caches.match(request);
                if (cached) {
                    return cached;
                }
                return new Response('', { status: 504, statusText: 'Gateway Timeout (offline)' });
            }
        })());
        return;
    }

    // ── Static subresources (css/, lib/, _content/, icons/,
    // manifest, favicon, scoped-css bundles, …): network-first,
    // fall back to the cache when offline. The previous version
    // of this handler was cache-first, which served stale
    // css/app.css (and any other static asset) from a previous
    // build until the user did a Ctrl+Shift+R. That broke the
    // layout every time the UI was refreshed (the old CSS
    // predated the grid/flex rules added in the UI refresh) and
    // made the page look unstyled. Network-first here means the
    // user always gets the current styles; the cache is only used
    // when the network is unreachable (so offline launch from
    // the home-screen icon still works).
    event.respondWith((async () => {
        try {
            const fresh = await fetch(request);
            if (fresh && fresh.status === 200 && fresh.type === 'basic') {
                const cache = await caches.open(RUNTIME_CACHE);
                cache.put(request, fresh.clone());
            }
            return fresh;
        } catch (err) {
            const cached = await caches.match(request);
            if (cached) {
                return cached;
            }
            return new Response('', { status: 504, statusText: 'Gateway Timeout (offline)' });
        }
    })());
});

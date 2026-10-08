// Cardscape boot splash — runs synchronously in <head>, before the
// first paint, so the screen shown while the WebAssembly runtime
// downloads already wears the user's theme instead of a white page.
//
// The theme stylesheet itself is only emitted by <RadzenTheme> once
// Blazor renders App.razor, which is seconds away on a cold load.
// Until then we paint from a tiny palette per theme (body background,
// surface, primary, text) mirrored from the Radzen / Cardscape theme
// files. The palette is keyed by the `CardscapeTheme` cookie the
// Radzen cookie theme service writes (Program.cs), which always holds
// the *applied* theme, light or dark sibling included.
//
// When Blazor has rendered and the theme stylesheet has loaded,
// App.razor calls cardscape.bootComplete() and the splash fades out.
(function () {
    'use strict';

    // [body background, surface, primary, text, secondary text]
    var palettes = {
        'cardscape-classic': ['#f6f8fa', '#ffffff', '#0f766e', '#1f2328', '#57606a'],
        'cardscape-classic-dark': ['#0d1117', '#161b22', '#2dd4bf', '#e6edf3', '#8b949e'],
        'default': ['#f6f7fa', '#ffffff', '#ff6d41', '#3a474d', '#545e61'],
        'dark': ['#28363c', '#38474e', '#ff6d41', '#f6f7fa', '#dadfe2'],
        'humanistic': ['#f3f5f7', '#ffffff', '#d64d42', '#30445f', '#395374'],
        'humanistic-dark': ['#2b3a50', '#30445f', '#d64d42', '#f3f5f7', '#d9e1ea'],
        'material': ['#f5f5f5', '#ffffff', '#4340d2', '#424242', '#616161'],
        'material-dark': ['#121212', '#1e1e1e', '#bb86fc', '#e0e0e0', '#a0a0a0'],
        'software': ['#f6f7fa', '#ffffff', '#598087', '#3a474d', '#545e61'],
        'software-dark': ['#28363c', '#3a474d', '#598087', '#f5f8f9', '#dae0e2'],
        'standard': ['#f4f5f9', '#ffffff', '#1151f3', '#4f4f50', '#707072'],
        'standard-dark': ['#19191a', '#242527', '#3871ff', '#eaebec', '#c9cacd']
    };
    var darkThemes = /(^dark$|-dark$)/;

    function readCookie(name) {
        var match = document.cookie.match(new RegExp('(?:^|; )' + name + '=([^;]*)'));
        if (!match) {
            return null;
        }
        try {
            return decodeURIComponent(match[1]);
        } catch (e) {
            return match[1];
        }
    }

    var prefersDark = !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches);
    var theme = readCookie('CardscapeTheme');
    if (!theme || !palettes.hasOwnProperty(theme)) {
        theme = prefersDark ? 'cardscape-classic-dark' : 'cardscape-classic';
    }

    var p = palettes[theme];
    var root = document.documentElement;
    root.style.setProperty('--cs-boot-bg', p[0]);
    root.style.setProperty('--cs-boot-surface', p[1]);
    root.style.setProperty('--cs-boot-primary', p[2]);
    root.style.setProperty('--cs-boot-text', p[3]);
    root.style.setProperty('--cs-boot-muted', p[4]);
    root.style.colorScheme = darkThemes.test(theme) ? 'dark' : 'light';

    var themeColor = document.querySelector('meta[name="theme-color"]');
    if (themeColor) {
        themeColor.setAttribute('content', p[0]);
    }

    var language = null;
    try {
        language = window.localStorage.getItem('Cardscape.Culture');
    } catch (e) {
        // Storage blocked (private mode, cookies off): use the browser language.
    }
    language = (language || navigator.language || 'en').toLowerCase();
    var loadingLabel = language.indexOf('es') === 0 ? 'Cargando Cardscape…' : 'Loading Cardscape…';

    document.addEventListener('DOMContentLoaded', function () {
        var splash = document.getElementById('cs-boot');
        if (splash) {
            splash.setAttribute('aria-label', loadingLabel);
        }
    });

    function themeStylesheets() {
        var links = document.querySelectorAll('link[rel="stylesheet"]');
        var result = [];
        for (var i = 0; i < links.length; i++) {
            var href = links[i].getAttribute('href') || '';
            if (href.indexOf('Radzen.Blazor/css/') !== -1 || href.indexOf('css/themes/') !== -1) {
                result.push(links[i]);
            }
        }
        return result;
    }

    function whenLoaded(link) {
        return new Promise(function (resolve) {
            if (link.sheet) {
                resolve();
                return;
            }
            link.addEventListener('load', resolve, { once: true });
            link.addEventListener('error', resolve, { once: true });
        });
    }

    // <RadzenTheme> may emit its <link> a render after App.razor's
    // first one; look again for a few frames before giving up.
    function findThemeStylesheets(attempts) {
        return new Promise(function (resolve) {
            (function look(left) {
                var links = themeStylesheets();
                if (links.length > 0 || left <= 0) {
                    resolve(links);
                    return;
                }
                requestAnimationFrame(function () { look(left - 1); });
            })(attempts);
        });
    }

    var completed = false;

    window.cardscape = window.cardscape || {};

    // Fades the splash out once the theme stylesheet is in the
    // cascade, so the app never shows unstyled markup. Idempotent;
    // a timeout keeps a broken stylesheet from pinning the splash.
    window.cardscape.bootComplete = function () {
        if (completed) {
            return;
        }
        completed = true;

        var splash = document.getElementById('cs-boot');
        if (!splash) {
            return;
        }

        var timeout = new Promise(function (resolve) { setTimeout(resolve, 4000); });
        var sheets = findThemeStylesheets(20).then(function (links) {
            return Promise.all(links.map(whenLoaded));
        });

        Promise.race([sheets, timeout]).then(function () {
            // Two frames: let the browser apply the theme before
            // the overlay starts to fade.
            requestAnimationFrame(function () {
                requestAnimationFrame(function () {
                    splash.classList.add('cs-boot-done');
                    setTimeout(function () {
                        if (splash.parentNode) {
                            splash.parentNode.removeChild(splash);
                        }
                    }, 450);
                });
            });
        });
    };
})();

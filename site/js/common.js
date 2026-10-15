// Shared by every page: language switching and the version badge.
let currentLang = 'es';
let latestRelease = null;
const onLanguageChange = [];

function t(key) {
  return translations[currentLang]?.[key] ?? translations.en[key] ?? key;
}

function setMeta(selector, value) {
  const el = document.querySelector(selector);
  if (el && value) el.setAttribute('content', value);
}

function updateLanguage(lang) {
  if (typeof translations === 'undefined' || !translations[lang]) return;
  currentLang = lang;
  document.documentElement.setAttribute('lang', lang);

  document.querySelectorAll('[data-i18n]').forEach(el => {
    const value = translations[lang][el.getAttribute('data-i18n')];
    if (typeof value !== 'string') return;
    if (value.includes('<')) el.innerHTML = value;
    else el.textContent = value;
  });
  document.querySelectorAll('[data-i18n-alt]').forEach(el => {
    const value = translations[lang][el.getAttribute('data-i18n-alt')];
    if (value) el.setAttribute('alt', value);
  });

  const page = document.body.dataset.page;
  const title = page ? t(`${page}_seo_title`) : t('title');
  const desc = page ? t(`${page}_seo_desc`) : t('description');
  document.title = title;
  setMeta('meta[name="description"]', desc);
  setMeta('meta[property="og:title"]', title);
  setMeta('meta[property="og:description"]', desc);
  setMeta('meta[name="twitter:title"]', title);
  setMeta('meta[name="twitter:description"]', desc);

  document.querySelectorAll('.lang-btn').forEach(btn => {
    btn.classList.toggle('active', btn.getAttribute('data-lang') === lang);
  });

  updateReleaseBadge();
  onLanguageChange.forEach(fn => fn(lang));
}

function switchLanguage(lang) {
  try { localStorage.setItem('preferred-lang', lang); } catch (_) { /* storage blocked */ }
  updateLanguage(lang);
}

function preferredLanguage() {
  let lang = null;
  try { lang = localStorage.getItem('preferred-lang'); } catch (_) { /* storage blocked */ }
  if (!lang) {
    const browserLang = navigator.language || '';
    lang = browserLang.startsWith('es') ? 'es' : 'en';
  }
  return lang;
}

// Newest version. data/version.json is generated at publish time from the
// newest vX.Y.Z git tag (scripts/site-data.py); the GitHub tags API is only a
// fallback for local previews where the file was not generated.
const REPO_URL = 'https://github.com/FittyAr/Cardscape';

function normaliseTag(tag) {
  const m = /^(v\d+\.\d+\.\d+)/.exec(tag || '');
  return m ? m[1] : tag;
}

async function fetchLatestRelease() {
  if (!document.getElementById('release-badge')) return;
  try {
    const response = await fetch('data/version.json', { cache: 'no-cache' });
    if (!response.ok) throw new Error('version.json not available');
    const v = await response.json();
    const days = v.date ? (Date.now() - new Date(v.date).getTime()) / 86400000 : Infinity;
    latestRelease = { tag: v.version, url: v.url || REPO_URL + '/tags', isNew: days <= 30, pre: false };
  } catch (_) {
    try {
      const response = await fetch('https://api.github.com/repos/FittyAr/Cardscape/tags?per_page=100');
      if (!response.ok) throw new Error('API request failed');
      const tags = (await response.json()).map(x => x.name).filter(n => /^v\d+\.\d+\.\d+/.test(n));
      const key = n => normaliseTag(n).slice(1).split('.').map(Number);
      tags.sort((a, b) => { const x = key(a), y = key(b); return (y[0] - x[0]) || (y[1] - x[1]) || (y[2] - x[2]); });
      if (tags.length === 0) throw new Error('No tags');
      latestRelease = { tag: normaliseTag(tags[0]), url: REPO_URL + '/releases/tag/' + encodeURIComponent(tags[0]), isNew: false, pre: false };
    } catch (error) {
      console.error('Error fetching version:', error);
      const badge = document.getElementById('release-badge');
      if (badge) badge.style.display = 'none';
      return;
    }
  }
  updateReleaseBadge();
}

function updateReleaseBadge() {
  const textEl = document.getElementById('release-text');
  const linkEl = document.querySelector('.release-link');
  if (!latestRelease || !textEl) return;
  if (linkEl) linkEl.href = latestRelease.url;
  const key = latestRelease.pre ? 'release_pre' : latestRelease.isNew ? 'release_new' : 'release_old';
  textEl.textContent = t(key).replace('{version}', latestRelease.tag);
}

document.addEventListener('DOMContentLoaded', () => {
  updateLanguage(preferredLanguage());
  fetchLatestRelease();
});

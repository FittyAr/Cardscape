// Changelog page. data/changelog.json is generated at publish time from the
// git tags and docs/community/CHANGELOG.md (scripts/site-data.py). The GitHub
// tags API is only a fallback for local previews without that file.
const CHANGELOG_URL = 'data/changelog.json';
const TAGS_URL = 'https://api.github.com/repos/FittyAr/Cardscape/tags?per_page=100';
const VISIBLE_RELEASES = 3;
const LONG_BODY = 1200; // characters before a card's notes are collapsed
let showOlder = false;
let releases = [];

function escapeHtml(text) {
  return (text || '')
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&#039;');
}

// Inline Markdown subset: `code`, **bold** and [links](https://...).
function inline(text) {
  return escapeHtml(text)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\[([^\]]+)\]\((https:\/\/[^)\s]+)\)/g, '<a href="$2" target="_blank" rel="noopener">$1</a>');
}

// Release notes: headings, bullet lists and paragraphs.
function renderBody(md) {
  const out = [];
  let list = null;
  const close = () => { if (list) { out.push('</ul>'); list = null; } };
  for (const raw of (md || '').split('\n')) {
    const line = raw.trim();
    const bullet = line.match(/^[-*]\s+(.+)$/);
    const head = line.match(/^#{1,6}\s+(.+)$/);
    if (bullet) {
      if (!list) { out.push('<ul class="changelog-list">'); list = true; }
      out.push(`<li>${inline(bullet[1])}</li>`);
    } else {
      close();
      if (head) out.push(`<span class="changelog-category-title">${escapeHtml(head[1])}</span>`);
      else if (line) out.push(`<p class="changelog-intro">${inline(line)}</p>`);
    }
  }
  close();
  return out.join('');
}

function render() {
  const container = document.getElementById('changelog-container');
  container.innerHTML = '';
  releases.forEach((r, idx) => {
    const item = document.createElement('div');
    item.className = 'changelog-item' + (idx >= VISIBLE_RELEASES ? ' older-release' : '') + (showOlder ? ' show' : '');
    const date = r.date ? new Date(r.date + 'T12:00:00Z').toLocaleDateString(currentLang, { year: 'numeric', month: 'long', day: 'numeric' }) : '';
    item.innerHTML = `
      <div class="changelog-dot"></div>
      <div class="changelog-card">
        <div class="changelog-header">
          <div class="changelog-version">${escapeHtml(r.version)}
            ${idx === 0 ? `<span class="latest-tag">${escapeHtml(t('changelog_latest'))}</span>` : ''}
            ${r.codename ? `<span class="changelog-codename">${escapeHtml(r.codename)}</span>` : ''}</div>
          <div class="changelog-date">${escapeHtml(date)}</div>
        </div>
        <div class="changelog-body${(r.body || '').length > LONG_BODY ? ' collapsed' : ''}">${renderBody(r.body)}</div>
        ${(r.body || '').length > LONG_BODY ? `<button type="button" class="changelog-more" onclick="toggleBody(this)">${escapeHtml(t('changelog_more'))}</button>` : ''}
        <div class="changelog-body">
          <p class="changelog-intro"><a href="${escapeHtml(r.url)}" target="_blank" rel="noopener">${escapeHtml(t('changelog_view'))}</a></p>
        </div>
      </div>`;
    container.appendChild(item);
  });
  document.getElementById('changelog-actions').style.display = releases.length > VISIBLE_RELEASES ? 'flex' : 'none';
  updateToggle();
}

function updateToggle() {
  document.getElementById('btn-toggle-text').textContent = t(showOlder ? 'btn_hide_older' : 'btn_show_older');
  document.getElementById('btn-toggle-icon').textContent = showOlder ? '▲' : '▼';
}

function toggleBody(button) {
  const body = button.previousElementSibling;
  const collapsed = body.classList.toggle('collapsed');
  button.textContent = t(collapsed ? 'changelog_more' : 'changelog_less');
}

function toggleOlderReleases() {
  showOlder = !showOlder;
  document.querySelectorAll('.changelog-item.older-release').forEach(el => el.classList.toggle('show', showOlder));
  updateToggle();
}

function setStatus(key) {
  const status = document.getElementById('changelog-status');
  if (status) {
    status.setAttribute('data-i18n', key);
    status.textContent = t(key);
  }
}

async function loadFromTagsApi() {
  const response = await fetch(TAGS_URL);
  if (!response.ok) throw new Error('Failed to fetch tags');
  const key = n => n.slice(1).split(/[.-]/).slice(0, 3).map(Number);
  return (await response.json())
    .map(x => x.name)
    .filter(n => /^v\d+\.\d+\.\d+/.test(n))
    .sort((a, b) => { const x = key(a), y = key(b); return (y[0] - x[0]) || (y[1] - x[1]) || (y[2] - x[2]); })
    .map(n => ({
      tag: n, version: n.match(/^v\d+\.\d+\.\d+/)[0], codename: n.replace(/^v\d+\.\d+\.\d+-?/, ''),
      date: '', body: '', url: 'https://github.com/FittyAr/Cardscape/releases/tag/' + encodeURIComponent(n)
    }));
}

async function loadChangelog() {
  try {
    try {
      const response = await fetch(CHANGELOG_URL, { cache: 'no-cache' });
      if (!response.ok) throw new Error('changelog.json not available');
      releases = await response.json();
    } catch (_) {
      releases = await loadFromTagsApi();
    }
    if (releases.length === 0) { setStatus('changelog_empty'); return; }
    render();
  } catch (error) {
    console.error('Error loading changelog:', error);
    setStatus('changelog_error');
  }
}

onLanguageChange.push(() => { if (releases.length) render(); });
document.addEventListener('DOMContentLoaded', loadChangelog);

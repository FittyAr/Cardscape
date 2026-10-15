// Changelog page: renders the GitHub releases of FittyAr/Cardscape.
const RELEASES_URL = 'https://api.github.com/repos/FittyAr/Cardscape/releases?per_page=30';
const VISIBLE_RELEASES = 3;
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
    const date = r.published_at ? new Date(r.published_at).toLocaleDateString(currentLang) : '';
    item.innerHTML = `
      <div class="changelog-dot"></div>
      <div class="changelog-card">
        <div class="changelog-header">
          <div class="changelog-version">${escapeHtml(r.name || r.tag_name)}
            ${r.prerelease ? `<span class="latest-tag">${escapeHtml(t('changelog_prerelease'))}</span>` : ''}</div>
          <div class="changelog-date">${escapeHtml(date)}</div>
        </div>
        <div class="changelog-body">${renderBody(r.body)}
          <p class="changelog-intro"><a href="${escapeHtml(r.html_url)}" target="_blank" rel="noopener">${escapeHtml(t('changelog_view'))}</a></p>
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

async function loadChangelog() {
  try {
    const response = await fetch(RELEASES_URL);
    if (!response.ok) throw new Error('Failed to fetch releases');
    releases = (await response.json()).filter(r => !r.draft);
    if (releases.length === 0) {
      // No GitHub releases yet: fall back to the published tags.
      const tags = await fetch('https://api.github.com/repos/FittyAr/Cardscape/tags?per_page=30');
      if (tags.ok) {
        releases = (await tags.json()).map(x => ({
          tag_name: x.name, body: '', prerelease: /-/.test(x.name),
          html_url: 'https://github.com/FittyAr/Cardscape/releases/tag/' + encodeURIComponent(x.name)
        }));
      }
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

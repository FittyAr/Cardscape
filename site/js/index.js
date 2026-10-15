// Landing page: screenshot gallery, install tabs and copy button.
const SCREENS = [
  '01-board', '02-card', '03-dashboard', '04-calendar', '05-inbox', '06-automation',
  '07-members', '08-integrations', '09-tokens', '10-themes', '11-light-board'
];
const shotLang = () => (currentLang === 'es' ? 'es' : 'en');
const shotSrc = (name, thumb) => `assets/screens/${shotLang()}-${name}${thumb ? '-thumb' : ''}.webp`;

let currentIndex = 0;
let activeOS = 'docker';

function buildGallery() {
  const display = document.getElementById('gallery-display');
  const thumbs = document.getElementById('gallery-thumbnails');
  SCREENS.forEach((name, i) => {
    const slide = document.createElement('div');
    slide.className = 'gallery-slide' + (i === 0 ? ' active' : '');
    slide.innerHTML = `<img data-shot="${name}" src="${shotSrc(name)}" width="1440" height="900" loading="lazy" alt="">`;
    display.insertBefore(slide, display.querySelector('.gallery-nav'));

    const thumb = document.createElement('button');
    thumb.className = 'gallery-thumb' + (i === 0 ? ' active' : '');
    thumb.innerHTML = `<img data-shot="${name}" data-thumb="1" src="${shotSrc(name, true)}" width="320" height="200" loading="lazy" alt="">`;
    thumb.addEventListener('click', () => showSlide(i));
    thumbs.appendChild(thumb);
  });
  document.getElementById('next-btn').addEventListener('click', () => showSlide(currentIndex + 1));
  document.getElementById('prev-btn').addEventListener('click', () => showSlide(currentIndex - 1));
  document.addEventListener('keydown', e => {
    const rect = display.getBoundingClientRect();
    if (rect.bottom < 0 || rect.top > window.innerHeight) return;
    if (e.key === 'ArrowRight') showSlide(currentIndex + 1);
    if (e.key === 'ArrowLeft') showSlide(currentIndex - 1);
  });
}

function showSlide(index, scroll = true) {
  const slides = document.querySelectorAll('.gallery-slide');
  const thumbs = document.querySelectorAll('.gallery-thumb');
  currentIndex = (index + slides.length) % slides.length;
  slides.forEach((s, i) => s.classList.toggle('active', i === currentIndex));
  thumbs.forEach((th, i) => {
    th.classList.toggle('active', i === currentIndex);
    if (i === currentIndex && scroll) {
      const strip = th.parentElement;
      strip.scrollTo({ left: th.offsetLeft - strip.clientWidth / 2 + th.clientWidth / 2, behavior: 'smooth' });
    }
  });
  document.querySelectorAll('img[data-shot]').forEach(img => {
    img.src = shotSrc(img.dataset.shot, !!img.dataset.thumb);
  });
  const captions = translations[currentLang].gallery;
  document.getElementById('gallery-caption').textContent = captions[currentIndex][0];
  slides.forEach((s, i) => s.querySelector('img').alt = captions[i][1]);
  thumbs.forEach((th, i) => th.querySelector('img').alt = captions[i][1]);
}

function switchTab(os) {
  activeOS = os;
  document.getElementById('tab-dev').classList.toggle('active', os === 'dev');
  document.getElementById('tab-docker').classList.toggle('active', os === 'docker');
  document.getElementById('code-dev').style.display = os === 'dev' ? 'block' : 'none';
  document.getElementById('code-docker').style.display = os === 'docker' ? 'block' : 'none';
  document.getElementById('terminal-title').textContent = os === 'dev' ? 'terminal' : 'docker compose';
  resetCopyButton();
}

function resetCopyButton() {
  const btn = document.getElementById('btn-copy');
  btn.classList.remove('copied');
  btn.textContent = t('btn_copy');
}

function copyCommand() {
  const text = document.getElementById(activeOS === 'dev' ? 'cmd-dev' : 'cmd-docker').textContent.trim();
  const btn = document.getElementById('btn-copy');
  navigator.clipboard.writeText(text).then(() => {
    btn.classList.add('copied');
    btn.textContent = t('btn_copied');
    setTimeout(resetCopyButton, 3000);
  }).catch(err => console.error('Copy failed:', err));
}

buildGallery();
onLanguageChange.push(() => showSlide(currentIndex, false));

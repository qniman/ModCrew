// Appearance settings → CSS: data-theme, accent colours, reduced motion. (Scale is WebView zoom, set by the host.)

export const accents = {
  violet: { label: 'Фиолетовый', colors: ['#7c5cff', '#4f7cff', '#22d3ee'] },
  blue: { label: 'Синий', colors: ['#3b82f6', '#2563eb', '#38bdf8'] },
  teal: { label: 'Бирюзовый', colors: ['#14b8a6', '#0ea5e9', '#22d3ee'] },
  green: { label: 'Зелёный', colors: ['#22c55e', '#10b981', '#a3e635'] },
  orange: { label: 'Оранжевый', colors: ['#f97316', '#f59e0b', '#facc15'] },
  red: { label: 'Красный', colors: ['#ef4444', '#f43f5e', '#fb923c'] },
  pink: { label: 'Розовый', colors: ['#ec4899', '#d946ef', '#a855f7'] },
};

const systemDark = window.matchMedia('(prefers-color-scheme: dark)');
let current = null;

export function applyAppearance(a) {
  current = a;
  const root = document.documentElement;
  const dark = a.theme === 'system' ? systemDark.matches : a.theme !== 'light';
  root.dataset.theme = dark ? 'dark' : 'light';
  const [accent, mid, second] = (accents[a.accent] ?? accents.violet).colors;
  root.style.setProperty('--accent', accent);
  root.style.setProperty('--accent-mid', mid);
  root.style.setProperty('--accent-2', second);
  root.classList.toggle('reduce-motion', a.animations === false);
}

systemDark.addEventListener('change', () => {
  if (current?.theme === 'system') applyAppearance(current);
});

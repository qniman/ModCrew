import { call, callWithFiles, on } from './api.js';
import { applyAppearance } from './theme.js';

/** Global UI state. Components read it directly; actions below change it. */
export const app = $state({
  page: 'packs', // packs | games | about
  packId: null, // open pack detail
  editId: null, // pack open in the editor
  editorDirty: false,
  info: null,
  packs: [],
  games: [],
  loading: true,
  /** {packId, step, total, mod, text, fraction, log: string[]} while installing */
  install: null,
  /** {token, title, text, okLabel} — confirm dialog asked by the host or by the UI */
  ask: null,
  toasts: [],
  /** settings.get result: autoUpdate, appearance, version, devBuild, installed, … */
  settings: null,
  /** New version: {version, notes, pageUrl, state: 'available' | 'downloading' | 'ready', progress} */
  update: null,
});

let toastSeq = 0;
export function toast(text, kind = 'info') {
  const id = ++toastSeq;
  app.toasts.push({ id, text, kind });
  setTimeout(() => (app.toasts = app.toasts.filter((t) => t.id !== id)), kind === 'error' ? 7000 : 3500);
}

export async function refresh(refreshGames = false) {
  try {
    const [packs, games] = await Promise.all([call('packs.list'), call('games.list', { refresh: refreshGames })]);
    app.packs = packs;
    app.games = games;
  } catch (e) {
    toast(e.message, 'error');
  } finally {
    app.loading = false;
  }
}

export async function init() {
  app.info = await call('app.info').catch(() => null);
  await loadSettings();
  if (app.info?.mode === 'setup') return;
  await refresh();
  // Started by double-clicking a .modcrew file.
  const opened = await call('app.takeOpened').catch(() => null);
  if (opened) await showOpened(opened);
}

// ---------------------------------------------------------------- settings & updates

async function loadSettings() {
  const s = await call('settings.get').catch(() => null);
  if (!s) return;
  app.settings = s;
  applyAppearance(s.appearance);
  if (s.update) app.update = { ...s.update, state: s.update.ready ? 'ready' : 'available' };
}

export async function saveSettings(patch) {
  // Apply locally first so switching themes feels instant.
  if (patch.appearance) applyAppearance(patch.appearance);
  try {
    app.settings = await call('settings.set', patch);
  } catch (e) {
    toast(e.message, 'error');
  }
}

on('update.available', (u) => (app.update = { ...u, state: 'available' }));
on('update.ready', (u) => {
  app.update = { ...u, state: 'ready' };
  toast(`ModCrew обновлён до версии ${u.version}. Перезапусти, чтобы включить её.`, 'ok');
});
on('update.progress', (p) => {
  if (app.update) app.update.progress = p.total ? p.done / p.total : null;
});

export async function installUpdate() {
  if (!app.update) return;
  app.update.state = 'downloading';
  app.update.progress = 0;
  try {
    await call('update.apply');
  } catch (e) {
    app.update.state = 'available';
    toast(e.message, 'error');
  }
}

export async function restartApp() {
  await run('update.restart');
}

/** The editor registers here to receive dropped mod files. */
export const dropTarget = { handler: null };

/** Result of opening files from Explorer: import packs, report errors, pass other files on. */
async function showOpened({ packs, errors, other }) {
  for (const e of errors) toast(`Не получилось добавить сборку.\n${e}`, 'error');
  if (packs.length) {
    await refresh();
    const view = app.packs.find((p) => p.pack.id === packs[packs.length - 1]);
    toast(packs.length > 1 ? `Добавлено сборок: ${packs.length}` : `Сборка «${view?.pack.name ?? packs[0]}» добавлена`, 'ok');
    await navigate('packs', packs[packs.length - 1]);
  }
  if (other.length) {
    if (dropTarget.handler) dropTarget.handler(other);
    else toast('Это не сборка ModCrew (.modcrew).\nЧтобы добавить файл мода в свою сборку — открой её редактор и перетащи файл туда.', 'info');
  }
}

on('opened', showOpened);

export async function dropFiles(files) {
  try {
    await showOpened(await callWithFiles('drop.files', null, files));
  } catch (e) {
    toast(e.message, 'error');
  }
}

on('install.progress', (p) => {
  if (!app.install || app.install.packId !== p.packId) return;
  const prevText = app.install.text;
  Object.assign(app.install, p);
  // Log one line per step, not one per download tick.
  const line = p.text.replace(/ — [\d.,]+ из [\d.,]+ МБ$/, '');
  if (line !== prevText?.replace(/ — [\d.,]+ из [\d.,]+ МБ$/, '')) app.install.log.push(line);
});

// Host asks the player something (e.g. "run this installer?").
on('ask', (q) => (app.ask = { ...q, host: true }));

/** UI-side confirm that returns a promise. */
export function confirm(title, text, okLabel = 'OK', danger = false, cancelLabel = 'Отмена') {
  return new Promise((resolve) => (app.ask = { title, text, okLabel, danger, cancelLabel, resolve }));
}

/** Navigation that respects unsaved editor changes. */
export async function navigate(page, packId = null, editId = null) {
  if (app.editId && app.editorDirty && editId !== app.editId) {
    if (!(await confirm('Выйти без сохранения?', 'В редакторе есть несохранённые изменения. Они пропадут.', 'Выйти', true))) return;
  }
  app.editorDirty = false;
  app.page = page;
  app.packId = packId;
  app.editId = editId;
}

export function answer(ok) {
  const q = app.ask;
  app.ask = null;
  if (!q) return;
  if (q.host) call('ui.answer', { token: q.token, ok });
  else q.resolve(ok);
}

/** Runs a host call, shows its error as a toast, returns the result or undefined. */
export async function run(method, params, success) {
  try {
    const result = await call(method, params);
    if (success) toast(success, 'ok');
    return result;
  } catch (e) {
    if (!e.cancelled) toast(e.message, 'error');
    return undefined;
  }
}

export async function installPack(view) {
  const id = view.pack.id;
  app.install = { packId: id, step: 0, total: 0, mod: '', text: 'Готовлюсь…', fraction: null, log: [] };
  try {
    await call('install.run', { id });
    app.install.done = true;
    toast(`«${view.pack.name}» установлена. Можно играть!`, 'ok');
  } catch (e) {
    app.install.error = e.cancelled ? 'Установка отменена — всё вернул как было.' : e.message;
  } finally {
    await refresh();
  }
}

export function statusOf(view) {
  if (app.install?.packId === view.pack.id && !app.install.done && !app.install.error) return 'installing';
  if (!view.game.found) return 'nogame';
  if (!view.installed) return 'available';
  if (!view.installed.complete) return 'broken';
  if (view.installed.version !== view.pack.version) return 'update';
  return 'installed';
}

/** Steam CDN fallback for images Steam has not cached locally. */
export const steamCdn = (appId, file) => `https://cdn.cloudflare.steamstatic.com/steam/apps/${appId}/${file}`;

export const statusLabel = {
  installing: 'Устанавливается',
  nogame: 'Игра не найдена',
  available: 'Не установлена',
  broken: 'Прервана',
  update: 'Есть обновление',
  installed: 'Установлена',
};

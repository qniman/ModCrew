// What each step action looks like in the editor. Field kinds: text | path-read | path-write | bool | multiline | mono.
export const actions = {
  download: {
    label: 'Скачать', icon: 'download', color: '#22d3ee',
    fields: [
      { key: 'url', label: 'Ссылка (https)', kind: 'text', placeholder: 'https://github.com/…/mod.zip' },
      { key: 'sha256', label: 'SHA-256', kind: 'mono', optional: true, hint: 'Защита от подмены файла. Заполняется само в «Мод по ссылке».' },
      { key: 'saveAs', label: 'Сохранить как', kind: 'text', optional: true, placeholder: 'mod.zip — во временную папку' },
    ],
  },
  extract: {
    label: 'Распаковать', icon: 'packs', color: '#a78bfa',
    fields: [
      { key: 'from', label: 'Архив (.zip)', kind: 'path-read' },
      { key: 'to', label: 'Куда', kind: 'path-write', optional: true, placeholder: 'по умолчанию — во временную папку' },
    ],
  },
  copy: {
    label: 'Скопировать', icon: 'copy', color: '#60a5fa',
    fields: [
      { key: 'from', label: 'Что (файл или папка)', kind: 'path-read' },
      { key: 'to', label: 'Куда', kind: 'path-write', hint: 'Со «/» на конце — положить внутрь этой папки.' },
      { key: 'overwrite', label: 'Заменять существующие файлы', kind: 'bool', default: true },
    ],
  },
  delete: {
    label: 'Удалить', icon: 'trash', color: '#f87171',
    fields: [{ key: 'path', label: 'Что удалить', kind: 'path-write' }],
  },
  replaceText: {
    label: 'Заменить текст', icon: 'edit', color: '#fbbf24',
    fields: [
      { key: 'file', label: 'Файл', kind: 'path-write' },
      { key: 'find', label: 'Найти', kind: 'multiline' },
      { key: 'replace', label: 'Заменить на', kind: 'multiline', optional: true },
    ],
  },
  setIni: {
    label: 'Настройка .ini', icon: 'sliders', color: '#34d399',
    fields: [
      { key: 'file', label: 'Файл .ini', kind: 'path-write' },
      { key: 'section', label: 'Секция', kind: 'text', optional: true, placeholder: 'например Video' },
      { key: 'key', label: 'Ключ', kind: 'text' },
      { key: 'value', label: 'Значение', kind: 'text', optional: true },
    ],
  },
  run: {
    label: 'Запустить программу', icon: 'play', color: '#fb923c',
    fields: [
      { key: 'path', label: 'Программа', kind: 'path-read' },
      { key: 'args', label: 'Аргументы', kind: 'text', optional: true },
    ],
  },
};

export const actionOrder = ['download', 'extract', 'copy', 'delete', 'setIni', 'replaceText', 'run'];

const base = (p) => (p ?? '').replace(/[\\/]+$/, '').split(/[\\/]/).pop();

/** Short human description of a step, used when it has no title. */
export function describe(s) {
  switch (s.action) {
    case 'download': return s.url ? `Скачать ${s.saveAs || base(s.url.split('?')[0])}` : 'Скачать файл';
    case 'extract': return `Распаковать ${base(s.from) || 'архив'}${s.to ? ` → ${s.to}` : ''}`;
    case 'copy': return `${base(s.from) || 'Скопировать'} → ${s.to || '…'}`;
    case 'delete': return `Удалить ${s.path || '…'}`;
    case 'replaceText': return `Заменить текст в ${base(s.file) || 'файле'}`;
    case 'setIni': return `${base(s.file) || '.ini'}: ${s.section ? `[${s.section}] ` : ''}${s.key || 'ключ'} = ${s.value ?? ''}`;
    case 'run': return `Запустить ${base(s.path) || 'программу'}`;
    default: return s.action;
  }
}

let uid = 0;
/** Editor-only keys start with "_" and are stripped before saving. */
export const key = () => `_${++uid}`;

export function strip(pack) {
  return JSON.parse(JSON.stringify(pack, (k, v) => (k.startsWith('_') ? undefined : v === '' ? undefined : v)));
}

export function withKeys(pack) {
  for (const m of pack.mods) {
    m._k = key();
    for (const s of m.steps) s._k = key();
  }
  return pack;
}

export function newStep(action) {
  const s = { _k: key(), _open: true, action };
  for (const f of actions[action].fields) if (f.kind === 'bool') s[f.key] = f.default;
  return s;
}

/** "CoolMod_v1.2.3.zip" → { name: "CoolMod", version: "1.2.3" } */
export function guessName(file) {
  const stem = file.replace(/\.[a-z0-9]{1,4}$/i, '');
  const m = stem.match(/^(.*?)[\s._-]*v?(\d+(?:\.\d+)+)$/i);
  const clean = (s) => s.replace(/[._-]+/g, ' ').trim();
  return m && m[1] ? { name: clean(m[1]), version: m[2] } : { name: clean(stem), version: '' };
}

const slug = (s) => s.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'mod';

/**
 * Steps for a mod file the wizard inspected.
 * info: result of packs.addFile / packs.probeUrl; from: folder inside the archive ("" = all); to: target path.
 */
export function buildSteps(info, from, to) {
  const steps = [];
  let source = info.packPath; // file inside the pack
  if (info.url) {
    steps.push({ action: 'download', url: info.url, sha256: info.sha256, saveAs: info.name });
    source = info.name; // relative = {work}
  }
  if (info.kind === 'zip') {
    if (!from) {
      steps.push({ action: 'extract', from: source, to });
    } else {
      const tmp = `unpack-${slug(info.name.replace(/\.zip$/i, ''))}`;
      steps.push({ action: 'extract', from: source, to: tmp });
      steps.push({ action: 'copy', from: `${tmp}/${from}`, to, overwrite: true });
    }
  } else {
    steps.push({ action: 'copy', from: source, to: to.endsWith('/') ? to : `${to}/`, overwrite: true });
  }
  return steps.map((s) => ({ _k: key(), ...s }));
}

export function bumpVersion(v) {
  const m = (v ?? '').match(/^(.*?)(\d+)([^\d]*)$/);
  return m ? `${m[1]}${Number(m[2]) + 1}${m[3]}` : `${v || '1.0'}.1`;
}

export const formatSize = (n) =>
  n > 1048576 ? `${(n / 1048576).toFixed(1)} МБ` : n > 1024 ? `${Math.round(n / 1024)} КБ` : `${n} Б`;

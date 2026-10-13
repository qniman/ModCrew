// Fake host for browser development. Mirrors the shapes returned by Api.cs.
const cdn = (id, file) => `https://cdn.cloudflare.steamstatic.com/steam/apps/${id}/${file}`;
const images = (id) => ({
  poster: cdn(id, 'library_600x900.jpg'),
  hero: cdn(id, 'library_hero.jpg'),
  logo: cdn(id, 'logo.png'),
  header: cdn(id, 'header.jpg'),
});

const games = [
  { appId: 516750, name: 'My Summer Car', dir: 'D:\\SteamLibrary\\steamapps\\common\\My Summer Car' },
  { appId: 4164420, name: 'My Winter Car', dir: 'D:\\SteamLibrary\\steamapps\\common\\My Winter Car' },
  { appId: 892970, name: 'Valheim', dir: 'D:\\SteamLibrary\\steamapps\\common\\Valheim' },
  { appId: 548430, name: 'Deep Rock Galactic', dir: 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Deep Rock Galactic' },
  { appId: 413150, name: 'Stardew Valley', dir: 'D:\\SteamLibrary\\steamapps\\common\\Stardew Valley' },
  { appId: 1203220, name: 'NARAKA: BLADEPOINT', dir: 'D:\\SteamLibrary\\steamapps\\common\\NARAKA' },
];

const packs = [
  {
    pack: {
      id: 'msc-friends', name: 'MSC с друзьями', version: '1.2.0', author: 'qniman',
      description: 'Мультиплеер, удобные мелочи и наша сигнализация на Сатсуму. Ставится за минуту — просто жми «Установить».',
      game: { steamAppId: 516750, name: 'My Summer Car' },
      mods: [
        { id: 'mscloader', name: 'MSCLoader', version: '1.4.2', description: 'Загрузчик модов — нужен всем остальным.', url: 'https://github.com/piotrulos/MSCModLoader', steps: [{}, {}, {}] },
        { id: 'mscmp', name: 'MSC Multiplayer', version: '0.4', description: 'Играем вместе.', steps: [{}, {}] },
        { id: 'cse', name: 'Car Systems Extended', version: '1.0.1', description: 'Сигнализация, центральный замок, автозапуск.', steps: [{}] },
        { id: 'fps', name: 'FPS Boost', version: '2.1', steps: [{}, {}] },
      ],
    },
    coverUrl: null,
    installed: { version: '1.1.0', complete: true, at: '2026-10-01T18:00:00' },
  },
  {
    pack: {
      id: 'valheim-qol', name: 'Valheim — удобства', version: '3.0.1', author: 'qniman',
      description: 'BepInEx, большой инвентарь, карта на всех.',
      game: { steamAppId: 892970, name: 'Valheim' },
      mods: [
        { id: 'bepinex', name: 'BepInExPack Valheim', version: '5.4.2202', steps: [{}, {}] },
        { id: 'equip', name: 'Equipment & Quickslots', version: '2.1.13', steps: [{}] },
      ],
    },
    coverUrl: null,
    installed: null,
  },
  {
    pack: {
      id: 'stardew-coop', name: 'Stardew на четверых', version: '1.0.0', author: 'Саня',
      description: 'SMAPI и пачка модов для кооператива.',
      game: { steamAppId: 413150, name: 'Stardew Valley' },
      mods: [{ id: 'smapi', name: 'SMAPI', version: '4.1.10', steps: [{}, {}, {}] }],
    },
    coverUrl: null,
    installed: { version: '1.0.0', complete: true, at: '2026-09-20T12:00:00' },
  },
];

const wait = (ms) => new Promise((r) => setTimeout(r, ms));

const mockFiles = {};
const mockSettings = {
  autoUpdate: true, appearance: { theme: 'dark', accent: 'violet', scale: 1, animations: true },
  version: '0.2.0', devBuild: false, installed: true, exePath: 'C:\\Users\\you\\AppData\\Local\\Programs\\ModCrew\\ModCrew.exe',
  dataDir: 'C:\\Users\\you\\AppData\\Local\\ModCrew', releasesPage: 'https://github.com/qniman/ModCrew/releases', update: null,
};
const mockZip = (extra) => ({
  size: 2_457_600, kind: 'zip', fileCount: 14,
  topLevel: ['BetterSatsuma-2.1'],
  dirs: ['BetterSatsuma-2.1', 'BetterSatsuma-2.1/Mods', 'BetterSatsuma-2.1/Mods/Assets', 'BetterSatsuma-2.1/Mods/Assets/BetterSatsuma', 'BetterSatsuma-2.1/docs'],
  suggestFrom: 'BetterSatsuma-2.1', suggestTo: '{game}', executables: [], suggestAction: 'place',
  ...extra,
});

function packView(p) {
  const g = games.find((x) => x.appId === p.pack.game.steamAppId);
  return { ...p, game: { appId: p.pack.game.steamAppId, name: g?.name ?? p.pack.game.name, found: !!g, images: images(p.pack.game.steamAppId) } };
}

async function install(id, emit) {
  const p = packs.find((x) => x.pack.id === id);
  const steps = [
    ['MSCLoader', 'Скачиваю MSCLoader.zip'], ['MSCLoader', 'Распаковываю MSCLoader.zip'], ['MSCLoader', 'Копирую в Mods'],
    ['MSC Multiplayer', 'Скачиваю MSCMP.zip'], ['MSC Multiplayer', 'Копирую MSCMP'], ['Car Systems Extended', 'Копирую CarSystemsExtended.dll'],
  ];
  for (let i = 0; i < steps.length; i++) {
    const [mod, text] = steps[i];
    if (text.startsWith('Скачиваю')) {
      for (let f = 0; f <= 1; f += 0.1) {
        emit('install.progress', { packId: id, step: i + 1, total: steps.length, mod, text: `${text} — ${(f * 12.4).toFixed(1)} из 12.4 МБ`, fraction: f });
        await wait(120);
      }
    } else {
      emit('install.progress', { packId: id, step: i + 1, total: steps.length, mod, text });
      await wait(500);
    }
  }
  p.installed = { version: p.pack.version, complete: true, at: new Date().toISOString() };
}

export const mock = {
  async call(method, params, emit) {
    await wait(60);
    switch (method) {
      case 'app.info': return { version: '0.2.0', dataDir: 'C:\\Users\\you\\AppData\\Local\\ModCrew', steamFound: true, mode: location.hash === '#setup' ? 'setup' : 'app' };
      case 'settings.get': return mockSettings;
      case 'settings.set':
        if ('autoUpdate' in params) mockSettings.autoUpdate = params.autoUpdate;
        if (params.appearance) mockSettings.appearance = params.appearance;
        return mockSettings;
      case 'update.check': await wait(700); return { status: 'available', version: '0.3.0' };
      case 'update.apply':
        for (let d = 0; d <= 10; d++) { emit('update.progress', { done: d, total: 10 }); await wait(120); }
        emit('update.ready', { version: '0.3.0', notes: '', pageUrl: '' });
        return { version: '0.3.0' };
      case 'setup.info': return { version: '0.2.0', installedVersion: null, dir: 'C:\\Users\\you\\AppData\\Local\\Programs\\ModCrew', desktop: true };
      case 'setup.install': await wait(900); return null;
      case 'games.list':
        return games.map((g) => {
          const inst = packs.find((p) => p.pack.game.steamAppId === g.appId && p.installed);
          return {
            ...g, custom: false, images: images(g.appId),
            packCount: packs.filter((p) => p.pack.game.steamAppId === g.appId).length,
            installedPack: inst ? { id: inst.pack.id, name: inst.pack.name, version: inst.installed.version } : null,
          };
        });
      case 'packs.list': return packs.map(packView);
      case 'install.run': return install(params.id, emit);
      case 'install.uninstall': packs.find((x) => x.pack.id === params.id).installed = null; return null;
      case 'packs.remove': packs.splice(packs.findIndex((x) => x.pack.id === params.id), 1); return null;
      case 'packs.create': {
        const id = params.name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || `pack-${packs.length + 1}`;
        const g = games.find((x) => x.appId === params.appId);
        packs.push({ pack: { id, name: params.name, version: '1.0.0', author: 'you', game: { steamAppId: params.appId, name: g?.name }, mods: [] }, coverUrl: null, installed: null });
        mockFiles[id] = [];
        return id;
      }
      case 'packs.save': {
        const p = packs.find((x) => x.pack.id === params.id);
        p.pack = structuredClone(params.pack);
        return null;
      }
      case 'packs.check': {
        const out = [];
        params.pack.mods.forEach((m, mi) => {
          if (!m.steps.length) out.push({ level: 'warning', text: 'У мода нет шагов — он ничего не установит.', mod: mi });
          m.steps.forEach((s, si) => {
            if (s.action === 'copy' && (!s.from || !s.to)) out.push({ level: 'error', text: 'Не заполнено поле «что» или «куда».', mod: mi, step: si });
            if (s.action === 'download' && !s.sha256) out.push({ level: 'warning', text: 'Нет sha256: если файл по ссылке поменяется, у друзей установится другая версия.', mod: mi, step: si });
          });
        });
        return out;
      }
      case 'packs.files': return mockFiles[params.id] ?? ['files/MSCLoader.zip'];
      case 'packs.addFile': {
        await wait(400);
        (mockFiles[params.id] ??= []).push('files/BetterSatsuma_v2.1.zip');
        return mockZip({ packPath: '{pack}/files/BetterSatsuma_v2.1.zip', name: 'BetterSatsuma_v2.1.zip' });
      }
      case 'packs.probeUrl':
        for (let d = 0; d <= 8; d++) {
          emit('probe.progress', { done: d * 1048576, total: 8 * 1048576 });
          await wait(150);
        }
        return mockZip({ url: params.url, sha256: '3f2a…e91c', name: params.url.split('/').pop() || 'mod.zip' });
      case 'packs.deleteFile': return null;
      case 'app.takeOpened': return null;
      case 'drop.files': {
        const packsIn = params.names.filter((n) => n.endsWith('.modcrew'));
        return { packs: [], errors: packsIn.map((n) => `${n}: в браузерном режиме импорт недоступен`), other: params.names.filter((n) => !n.endsWith('.modcrew')) };
      }
      case 'packs.addFileFrom':
        await wait(300);
        return /\.exe$/i.test(params.path)
          ? { name: params.path, size: 4_200_000, kind: 'file', dirs: [], topLevel: [], executables: [], fileCount: 0, packPath: `{pack}/files/${params.path}`, suggestAction: 'run', suggestTo: '{game}', suggestFrom: '' }
          : mockZip({ packPath: `{pack}/files/${params.path}`, name: params.path, executables: ['BeamMP_Installer.exe'], topLevel: ['BeamMP_Installer.exe'], dirs: [], fileCount: 1, suggestAction: 'run', suggestRun: 'BeamMP_Installer.exe', suggestFrom: '' });
      case 'packs.stepOutputs': return ['unpack-beammp/BeamMP_Installer.exe', 'unpack-beammp/readme.txt'];
      case 'paths.pick': return '{game}/Mods';
      case 'packs.importFile':
      case 'packs.importFolder':
      case 'packs.export':
      case 'games.setPath':
        throw new Error('В браузерном режиме диалоги файлов недоступны — запусти ModCrew.exe.');
      default: return null;
    }
  },
};

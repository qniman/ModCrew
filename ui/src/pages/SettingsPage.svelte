<script>
  import { app, saveSettings, installUpdate, restartApp, run } from '../lib/state.svelte.js';
  import { call } from '../lib/api.js';
  import { accents } from '../lib/theme.js';
  import Icon from '../lib/Icon.svelte';

  let s = $derived(app.settings);
  let a = $derived(s?.appearance ?? { theme: 'dark', accent: 'violet', scale: 1, animations: true });

  let checking = $state(false);
  let checkResult = $state('');

  const themes = [
    { id: 'dark', label: 'Тёмная' },
    { id: 'light', label: 'Светлая' },
    { id: 'system', label: 'Как в Windows' },
  ];
  const scales = [0.9, 1, 1.1, 1.25];

  function setAppearance(patch) {
    saveSettings({ appearance: { ...a, ...patch } });
  }

  async function check() {
    checking = true;
    checkResult = '';
    try {
      const r = await call('update.check');
      if (r.status === 'latest') checkResult = `У тебя последняя версия (${r.version}).`;
      else if (r.status === 'dev') checkResult = 'Это сборка из исходников — она не обновляется из релизов.';
      else if (r.status === 'ready') checkResult = `Версия ${r.version} уже скачана — перезапусти ModCrew.`;
      else {
        app.update = { version: r.version, state: 'available' };
        checkResult = `Есть новая версия: ${r.version}.`;
      }
    } catch (e) {
      checkResult = `Не удалось проверить: ${e.message}`;
    } finally {
      checking = false;
    }
  }
</script>

<div class="page">
  <h1>Настройки</h1>
  <p class="sub">Внешний вид и обновления</p>

  <section class="card">
    <h2><Icon name="image" size={18} /> Внешний вид</h2>

    <div class="row">
      <div class="label">Тема</div>
      <div class="themes">
        {#each themes as t}
          <button class="theme {t.id}" class:sel={a.theme === t.id} onclick={() => setAppearance({ theme: t.id })}>
            <span class="preview"><span class="pv-side"></span><span class="pv-main"><i></i><i></i></span></span>
            {t.label}
          </button>
        {/each}
      </div>
    </div>

    <div class="row">
      <div class="label">Цвет акцента</div>
      <div class="swatches">
        {#each Object.entries(accents) as [id, acc]}
          <button class="swatch" class:sel={a.accent === id} title={acc.label} aria-label={acc.label}
            style:background="linear-gradient(135deg, {acc.colors[0]}, {acc.colors[1]} 55%, {acc.colors[2]})"
            onclick={() => setAppearance({ accent: id })}>
            {#if a.accent === id}<Icon name="check" size={16} />{/if}
          </button>
        {/each}
      </div>
    </div>

    <div class="row">
      <div class="label">Масштаб интерфейса</div>
      <div class="seg">
        {#each scales as sc}
          <button class:on={Math.abs(a.scale - sc) < 0.01} onclick={() => setAppearance({ scale: sc })}>{Math.round(sc * 100)}%</button>
        {/each}
      </div>
    </div>

    <div class="row">
      <div class="label">
        Анимации
        <span class="hint">Плавные переходы и появление карточек</span>
      </div>
      <button class="toggle" class:on={a.animations} role="switch" aria-checked={a.animations} aria-label="Анимации"
        onclick={() => setAppearance({ animations: !a.animations })}><span></span></button>
    </div>
  </section>

  <section class="card">
    <h2><Icon name="refresh" size={18} /> Обновления</h2>

    <div class="row">
      <div class="label">
        Версия {s?.version ?? '…'}
        <span class="hint">
          {#if s?.devBuild}Сборка из исходников{:else if s?.installed}Установлена{:else}Портативная (без установки){/if}
        </span>
      </div>
      <div class="upd-actions">
        {#if app.update?.state === 'ready'}
          <button class="btn primary" onclick={restartApp}><Icon name="refresh" size={15} /> Перезапустить на {app.update.version}</button>
        {:else if app.update?.state === 'downloading'}
          <button class="btn" disabled>Скачиваю… {app.update.progress != null ? Math.round(app.update.progress * 100) + '%' : ''}</button>
        {:else if app.update?.state === 'available'}
          <button class="btn primary" onclick={installUpdate}><Icon name="download" size={15} /> Обновить до {app.update.version}</button>
        {/if}
        <button class="btn" onclick={check} disabled={checking || s?.devBuild}>
          <Icon name="search" size={15} /> {checking ? 'Проверяю…' : 'Проверить'}
        </button>
      </div>
    </div>
    {#if checkResult}<div class="result">{checkResult}</div>{/if}

    <div class="row">
      <div class="label">
        Обновлять автоматически
        <span class="hint">ModCrew сам скачает новую версию с GitHub и включит её при следующем запуске. Если выключить — только сообщит, что вышла новая версия.</span>
      </div>
      <button class="toggle" class:on={s?.autoUpdate} role="switch" aria-checked={s?.autoUpdate} aria-label="Обновлять автоматически"
        disabled={!s} onclick={() => saveSettings({ autoUpdate: !s.autoUpdate })}><span></span></button>
    </div>

    <button class="link" onclick={() => call('shell.openUrl', { url: s?.releasesPage })}><Icon name="external" size={14} /> Все версии и что в них нового</button>
  </section>

  <section class="card">
    <h2><Icon name="folder" size={18} /> Данные</h2>
    <div class="row">
      <div class="label">
        Папка ModCrew
        <span class="hint mono">{s?.dataDir}</span>
      </div>
      <button class="btn" onclick={() => run('app.openDataDir')}><Icon name="folder" size={15} /> Открыть</button>
    </div>
    <div class="row">
      <div class="label">
        Программа
        <span class="hint mono">{s?.exePath}</span>
      </div>
    </div>
  </section>
</div>

<style>
  .page { padding: 34px 40px 48px; max-width: 860px; margin: 0 auto; }
  h1 { font-size: 30px; font-weight: 700; }
  .sub { color: var(--muted); margin: 4px 0 24px; }

  .card { background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius-lg); padding: 22px 24px; margin-bottom: 16px; }
  h2 { font-size: 16px; display: flex; align-items: center; gap: 9px; margin-bottom: 8px; }
  h2 :global(svg) { color: var(--accent-text); }

  .row { display: flex; align-items: center; justify-content: space-between; gap: 20px; padding: 14px 0; border-top: 1px solid var(--line); }
  .row:first-of-type { border-top: 0; }
  .label { font-weight: 600; display: flex; flex-direction: column; gap: 3px; min-width: 0; }
  .hint { font-weight: 400; font-size: 12.5px; color: var(--muted); line-height: 1.5; max-width: 440px; }
  .mono { font-family: 'Cascadia Mono', Consolas, monospace; font-size: 12px; overflow-wrap: anywhere; user-select: text; }

  .themes { display: flex; gap: 10px; }
  .theme {
    display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 8px 8px 10px; cursor: pointer;
    border: 1px solid var(--line-2); border-radius: 12px; background: var(--bg-2); font-size: 12.5px; font-weight: 600;
  }
  .theme.sel { border-color: var(--accent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent) 20%, transparent); }
  .preview { display: flex; width: 92px; height: 56px; border-radius: 8px; overflow: hidden; border: 1px solid rgb(var(--fg-rgb) / 0.1); }
  .pv-side { width: 26%; }
  .pv-main { flex: 1; padding: 7px; display: flex; flex-direction: column; gap: 5px; }
  .pv-main i { display: block; height: 9px; border-radius: 3px; }
  .pv-main i:last-child { width: 60%; background: var(--grad) !important; }
  .dark .pv-side { background: #10121a; } .dark .pv-main { background: #0c0d12; } .dark .pv-main i { background: #232836; }
  .light .pv-side { background: #ffffff; } .light .pv-main { background: #f3f4f8; } .light .pv-main i { background: #e2e5ec; }
  .system .pv-side { background: linear-gradient(135deg, #ffffff 50%, #10121a 50%); }
  .system .pv-main { background: linear-gradient(135deg, #f3f4f8 50%, #0c0d12 50%); }
  .system .pv-main i { background: #8b91a3; }

  .swatches { display: flex; gap: 8px; }
  .swatch {
    width: 34px; height: 34px; border-radius: 50%; border: 2px solid transparent; cursor: pointer;
    display: grid; place-items: center; color: white; transition: transform 0.15s var(--ease);
  }
  .swatch:hover { transform: scale(1.08); }
  .swatch.sel { box-shadow: 0 0 0 2px var(--panel), 0 0 0 4px var(--text); }

  .seg { display: flex; background: var(--bg-2); border: 1px solid var(--line-2); border-radius: 10px; padding: 3px; }
  .seg button { border: 0; background: none; padding: 6px 14px; border-radius: 7px; cursor: pointer; font-weight: 600; font-size: 13px; color: var(--muted); }
  .seg button.on { background: var(--panel); color: var(--text); box-shadow: var(--shadow); }

  .toggle {
    flex: none; width: 46px; height: 26px; border-radius: 99px; border: 0; cursor: pointer; padding: 3px;
    background: rgb(var(--fg-rgb) / 0.15); transition: background 0.2s;
  }
  .toggle span { display: block; width: 20px; height: 20px; border-radius: 50%; background: white; transition: transform 0.2s var(--ease); box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3); }
  .toggle.on { background: var(--accent); }
  .toggle.on span { transform: translateX(20px); }
  .toggle:disabled { opacity: 0.5; }

  .upd-actions { display: flex; gap: 8px; flex: none; }
  .result { font-size: 13px; color: var(--muted); padding: 0 0 12px; }
  .link { display: inline-flex; align-items: center; gap: 6px; border: 0; background: none; color: var(--accent-text); cursor: pointer; padding: 6px 0 0; font-weight: 600; font-size: 13px; }
</style>

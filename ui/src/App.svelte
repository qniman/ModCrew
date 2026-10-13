<script>
  import { onMount } from 'svelte';
  import { app, init, navigate, dropFiles, installUpdate, restartApp } from './lib/state.svelte.js';
  import { isMock } from './lib/api.js';
  import Icon from './lib/Icon.svelte';
  import PacksPage from './pages/PacksPage.svelte';
  import PackDetail from './pages/PackDetail.svelte';
  import PackEditor from './pages/PackEditor.svelte';
  import GamesPage from './pages/GamesPage.svelte';
  import AboutPage from './pages/AboutPage.svelte';
  import SettingsPage from './pages/SettingsPage.svelte';
  import SetupPage from './pages/SetupPage.svelte';
  import AskDialog from './lib/AskDialog.svelte';
  import Toasts from './lib/Toasts.svelte';

  onMount(init);

  const nav = [
    { id: 'packs', label: 'Сборки', icon: 'packs' },
    { id: 'games', label: 'Мои игры', icon: 'games' },
    { id: 'about', label: 'Как это работает', icon: 'info' },
    { id: 'settings', label: 'Настройки', icon: 'sliders' },
  ];

  const go = (page) => navigate(page);

  // Files dragged from Explorer anywhere onto the window.
  let dragging = $state(false);
  let depth = 0;
  const hasFiles = (e) => e.dataTransfer?.types?.includes('Files');
  function dragenter(e) {
    if (!hasFiles(e)) return;
    e.preventDefault();
    depth++;
    dragging = true;
  }
  function dragleave(e) {
    if (!hasFiles(e)) return;
    if (--depth <= 0) dragging = false;
  }
  function dragover(e) {
    if (!hasFiles(e)) return;
    e.preventDefault(); // otherwise the WebView would navigate to the file
    e.dataTransfer.dropEffect = 'copy';
  }
  function drop(e) {
    if (!hasFiles(e)) return;
    e.preventDefault();
    depth = 0;
    dragging = false;
    if (e.dataTransfer.files.length && app.info?.mode !== 'setup') dropFiles(e.dataTransfer.files);
  }

  let installedCount = $derived(app.packs.filter((p) => p.installed).length);
</script>

{#if app.info?.mode === 'setup'}
  <SetupPage />
{:else}
<div class="shell">
  <aside>
    <div class="brand">
      <div class="logo"><Icon name="puzzle" size={20} /></div>
      <div>
        <div class="name">ModCrew</div>
        <div class="tag">моды для своих</div>
      </div>
    </div>

    <nav>
      {#each nav as item}
        <button class="nav" class:active={app.page === item.id} onclick={() => go(item.id)}>
          <Icon name={item.icon} />
          <span>{item.label}</span>
          {#if item.id === 'packs' && app.packs.length}
            <span class="count">{app.packs.length}</span>
          {/if}
        </button>
      {/each}
    </nav>

    {#if app.install && !app.install.done && !app.install.error}
      {@const view = app.packs.find((p) => p.pack.id === app.install.packId)}
      <button class="mini-progress" onclick={() => navigate('packs', app.install.packId)}>
        <div class="mp-title">Устанавливаю {view?.pack.name ?? ''}</div>
        <div class="mp-bar"><div style:width="{app.install.total ? (100 * app.install.step) / app.install.total : 5}%"></div></div>
      </button>
    {/if}

    {#if app.update && app.update.state !== 'downloading'}
      <div class="upd">
        <div class="upd-title"><Icon name="sparkle" size={14} /> {app.update.state === 'ready' ? 'Обновление готово' : 'Новая версия'} {app.update.version}</div>
        {#if app.update.state === 'ready'}
          <button class="btn primary mini" onclick={restartApp}>Перезапустить</button>
        {:else}
          <button class="btn primary mini" onclick={installUpdate}>Обновить</button>
        {/if}
      </div>
    {:else if app.update?.state === 'downloading'}
      <div class="upd">
        <div class="upd-title">Скачиваю {app.update.version}…</div>
        <div class="mp-bar"><div style:width="{(app.update.progress ?? 0.05) * 100}%"></div></div>
      </div>
    {/if}

    <div class="foot">
      <div>{installedCount} установлено</div>
      <div class="ver">v{app.info?.version ?? '…'}{isMock ? ' · браузер' : ''}</div>
    </div>
  </aside>

  <main>
    {#if app.page === 'packs' && app.editId && app.packs.some((p) => p.pack.id === app.editId)}
      {#key app.editId}<PackEditor />{/key}
    {:else if app.page === 'packs' && app.packId}
      <PackDetail />
    {:else if app.page === 'packs'}
      <PacksPage />
    {:else if app.page === 'games'}
      <GamesPage />
    {:else if app.page === 'settings'}
      <SettingsPage />
    {:else}
      <AboutPage />
    {/if}
  </main>
</div>
{/if}

<svelte:window ondragenter={dragenter} ondragleave={dragleave} ondragover={dragover} ondrop={drop} />

{#if dragging}
  <div class="dropzone">
    <div class="dz-box">
      <Icon name={app.editId ? 'file' : 'packs'} size={36} />
      <b>{app.editId ? 'Отпусти — добавлю мод в сборку' : 'Отпусти, чтобы добавить сборку'}</b>
      <span>{app.editId ? '.zip, .dll, .exe — ModCrew посмотрит, что внутри' : 'файл .modcrew от друга'}</span>
    </div>
  </div>
{/if}

<AskDialog />
<Toasts />

<style>
  .shell { display: grid; grid-template-columns: 232px 1fr; height: 100%; }

  aside {
    display: flex; flex-direction: column; gap: 6px;
    padding: 20px 14px 16px;
    background: var(--side-bg);
    border-right: 1px solid var(--line);
  }

  .brand { display: flex; align-items: center; gap: 12px; padding: 4px 8px 22px; }
  .logo {
    width: 40px; height: 40px; border-radius: 12px; display: grid; place-items: center;
    background: var(--grad); color: white;
    box-shadow: 0 6px 18px color-mix(in srgb, var(--accent) 45%, transparent), inset 0 1px 0 rgba(255, 255, 255, 0.25);
  }
  .name { font-weight: 700; font-size: 17px; letter-spacing: -0.01em; }
  .tag { color: var(--faint); font-size: 12px; }

  nav { display: flex; flex-direction: column; gap: 2px; }
  .nav {
    display: flex; align-items: center; gap: 12px;
    height: 40px; padding: 0 12px; border: 0; border-radius: 10px;
    background: transparent; color: var(--muted); cursor: pointer; text-align: left;
    font-weight: 500; transition: background 0.15s, color 0.15s;
    position: relative;
  }
  .nav:hover { background: rgb(var(--fg-rgb) / 0.04); color: var(--text); }
  .nav.active { background: var(--accent-soft); color: var(--text); }
  .nav.active::before {
    content: ''; position: absolute; left: -14px; top: 10px; bottom: 10px; width: 3px;
    border-radius: 0 3px 3px 0; background: var(--grad);
  }
  .nav.active :global(svg) { color: var(--accent-text); }
  .count {
    margin-left: auto; font-size: 11.5px; font-weight: 600; color: var(--muted);
    background: rgb(var(--fg-rgb) / 0.06); padding: 1px 8px; border-radius: 99px;
  }

  .mini-progress {
    margin-top: 14px; padding: 12px; border-radius: 12px; border: 1px solid var(--line-2);
    background: var(--panel); text-align: left; cursor: pointer;
  }
  .mp-title { font-size: 12.5px; font-weight: 600; margin-bottom: 8px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .mp-bar { height: 4px; border-radius: 4px; background: rgb(var(--fg-rgb) / 0.08); overflow: hidden; }
  .mp-bar div { height: 100%; background: var(--grad); transition: width 0.3s var(--ease); }

  .upd { margin-top: auto; margin-bottom: 14px; padding: 12px; border-radius: 12px; background: var(--accent-soft); border: 1px solid color-mix(in srgb, var(--accent) 30%, transparent); display: flex; flex-direction: column; gap: 9px; }
  .upd-title { font-size: 12.5px; font-weight: 650; display: flex; align-items: center; gap: 6px; }
  .upd-title :global(svg) { color: var(--accent-text); }
  .upd .mini { height: 30px; font-size: 12.5px; justify-content: center; }
  .upd + .foot { margin-top: 0; }
  .foot { margin-top: auto; padding: 0 10px; color: var(--faint); font-size: 12px; display: flex; flex-direction: column; gap: 2px; }
  .ver { opacity: 0.7; }

  main { overflow-y: auto; overflow-x: hidden; position: relative; }

  .dropzone {
    position: fixed; inset: 0; z-index: 70; display: grid; place-items: center; pointer-events: none;
    background: var(--scrim); backdrop-filter: blur(6px); animation: fade-up 0.15s;
  }
  .dz-box {
    display: flex; flex-direction: column; align-items: center; gap: 10px; text-align: center;
    padding: 46px 70px; border-radius: 26px; border: 2px dashed color-mix(in srgb, var(--accent) 70%, transparent);
    background: color-mix(in srgb, var(--accent) 8%, transparent); color: var(--accent-text);
  }
  .dz-box b { font-size: 20px; color: var(--text); }
  .dz-box span { color: var(--muted); }
</style>

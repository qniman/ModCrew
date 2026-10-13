<script>
  import { onMount } from 'svelte';
  import { app, init } from './lib/state.svelte.js';
  import { isMock } from './lib/api.js';
  import Icon from './lib/Icon.svelte';
  import PacksPage from './pages/PacksPage.svelte';
  import PackDetail from './pages/PackDetail.svelte';
  import GamesPage from './pages/GamesPage.svelte';
  import AboutPage from './pages/AboutPage.svelte';
  import AskDialog from './lib/AskDialog.svelte';
  import Toasts from './lib/Toasts.svelte';

  onMount(init);

  const nav = [
    { id: 'packs', label: 'Сборки', icon: 'packs' },
    { id: 'games', label: 'Мои игры', icon: 'games' },
    { id: 'about', label: 'Как это работает', icon: 'info' },
  ];

  function go(page) {
    app.page = page;
    app.packId = null;
  }

  let installedCount = $derived(app.packs.filter((p) => p.installed).length);
</script>

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
      <button class="mini-progress" onclick={() => { app.page = 'packs'; app.packId = app.install.packId; }}>
        <div class="mp-title">Устанавливаю {view?.pack.name ?? ''}</div>
        <div class="mp-bar"><div style:width="{app.install.total ? (100 * app.install.step) / app.install.total : 5}%"></div></div>
      </button>
    {/if}

    <div class="foot">
      <div>{installedCount} установлено</div>
      <div class="ver">v{app.info?.version ?? '…'}{isMock ? ' · браузер' : ''}</div>
    </div>
  </aside>

  <main>
    {#if app.page === 'packs' && app.packId}
      <PackDetail />
    {:else if app.page === 'packs'}
      <PacksPage />
    {:else if app.page === 'games'}
      <GamesPage />
    {:else}
      <AboutPage />
    {/if}
  </main>
</div>

<AskDialog />
<Toasts />

<style>
  .shell { display: grid; grid-template-columns: 232px 1fr; height: 100%; }

  aside {
    display: flex; flex-direction: column; gap: 6px;
    padding: 20px 14px 16px;
    background: linear-gradient(180deg, #10121a 0%, #0d0e14 100%);
    border-right: 1px solid var(--line);
  }

  .brand { display: flex; align-items: center; gap: 12px; padding: 4px 8px 22px; }
  .logo {
    width: 40px; height: 40px; border-radius: 12px; display: grid; place-items: center;
    background: var(--grad); color: white;
    box-shadow: 0 6px 18px rgba(91, 108, 255, 0.45), inset 0 1px 0 rgba(255, 255, 255, 0.25);
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
  .nav:hover { background: rgba(255, 255, 255, 0.04); color: var(--text); }
  .nav.active { background: var(--accent-soft); color: var(--text); }
  .nav.active::before {
    content: ''; position: absolute; left: -14px; top: 10px; bottom: 10px; width: 3px;
    border-radius: 0 3px 3px 0; background: var(--grad);
  }
  .nav.active :global(svg) { color: #a993ff; }
  .count {
    margin-left: auto; font-size: 11.5px; font-weight: 600; color: var(--muted);
    background: rgba(255, 255, 255, 0.06); padding: 1px 8px; border-radius: 99px;
  }

  .mini-progress {
    margin-top: 14px; padding: 12px; border-radius: 12px; border: 1px solid var(--line-2);
    background: var(--panel); text-align: left; cursor: pointer;
  }
  .mp-title { font-size: 12.5px; font-weight: 600; margin-bottom: 8px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .mp-bar { height: 4px; border-radius: 4px; background: rgba(255, 255, 255, 0.08); overflow: hidden; }
  .mp-bar div { height: 100%; background: var(--grad); transition: width 0.3s var(--ease); }

  .foot { margin-top: auto; padding: 0 10px; color: var(--faint); font-size: 12px; display: flex; flex-direction: column; gap: 2px; }
  .ver { opacity: 0.7; }

  main { overflow-y: auto; overflow-x: hidden; position: relative; }
</style>

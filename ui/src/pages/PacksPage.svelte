<script>
  import { app, refresh, run, statusOf, statusLabel, steamCdn } from '../lib/state.svelte.js';
  import Icon from '../lib/Icon.svelte';
  import Cover from '../lib/Cover.svelte';
  import NewPackDialog from '../lib/editor/NewPackDialog.svelte';

  let filter = $state('all');
  let creating = $state(false);

  let counts = $derived({
    all: app.packs.length,
    installed: app.packs.filter((v) => ['installed', 'update'].includes(statusOf(v))).length,
    update: app.packs.filter((v) => statusOf(v) === 'update').length,
  });

  let shown = $derived(
    app.packs.filter((v) => {
      const s = statusOf(v);
      if (filter === 'installed') return s === 'installed' || s === 'update';
      if (filter === 'update') return s === 'update';
      return true;
    }),
  );

  async function importFile() {
    const id = await run('packs.importFile');
    if (id) {
      await refresh();
      app.packId = id;
    }
  }

  async function importFolder() {
    const id = await run('packs.importFolder');
    if (id) {
      await refresh();
      app.packId = id;
    }
  }

  const plural = (n) => (n % 10 === 1 && n % 100 !== 11 ? 'мод' : [2, 3, 4].includes(n % 10) && ![12, 13, 14].includes(n % 100) ? 'мода' : 'модов');
</script>

<div class="page">
  <header>
    <div>
      <h1>Сборки</h1>
      <p class="sub">Наборы модов, которые ставятся одной кнопкой</p>
    </div>
    <div class="head-actions">
      <button class="btn ghost" onclick={importFolder} title="Для авторов: папка с pack.json">
        <Icon name="folder" size={16} /> Из папки
      </button>
      <button class="btn" onclick={() => (creating = true)}>
        <Icon name="edit" size={16} /> Новая сборка
      </button>
      <button class="btn primary" onclick={importFile}>
        <Icon name="plus" size={16} /> Добавить сборку
      </button>
    </div>
  </header>

  {#if app.packs.length}
    <div class="tabs">
      <button class:on={filter === 'all'} onclick={() => (filter = 'all')}>Все <b>{counts.all}</b></button>
      <button class:on={filter === 'installed'} onclick={() => (filter = 'installed')}>Установленные <b>{counts.installed}</b></button>
      {#if counts.update}
        <button class:on={filter === 'update'} onclick={() => (filter = 'update')} class="upd">Обновления <b>{counts.update}</b></button>
      {/if}
    </div>

    <div class="grid">
      {#each shown as view (view.pack.id)}
        {@const status = statusOf(view)}
        <button class="card" onclick={() => (app.packId = view.pack.id)}>
          <div class="art">
            <Cover urls={[view.coverUrl, view.game.images.hero, steamCdn(view.game.appId, 'library_hero.jpg'), view.game.images.header]} label={view.pack.name} />
            <div class="shade"></div>
            {#if view.game.images.logo}
              <img class="game-logo" src={view.game.images.logo} alt="" onerror={(e) => e.currentTarget.remove()} draggable="false" />
            {/if}
            <span class="pill {status} on-art">{statusLabel[status]}</span>
          </div>
          <div class="body">
            <div class="game">{view.game.name}</div>
            <h3>{view.pack.name}</h3>
            {#if view.pack.description}<p>{view.pack.description}</p>{/if}
            <div class="meta">
              <span><Icon name="puzzle" size={14} /> {view.pack.mods.length} {plural(view.pack.mods.length)}</span>
              <span>v{view.pack.version}</span>
              {#if view.pack.author}<span><Icon name="user" size={14} /> {view.pack.author}</span>{/if}
            </div>
          </div>
        </button>
      {/each}
    </div>
  {:else if !app.loading}
    <div class="empty">
      <div class="empty-art"><Icon name="packs" size={40} /></div>
      <h2>Сборок пока нет</h2>
      <p>
        Попроси друга прислать файл <code>.modcrew</code> и открой его двойным кликом<br />
        (или перетащи в это окно). Потом жми «Установить» — ModCrew всё сделает сам.
      </p>
      <div class="empty-actions">
        <button class="btn primary big" onclick={importFile}><Icon name="plus" /> Добавить сборку</button>
        <button class="btn big" onclick={() => (creating = true)}><Icon name="edit" /> Сделать свою</button>
      </div>
    </div>
  {/if}
</div>

{#if creating}<NewPackDialog onclose={() => (creating = false)} />{/if}

<style>
  .page { padding: 34px 40px 48px; max-width: 1400px; margin: 0 auto; }
  header { display: flex; align-items: flex-end; justify-content: space-between; gap: 20px; margin-bottom: 24px; }
  h1 { font-size: 30px; font-weight: 700; }
  .sub { color: var(--muted); margin: 4px 0 0; }
  .head-actions { display: flex; gap: 8px; }

  .tabs { display: flex; gap: 6px; margin-bottom: 22px; }
  .tabs button {
    height: 32px; padding: 0 14px; border-radius: 99px; border: 1px solid var(--line);
    background: transparent; color: var(--muted); cursor: pointer; font-weight: 500; font-size: 13px;
    transition: all 0.15s;
  }
  .tabs button b { font-weight: 600; margin-left: 4px; color: var(--faint); }
  .tabs button:hover { color: var(--text); border-color: var(--line-2); }
  .tabs button.on { background: var(--text); color: var(--bg); border-color: var(--text); }
  .tabs button.on b { color: rgba(0, 0, 0, 0.45); }
  .tabs .upd:not(.on) b { color: var(--warn); }

  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(300px, 1fr)); gap: 20px; }

  .card {
    display: flex; flex-direction: column; text-align: left; padding: 0; cursor: pointer;
    background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius-lg); overflow: hidden;
    transition: transform 0.25s var(--ease), box-shadow 0.25s var(--ease), border-color 0.25s;
  }
  .card:hover { transform: translateY(-4px); border-color: var(--line-2); box-shadow: 0 18px 40px rgba(0, 0, 0, 0.45); }
  .card:hover .art :global(img) { transform: scale(1.04); }

  .art { position: relative; aspect-ratio: 16 / 8; overflow: hidden; }
  .art :global(img) { transition: transform 0.6s var(--ease), opacity 0.4s; }
  .shade { position: absolute; inset: 0; background: linear-gradient(180deg, rgba(0, 0, 0, 0) 40%, rgba(21, 24, 33, 0.95) 100%); }
  .game-logo {
    position: absolute; left: 16px; bottom: 12px; max-width: 46%; max-height: 42%;
    object-fit: contain; object-position: left bottom; filter: drop-shadow(0 4px 12px rgba(0, 0, 0, 0.6));
  }
  .on-art { position: absolute; top: 12px; right: 12px; background-color: rgba(10, 12, 18, 0.7); }

  .body { padding: 14px 18px 18px; display: flex; flex-direction: column; flex: 1; }
  .game { font-size: 12px; color: var(--faint); font-weight: 600; text-transform: uppercase; letter-spacing: 0.06em; }
  h3 { font-size: 18px; font-weight: 650; margin: 4px 0 6px; }
  p {
    margin: 0 0 14px; color: var(--muted); font-size: 13.5px;
    display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
  }
  .meta { margin-top: auto; display: flex; gap: 16px; color: var(--faint); font-size: 12.5px; }
  .meta span { display: inline-flex; align-items: center; gap: 5px; }

  .empty { text-align: center; padding: 80px 20px; display: flex; flex-direction: column; align-items: center; }
  .empty-art {
    width: 96px; height: 96px; border-radius: 28px; display: grid; place-items: center; margin-bottom: 22px;
    background: var(--accent-soft); color: #a993ff; border: 1px solid rgba(124, 92, 255, 0.3);
    box-shadow: 0 0 60px rgba(124, 92, 255, 0.25);
  }
  .empty h2 { font-size: 22px; margin-bottom: 8px; }
  .empty p { color: var(--muted); margin: 0 0 26px; max-width: 460px; line-height: 1.6; }
  .empty-actions { display: flex; gap: 10px; }
  code { background: rgba(255, 255, 255, 0.08); padding: 1px 6px; border-radius: 6px; font-size: 13px; }
</style>

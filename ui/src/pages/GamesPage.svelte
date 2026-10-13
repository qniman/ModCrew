<script>
  import { app, refresh, run, steamCdn } from '../lib/state.svelte.js';
  import Icon from '../lib/Icon.svelte';
  import Cover from '../lib/Cover.svelte';

  let query = $state('');
  let refreshing = $state(false);

  let shown = $derived(
    app.games
      .filter((g) => g.name.toLowerCase().includes(query.trim().toLowerCase()))
      // Games with packs first: that is what the player came here for.
      .toSorted((a, b) => (b.installedPack ? 2 : b.packCount ? 1 : 0) - (a.installedPack ? 2 : a.packCount ? 1 : 0)),
  );

  async function rescan() {
    refreshing = true;
    await refresh(true);
    refreshing = false;
  }

  async function setPath(game) {
    if (await run('games.setPath', { appId: game.appId }, 'Папка игры сохранена')) await refresh();
  }

  function openPack(id) {
    app.page = 'packs';
    app.packId = id;
  }
</script>

<div class="page">
  <header>
    <div>
      <h1>Мои игры</h1>
      <p class="sub">
        {#if app.info && !app.info.steamFound}Steam не найден на этом компьютере{:else}Найдено в Steam: {app.games.length}{/if}
      </p>
    </div>
    <div class="head-actions">
      <input class="search" placeholder="Поиск игры…" bind:value={query} />
      <button class="btn icon" class:spin={refreshing} onclick={rescan} title="Пересканировать Steam"><Icon name="refresh" size={16} /></button>
    </div>
  </header>

  <div class="grid">
    {#each shown as game (game.appId)}
      <div class="game" class:has={game.installedPack}>
        <div class="poster">
          <Cover urls={[game.images.poster, steamCdn(game.appId, 'library_600x900.jpg'), game.images.header]} label={game.name} />
          <div class="overlay">
            <button class="btn" onclick={() => run('games.open', { appId: game.appId })}><Icon name="folder" size={15} /> Открыть папку</button>
            <button class="btn ghost" onclick={() => setPath(game)}>Указать папку…</button>
          </div>
          {#if game.installedPack}
            <button class="badge" onclick={() => openPack(game.installedPack.id)} title="Открыть сборку">
              <Icon name="check" size={13} /> {game.installedPack.name}
            </button>
          {:else if game.packCount}
            <span class="badge soft">Сборок: {game.packCount}</span>
          {/if}
        </div>
        <div class="title" title={game.name}>{game.name}</div>
        {#if game.custom}<div class="custom">своя папка</div>{/if}
      </div>
    {/each}
  </div>

  {#if !shown.length && !app.loading}
    <p class="none">{query ? 'Ничего не нашлось.' : 'Игры не найдены. Установи что-нибудь в Steam и нажми ↻.'}</p>
  {/if}
</div>

<style>
  .page { padding: 34px 40px 48px; max-width: 1400px; margin: 0 auto; }
  header { display: flex; align-items: flex-end; justify-content: space-between; gap: 20px; margin-bottom: 26px; }
  h1 { font-size: 30px; font-weight: 700; }
  .sub { color: var(--muted); margin: 4px 0 0; }
  .head-actions { display: flex; gap: 8px; }
  .search {
    width: 240px; height: 38px; padding: 0 14px; border-radius: 11px;
    border: 1px solid var(--line-2); background: var(--panel); color: var(--text); font: inherit; outline: none;
    transition: border-color 0.15s, box-shadow 0.15s;
  }
  .search:focus { border-color: color-mix(in srgb, var(--accent) 60%, transparent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent) 18%, transparent); }
  .spin :global(svg) { animation: spin 0.8s linear infinite; }
  @keyframes spin { to { transform: rotate(360deg); } }

  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(168px, 1fr)); gap: 22px 18px; }
  .poster {
    position: relative; aspect-ratio: 2 / 3; border-radius: var(--radius); overflow: hidden;
    border: 1px solid var(--line); box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
    transition: transform 0.25s var(--ease), box-shadow 0.25s var(--ease);
  }
  .game:hover .poster { transform: translateY(-4px); box-shadow: 0 18px 36px rgba(0, 0, 0, 0.5); }
  .game.has .poster { border-color: rgba(52, 211, 153, 0.45); }
  .overlay {
    position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px;
    background: rgba(10, 11, 16, 0.72); backdrop-filter: blur(4px); opacity: 0; transition: opacity 0.2s;
  }
  .game:hover .overlay { opacity: 1; }
  .overlay .btn { height: 34px; font-size: 12.5px; }
  .badge {
    position: absolute; left: 8px; right: 8px; bottom: 8px; border: 0; cursor: pointer;
    display: flex; align-items: center; gap: 6px; padding: 6px 10px; border-radius: 9px;
    background: rgba(16, 80, 60, 0.85); color: #b7f5dc; backdrop-filter: blur(8px);
    font-size: 12px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
  }
  .badge.soft { background: rgba(20, 22, 30, 0.8); color: var(--muted); cursor: default; }
  .title { margin-top: 10px; font-weight: 600; font-size: 13.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .custom { font-size: 11.5px; color: var(--faint); }
  .none { color: var(--muted); text-align: center; padding: 40px; }
</style>

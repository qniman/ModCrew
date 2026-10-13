<script>
  import { app, refresh, steamCdn, toast, navigate } from '../state.svelte.js';
  import { call } from '../api.js';
  import Icon from '../Icon.svelte';
  import Cover from '../Cover.svelte';

  let { onclose } = $props();

  let name = $state('');
  let appId = $state(null);
  let query = $state('');
  let busy = $state(false);

  let games = $derived(app.games.filter((g) => g.name.toLowerCase().includes(query.trim().toLowerCase())));

  async function create() {
    busy = true;
    try {
      const id = await call('packs.create', { name: name.trim(), appId });
      await refresh();
      onclose();
      navigate('packs', null, id);
    } catch (e) {
      toast(e.message, 'error');
    } finally {
      busy = false;
    }
  }
</script>

<svelte:window onkeydown={(e) => e.key === 'Escape' && onclose()} />

<div class="backdrop" role="presentation" onclick={(e) => e.target === e.currentTarget && onclose()}>
  <div class="dialog" role="dialog" aria-modal="true">
    <header>
      <h3>Новая сборка</h3>
      <button class="btn ghost icon" onclick={onclose}><Icon name="x" size={16} /></button>
    </header>
    <div class="content">
      <label class="field">
        <span class="lbl">Название</span>
        <!-- svelte-ignore a11y_autofocus -->
        <input bind:value={name} placeholder="Например: MSC с друзьями" autofocus />
      </label>
      <div class="field">
        <div class="lbl-row">
          <span class="lbl">Игра</span>
          <input class="search" bind:value={query} placeholder="Поиск…" />
        </div>
        <div class="games">
          {#each games as g (g.appId)}
            <button class="game" class:sel={appId === g.appId} onclick={() => (appId = g.appId)}>
              <div class="poster"><Cover urls={[g.images.poster, steamCdn(g.appId, 'library_600x900.jpg'), g.images.header]} label={g.name} /></div>
              <span>{g.name}</span>
              {#if appId === g.appId}<div class="tick"><Icon name="check" size={14} /></div>{/if}
            </button>
          {:else}
            <div class="none">Игры не найдены. Установи игру в Steam.</div>
          {/each}
        </div>
      </div>
    </div>
    <footer>
      <button class="btn ghost" onclick={onclose}>Отмена</button>
      <button class="btn primary" disabled={!name.trim() || !appId || busy} onclick={create}><Icon name="plus" size={16} /> Создать</button>
    </footer>
  </div>
</div>

<style>
  .backdrop { position: fixed; inset: 0; z-index: 40; display: grid; place-items: center; background: rgba(5, 6, 10, 0.6); backdrop-filter: blur(6px); }
  .dialog {
    width: min(720px, calc(100vw - 32px)); max-height: calc(100vh - 60px); display: flex; flex-direction: column;
    background: var(--panel); border: 1px solid var(--line-2); border-radius: var(--radius-lg);
    box-shadow: 0 30px 80px rgba(0, 0, 0, 0.6); animation: fade-up 0.2s var(--ease);
  }
  header { display: flex; justify-content: space-between; align-items: center; padding: 20px 22px 4px; }
  h3 { font-size: 18px; }
  .content { padding: 14px 22px; display: flex; flex-direction: column; gap: 16px; overflow: hidden; }
  .field { display: flex; flex-direction: column; gap: 6px; min-height: 0; }
  .lbl { font-size: 12px; color: var(--muted); font-weight: 600; }
  .lbl-row { display: flex; justify-content: space-between; align-items: center; }
  input {
    height: 40px; padding: 0 12px; border-radius: 10px; border: 1px solid var(--line-2); background: var(--bg-2);
    color: var(--text); font: inherit; outline: none;
  }
  input:focus { border-color: rgba(124, 92, 255, 0.6); box-shadow: 0 0 0 3px rgba(124, 92, 255, 0.15); }
  .search { height: 30px; width: 180px; font-size: 12.5px; }
  .games { display: grid; grid-template-columns: repeat(auto-fill, minmax(104px, 1fr)); gap: 12px; max-height: 360px; overflow-y: auto; padding: 4px 2px; }
  .game { position: relative; border: 0; background: none; cursor: pointer; padding: 0; text-align: left; }
  .poster { aspect-ratio: 2 / 3; border-radius: 10px; overflow: hidden; border: 2px solid transparent; transition: border-color 0.15s, transform 0.2s var(--ease); }
  .game:hover .poster { transform: translateY(-2px); }
  .game.sel .poster { border-color: var(--accent); box-shadow: 0 0 0 4px rgba(124, 92, 255, 0.25); }
  .game span { display: block; margin-top: 6px; font-size: 12px; color: var(--muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .game.sel span { color: var(--text); }
  .tick { position: absolute; top: 6px; right: 6px; width: 22px; height: 22px; border-radius: 50%; display: grid; place-items: center; background: var(--accent); color: white; }
  .none { color: var(--muted); padding: 20px 0; grid-column: 1 / -1; }
  footer { display: flex; justify-content: flex-end; gap: 8px; padding: 14px 22px 20px; border-top: 1px solid var(--line); }
</style>

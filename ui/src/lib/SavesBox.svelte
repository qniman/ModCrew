<script>
  import { onMount } from 'svelte';
  import { call } from './api.js';
  import { confirm, toast, run, navigate } from './state.svelte.js';
  import Icon from './Icon.svelte';

  let { view } = $props();

  let data = $state(null); // { dir, exists, backups: [{id, at, reason, auto, size, files}] }
  let showAll = $state(false);
  let working = $state(false);

  const load = async () => (data = await call('saves.list', { id: view.pack.id }).catch(() => null));
  onMount(load);

  let shown = $derived(data ? (showAll ? data.backups : data.backups.slice(0, 4)) : []);

  const when = (iso) => {
    const d = new Date(iso);
    const today = new Date();
    const time = d.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' });
    if (d.toDateString() === today.toDateString()) return `сегодня, ${time}`;
    return `${d.toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' })}, ${time}`;
  };
  const size = (n) => (n > 1048576 ? `${(n / 1048576).toFixed(1)} МБ` : `${Math.max(1, Math.round(n / 1024))} КБ`);

  async function backupNow() {
    working = true;
    if ((await run('saves.backup', { id: view.pack.id })) !== undefined) toast('Сохранения скопированы', 'ok');
    working = false;
    load();
  }

  async function restore(b) {
    const ok = await confirm('Вернуть сохранение?',
      `Сохранения станут такими, как были ${when(b.at)} (${b.reason}).\n\nТекущие ModCrew сначала сам скопирует — если передумаешь, их можно будет вернуть так же.\n\nЕсли Steam спросит, какое сохранение оставить, — выбирай «на этом компьютере».`,
      'Вернуть');
    if (!ok) return;
    working = true;
    if ((await run('saves.restore', { id: view.pack.id, backup: b.id })) !== undefined) toast('Сохранение возвращено', 'ok');
    working = false;
    load();
  }

  async function remove(b) {
    if (!(await confirm('Удалить бэкап?', `Бэкап от ${when(b.at)} пропадёт насовсем.`, 'Удалить', true))) return;
    await run('saves.delete', { id: view.pack.id, backup: b.id });
    load();
  }
</script>

{#if data}
  <section class="saves">
    <div class="head">
      <h2>Сохранения</h2>
      {#if data.dir}
        <div class="tools">
          <button class="btn ghost small" onclick={() => run('saves.open', { id: view.pack.id })} disabled={!data.exists}><Icon name="folder" size={14} /> Папка</button>
          <button class="btn small" onclick={backupNow} disabled={working || !data.exists}><Icon name="save" size={14} /> Бэкап сейчас</button>
        </div>
      {/if}
    </div>

    {#if !data.dir}
      <div class="empty">
        <Icon name="save" size={18} />
        <span>ModCrew не знает, где эта игра хранит сохранения, поэтому не делает их копии.
          Автор сборки может указать папку в редакторе.
          <button class="link" onclick={() => navigate('packs', null, view.pack.id)}>Открыть редактор</button></span>
      </div>
    {:else if !data.backups.length}
      <div class="empty">
        <Icon name="check" size={18} />
        <span>Перед каждой установкой, обновлением и починкой ModCrew сам копирует сохранения. Пока копий нет{data.exists ? '' : ' — в игре ещё нет сохранений'}.</span>
      </div>
    {:else}
      <div class="list">
        {#each shown as b (b.id)}
          <div class="row">
            <div class="ic" class:manual={!b.auto}><Icon name={b.auto ? 'refresh' : 'save'} size={15} /></div>
            <div class="txt">
              <div class="t1">{when(b.at)}</div>
              <div class="t2">{b.reason} · {size(b.size)}</div>
            </div>
            <button class="btn small" disabled={working} onclick={() => restore(b)}><Icon name="undo" size={14} /> Вернуть</button>
            <button class="btn ghost icon small" title="Удалить бэкап" onclick={() => remove(b)}><Icon name="trash" size={14} /></button>
          </div>
        {/each}
      </div>
      {#if data.backups.length > 4}
        <button class="link more" onclick={() => (showAll = !showAll)}>{showAll ? 'Свернуть' : `Показать все (${data.backups.length})`}</button>
      {/if}
    {/if}
  </section>
{/if}

<style>
  .saves { margin-top: 26px; }
  .head { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
  h2 { font-size: 18px; }
  .tools { display: flex; gap: 6px; }
  .small { height: 32px; padding: 0 12px; font-size: 12.5px; }
  .small.icon { width: 32px; padding: 0; }
  .empty {
    display: flex; gap: 12px; align-items: flex-start; padding: 14px 16px; border-radius: var(--radius);
    background: var(--panel); border: 1px dashed var(--line-2); color: var(--muted); font-size: 13px; line-height: 1.55;
  }
  .empty :global(svg) { flex: none; margin-top: 2px; color: var(--accent-text); }
  .list { display: flex; flex-direction: column; gap: 6px; }
  .row { display: flex; align-items: center; gap: 12px; padding: 10px 12px; border-radius: 12px; background: var(--panel); border: 1px solid var(--line); }
  .ic { width: 32px; height: 32px; border-radius: 9px; display: grid; place-items: center; flex: none; background: rgb(var(--fg-rgb) / 0.05); color: var(--muted); }
  .ic.manual { background: var(--accent-soft); color: var(--accent-text); }
  .txt { flex: 1; min-width: 0; }
  .t1 { font-weight: 600; font-size: 13.5px; }
  .t2 { color: var(--muted); font-size: 12.5px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .link { border: 0; background: none; padding: 0; color: var(--accent-text); cursor: pointer; font: inherit; font-weight: 600; }
  .more { margin-top: 8px; font-size: 13px; }
</style>

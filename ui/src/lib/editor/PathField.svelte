<script>
  import { call } from '../api.js';
  import { toast } from '../state.svelte.js';
  import Icon from '../Icon.svelte';

  /** getOutputs(): files earlier steps leave in the temporary folder; programs: show only .exe/.msi first. */
  let { value = $bindable(), kind = 'path-write', packId, appId, placeholder = '', invalid = false, getOutputs = null, programs = false } = $props();

  let open = $state(false);
  let files = $state([]);
  let outputs = $state([]);

  const isProgram = (f) => /\.(exe|msi)$/i.test(f);
  let shownOutputs = $derived((programs ? outputs.filter(isProgram) : outputs).slice(0, 60));
  let shownFiles = $derived(files.filter((f) => !f.startsWith('cover.') && (!programs || isProgram(f))));
  let root;

  const vars = [
    ['{game}', 'папка игры'],
    ['{work}', 'временная папка'],
    ['{pack}', 'файлы сборки'],
    ['{documents}', 'Документы'],
    ['{appdata}', 'AppData\\Roaming'],
    ['{localappdata}', 'AppData\\Local'],
    ['{locallow}', 'AppData\\LocalLow'],
  ];

  async function toggle() {
    open = !open;
    if (open && kind === 'path-read') {
      [files, outputs] = await Promise.all([
        call('packs.files', { id: packId }).catch(() => []),
        getOutputs ? getOutputs().catch(() => []) : [],
      ]);
    }
  }

  function set(v) {
    value = v;
    open = false;
  }

  function insertVar(v) {
    // Replace an existing leading variable, otherwise prefix.
    const rest = (value ?? '').replace(/^\{[a-z]+\}\/?/i, '');
    set(rest ? `${v}/${rest}` : v);
  }

  async function pick(what) {
    open = false;
    try {
      const p = await call('paths.pick', { appId, kind: what });
      if (p) value = p;
    } catch (e) {
      toast(e.message, 'error');
    }
  }

  async function addFile() {
    open = false;
    try {
      const info = await call('packs.addFile', { id: packId, appId });
      if (info) value = info.packPath;
    } catch (e) {
      toast(e.message, 'error');
    }
  }

  function outside(e) {
    if (open && root && !root.contains(e.target)) open = false;
  }
</script>

<svelte:window onmousedown={outside} />

<div class="pf" bind:this={root}>
  <div class="row" class:invalid>
    <input class="mono" bind:value {placeholder} spellcheck="false" />
    <button class="pick" onclick={toggle} title="Выбрать"><Icon name="folder" size={15} /></button>
  </div>
  {#if open}
    <div class="pop">
      {#if kind === 'path-read'}
        {#if shownOutputs.length}
          <div class="sect">Из предыдущих шагов</div>
          {#each shownOutputs as f}
            <button class="opt" onclick={() => set(f)}><Icon name={isProgram(f) ? 'play' : 'file'} size={14} /> <span class="mono">{f}</span></button>
          {/each}
        {/if}
        <div class="sect">Файлы сборки</div>
        {#each shownFiles as f}
          <button class="opt" onclick={() => set(`{pack}/${f}`)}><Icon name="file" size={14} /> <span class="mono">{f}</span></button>
        {:else}
          <div class="empty">{programs ? 'Программ в сборке нет' : 'В сборке пока нет файлов'}</div>
        {/each}
        <button class="opt accent" onclick={addFile}><Icon name="plus" size={14} /> Добавить файл в сборку…</button>
        <button class="opt" onclick={() => pick('file')}><Icon name="search" size={14} /> Файл в папке игры…</button>
      {:else}
        <button class="opt accent" onclick={() => pick('folder')}><Icon name="folder" size={14} /> Выбрать папку в игре…</button>
        <button class="opt" onclick={() => pick('file')}><Icon name="file" size={14} /> Выбрать файл в игре…</button>
      {/if}
      <div class="sect">Начало пути</div>
      <div class="vars">
        {#each vars as [v, title]}
          <button class="var mono" {title} onclick={() => insertVar(v)}>{v}</button>
        {/each}
      </div>
    </div>
  {/if}
</div>

<style>
  .pf { position: relative; }
  .row {
    display: flex; height: 36px; border-radius: 9px; border: 1px solid var(--line-2); background: var(--bg-2);
    transition: border-color 0.15s, box-shadow 0.15s;
  }
  .row:focus-within { border-color: rgba(124, 92, 255, 0.6); box-shadow: 0 0 0 3px rgba(124, 92, 255, 0.15); }
  .row.invalid { border-color: rgba(248, 113, 113, 0.55); }
  input { flex: 1; min-width: 0; border: 0; background: transparent; color: var(--text); padding: 0 10px; outline: none; font-size: 12.5px; }
  .pick { width: 36px; border: 0; border-left: 1px solid var(--line); background: transparent; color: var(--muted); cursor: pointer; display: grid; place-items: center; border-radius: 0 9px 9px 0; }
  .pick:hover { color: var(--text); background: rgba(255, 255, 255, 0.05); }
  .mono { font-family: 'Cascadia Mono', Consolas, monospace; }

  .pop {
    position: absolute; z-index: 20; top: 40px; right: 0; width: min(380px, 100%); min-width: 300px;
    padding: 6px; border-radius: 12px; background: var(--panel-2); border: 1px solid var(--line-2);
    box-shadow: 0 20px 50px rgba(0, 0, 0, 0.55); animation: fade-up 0.15s var(--ease);
    max-height: 340px; overflow-y: auto;
  }
  .sect { font-size: 11px; text-transform: uppercase; letter-spacing: 0.06em; color: var(--faint); padding: 8px 8px 4px; font-weight: 600; }
  .opt {
    display: flex; align-items: center; gap: 8px; width: 100%; padding: 7px 8px; border: 0; border-radius: 7px;
    background: transparent; cursor: pointer; text-align: left; font-size: 13px; color: var(--text);
  }
  .opt:hover { background: rgba(255, 255, 255, 0.06); }
  .opt.accent { color: #b9a8ff; }
  .opt span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 12px; }
  .empty { padding: 6px 8px; color: var(--faint); font-size: 12.5px; }
  .vars { display: flex; flex-wrap: wrap; gap: 4px; padding: 2px 6px 6px; }
  .var {
    border: 1px solid var(--line-2); background: var(--bg-2); color: #d6ccff; border-radius: 6px;
    padding: 3px 7px; font-size: 11.5px; cursor: pointer;
  }
  .var:hover { border-color: rgba(124, 92, 255, 0.5); }
</style>

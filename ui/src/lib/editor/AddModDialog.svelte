<script>
  import { onMount } from 'svelte';
  import { call, on } from '../api.js';
  import { buildSteps, describe, guessName, formatSize } from './steps.js';
  import PathField from './PathField.svelte';
  import Icon from '../Icon.svelte';

  /** mode: 'file' | 'url'; append: add steps to an existing mod instead of creating one. */
  let { mode, packId, appId, append = false, onclose, onadd } = $props();

  // svelte-ignore state_referenced_locally
  let stage = $state(mode === 'url' ? 'url' : 'busy'); // mode never changes while open
  let url = $state('');
  let error = $state('');
  let progress = $state(null);
  let info = $state(null);
  let from = $state('');
  let to = $state('{game}');
  let name = $state('');
  let version = $state('');

  let preview = $derived(info ? buildSteps(info, from, to) : []);
  let tree = $derived(
    (info?.dirs ?? []).slice(0, 400).map((d) => ({ path: d, depth: d.split('/').length - 1, label: d.split('/').pop() })),
  );

  function accept(result) {
    info = result;
    from = result.suggestFrom ?? '';
    to = result.suggestTo ?? '{game}';
    const g = guessName(result.name);
    name = g.name;
    version = g.version;
    stage = 'choose';
  }

  onMount(async () => {
    if (mode !== 'file') return;
    try {
      const result = await call('packs.addFile', { id: packId, appId });
      if (!result) return onclose();
      accept(result);
    } catch (e) {
      error = e.message;
      stage = 'failed';
    }
  });

  async function probe() {
    error = '';
    if (!/^https:\/\//i.test(url.trim())) {
      error = 'Нужна ссылка, начинающаяся с https://';
      return;
    }
    stage = 'busy';
    progress = { done: 0, total: null };
    const off = on('probe.progress', (p) => (progress = p));
    try {
      accept(await call('packs.probeUrl', { url: url.trim(), appId }));
    } catch (e) {
      error = e.message;
      stage = 'url';
    } finally {
      off();
    }
  }

  async function cancel() {
    // A file copied into the pack but not used would only bloat it.
    if (info?.packPath) await call('packs.deleteFile', { id: packId, path: info.packPath.replace('{pack}/', '') }).catch(() => {});
    onclose();
  }

  function add() {
    onadd({ name: name.trim() || info.name, version: version.trim() || undefined, steps: preview });
  }

  function key(e) {
    if (e.key === 'Escape') cancel();
  }
</script>

<svelte:window onkeydown={key} />

<div class="backdrop" role="presentation" onclick={(e) => e.target === e.currentTarget && cancel()}>
  <div class="dialog" role="dialog" aria-modal="true">
    <header>
      <div class="ic"><Icon name={mode === 'url' ? 'link' : 'file'} size={20} /></div>
      <div>
        <h3>{append ? 'Добавить файл в мод' : mode === 'url' ? 'Мод по ссылке' : 'Мод из файла'}</h3>
        <p>{mode === 'url' ? 'ModCrew скачает файл один раз, чтобы посмотреть, что внутри.' : 'Файл будет лежать внутри сборки и уедет к друзьям вместе с ней.'}</p>
      </div>
      <button class="btn ghost icon close" onclick={cancel}><Icon name="x" size={16} /></button>
    </header>

    {#if stage === 'url'}
      <div class="content">
        <label class="field">
          <span class="lbl">Прямая ссылка на файл</span>
          <!-- svelte-ignore a11y_autofocus -->
          <input class="mono" bind:value={url} placeholder="https://github.com/автор/мод/releases/download/v1.0/mod.zip" autofocus
            onkeydown={(e) => e.key === 'Enter' && probe()} />
          <span class="hint">Ссылка должна сразу отдавать файл (например, GitHub Releases). Для Nexus скачай файл сам и выбери «Мод из файла».</span>
        </label>
        {#if error}<div class="error">{error}</div>{/if}
      </div>
      <footer>
        <button class="btn ghost" onclick={cancel}>Отмена</button>
        <button class="btn primary" onclick={probe}><Icon name="download" size={16} /> Скачать и разобрать</button>
      </footer>
    {:else if stage === 'busy'}
      <div class="content center">
        <div class="spinner"></div>
        {#if progress}
          <div class="muted">
            Скачиваю… {formatSize(progress.done)}{progress.total ? ` из ${formatSize(progress.total)}` : ''}
          </div>
          {#if progress.total}<div class="bar"><div style:width="{(100 * progress.done) / progress.total}%"></div></div>{/if}
        {:else}
          <div class="muted">Жду выбора файла…</div>
        {/if}
      </div>
    {:else if stage === 'failed'}
      <div class="content"><div class="error">{error}</div></div>
      <footer><button class="btn" onclick={onclose}>Закрыть</button></footer>
    {:else if info}
      <div class="content scroll">
        <div class="filecard">
          <Icon name={info.kind === 'zip' ? 'packs' : 'file'} size={20} />
          <div class="fc-text">
            <div class="fc-name">{info.name}</div>
            <div class="muted small">
              {formatSize(info.size)}{info.kind === 'zip' ? ` · архив, файлов: ${info.fileCount}` : ''}{info.sha256 ? ' · sha256 посчитан' : ''}
            </div>
          </div>
        </div>

        {#if !append}
          <div class="two">
            <label class="field"><span class="lbl">Название мода</span><input bind:value={name} /></label>
            <label class="field"><span class="lbl">Версия</span><input bind:value={version} placeholder="1.0" /></label>
          </div>
        {/if}

        {#if info.kind === 'zip'}
          <div class="field">
            <span class="lbl">Что взять из архива</span>
            <div class="tree">
              <label class="node" class:sel={from === ''}>
                <input type="radio" bind:group={from} value="" /> <Icon name="packs" size={14} /> <b>Весь архив</b>
                <span class="muted small">({info.topLevel.join(', ')})</span>
              </label>
              {#each tree as n}
                <label class="node" class:sel={from === n.path} style:padding-left="{14 + n.depth * 18}px">
                  <input type="radio" bind:group={from} value={n.path} /> <Icon name="folder" size={14} /> {n.label}
                </label>
              {/each}
            </div>
          </div>
        {/if}

        <div class="field">
          <span class="lbl">Куда положить {info.kind === 'zip' ? (from ? `папку «${from.split('/').pop()}» (её содержимое)` : 'содержимое архива') : 'файл'}</span>
          <PathField bind:value={to} kind="path-write" {packId} {appId} />
          {#if info.suggestTo === to && info.suggestFrom === from}
            <span class="hint ok"><Icon name="sparkle" size={12} /> Подобрано по структуре архива и папки игры</span>
          {/if}
        </div>

        <div class="field">
          <span class="lbl">Получатся шаги</span>
          <ol class="preview">
            {#each preview as s}<li>{describe(s)}</li>{/each}
          </ol>
        </div>
      </div>
      <footer>
        <button class="btn ghost" onclick={cancel}>Отмена</button>
        <button class="btn primary" disabled={!to} onclick={add}><Icon name="plus" size={16} /> {append ? 'Добавить шаги' : 'Добавить мод'}</button>
      </footer>
    {/if}
  </div>
</div>

<style>
  .backdrop { position: fixed; inset: 0; z-index: 40; display: grid; place-items: center; background: rgba(5, 6, 10, 0.6); backdrop-filter: blur(6px); }
  .dialog {
    width: min(620px, calc(100vw - 32px)); max-height: calc(100vh - 60px); display: flex; flex-direction: column;
    background: var(--panel); border: 1px solid var(--line-2); border-radius: var(--radius-lg);
    box-shadow: 0 30px 80px rgba(0, 0, 0, 0.6); animation: fade-up 0.2s var(--ease);
  }
  header { display: flex; gap: 14px; padding: 22px 22px 6px; align-items: flex-start; }
  .ic { width: 42px; height: 42px; flex: none; border-radius: 12px; display: grid; place-items: center; background: var(--accent-soft); color: #a993ff; }
  h3 { font-size: 17px; }
  header p { margin: 2px 0 0; color: var(--muted); font-size: 13px; }
  .close { margin-left: auto; }
  .content { padding: 16px 22px; display: flex; flex-direction: column; gap: 16px; }
  .content.scroll { overflow-y: auto; }
  .content.center { align-items: center; padding: 40px 22px; }
  footer { display: flex; justify-content: flex-end; gap: 8px; padding: 14px 22px 20px; border-top: 1px solid var(--line); }

  .field { display: flex; flex-direction: column; gap: 6px; }
  .two { display: grid; grid-template-columns: 2fr 1fr; gap: 12px; }
  .lbl { font-size: 12px; color: var(--muted); font-weight: 600; }
  .hint { font-size: 12px; color: var(--faint); }
  .hint.ok { color: #9ee6c4; display: flex; align-items: center; gap: 5px; }
  input:not([type]) {
    height: 38px; padding: 0 12px; border-radius: 10px; border: 1px solid var(--line-2); background: var(--bg-2);
    color: var(--text); font: inherit; outline: none;
  }
  input:focus { border-color: rgba(124, 92, 255, 0.6); box-shadow: 0 0 0 3px rgba(124, 92, 255, 0.15); }
  .mono { font-family: 'Cascadia Mono', Consolas, monospace; font-size: 12.5px; }
  .error { color: var(--danger); background: rgba(248, 113, 113, 0.08); border: 1px solid rgba(248, 113, 113, 0.25); padding: 10px 12px; border-radius: 10px; font-size: 13px; }
  .muted { color: var(--muted); }
  .small { font-size: 12px; }

  .filecard { display: flex; align-items: center; gap: 12px; padding: 12px 14px; border-radius: 12px; background: var(--bg-2); border: 1px solid var(--line); color: #a993ff; }
  .fc-text { min-width: 0; }
  .fc-name { color: var(--text); font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

  .tree { max-height: 220px; overflow-y: auto; border: 1px solid var(--line); border-radius: 10px; background: var(--bg-2); padding: 4px; }
  .node { display: flex; align-items: center; gap: 7px; padding: 5px 10px; border-radius: 7px; cursor: pointer; font-size: 13px; color: var(--muted); }
  .node:hover { background: rgba(255, 255, 255, 0.04); }
  .node.sel { background: var(--accent-soft); color: var(--text); }
  .node input { accent-color: var(--accent); margin: 0; }
  .node b { color: var(--text); font-weight: 600; }

  .preview { margin: 0; padding: 10px 12px 10px 32px; border-radius: 10px; background: var(--bg-2); border: 1px solid var(--line); font-size: 13px; color: var(--muted); }
  .preview li { padding: 2px 0; font-family: 'Cascadia Mono', Consolas, monospace; font-size: 12px; overflow-wrap: anywhere; }

  .spinner { width: 28px; height: 28px; border-radius: 50%; border: 3px solid rgba(169, 147, 255, 0.25); border-top-color: #a993ff; animation: spin 0.8s linear infinite; margin-bottom: 14px; }
  @keyframes spin { to { transform: rotate(360deg); } }
  .bar { width: 70%; height: 5px; border-radius: 5px; background: rgba(255, 255, 255, 0.08); margin-top: 12px; overflow: hidden; }
  .bar div { height: 100%; background: var(--grad); transition: width 0.2s; }
</style>

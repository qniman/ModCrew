<script>
  import { onMount } from 'svelte';
  import { app, refresh, toast, confirm, navigate, installPack, steamCdn, dropTarget } from '../lib/state.svelte.js';
  import { call } from '../lib/api.js';
  import { actions, actionOrder, newStep, strip, withKeys, key, bumpVersion } from '../lib/editor/steps.js';
  import StepCard from '../lib/editor/StepCard.svelte';
  import PathField from '../lib/editor/PathField.svelte';
  import AddModDialog from '../lib/editor/AddModDialog.svelte';
  import Icon from '../lib/Icon.svelte';
  import Cover from '../lib/Cover.svelte';

  const id = app.editId;
  const view0 = app.packs.find((p) => p.pack.id === id);

  let draft = $state(withKeys(structuredClone($state.snapshot(view0.pack))));
  let saved = $state(JSON.stringify(strip($state.snapshot(draft))));
  let savedVersion = $state(view0.pack.version);
  let coverUrl = $state(view0.coverUrl);
  let expanded = $state({});
  let problems = $state([]);
  let wizard = $state(null); // { mode, modIndex? }
  let saving = $state(false);
  let drag = $state(null);

  let view = $derived(app.packs.find((p) => p.pack.id === id));
  let json = $derived(JSON.stringify(strip($state.snapshot(draft))));
  let dirty = $derived(json !== saved);
  let game = $derived(app.games.find((g) => g.appId === draft.game.steamAppId));
  let errors = $derived(problems.filter((p) => p.level === 'error').length);
  let warnings = $derived(problems.length - errors);

  $effect(() => {
    app.editorDirty = dirty;
  });

  // Re-check a moment after every change, so problems stay in sync with the draft.
  let checkTimer;
  $effect(() => {
    const pack = JSON.parse(json);
    clearTimeout(checkTimer);
    checkTimer = setTimeout(async () => {
      problems = await call('packs.check', { id, pack }).catch(() => problems);
    }, 400);
  });

  // Files dropped onto the window go through the wizard one by one.
  const dropQueue = [];
  function nextDropped() {
    if (wizard || !dropQueue.length) return;
    wizard = { mode: 'drop', path: dropQueue.shift() };
  }
  function closeWizard() {
    wizard = null;
    nextDropped();
  }
  onMount(() => {
    dropTarget.handler = (paths) => {
      dropQueue.push(...paths);
      nextDropped();
    };
    return () => (dropTarget.handler = null);
  });

  const modProblems = (m) => problems.filter((p) => p.mod === m && p.step == null);
  const stepProblems = (m, s) => problems.filter((p) => p.mod === m && p.step === s);
  const modProblemCount = (m) => problems.filter((p) => p.mod === m).length;
  let packProblems = $derived(problems.filter((p) => p.mod == null));

  function addEmptyMod() {
    const mod = { _k: key(), id: '', name: 'Новый мод', steps: [] };
    draft.mods.push(mod);
    expanded[mod._k] = true;
  }

  function onWizardAdd(result) {
    if (wizard.modIndex != null) {
      draft.mods[wizard.modIndex].steps.push(...result.steps);
    } else {
      const mod = { _k: key(), id: modId(result.name), name: result.name, version: result.version, steps: result.steps };
      draft.mods.push(mod);
      expanded[mod._k] = true;
    }
    closeWizard();
  }

  function modId(name) {
    const base = name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'mod';
    let id = base;
    for (let n = 2; draft.mods.some((m) => m.id === id); n++) id = `${base}-${n}`;
    return id;
  }

  async function removeMod(i) {
    const m = draft.mods[i];
    if (m.steps.length && !(await confirm('Удалить мод?', `«${m.name}» и его шаги (${m.steps.length}) пропадут из сборки.`, 'Удалить', true))) return;
    draft.mods.splice(i, 1);
  }

  function move(list, from, to) {
    if (to < 0 || to >= list.length || from === to) return;
    const [item] = list.splice(from, 1);
    list.splice(to, 0, item);
  }

  function addStep(m, action) {
    draft.mods[m].steps.push(newStep(action));
  }

  async function setCover() {
    try {
      const r = await call('packs.setCover', { id });
      if (r) {
        draft.cover = r.cover;
        coverUrl = r.url;
      }
    } catch (e) {
      toast(e.message, 'error');
    }
  }

  async function deleteFile(file) {
    await call('packs.deleteFile', { id, path: file }).catch((e) => toast(e.message, 'error'));
    problems = await call('packs.check', { id, pack: JSON.parse(json) }).catch(() => problems);
  }

  async function save() {
    if (!dirty) return true;
    saving = true;
    try {
      if (view?.installed?.version === savedVersion && draft.version === savedVersion) {
        const next = bumpVersion(draft.version);
        if (await confirm('Поднять версию?',
          `Если поднять версию до ${next}, у тех, кто уже поставил сборку, появится «Есть обновление».`,
          `Поднять до ${next}`, false, `Оставить ${draft.version}`)) draft.version = next;
      }
      const pack = strip($state.snapshot(draft));
      await call('packs.save', { id, pack });
      saved = JSON.stringify(pack);
      savedVersion = pack.version;
      app.editorDirty = false;
      toast('Сборка сохранена', 'ok');
      await refresh();
      return true;
    } catch (e) {
      toast(e.message, 'error');
      return false;
    } finally {
      saving = false;
    }
  }

  async function testInstall() {
    if (!(await save())) return;
    if (errors && !(await confirm('В сборке есть ошибки', `Найдено ошибок: ${errors}. Установка, скорее всего, остановится на них. Всё равно попробовать?`, 'Попробовать'))) return;
    navigate('packs', id);
    const v = app.packs.find((p) => p.pack.id === id);
    if (v) installPack(v);
  }

  function changeGame(e) {
    const g = app.games.find((x) => x.appId === Number(e.currentTarget.value));
    if (g) draft.game = { steamAppId: g.appId, name: g.name };
  }
</script>

<div class="page">
  <div class="topbar">
    <button class="btn ghost" onclick={() => navigate('packs', id)}><Icon name="back" size={16} /> Готово</button>
    <div class="tb-title">
      <span class="muted">Редактор</span>
      <b>{draft.name || 'Без названия'}</b>
      {#if dirty}<span class="dot" title="Есть несохранённые изменения"></span>{/if}
    </div>
    <div class="tb-status">
      {#if errors}<span class="st err"><Icon name="alert" size={14} /> {errors}</span>{/if}
      {#if warnings}<span class="st warn"><Icon name="alert" size={14} /> {warnings}</span>{/if}
      {#if !problems.length && draft.mods.length}<span class="st ok"><Icon name="check" size={14} /> Ошибок нет</span>{/if}
    </div>
    <button class="btn" onclick={testInstall} disabled={saving || !game}><Icon name="play" size={14} /> Установить и проверить</button>
    <button class="btn primary" onclick={save} disabled={!dirty || saving}><Icon name="save" size={16} /> Сохранить</button>
  </div>

  <div class="layout">
    <aside class="info">
      <button class="cover" onclick={setCover} title="Сменить обложку">
        <Cover urls={[coverUrl, game?.images.hero, steamCdn(draft.game.steamAppId, 'library_hero.jpg')]} label={draft.name} />
        <span class="cover-hint"><Icon name="image" size={15} /> {draft.cover ? 'Сменить обложку' : 'Своя обложка'}</span>
      </button>

      <label class="field"><span class="lbl">Название</span><input bind:value={draft.name} /></label>
      <label class="field">
        <span class="lbl">Игра</span>
        <select value={draft.game.steamAppId} onchange={changeGame}>
          {#if !game}<option value={draft.game.steamAppId}>{draft.game.name ?? draft.game.steamAppId} (не найдена)</option>{/if}
          {#each app.games as g}<option value={g.appId}>{g.name}</option>{/each}
        </select>
      </label>
      <div class="two">
        <label class="field"><span class="lbl">Версия</span><input bind:value={draft.version} /></label>
        <label class="field"><span class="lbl">Автор</span><input bind:value={draft.author} /></label>
      </div>
      <label class="field"><span class="lbl">Описание</span><textarea rows="4" bind:value={draft.description} placeholder="Что даёт сборка, как играть…"></textarea></label>
      <div class="field">
        <span class="lbl">Папка сохранений</span>
        <PathField bind:value={draft.saves} kind="path-write" packId={id} appId={draft.game.steamAppId}
          placeholder={view?.game.savesDefault ?? 'не указана — бэкапов не будет'} />
        <span class="hint">
          {#if draft.saves}Её ModCrew копирует перед установкой и обновлением сборки.
          {:else if view?.game.savesDefault}Для этой игры ModCrew знает её сам — можно не заполнять.
          {:else}Укажи, где игра хранит сохранения, — ModCrew будет делать их копии перед установкой.{/if}
        </span>
      </div>
      <div class="pid">id: <span>{id}</span></div>

      {#if packProblems.length}
        <div class="pp">
          {#each packProblems as p}
            <div class="pp-row {p.level}">
              <Icon name="alert" size={14} />
              <span>{p.text}</span>
              {#if p.file}<button class="btn ghost mini" onclick={() => deleteFile(p.file)}>Удалить</button>{/if}
            </div>
          {/each}
        </div>
      {/if}
    </aside>

    <section class="mods">
      <div class="mods-head">
        <h2>Моды <span>{draft.mods.length}</span></h2>
        <div class="add">
          <button class="btn primary" onclick={() => (wizard = { mode: 'file' })}><Icon name="file" size={15} /> Мод из файла</button>
          <button class="btn" onclick={() => (wizard = { mode: 'url' })}><Icon name="link" size={15} /> По ссылке</button>
          <button class="btn ghost" onclick={addEmptyMod}><Icon name="plus" size={15} /> Пустой</button>
        </div>
      </div>

      {#each draft.mods as mod, m (mod._k)}
        <div class="mod" class:open={expanded[mod._k]} class:over={drag?.over === m && drag?.from !== m}
          ondragover={(e) => { if (drag) { e.preventDefault(); drag.over = m; } }}
          ondrop={(e) => { e.preventDefault(); move(draft.mods, drag.from, m); drag = null; }}
          role="listitem">
          <div class="mod-head">
            <span class="grip" draggable="true" role="button" tabindex="-1" title="Перетащи, чтобы поменять порядок"
              ondragstart={(e) => { drag = { from: m, over: m }; e.dataTransfer.effectAllowed = 'move'; }}
              ondragend={() => (drag = null)}><Icon name="grip" size={16} /></span>
            <button class="mh-main" onclick={() => (expanded[mod._k] = !expanded[mod._k])}>
              <span class="mnum">{m + 1}</span>
              <span class="mname">{mod.name || 'Без названия'}</span>
              {#if mod.version}<span class="ver">{mod.version}</span>{/if}
              <span class="muted small">шагов: {mod.steps.length}</span>
              {#if modProblemCount(m)}
                <span class="pcount" class:err={problems.some((p) => p.mod === m && p.level === 'error')}><Icon name="alert" size={13} /> {modProblemCount(m)}</span>
              {/if}
            </button>
            <button class="t" onclick={() => removeMod(m)} title="Удалить мод"><Icon name="trash" size={15} /></button>
            <button class="t chev" onclick={() => (expanded[mod._k] = !expanded[mod._k])}><Icon name="chevron" size={16} /></button>
          </div>

          {#if expanded[mod._k]}
            <div class="mod-body">
              <div class="grid3">
                <label class="field"><span class="lbl">Название</span><input bind:value={mod.name} /></label>
                <label class="field"><span class="lbl">Версия</span><input bind:value={mod.version} /></label>
                <label class="field"><span class="lbl">Страница мода</span><input bind:value={mod.url} placeholder="https://www.nexusmods.com/…" /></label>
              </div>
              <label class="field"><span class="lbl">Описание</span><input bind:value={mod.description} placeholder="Коротко, что делает мод" /></label>
              {#each modProblems(m) as p}<div class="pp-row {p.level}"><Icon name="alert" size={14} /> {p.text}</div>{/each}

              <div class="steps">
                {#each mod.steps as step, s (step._k)}
                  <StepCard bind:step={mod.steps[s]} index={s} count={mod.steps.length} packId={id} appId={draft.game.steamAppId}
                    problems={stepProblems(m, s)} onmove={(d) => move(mod.steps, s, s + d)} onremove={() => mod.steps.splice(s, 1)}
                    getOutputs={() => call('packs.stepOutputs', { id, pack: JSON.parse(json), mod: m, step: s })} />
                {/each}
              </div>

              <div class="add-step">
                <span class="muted small">Добавить шаг:</span>
                <button class="chip file" onclick={() => (wizard = { mode: 'file', modIndex: m })}><Icon name="file" size={13} /> Файл…</button>
                {#each actionOrder as a}
                  <button class="chip" style:--c={actions[a].color} onclick={() => addStep(m, a)}><Icon name={actions[a].icon} size={13} /> {actions[a].label}</button>
                {/each}
              </div>
            </div>
          {/if}
        </div>
      {:else}
        <div class="empty">
          <Icon name="puzzle" size={30} />
          <h3>Добавь первый мод</h3>
          <p>Выбери файл мода (.zip или .dll) — ModCrew заглянет внутрь, подскажет, куда его положить, и сам составит шаги.</p>
          <button class="btn primary big" onclick={() => (wizard = { mode: 'file' })}><Icon name="file" /> Мод из файла</button>
        </div>
      {/each}
    </section>
  </div>
</div>

{#if wizard}
  {#key wizard}
    <AddModDialog mode={wizard.mode} path={wizard.path} packId={id} appId={draft.game.steamAppId} append={wizard.modIndex != null}
      onclose={closeWizard} onadd={onWizardAdd} />
  {/key}
{/if}

<style>
  .page { min-height: 100%; }
  .topbar {
    position: sticky; top: 0; z-index: 10; display: flex; align-items: center; gap: 10px;
    padding: 12px 28px; background: color-mix(in srgb, var(--bg) 85%, transparent); backdrop-filter: blur(14px);
    border-bottom: 1px solid var(--line);
  }
  .tb-title { display: flex; align-items: center; gap: 8px; margin-right: auto; min-width: 0; }
  .tb-title b { font-weight: 650; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .dot { width: 8px; height: 8px; border-radius: 50%; background: var(--warn); box-shadow: 0 0 10px rgba(251, 191, 36, 0.6); flex: none; }
  .tb-status { display: flex; gap: 6px; }
  .st { display: inline-flex; align-items: center; gap: 4px; font-size: 12.5px; font-weight: 600; padding: 4px 9px; border-radius: 99px; }
  .st.err { color: var(--danger); background: rgba(248, 113, 113, 0.1); }
  .st.warn { color: var(--warn); background: rgba(251, 191, 36, 0.1); }
  .st.ok { color: var(--ok); background: rgba(52, 211, 153, 0.1); }

  .mods { min-width: 0; }
  .layout { display: grid; grid-template-columns: 320px minmax(0, 1fr); gap: 28px; padding: 24px 28px 60px; align-items: start; }

  .info { position: sticky; top: 86px; display: flex; flex-direction: column; gap: 14px; }
  .cover { position: relative; aspect-ratio: 16 / 9; border-radius: var(--radius); overflow: hidden; border: 1px solid var(--line-2); padding: 0; cursor: pointer; background: none; }
  .cover-hint {
    position: absolute; left: 10px; bottom: 10px; display: inline-flex; align-items: center; gap: 6px;
    padding: 5px 10px; border-radius: 8px; background: rgba(10, 11, 16, 0.75); backdrop-filter: blur(8px);
    font-size: 12px; font-weight: 600; opacity: 0.85; transition: opacity 0.15s;
  }
  .cover:hover .cover-hint { opacity: 1; }

  .field { display: flex; flex-direction: column; gap: 5px; min-width: 0; }
  .lbl { font-size: 12px; color: var(--muted); font-weight: 600; }
  .two { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
  input, textarea, select {
    height: 38px; padding: 0 11px; border-radius: 10px; border: 1px solid var(--line-2); background: var(--bg-2);
    color: var(--text); font: inherit; font-size: 13.5px; outline: none; transition: border-color 0.15s, box-shadow 0.15s; min-width: 0;
  }
  textarea { height: auto; padding: 9px 11px; resize: vertical; line-height: 1.5; }
  select { cursor: pointer; }
  input:focus, textarea:focus, select:focus { border-color: color-mix(in srgb, var(--accent) 60%, transparent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent) 15%, transparent); }
  .hint { font-size: 12px; color: var(--faint); line-height: 1.45; }
  .pid { font-size: 12px; color: var(--faint); }
  .pid span { font-family: 'Cascadia Mono', Consolas, monospace; user-select: text; }

  .pp { display: flex; flex-direction: column; gap: 6px; }
  .pp-row { display: flex; align-items: center; gap: 8px; font-size: 12.5px; padding: 8px 10px; border-radius: 9px; }
  .pp-row span { flex: 1; overflow-wrap: anywhere; }
  .pp-row.error { color: var(--danger); background: rgba(248, 113, 113, 0.08); }
  .pp-row.warning { color: var(--warn); background: rgba(251, 191, 36, 0.07); }
  .pp-row :global(svg) { flex: none; }
  .mini { height: 26px; padding: 0 8px; font-size: 12px; }

  .mods-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 14px; flex-wrap: wrap; }
  h2 { font-size: 20px; display: flex; align-items: center; gap: 8px; }
  h2 span { font-size: 12px; color: var(--muted); background: rgb(var(--fg-rgb) / 0.07); padding: 1px 8px; border-radius: 99px; }
  .add { display: flex; gap: 8px; }

  .mod {
    background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius-lg); margin-bottom: 10px;
    transition: border-color 0.15s, box-shadow 0.15s; animation: fade-up 0.25s var(--ease);
  }
  .mod.open { border-color: var(--line-2); }
  .mod.over { border-color: var(--accent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent) 20%, transparent); }
  .mod-head { display: flex; align-items: center; gap: 4px; padding: 8px 8px 8px 6px; }
  .grip { width: 28px; height: 36px; display: grid; place-items: center; color: var(--faint); cursor: grab; border-radius: 8px; }
  .grip:hover { color: var(--text); background: rgb(var(--fg-rgb) / 0.04); }
  .mh-main { flex: 1; min-width: 0; display: flex; align-items: center; gap: 10px; border: 0; background: none; cursor: pointer; text-align: left; padding: 6px 4px; }
  .mnum { width: 26px; height: 26px; border-radius: 8px; flex: none; display: grid; place-items: center; background: rgb(var(--fg-rgb) / 0.05); color: var(--muted); font-weight: 700; font-size: 12px; }
  .mname { font-weight: 650; font-size: 15px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .ver { font-size: 11.5px; color: var(--faint); font-weight: 600; background: rgb(var(--fg-rgb) / 0.05); padding: 1px 7px; border-radius: 6px; flex: none; }
  .pcount { display: inline-flex; align-items: center; gap: 3px; font-size: 12px; font-weight: 600; color: var(--warn); }
  .pcount.err { color: var(--danger); }
  .t { width: 32px; height: 32px; border: 0; border-radius: 8px; background: none; color: var(--muted); cursor: pointer; display: grid; place-items: center; }
  .t:hover { background: rgb(var(--fg-rgb) / 0.06); color: var(--text); }
  .chev :global(svg) { transition: transform 0.2s var(--ease); }
  .open .chev :global(svg) { transform: rotate(180deg); }

  .mod-body { padding: 4px 18px 18px; display: flex; flex-direction: column; gap: 12px; }
  .grid3 { display: grid; grid-template-columns: 1.4fr 0.6fr 1.6fr; gap: 10px; }
  .steps { display: flex; flex-direction: column; gap: 8px; margin-top: 4px; }
  .add-step { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; padding-top: 4px; }
  .chip {
    --c: var(--accent-text);
    display: inline-flex; align-items: center; gap: 5px; height: 28px; padding: 0 10px; border-radius: 8px;
    border: 1px dashed color-mix(in srgb, var(--c) 45%, transparent); background: transparent;
    color: var(--c); font-size: 12px; font-weight: 600; cursor: pointer; transition: background 0.15s;
  }
  .chip:hover { background: color-mix(in srgb, var(--c) 12%, transparent); }
  .chip.file { border-style: solid; }

  .empty {
    display: flex; flex-direction: column; align-items: center; text-align: center; padding: 50px 30px;
    border: 1px dashed var(--line-2); border-radius: var(--radius-lg); color: var(--accent-text);
  }
  .empty h3 { color: var(--text); margin: 12px 0 6px; font-size: 17px; }
  .empty p { color: var(--muted); max-width: 420px; margin: 0 0 20px; line-height: 1.6; }
  .muted { color: var(--muted); }
  .small { font-size: 12px; }

  @media (max-width: 1150px) {
    .layout { grid-template-columns: minmax(0, 1fr); }
    .info { position: static; }
    .add { flex-wrap: wrap; }
  }
</style>

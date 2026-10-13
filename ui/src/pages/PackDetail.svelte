<script>
  import { app, refresh, run, confirm, installPack, statusOf, statusLabel, steamCdn, navigate } from '../lib/state.svelte.js';
  import { call } from '../lib/api.js';
  import Icon from '../lib/Icon.svelte';
  import Cover from '../lib/Cover.svelte';

  let view = $derived(app.packs.find((p) => p.pack.id === app.packId));
  let status = $derived(view ? statusOf(view) : 'available');
  let inst = $derived(app.install?.packId === view?.pack.id ? app.install : null);
  let percent = $derived(
    inst && inst.total ? Math.min(100, ((inst.step - 1 + (inst.fraction ?? (inst.done ? 1 : 0.5))) / inst.total) * 100) : inst?.done ? 100 : 3,
  );

  const primary = {
    available: { label: 'Установить', icon: 'download' },
    update: { label: 'Обновить', icon: 'refresh' },
    installed: { label: 'Переустановить', icon: 'refresh' },
    broken: { label: 'Установить заново', icon: 'refresh' },
    nogame: { label: 'Установить', icon: 'download' },
    installing: { label: 'Устанавливается…', icon: 'download' },
  };

  // Programs the pack runs as installers: they install themselves, outside our rollback.
  let installers = $derived(
    view ? [...new Set(view.pack.mods.flatMap((m) => m.steps.filter((s) => s.action === 'run' && s.path).map((s) => s.path.split(/[\\/]/).pop())))] : [],
  );

  async function uninstall() {
    const extra = installers.length
      ? `\n\nПрограммы, которые ставили установщики (${installers.join(', ')}), так не удалятся — их убирают через «Параметры → Приложения».`
      : '';
    if (!(await confirm('Удалить моды из игры?', `Игра вернётся в состояние до установки «${view.pack.name}». Сама сборка останется в списке.${extra}`, 'Удалить из игры', true))) return;
    await run('install.uninstall', { id: view.pack.id }, 'Готово — игра как до установки.');
    if (app.install?.packId === view.pack.id) app.install = null;
    await refresh();
  }

  async function removePack() {
    if (!(await confirm('Убрать сборку из списка?', 'Файлы сборки удалятся из ModCrew. Добавить её снова можно из файла .modcrew.', 'Убрать', true))) return;
    await run('packs.remove', { id: view.pack.id });
    app.packId = null;
    await refresh();
  }

  const fmt = (iso) => new Date(iso).toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });
</script>

{#if view}
  <div class="page">
    <section class="hero">
      <div class="bg">
        <Cover urls={[view.coverUrl, view.game.images.hero, steamCdn(view.game.appId, 'library_hero.jpg'), view.game.images.header]} label={view.pack.name} />
      </div>
      <div class="hero-shade"></div>

      <button class="btn ghost back" onclick={() => (app.packId = null)}><Icon name="back" size={16} /> Сборки</button>

      <div class="hero-content">
        {#if view.game.images.logo}
          <img class="logo" src={view.game.images.logo} alt={view.game.name} onerror={(e) => e.currentTarget.remove()} draggable="false" />
        {:else}
          <div class="game-name">{view.game.name}</div>
        {/if}
        <h1>{view.pack.name}</h1>
        <div class="meta">
          <span class="pill {status}">{statusLabel[status]}</span>
          <span>Версия {view.pack.version}</span>
          {#if view.installed && view.installed.version !== view.pack.version}
            <span class="dim">у тебя {view.installed.version}</span>
          {/if}
          {#if view.pack.author}<span><Icon name="user" size={14} /> {view.pack.author}</span>{/if}
          {#if view.installed}<span class="dim">установлена {fmt(view.installed.at)}</span>{/if}
        </div>

        <div class="actions">
          <button class="btn primary big" disabled={status === 'installing' || status === 'nogame' || !!(app.install && !app.install.done && !app.install.error)} onclick={() => installPack(view)}>
            <Icon name={primary[status].icon} /> {primary[status].label}
          </button>
          {#if view.installed}
            <button class="btn big danger" disabled={status === 'installing'} onclick={uninstall}><Icon name="undo" /> Удалить из игры</button>
          {/if}
        </div>
        {#if installers.length && (status === 'available' || status === 'update')}
          <p class="info-note"><Icon name="play" size={15} /> Во время установки откроется установщик {installers.join(', ')} — пройди его до конца, ModCrew подождёт.</p>
        {/if}
        {#if status === 'nogame'}
          <p class="warn"><Icon name="alert" size={16} /> Игра «{view.game.name}» не найдена. Установи её в Steam или укажи папку на вкладке «Мои игры».</p>
        {/if}
      </div>
    </section>

    {#if inst}
      <section class="progress" class:done={inst.done} class:failed={inst.error}>
        <div class="p-head">
          <div class="p-icon">
            {#if inst.done}<Icon name="check" size={20} />{:else if inst.error}<Icon name="alert" size={20} />{:else}<div class="spinner"></div>{/if}
          </div>
          <div class="p-text">
            <div class="p-title">
              {#if inst.done}Готово! Запускай игру{:else if inst.error}Не получилось{:else}{inst.mod || 'Подготовка'}{/if}
            </div>
            <div class="p-sub">{inst.error ?? (inst.done ? `Установлено модов: ${view.pack.mods.length}` : inst.text)}</div>
          </div>
          {#if inst.total && !inst.done && !inst.error}<div class="p-count">{inst.step} / {inst.total}</div>{/if}
          {#if inst.done || inst.error}
            <button class="btn ghost icon" onclick={() => (app.install = null)} title="Скрыть"><Icon name="x" size={16} /></button>
          {/if}
        </div>
        <div class="bar"><div style:width="{inst.error ? 100 : percent}%"></div></div>
        {#if inst.log.length}
          <details>
            <summary>Подробности</summary>
            <ol>{#each inst.log as line}<li>{line}</li>{/each}</ol>
          </details>
        {/if}
      </section>
    {/if}

    <div class="columns">
      <section class="mods">
        <h2>Что внутри <span>{view.pack.mods.length}</span></h2>
        {#each view.pack.mods as mod, i}
          <div class="mod" style:animation-delay="{i * 40}ms">
            <div class="num">{i + 1}</div>
            <div class="mod-body">
              <div class="mod-title">
                {mod.name}
                {#if mod.version}<span class="ver">{mod.version}</span>{/if}
              </div>
              {#if mod.description}<div class="mod-desc">{mod.description}</div>{/if}
            </div>
            {#if mod.url}
              <button class="btn ghost icon" title="Страница мода" onclick={() => call('shell.openUrl', { url: mod.url })}><Icon name="external" size={16} /></button>
            {/if}
          </div>
        {/each}
      </section>

      <aside class="side">
        {#if view.pack.description}
          <div class="box">
            <h3>Описание</h3>
            <p>{view.pack.description}</p>
          </div>
        {/if}
        <div class="box">
          <h3>Поделиться</h3>
          <p>Сохрани сборку в файл и отправь друзьям — им останется нажать «Установить».</p>
          <div class="box-actions">
            <button class="btn" onclick={() => run('packs.export', { id: view.pack.id })}><Icon name="upload" size={16} /> Сохранить .modcrew</button>
            <button class="btn ghost" onclick={() => navigate('packs', null, view.pack.id)}><Icon name="edit" size={16} /> Редактировать</button>
          </div>
        </div>
        <div class="box links">
          <button class="btn ghost" onclick={() => run('games.open', { appId: view.game.appId })}><Icon name="folder" size={16} /> Папка игры</button>
          <button class="btn ghost" onclick={() => run('packs.openFolder', { id: view.pack.id })}><Icon name="packs" size={16} /> Файлы сборки</button>
          <button class="btn ghost danger" disabled={!!view.installed} title={view.installed ? 'Сначала удали из игры' : ''} onclick={removePack}><Icon name="trash" size={16} /> Убрать из списка</button>
        </div>
      </aside>
    </div>
  </div>
{/if}

<style>
  .page { padding-bottom: 48px; }

  .hero { position: relative; min-height: 380px; display: flex; align-items: flex-end; overflow: hidden; }
  .bg { position: absolute; inset: 0; }
  .bg :global(img) { animation: drift 30s ease-in-out infinite alternate; }
  @keyframes drift { from { transform: scale(1.02); } to { transform: scale(1.1) translateX(-1.5%); } }
  .hero-shade {
    position: absolute; inset: 0;
    background:
      linear-gradient(90deg, rgba(12, 13, 18, 0.92) 0%, rgba(12, 13, 18, 0.55) 55%, rgba(12, 13, 18, 0.2) 100%),
      linear-gradient(0deg, var(--bg) 0%, rgba(12, 13, 18, 0) 60%);
  }
  .back { position: absolute; top: 20px; left: 28px; z-index: 2; color: var(--text); background: rgba(12, 13, 18, 0.4); backdrop-filter: blur(10px); }
  .hero-content { position: relative; z-index: 1; padding: 80px 40px 30px; max-width: 900px; }
  .logo { max-height: 70px; max-width: 280px; display: block; margin-bottom: 14px; filter: drop-shadow(0 4px 16px rgba(0, 0, 0, 0.6)); }
  .game-name { font-size: 13px; color: var(--muted); font-weight: 600; text-transform: uppercase; letter-spacing: 0.08em; margin-bottom: 8px; }
  h1 { font-size: 40px; font-weight: 750; text-shadow: 0 2px 20px rgba(0, 0, 0, 0.5); }
  .meta { display: flex; align-items: center; gap: 16px; margin: 12px 0 24px; color: var(--muted); font-size: 13px; flex-wrap: wrap; }
  .meta span:not(.pill) { display: inline-flex; align-items: center; gap: 5px; }
  .dim { color: var(--faint); }
  .actions { display: flex; gap: 10px; }
  .info-note { display: flex; align-items: center; gap: 8px; color: #fcd9a8; margin: 16px 0 0; font-size: 13.5px; }
  .info-note :global(svg) { color: #fb923c; flex: none; }
  .warn { display: flex; align-items: center; gap: 8px; color: var(--warn); margin: 16px 0 0; }

  .progress {
    margin: 4px 40px 0; padding: 18px 20px; border-radius: var(--radius-lg);
    background: var(--panel); border: 1px solid var(--line-2); animation: fade-up 0.3s var(--ease);
  }
  .progress.done { border-color: rgba(52, 211, 153, 0.35); }
  .progress.failed { border-color: rgba(248, 113, 113, 0.35); }
  .p-head { display: flex; align-items: center; gap: 14px; }
  .p-icon { width: 40px; height: 40px; border-radius: 12px; display: grid; place-items: center; background: var(--accent-soft); color: #a993ff; flex: none; }
  .done .p-icon { background: rgba(52, 211, 153, 0.14); color: var(--ok); }
  .failed .p-icon { background: rgba(248, 113, 113, 0.14); color: var(--danger); }
  .p-text { flex: 1; min-width: 0; }
  .p-title { font-weight: 650; }
  .p-sub { color: var(--muted); font-size: 13px; white-space: pre-line; overflow-wrap: anywhere; }
  .p-count { color: var(--faint); font-variant-numeric: tabular-nums; font-weight: 600; }
  .bar { height: 6px; border-radius: 6px; background: rgba(255, 255, 255, 0.07); margin-top: 14px; overflow: hidden; }
  .bar div { height: 100%; border-radius: 6px; background: var(--grad); transition: width 0.35s var(--ease); position: relative; overflow: hidden; }
  .bar div::after {
    content: ''; position: absolute; inset: 0;
    background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.35), transparent);
    animation: shine 1.4s infinite;
  }
  .done .bar div { background: var(--ok); }
  .done .bar div::after, .failed .bar div::after { display: none; }
  .failed .bar div { background: var(--danger); opacity: 0.6; }
  @keyframes shine { from { transform: translateX(-100%); } to { transform: translateX(100%); } }
  .spinner { width: 18px; height: 18px; border-radius: 50%; border: 2.5px solid rgba(169, 147, 255, 0.25); border-top-color: #a993ff; animation: spin 0.8s linear infinite; }
  @keyframes spin { to { transform: rotate(360deg); } }
  details { margin-top: 12px; color: var(--muted); font-size: 12.5px; }
  summary { cursor: pointer; color: var(--faint); }
  ol { margin: 8px 0 0; padding-left: 22px; max-height: 160px; overflow: auto; user-select: text; }

  .columns { display: grid; grid-template-columns: 1fr 320px; gap: 28px; padding: 28px 40px 0; align-items: start; }
  h2 { font-size: 18px; margin-bottom: 14px; display: flex; align-items: center; gap: 8px; }
  h2 span { font-size: 12px; color: var(--muted); background: rgba(255, 255, 255, 0.07); padding: 1px 8px; border-radius: 99px; }

  .mod {
    display: flex; align-items: center; gap: 14px; padding: 14px 16px; margin-bottom: 8px;
    background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius);
    animation: fade-up 0.35s var(--ease) both;
  }
  .num {
    width: 30px; height: 30px; border-radius: 9px; flex: none; display: grid; place-items: center;
    background: rgba(255, 255, 255, 0.05); color: var(--muted); font-weight: 700; font-size: 13px;
  }
  .mod-body { flex: 1; min-width: 0; }
  .mod-title { font-weight: 600; display: flex; align-items: center; gap: 8px; }
  .ver { font-size: 11.5px; color: var(--faint); font-weight: 600; background: rgba(255, 255, 255, 0.05); padding: 0 7px; border-radius: 6px; }
  .mod-desc { color: var(--muted); font-size: 13px; margin-top: 2px; }

  .side { display: flex; flex-direction: column; gap: 14px; }
  .box { background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius); padding: 18px; }
  .box h3 { font-size: 14px; margin-bottom: 6px; }
  .box p { color: var(--muted); font-size: 13px; margin: 0 0 14px; line-height: 1.55; }
  .box p:last-child { margin: 0; }
  .box-actions { display: flex; gap: 6px; flex-wrap: wrap; }
  .links { display: flex; flex-direction: column; gap: 2px; padding: 8px; }
  .links .btn { justify-content: flex-start; }

  @media (max-width: 1100px) {
    .columns { grid-template-columns: 1fr; }
  }
</style>

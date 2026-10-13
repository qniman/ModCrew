<script>
  import { onMount } from 'svelte';
  import { call } from '../lib/api.js';
  import Icon from '../lib/Icon.svelte';

  let info = $state(null);
  let desktop = $state(true);
  let launch = $state(true);
  let stage = $state('ready'); // ready | working | done | error
  let error = $state('');

  onMount(async () => {
    info = await call('setup.info');
    desktop = info.desktop;
  });

  async function install() {
    stage = 'working';
    try {
      await call('setup.install', { desktop });
      stage = 'done';
    } catch (e) {
      error = e.message;
      stage = 'error';
    }
  }
</script>

<div class="setup">
  <div class="glow"></div>
  <div class="box">
    <div class="logo"><Icon name="puzzle" size={34} /></div>
    <h1>ModCrew</h1>
    <p class="tag">Моды для своих — ставятся одной кнопкой</p>

    {#if stage === 'done'}
      <div class="done-ic"><Icon name="check" size={28} /></div>
      <h2>Готово!</h2>
      <p class="muted">ModCrew есть в меню «Пуск»{desktop ? ' и на рабочем столе' : ''}. Сборки от друзей (.modcrew) теперь открываются двойным кликом.</p>
      <label class="check"><input type="checkbox" bind:checked={launch} /> Запустить ModCrew</label>
      <button class="btn primary big" onclick={() => call('setup.finish', { launch })}>Готово</button>
    {:else if info}
      <p class="what">
        {#if info.installedVersion}
          Уже установлена версия {info.installedVersion}. Установить {info.version} поверх? Сборки и настройки сохранятся.
        {:else}
          Версия {info.version}. Права администратора не нужны.
        {/if}
      </p>
      <label class="check"><input type="checkbox" bind:checked={desktop} disabled={stage === 'working'} /> Ярлык на рабочем столе</label>
      {#if stage === 'error'}<div class="error">{error}</div>{/if}
      <button class="btn primary big" disabled={stage === 'working'} onclick={install}>
        {#if stage === 'working'}<span class="spinner"></span> Устанавливаю…{:else}<Icon name="download" /> {info.installedVersion ? 'Обновить' : 'Установить'}{/if}
      </button>
      <div class="dir">Папка: <span>{info.dir}</span></div>
    {/if}
  </div>
</div>

<style>
  .setup { height: 100%; display: grid; place-items: center; position: relative; overflow: hidden; padding: 24px; }
  .glow {
    position: absolute; width: 520px; height: 520px; border-radius: 50%; top: -260px; left: 50%; transform: translateX(-50%);
    background: radial-gradient(circle, color-mix(in srgb, var(--accent) 35%, transparent), transparent 70%);
    filter: blur(20px);
  }
  .box { position: relative; display: flex; flex-direction: column; align-items: center; text-align: center; gap: 12px; max-width: 460px; animation: fade-up 0.4s var(--ease); }
  .logo {
    width: 76px; height: 76px; border-radius: 22px; display: grid; place-items: center; color: white;
    background: var(--grad); box-shadow: 0 14px 40px color-mix(in srgb, var(--accent) 45%, transparent), inset 0 1px 0 rgba(255, 255, 255, 0.25);
  }
  h1 { font-size: 32px; font-weight: 750; margin-top: 4px; }
  h2 { font-size: 22px; }
  .tag { color: var(--muted); margin: -6px 0 12px; }
  .what { color: var(--text); margin: 0; line-height: 1.55; }
  .muted { color: var(--muted); margin: 0; line-height: 1.55; }
  .check { display: flex; align-items: center; gap: 8px; cursor: pointer; color: var(--muted); }
  .check input { accent-color: var(--accent); width: 16px; height: 16px; }
  .big { min-width: 220px; justify-content: center; margin-top: 6px; }
  .dir { font-size: 12px; color: var(--faint); }
  .dir span { font-family: 'Cascadia Mono', Consolas, monospace; user-select: text; }
  .error { color: var(--danger); background: color-mix(in srgb, var(--danger) 10%, transparent); padding: 10px 14px; border-radius: 10px; font-size: 13px; }
  .done-ic { width: 56px; height: 56px; border-radius: 50%; display: grid; place-items: center; background: color-mix(in srgb, var(--ok) 15%, transparent); color: var(--ok); margin-top: 8px; }
  .spinner { width: 16px; height: 16px; border-radius: 50%; border: 2.5px solid rgba(255, 255, 255, 0.35); border-top-color: white; animation: spin 0.8s linear infinite; }
  @keyframes spin { to { transform: rotate(360deg); } }
</style>

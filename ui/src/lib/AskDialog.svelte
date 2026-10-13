<script>
  import { app, answer } from './state.svelte.js';
  import Icon from './Icon.svelte';

  function key(e) {
    if (!app.ask) return;
    if (e.key === 'Escape') answer(false);
    if (e.key === 'Enter') answer(true);
  }
</script>

<svelte:window onkeydown={key} />

{#if app.ask}
  <div class="backdrop" onclick={(e) => e.target === e.currentTarget && answer(false)} role="presentation">
    <div class="dialog" role="dialog" aria-modal="true" tabindex="-1">
      <div class="icon" class:danger={app.ask.danger}><Icon name={app.ask.danger ? 'trash' : 'alert'} size={22} /></div>
      <h3>{app.ask.title}</h3>
      <p>{app.ask.text}</p>
      <div class="actions">
        <button class="btn ghost" onclick={() => answer(false)}>{app.ask.cancelLabel ?? 'Отмена'}</button>
        <button class="btn primary" class:danger-btn={app.ask.danger} onclick={() => answer(true)}>{app.ask.okLabel ?? 'OK'}</button>
      </div>
    </div>
  </div>
{/if}

<style>
  .backdrop {
    position: fixed; inset: 0; z-index: 50; display: grid; place-items: center;
    background: rgba(5, 6, 10, 0.6); backdrop-filter: blur(6px);
    animation: fade 0.15s;
  }
  .dialog {
    width: min(440px, calc(100vw - 32px)); padding: 26px;
    background: var(--panel); border: 1px solid var(--line-2); border-radius: var(--radius-lg);
    box-shadow: 0 30px 80px rgba(0, 0, 0, 0.6);
    animation: pop 0.2s var(--ease);
  }
  .icon {
    width: 44px; height: 44px; border-radius: 12px; display: grid; place-items: center; margin-bottom: 16px;
    background: rgba(251, 191, 36, 0.12); color: var(--warn);
  }
  .icon.danger { background: rgba(248, 113, 113, 0.12); color: var(--danger); }
  h3 { font-size: 18px; margin-bottom: 8px; }
  p { color: var(--muted); margin: 0 0 24px; white-space: pre-line; }
  .actions { display: flex; justify-content: flex-end; gap: 8px; }
  .danger-btn { background: #e5484d; box-shadow: 0 6px 20px rgba(229, 72, 77, 0.35); }
  @keyframes fade { from { opacity: 0; } }
  @keyframes pop { from { opacity: 0; transform: scale(0.96) translateY(6px); } }
</style>

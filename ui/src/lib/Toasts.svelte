<script>
  import { app } from './state.svelte.js';
  import Icon from './Icon.svelte';
  import { fly } from 'svelte/transition';
</script>

<div class="toasts">
  {#each app.toasts as t (t.id)}
    <div class="toast {t.kind}" transition:fly={{ y: 16, duration: 220 }}>
      <Icon name={t.kind === 'error' ? 'alert' : t.kind === 'ok' ? 'check' : 'info'} size={16} />
      <span>{t.text}</span>
    </div>
  {/each}
</div>

<style>
  .toasts {
    position: fixed; right: 20px; bottom: 20px; z-index: 60;
    display: flex; flex-direction: column; gap: 8px; align-items: flex-end;
    pointer-events: none;
  }
  .toast {
    display: flex; align-items: flex-start; gap: 10px; max-width: 420px;
    padding: 12px 16px; border-radius: 12px;
    background: color-mix(in srgb, var(--panel-2) 92%, transparent); backdrop-filter: blur(12px);
    border: 1px solid var(--line-2); box-shadow: var(--shadow);
    white-space: pre-line;
  }
  .toast :global(svg) { flex: none; margin-top: 2px; }
  .ok :global(svg) { color: var(--ok); }
  .error { border-color: rgba(248, 113, 113, 0.35); }
  .error :global(svg) { color: var(--danger); }
  .info :global(svg) { color: var(--accent-2); }
</style>

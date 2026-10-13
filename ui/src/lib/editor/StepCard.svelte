<script>
  import { actions, describe } from './steps.js';
  import PathField from './PathField.svelte';
  import Icon from '../Icon.svelte';

  let { step = $bindable(), index, count, packId, appId, problems = [], onmove, onremove, getOutputs = null } = $props();

  let def = $derived(actions[step.action]);
  // svelte-ignore state_referenced_locally
  let open = $state(!!step._open); // steps added by hand start open
  let hasError = $derived(problems.some((p) => p.level === 'error'));
  // Open steps with errors so the author sees what to fix.
  $effect(() => {
    if (hasError) open = true;
  });
</script>

<div class="step" class:open class:error={hasError} style:--c={def?.color ?? '#888'}>
  <div class="head">
    <span class="num">{index + 1}</span>
    <button class="title" onclick={() => (open = !open)}>
      <span class="badge"><Icon name={def?.icon ?? 'file'} size={13} /> {def?.label ?? step.action}</span>
      <span class="desc">{step.title || describe(step)}</span>
      {#if problems.length}
        <span class="warn" class:err={hasError}><Icon name="alert" size={13} /> {problems.length}</span>
      {/if}
    </button>
    <div class="tools">
      <button class="t" disabled={index === 0} onclick={() => onmove(-1)} title="Выше"><Icon name="up" size={15} /></button>
      <button class="t" disabled={index === count - 1} onclick={() => onmove(1)} title="Ниже"><Icon name="down" size={15} /></button>
      <button class="t del" onclick={onremove} title="Удалить шаг"><Icon name="trash" size={14} /></button>
      <button class="t chev" onclick={() => (open = !open)} title={open ? 'Свернуть' : 'Развернуть'}><Icon name="chevron" size={16} /></button>
    </div>
  </div>

  {#if open && def}
    <div class="body">
      {#each def.fields as f}
        <label class="field" class:wide={f.kind === 'multiline' || f.kind.startsWith('path') || f.key === 'url'}>
          {#if f.kind === 'bool'}
            <span class="check">
              <input type="checkbox" checked={step[f.key] ?? f.default} onchange={(e) => (step[f.key] = e.currentTarget.checked)} />
              {f.label}
            </span>
          {:else}
            <span class="lbl">{f.label}{#if f.optional}<i> — необязательно</i>{/if}</span>
            {#if f.kind.startsWith('path')}
              <PathField bind:value={step[f.key]} kind={f.kind} {packId} {appId} placeholder={f.placeholder ?? ''}
                {getOutputs} programs={!!f.programs}
                invalid={!f.optional && !step[f.key] && hasError} />
            {:else if f.kind === 'multiline'}
              <textarea class="mono" rows="3" bind:value={step[f.key]} spellcheck="false"></textarea>
            {:else}
              <input class:mono={f.kind === 'mono'} bind:value={step[f.key]} placeholder={f.placeholder ?? ''} spellcheck="false"
                class:invalid={!f.optional && !step[f.key] && hasError} />
            {/if}
            {#if f.hint}<span class="hint">{f.hint}</span>{/if}
          {/if}
        </label>
      {/each}
      <label class="field wide">
        <span class="lbl">Подпись для друзей<i> — необязательно</i></span>
        <input bind:value={step.title} placeholder={describe(step)} />
      </label>
      {#if problems.length}
        <ul class="problems">
          {#each problems as p}<li class={p.level}>{p.text}</li>{/each}
        </ul>
      {/if}
    </div>
  {/if}
</div>

<style>
  .step {
    border: 1px solid var(--line); border-radius: 12px; background: var(--bg-2);
    position: relative; overflow: visible; transition: border-color 0.15s;
  }
  .step::before { content: ''; position: absolute; left: -1px; top: 10px; bottom: 10px; width: 3px; border-radius: 3px; background: var(--c); opacity: 0.8; }
  .step.error { border-color: rgba(248, 113, 113, 0.4); }
  .head { display: flex; align-items: center; gap: 8px; padding: 6px 6px 6px 14px; min-height: 46px; }
  .num { color: var(--faint); font-size: 12px; font-weight: 700; width: 16px; font-variant-numeric: tabular-nums; }
  .title { flex: 1; min-width: 0; display: flex; align-items: center; gap: 10px; border: 0; background: none; cursor: pointer; text-align: left; padding: 4px 0; }
  .badge {
    display: inline-flex; align-items: center; gap: 5px; flex: none;
    font-size: 11.5px; font-weight: 650; padding: 3px 8px; border-radius: 7px;
    color: var(--c); background: color-mix(in srgb, var(--c) 14%, transparent);
  }
  .desc { color: var(--muted); font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .warn { display: inline-flex; align-items: center; gap: 3px; color: var(--warn); font-size: 12px; font-weight: 600; flex: none; }
  .warn.err { color: var(--danger); }
  .tools { display: flex; gap: 2px; opacity: 0.55; transition: opacity 0.15s; }
  .step:hover .tools, .step.open .tools { opacity: 1; }
  .t { width: 30px; height: 30px; border: 0; border-radius: 8px; background: none; color: var(--muted); cursor: pointer; display: grid; place-items: center; }
  .t:hover:not(:disabled) { background: rgb(var(--fg-rgb) / 0.06); color: var(--text); }
  .t:disabled { opacity: 0.3; cursor: default; }
  .t.del:hover { color: var(--danger); }
  .chev :global(svg) { transition: transform 0.2s var(--ease); }
  .open .chev :global(svg) { transform: rotate(180deg); }

  .body { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 14px; padding: 4px 16px 16px 38px; animation: fade-up 0.2s var(--ease); }
  .field { display: flex; flex-direction: column; gap: 5px; min-width: 0; }
  .field.wide { grid-column: 1 / -1; }
  .lbl { font-size: 12px; color: var(--muted); font-weight: 600; }
  .lbl i { font-weight: 400; color: var(--faint); font-style: normal; }
  .hint { font-size: 11.5px; color: var(--faint); }
  input:not([type]), textarea {
    height: 36px; padding: 0 10px; border-radius: 9px; border: 1px solid var(--line-2); background: var(--bg);
    color: var(--text); font: inherit; font-size: 13px; outline: none; transition: border-color 0.15s, box-shadow 0.15s;
  }
  textarea { height: auto; padding: 8px 10px; resize: vertical; }
  input:focus, textarea:focus { border-color: color-mix(in srgb, var(--accent) 60%, transparent); box-shadow: 0 0 0 3px color-mix(in srgb, var(--accent) 15%, transparent); }
  input.invalid { border-color: rgba(248, 113, 113, 0.55); }
  .mono { font-family: 'Cascadia Mono', Consolas, monospace; font-size: 12.5px; }
  .check { display: flex; align-items: center; gap: 8px; font-size: 13px; cursor: pointer; padding-top: 4px; }
  .check input { accent-color: var(--accent); width: 16px; height: 16px; }
  .problems { grid-column: 1 / -1; margin: 0; padding-left: 18px; font-size: 12.5px; }
  .problems .error { color: var(--danger); }
  .problems .warning { color: var(--warn); }
</style>

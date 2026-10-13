<script>
  // Image that falls back through a list of URLs, then to a gradient with initials.
  let { urls = [], alt = '', label = '', fit = 'cover', position = 'center' } = $props();

  let list = $derived(urls.filter(Boolean));
  // Failures are remembered per URL list, so a new list starts from its first URL again.
  let key = $derived(list.join('|'));
  let failed = $state({ key: '', n: 0 });
  let index = $derived(failed.key === key ? failed.n : 0);
  let loadedSrc = $state(null);
  let loaded = $derived(loadedSrc === list[index]);

  let initials = $derived(
    label.split(/\s+/).filter((w) => /\p{L}|\d/u.test(w[0] ?? '')).slice(0, 2).map((w) => w[0].toUpperCase()).join(''),
  );
  let hue = $derived([...label].reduce((h, c) => (h * 31 + c.charCodeAt(0)) % 360, 210));
</script>

<div class="cover" style:--hue={hue}>
  {#if index < list.length}
    <img
      src={list[index]}
      {alt}
      class:loaded
      style:object-fit={fit}
      style:object-position={position}
      onload={(e) => (loadedSrc = e.currentTarget.getAttribute('src'))}
      onerror={() => (failed = { key, n: index + 1 })}
      draggable="false"
    />
  {/if}
  {#if index >= list.length}
    <div class="placeholder">{initials}</div>
  {/if}
</div>

<style>
  .cover {
    position: relative; width: 100%; height: 100%; overflow: hidden;
    background: linear-gradient(135deg, hsl(var(--hue) 45% 22%), hsl(calc(var(--hue) + 40) 50% 12%));
  }
  img { width: 100%; height: 100%; display: block; opacity: 0; transition: opacity 0.4s ease; }
  img.loaded { opacity: 1; }
  .placeholder {
    position: absolute; inset: 0; display: grid; place-items: center;
    font-size: 42px; font-weight: 800; letter-spacing: 0.02em; color: rgba(255, 255, 255, 0.85);
    text-shadow: 0 4px 20px rgba(0, 0, 0, 0.4);
  }
</style>

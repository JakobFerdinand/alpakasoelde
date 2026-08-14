<script lang="ts">
  import type { Snippet } from 'svelte';

  let {
    eyebrow,
    title,
    subtitle,
    class: className,
    children,
  }: {
    eyebrow?: string;
    title?: string;
    subtitle?: string;
    class?: string;
    children?: Snippet;
  } = $props();

  const cardClasses = ['card', className].filter(Boolean).join(' ');
</script>

<div class={cardClasses}>
  {#if eyebrow}<p class="card-eyebrow">{eyebrow}</p>{/if}
  {#if title}<h2 class="card-title">{title}</h2>{/if}
  {#if subtitle}<p class="card-subtitle">{subtitle}</p>{/if}
  {@render children?.()}
</div>
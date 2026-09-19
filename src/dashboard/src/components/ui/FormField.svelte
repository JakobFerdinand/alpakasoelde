<script lang="ts">
  import type { Snippet } from 'svelte';

  let {
    label,
    id,
    hint,
    required,
    children,
  }: {
    label: string;
    id: string;
    hint?: string;
    required?: boolean;
    children?: Snippet;
  } = $props();
</script>

<div class="form-field">
  <label for={id}>{label}{required && '*'}</label>
  {@render children?.()}
  {#if hint}<p class="form-hint">{hint}</p>{/if}
</div>
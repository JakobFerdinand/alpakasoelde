<script lang="ts">
  import type { Snippet } from 'svelte';

  let {
    open = $bindable(false),
    label = '',
    children,
  }: {
    open: boolean;
    label?: string;
    children?: Snippet;
  } = $props();

  const close = () => {
    open = false;
  };

  const onBackdropClick = (event: MouseEvent) => {
    if (event.target === event.currentTarget) {
      close();
    }
  };

  const onBackdropKeydown = (event: KeyboardEvent) => {
    if ((event.key === 'Enter' || event.key === ' ') && event.target === event.currentTarget) {
      event.preventDefault();
      close();
    }
  };

  const onKeydown = (event: KeyboardEvent) => {
    if (event.key === 'Escape' && open) {
      close();
    }
  };

  $effect(() => {
    if (open) {
      document.body.style.overflow = 'hidden';
    }
    return () => {
      document.body.style.overflow = '';
    };
  });
</script>

{#if open}
  <div
    class="modal-backdrop is-visible"
    role="dialog"
    aria-modal="true"
    aria-label={label}
    tabindex="-1"
    onclick={onBackdropClick}
    onkeydown={onBackdropKeydown}
  >
    <div class="modal-content">
      <button type="button" class="modal-close" aria-label="Schließen" onclick={close}>&times;</button>
      {@render children?.()}
    </div>
  </div>
{/if}

<svelte:window onkeydown={onKeydown} />

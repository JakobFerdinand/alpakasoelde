<script lang="ts">
  let { message }: { message: string } = $props();

  let expanded = $state(false);
  let showToggle = $state(false);
  let textEl = $state<HTMLSpanElement>();

  $effect(() => {
    if (textEl) {
      showToggle = textEl.scrollHeight > textEl.clientHeight && message.length > 1;
    }
  });
</script>

<div class="message-clamp" class:expanded={expanded}>
  <span class="message-text" bind:this={textEl}>{message || '–'}</span>
  {#if showToggle}
    <button
      type="button"
      class="message-toggle"
      onclick={() => (expanded = !expanded)}
    >
      {expanded ? 'Weniger' : 'Mehr'}
    </button>
  {/if}
</div>

<style>
  .message-clamp .message-text {
    display: -webkit-box;
    line-clamp: 2;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  .message-clamp.expanded .message-text {
    line-clamp: unset;
    -webkit-line-clamp: unset;
  }

  .message-toggle {
    margin-top: 0.25rem;
    padding: 0;
    border: none;
    background: none;
    font-family: inherit;
    font-size: inherit;
    font-weight: 600;
    color: inherit;
    text-decoration: underline;
    cursor: pointer;
  }
</style>
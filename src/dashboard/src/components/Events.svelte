<script lang="ts">
  import EventList from './EventList.svelte';
</script>

<section class="dashboard-events section">
  <div class="container">
    <div class="events-header">
      <h2>Letzte Ereignisse</h2>
    </div>
    <EventList
      id="events-content"
      fetchUrl="/api/events"
      emptyText="Noch keine Ereignisse vorhanden."
    />
  </div>
</section>

<style>
  .dashboard-events {
    background-color: var(--schurwolle);
    color: var(--taubenblau);
  }

  .events-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1.5rem;
  }
</style>
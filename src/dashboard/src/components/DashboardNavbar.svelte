<script lang="ts">
  import { onMount } from 'svelte';
  import { Menu } from '@lucide/svelte';
  import Logo from '../images/AS_Symbolik_Backstein.svg?url';

  let expanded = $state(false);
  let userName = $state('');

  const closeMenu = () => {
    expanded = false;
  };

  async function loadUser(): Promise<void> {
    try {
      const res = await fetch('/.auth/me');
      if (!res.ok) return;
      const data = await res.json();
      const principal = data.clientPrincipal;
      if (!principal) return;
      userName = principal.userDetails || 'User';
    } catch {
      /* ignore */
    }
  }

  onMount(loadUser);
</script>

<nav class="navbar">
  <div class="container">
    <a href="/" class="logo" aria-label="Dashboard Startseite">
      <img style="width:60px;height:60px" src={Logo} alt="Alpakasölde Logo" />
    </a>
    <a href="https://alpakasoelde.at" class="app-link">Zur App</a>
    <ul id="dashboard-navigation" class="nav-links" class:open={expanded}>
      <li><a href="/" onclick={closeMenu}>Übersicht</a></li>
      <li><a href="/messages" onclick={closeMenu}>Nachrichten</a></li>
      <li><a href="/pageviews" onclick={closeMenu}>Statistik</a></li>
      <li><a href="/sitzungen" onclick={closeMenu}>Sitzungen</a></li>
      <li><a href="/gutscheine" onclick={closeMenu}>Gutscheine</a></li>
      <li class="user-info">
        <span class="user-name">{userName}</span>
        <img class="avatar" src={userName ? `https://github.com/${userName}.png` : undefined} alt={userName} />
      </li>
    </ul>
    <button class="nav-toggle" aria-label="Menü öffnen" aria-controls="dashboard-navigation" aria-expanded={expanded} onclick={() => (expanded = !expanded)}>
      <Menu aria-hidden="true" />
    </button>
  </div>
</nav>

<style>
  .navbar {
    position: sticky;
    top: 0;
    z-index: 1000;
    background-color: var(--schurwolle);
    padding: 0.5rem 0;
  }
  .navbar .container {
    display: flex;
    align-items: center;
  }
  .nav-links {
    list-style: none;
    display: flex;
    gap: 1rem;
    margin: 0;
    padding: 0;
    margin-left: auto;
    align-items: center;
  }
  .nav-links a {
    text-decoration: none;
    color: var(--taubenblau);
    font-weight: 600;
  }
  .app-link {
    margin-left: 1rem;
    text-decoration: none;
    color: var(--taubenblau);
    font-weight: 600;
  }
  .user-info {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-weight: 600;
  }
  .avatar {
    width: 40px;
    height: 40px;
    border-radius: 50%;
  }
  .nav-toggle {
    display: none;
    background: none;
    border: none;
    color: var(--taubenblau);
    font-size: 1.5rem;
    cursor: pointer;
  }
  :global(.nav-toggle svg) {
    width: 1.75rem;
    height: 1.75rem;
  }
  @media (max-width: 768px) {
    .nav-links {
      display: none;
      flex-direction: column;
      background-color: var(--schurwolle);
      position: absolute;
      top: 100%;
      left: 0;
      right: 0;
      padding: 1rem;
      margin-left: 0;
    }
    .nav-links.open {
      display: flex;
    }
    .nav-toggle {
      display: block;
      margin-left: auto;
    }
  }
</style>
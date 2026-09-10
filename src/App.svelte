<script lang="ts">
  import { onMount } from "svelte";
  import { getCurrentWindow } from "@tauri-apps/api/window";
  import { ChevronLeft, ChevronRight, Compass, Disc3, Download, Heart, Home, Library, ListMusic, Maximize2, Minus, Moon, Pause, Play, Search, Settings, SkipBack, SkipForward, Sun, Volume2, X } from "@lucide/svelte";
  import PlayerBridge from "./components/PlayerBridge.svelte";
  import MediaGrid from "./components/MediaGrid.svelte";
  import TrackList from "./components/TrackList.svelte";
  import { api } from "./lib/api";
  import { adjacent, pause, player, seek, setVolume, toggle } from "./lib/player";
  import type { AuthStatus, HomeSection, Playlist, SearchResults, Track, UpdateStatus } from "./lib/types";

  type Page = "home" | "search" | "discover" | "library" | "settings";
  const appWindow = getCurrentWindow();
  let page: Page = "home";
  let previousPage: Page = "home";
  let auth: AuthStatus = { configured: false, valid: false };
  let sections: HomeSection[] = [];
  let liked: Track[] = [];
  let playlists: Playlist[] = [];
  let playlistTracks: Track[] = [];
  let selectedPlaylist = "";
  let libraryMode: "liked" | "playlists" = "liked";
  let discover: Track[] = [];
  let searchResults: SearchResults = { tracks: [], albums: [], artists: [], playlists: [] };
  let query = "";
  let loading = true;
  let searchLoading = false;
  let message = "";
  let dark = localStorage.getItem("theme") !== "light";
  let loginTimer = 0;
  let searchTimer = 0;
  let update: UpdateStatus | null = null;
  let updateBusy = false;

  $: document.documentElement.dataset.theme = dark ? "dark" : "light";

  function navigate(next: Page) {
    if (next !== page) previousPage = page;
    page = next;
    if (next === "discover" && !discover.length) void loadDiscover();
    if (next === "library" && !liked.length) void loadLibrary();
  }

  async function bootstrap() {
    loading = true;
    try { auth = await api.authStatus(); if (auth.valid) sections = await api.home(); }
    catch (error) { message = String(error); }
    finally { loading = false; }
  }

  async function loadLibrary() {
    loading = true;
    try { [liked, playlists] = await Promise.all([api.liked(150), api.playlists(100)]); } catch (error) { message = String(error); } finally { loading = false; }
  }

  async function openPlaylist(playlist: Playlist) {
    loading = true;
    selectedPlaylist = playlist.title;
    try { playlistTracks = await api.playlistTracks(playlist.playlistId); } catch (error) { message = String(error); } finally { loading = false; }
  }

  async function loadDiscover() {
    loading = true;
    try { discover = await api.discover(30); } catch (error) { message = String(error); } finally { loading = false; }
  }

  function runSearch() {
    clearTimeout(searchTimer);
    if (!query.trim()) { searchResults = { tracks: [], albums: [], artists: [], playlists: [] }; return; }
    searchTimer = window.setTimeout(async () => {
      searchLoading = true;
      try { searchResults = await api.search(query.trim()); message = ""; } catch (error) { message = String(error); } finally { searchLoading = false; }
    }, 320);
  }

  async function signIn() {
    message = "Finish signing in inside the new window…";
    await api.startLogin();
    clearInterval(loginTimer);
    loginTimer = window.setInterval(async () => {
      try {
        const status = await api.pollLogin();
        if (!status) return;
        clearInterval(loginTimer); auth = status; message = "Connected. Loading your music…"; await bootstrap();
      } catch (error) {
        if (!String(error).includes("closed")) return;
        clearInterval(loginTimer); message = String(error);
      }
    }, 1500);
  }

  function switchTheme() { dark = !dark; localStorage.setItem("theme", dark ? "dark" : "light"); }
  async function updateAction() {
    updateBusy = true;
    try {
      if (update?.available) await api.installUpdate();
      else update = await api.checkUpdate();
    } catch (error) { message = String(error); }
    finally { updateBusy = false; }
  }
  const formatTime = (seconds: number) => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, "0")}`;

  onMount(() => {
    void bootstrap();
    const keys = (event: KeyboardEvent) => {
      if (event.ctrlKey && ["k", "l"].includes(event.key.toLowerCase())) { event.preventDefault(); navigate("search"); requestAnimationFrame(() => document.querySelector<HTMLInputElement>("#search-input")?.focus()); }
      if (event.code === "Space" && !(event.target instanceof HTMLInputElement)) { event.preventDefault(); toggle(); }
    };
    window.addEventListener("keydown", keys);
    return () => { window.removeEventListener("keydown", keys); clearInterval(loginTimer); clearTimeout(searchTimer); };
  });
</script>

<div class="app-shell">
  <header class="titlebar" data-tauri-drag-region>
    <div class="brand" data-tauri-drag-region><span class="brand-mark"><Disc3 size={16} /></span><b>YT Music Lite</b><span>7.0</span></div>
    <div class="history" data-tauri-drag-region><button aria-label="Back" title="Back" onclick={() => navigate(previousPage)}><ChevronLeft size={17} /></button><button aria-label="Forward" title="Forward" disabled><ChevronRight size={17} /></button></div>
    <div class="window-actions"><button aria-label="Minimize" onclick={() => { pause(); void appWindow.minimize(); }}><Minus size={15} /></button><button aria-label="Maximize" onclick={() => appWindow.toggleMaximize()}><Maximize2 size={13} /></button><button class="close" aria-label="Close" onclick={() => appWindow.close()}><X size={16} /></button></div>
  </header>

  <aside class="sidebar">
    <nav aria-label="Main navigation"><button class:active={page === "home"} onclick={() => navigate("home")}><Home size={19} /><span>Home</span></button><button class:active={page === "search"} onclick={() => navigate("search")}><Search size={19} /><span>Search</span></button><button class:active={page === "discover"} onclick={() => navigate("discover")}><Compass size={19} /><span>Discover</span></button></nav>
    <div class="library-label">Your music</div>
    <nav><button class:active={page === "library"} onclick={() => navigate("library")}><Library size={19} /><span>Library</span></button><button onclick={() => { libraryMode = "liked"; selectedPlaylist = ""; navigate("library"); }}><Heart size={19} /><span>Liked songs</span></button><button onclick={() => { libraryMode = "playlists"; selectedPlaylist = ""; navigate("library"); }}><ListMusic size={19} /><span>Playlists</span></button></nav>
    <div class="sidebar-fill"></div><nav><button class:active={page === "settings"} onclick={() => navigate("settings")}><Settings size={19} /><span>Settings</span></button></nav>
  </aside>

  <main>
    {#if loading}<div class="page-state"><span class="spinner"></span><h2>Loading your music</h2></div>
    {:else if page === "home"}
      <section class="page"><div class="hero"><span>GOOD TO SEE YOU</span><h1>Music for right now.</h1><p>Fast, focused, and built to stay out of your way.</p></div>
        {#if !auth.valid}<div class="connect-card"><div><b>Connect YouTube Music</b><p>Sign in to load your home feed, liked songs, and playlists.</p></div><button class="primary" onclick={signIn}>Connect account</button></div>
        {:else if sections.length}{#each sections.slice(0, 5) as section}{@const tracks = section.items.filter(item => item.kind === "track").map(item => item.kind === "track" ? item.track : null).filter(Boolean) as Track[]}{#if tracks.length}<div class="content-section"><h2>{section.title}</h2><MediaGrid tracks={tracks.slice(0, 12)} /></div>{/if}{/each}
        {:else}<div class="page-state compact"><Disc3 size={30} /><h2>Your home feed is quiet</h2><p>Search for something to start listening.</p></div>{/if}
      </section>
    {:else if page === "search"}
      <section class="page"><div class="search-box"><Search size={21} /><input id="search-input" bind:value={query} oninput={runSearch} placeholder="What do you want to play?" /><kbd>Ctrl K</kbd></div>{#if searchLoading}<div class="searching"><span class="spinner"></span> Searching YouTube Music…</div>{/if}
        {#if searchResults.tracks.length}<div class="content-section"><div class="section-heading"><div><span>TOP RESULTS</span><h1>Songs</h1></div><small>{searchResults.tracks.length} results</small></div><TrackList tracks={searchResults.tracks} /></div>
        {:else if query && !searchLoading}<div class="page-state compact"><Search size={29} /><h2>No matches found</h2><p>Try a song, artist, album, or playlist.</p></div>
        {:else if !query}<div class="browse-grid"><button><span>Made for you</span></button><button><span>New releases</span></button><button><span>Focus</span></button><button><span>Chill</span></button></div>{/if}
      </section>
    {:else if page === "discover"}<section class="page"><div class="section-heading"><div><span>UPDATED FROM YOUR LISTENING</span><h1>Weekly Mix</h1><p>A lightweight, local mix shaped by what you play and finish.</p></div><button class="secondary" onclick={loadDiscover}>Refresh</button></div><TrackList tracks={discover} empty="Play a few tracks first; your weekly mix will appear here." /></section>
    {:else if page === "library"}<section class="page"><div class="section-heading"><div><span>YOUR COLLECTION</span><h1>{selectedPlaylist || (libraryMode === "liked" ? "Liked songs" : "Playlists")}</h1><p>Synced from your connected YouTube Music account.</p></div><button class="secondary" onclick={loadLibrary}>Sync</button></div>
      <div class="tabs"><button class:active={libraryMode === "liked"} onclick={() => { libraryMode = "liked"; selectedPlaylist = ""; }}>Liked songs</button><button class:active={libraryMode === "playlists"} onclick={() => { libraryMode = "playlists"; selectedPlaylist = ""; }}>Playlists</button></div>
      {#if selectedPlaylist}<button class="text-button" onclick={() => { selectedPlaylist = ""; playlistTracks = []; }}>← Back to playlists</button><TrackList tracks={playlistTracks} empty="This playlist is empty." />
      {:else if libraryMode === "liked"}<TrackList tracks={liked} empty="No liked songs were returned by your account." />
      {:else}<div class="playlist-grid">{#each playlists as playlist (playlist.playlistId)}<button class="playlist-card" onclick={() => openPlaylist(playlist)}>{#if playlist.thumbnailUrl}<img src={playlist.thumbnailUrl} alt="" loading="lazy" />{:else}<span><ListMusic size={28} /></span>{/if}<b>{playlist.title}</b><small>{playlist.trackCount} tracks</small></button>{/each}</div>{#if !playlists.length}<div class="empty-state">No playlists were returned by your account.</div>{/if}{/if}
    </section>
    {:else}<section class="page settings-page"><div class="section-heading"><div><span>YT MUSIC LITE</span><h1>Settings</h1><p>Windows-only build · one playback WebView · bounded memory.</p></div></div>
      <div class="setting-card"><div class:ok={auth.valid} class="status-dot"></div><div><b>{auth.valid ? "YouTube Music connected" : "Account not connected"}</b><p>{auth.message ?? "Connect to load your personal library."}</p></div>{#if auth.valid}<button class="secondary" onclick={async () => { auth = await api.clearAuth(); sections = []; liked = []; }}>Disconnect</button>{:else}<button class="primary" onclick={signIn}>Connect</button>{/if}</div>
      <div class="setting-card"><div class="setting-icon">{#if dark}<Moon size={19} />{:else}<Sun size={19} />{/if}</div><div><b>Appearance</b><p>Use the {dark ? "dark" : "light"} theme.</p></div><button class="secondary" onclick={switchTheme}>Switch theme</button></div>
      <div class="setting-card"><div class="setting-icon"><Download size={19} /></div><div><b>{update?.available ? `Version ${update.version} is ready` : "Application updates"}</b><p>{update?.message ?? "Check the verified Windows release channel."}</p></div><button class={update?.available ? "primary" : "secondary"} disabled={updateBusy} onclick={updateAction}>{updateBusy ? "Working…" : update?.available ? "Install" : "Check"}</button></div>
      <div class="setting-card"><div class="setting-icon"><Disc3 size={19} /></div><div><b>Memory target</b><p>Release gate: under 200 MB private working set during normal playback.</p></div><span class="pill">ENFORCED IN QA</span></div>{#if message}<div class="notice">{message}</div>{/if}</section>{/if}
  </main>

  <aside class="now-panel"><div class="panel-title"><span>NOW PLAYING</span><ListMusic size={16} /></div><PlayerBridge />
    {#if $player.current}<img class="large-art" src={$player.current.thumbnailUrl} alt="" /><div class="now-copy"><h2>{$player.current.title}</h2><p>{$player.current.artist}</p></div>{#if $player.error}<div class="player-error">{$player.error}</div>{/if}
    {:else}<div class="no-track"><Disc3 size={38} /><b>Nothing playing</b><span>Choose a song to begin.</span></div>{/if}
  </aside>

  <footer class="playerbar"><div class="player-track">{#if $player.current?.thumbnailUrl}<img src={$player.current.thumbnailUrl} alt="" />{:else}<div class="art-empty"><Disc3 size={20} /></div>{/if}<div><b>{$player.current?.title ?? "Choose something to play"}</b><small>{$player.current?.artist ?? "YT Music Lite"}</small></div></div>
    <div class="transport"><div class="controls"><button aria-label="Previous" onclick={() => adjacent(-1)}><SkipBack size={18} fill="currentColor" /></button><button class="play" aria-label={$player.playing ? "Pause" : "Play"} onclick={toggle}>{#if $player.playing}<Pause size={19} fill="currentColor" />{:else}<Play size={19} fill="currentColor" />{/if}</button><button aria-label="Next" onclick={() => adjacent(1)}><SkipForward size={18} fill="currentColor" /></button></div><div class="timeline"><span>{formatTime($player.position)}</span><input aria-label="Position" type="range" min="0" max={Math.max(1, $player.duration)} value={$player.position} oninput={event => seek(Number(event.currentTarget.value))} /><span>{formatTime($player.duration)}</span></div></div>
    <div class="volume"><Volume2 size={17} /><input aria-label="Volume" type="range" min="0" max="1" step="0.01" value={$player.volume} oninput={event => setVolume(Number(event.currentTarget.value))} /></div></footer>
</div>

<script lang="ts">
  import { Play, Music2 } from "@lucide/svelte";
  import type { Track } from "../lib/types";
  import { playTrack } from "../lib/player";
  export let tracks: Track[] = [];
  export let empty = "Nothing here yet.";
  const format = (seconds: number) => seconds > 0 ? `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, "0")}` : "—";
</script>

{#if tracks.length}
  <div class="track-list" role="list">
    {#each tracks as track, index (track.videoId)}
      <button class="track-row" onclick={() => playTrack(track)}>
        <span class="track-index">{index + 1}</span>
        <span class="track-art">
          {#if track.thumbnailUrl}<img src={track.thumbnailUrl} alt="" loading="lazy" />{:else}<Music2 size={18} />{/if}
          <span class="row-play"><Play size={14} fill="currentColor" /></span>
        </span>
        <span class="track-copy"><strong>{track.title}</strong><small>{track.artist}</small></span>
        <span class="track-album">{track.album}</span>
        <span class="track-time">{format(track.durationSeconds)}</span>
      </button>
    {/each}
  </div>
{:else}
  <div class="empty-state">{empty}</div>
{/if}

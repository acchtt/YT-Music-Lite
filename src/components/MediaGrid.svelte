<script lang="ts">
  import { Music2, Play } from "@lucide/svelte";
  import type { Track } from "../lib/types";
  import { playTrack } from "../lib/player";
  export let tracks: Track[] = [];
</script>

<div class="media-grid">
  {#each tracks as track (track.videoId)}
    <button class="media-card" onclick={() => playTrack(track)}>
      <span class="media-art">
        {#if track.thumbnailUrl}<img src={track.thumbnailUrl} alt="" loading="lazy" />{:else}<Music2 size={28} />{/if}
        <span class="card-play"><Play size={19} fill="currentColor" /></span>
      </span>
      <strong>{track.title}</strong><small>{track.artist}</small>
    </button>
  {/each}
</div>

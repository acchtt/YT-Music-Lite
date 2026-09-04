import { type FormEvent, type ReactNode, useState } from "react";
import { SearchIcon } from "../components/Icons";
import { AlbumCard, ArtistCard, PlaylistCard, TrackCard } from "../components/MediaCard";
import { api } from "../lib/tauri";
import type { SearchResults, Track } from "../types/music";

export function SearchView({ onPlay }: { onPlay: (t: Track) => void }) {
  const [q, setQ] = useState("");
  const [r, setR] = useState<SearchResults | null>(null);
  const [busy, setBusy] = useState(false);
  const [err, setErr] = useState("");

  async function go(e: FormEvent) {
    e.preventDefault();
    if (!q.trim()) return;
    setBusy(true);
    setErr("");
    try { setR(await api.search(q.trim())); }
    catch (error) { setErr(String(error)); }
    finally { setBusy(false); }
  }

  return <div className="page">
    <form className="searchbar" onSubmit={go}>
      <span className="searchbar-icon"><SearchIcon size={19}/></span>
      <input autoFocus value={q} onChange={e => setQ(e.target.value)} placeholder="Search songs, artists, albums, playlists"/>
      <kbd>Enter</kbd>
    </form>
    {busy && <div className="loading-line">Searching…</div>}
    {err && <div className="error-box">{err}</div>}
    {r && <>
      {r.tracks.length > 0 && <Result title="Songs">{r.tracks.map((x, i) => <TrackCard key={x.videoId + i} item={x} onPlay={onPlay}/>)}</Result>}
      {r.albums.length > 0 && <Result title="Albums">{r.albums.map((x, i) => <AlbumCard key={x.browseId + i} item={x}/>)}</Result>}
      {r.artists.length > 0 && <Result title="Artists">{r.artists.map((x, i) => <ArtistCard key={x.channelId + i} item={x}/>)}</Result>}
      {r.playlists.length > 0 && <Result title="Playlists">{r.playlists.map((x, i) => <PlaylistCard key={x.playlistId + i} item={x}/>)}</Result>}
    </>}
  </div>;
}

function Result({ title, children }: { title: string; children: ReactNode }) {
  return <section className="section"><h2>{title}</h2><div className="card-row wrap">{children}</div></section>;
}

import { useEffect, useState } from "react";
import { api } from "../lib/tauri";
import type { Album, Artist, Playlist, Track } from "../types/music";
import { AlbumCard, ArtistCard, PlaylistCard, TrackCard } from "../components/MediaCard";

type Tab = "liked" | "playlists" | "albums" | "artists" | "history";
type LibraryData = Track[] | Playlist[] | Album[] | Artist[];

const tabs: { id: Tab; label: string }[] = [
  { id: "liked", label: "Liked songs" },
  { id: "playlists", label: "Playlists" },
  { id: "albums", label: "Albums" },
  { id: "artists", label: "Artists" },
  { id: "history", label: "History" }
];

export function LibraryView({ onPlay }: { onPlay: (track: Track) => void }) {
  const [tab, setTab] = useState<Tab>("liked");
  const [data, setData] = useState<LibraryData>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError("");
    const load = async (): Promise<LibraryData> => {
      if (tab === "liked") return api.likedSongs();
      if (tab === "playlists") return api.libraryPlaylists();
      if (tab === "albums") return api.libraryAlbums();
      if (tab === "artists") return api.libraryArtists();
      return api.history();
    };

    load().then((items) => active && setData(items))
      .catch((e) => active && setError(String(e)))
      .finally(() => active && setLoading(false));
    return () => { active = false; };
  }, [tab]);

  return <div className="page">
    <header className="simple-head"><p>YOUR MUSIC</p><h1>Library</h1></header>
    <div className="tabbar">
      {tabs.map((item) => <button key={item.id} className={tab === item.id ? "active" : ""} onClick={() => setTab(item.id)}>{item.label}</button>)}
    </div>
    {loading && <div className="loading-line">Loading your library…</div>}
    {error && <div className="error-box">{error}</div>}
    {!loading && !error && data.length === 0 && <div className="empty-library">Nothing to show here yet.</div>}
    {!loading && !error && <div className="card-row wrap library-grid">
      {(tab === "liked" || tab === "history") && (data as Track[]).map((item) => <TrackCard key={`${tab}-${item.videoId}`} item={item} onPlay={onPlay}/>)}
      {tab === "playlists" && (data as Playlist[]).map((item) => <PlaylistCard key={item.playlistId} item={item}/>)}
      {tab === "albums" && (data as Album[]).map((item) => <AlbumCard key={item.browseId} item={item}/>)}
      {tab === "artists" && (data as Artist[]).map((item) => <ArtistCard key={item.channelId} item={item}/>)}
    </div>}
  </div>;
}

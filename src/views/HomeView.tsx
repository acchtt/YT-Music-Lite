import { useEffect, useState } from "react";
import { StatusIcon } from "../components/Icons";
import { PlaylistCard, TrackCard } from "../components/MediaCard";
import { api } from "../lib/tauri";
import type { HomeSection, Track } from "../types/music";

export function HomeView({ onPlay }: { onPlay: (t: Track) => void }) {
  const [data, setData] = useState<HomeSection[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.home().then(setData).catch(e => setError(String(e))).finally(() => setLoading(false));
  }, []);

  if (loading) return <Status title="Loading your Home…"/>;
  if (error) return <Status title="Home needs authentication" body={error}/>;

  return <div className="page">
    <header className="hero"><p>FOR YOU</p><h1>Made for your session.</h1><span>Direct YouTube Music data — no embedded website.</span></header>
    {data.map((section, i) => <section className="section" key={`${section.title}-${i}`}>
      <div className="section-head"><h2>{section.title}</h2></div>
      <div className="card-row">{section.items.map((item, j) => item.kind === "track"
        ? <TrackCard key={item.track.videoId + j} item={item.track} onPlay={onPlay}/>
        : <PlaylistCard key={item.playlist.playlistId + j} item={item.playlist}/>)}</div>
    </section>)}
  </div>;
}

function Status({ title, body }: { title: string; body?: string }) {
  return <div className="status-panel">
    <div className="status-icon"><StatusIcon size={30}/></div>
    <h2>{title}</h2>
    {body && <p>{body}</p>}
    <span>Open Settings to connect a browser.json session.</span>
  </div>;
}

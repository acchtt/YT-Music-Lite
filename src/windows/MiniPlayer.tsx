import { getCurrentWindow } from "@tauri-apps/api/window";
import { CloseIcon, MusicIcon, NextIcon, PauseIcon, PlayIcon, PreviousIcon, VolumeIcon } from "../components/Icons";
import { usePlayer } from "../hooks/usePlayer";

export function MiniPlayer() {
  const { state, control } = usePlayer();
  const t = state.current;
  const pct = state.duration > 0 ? Math.min(100, Math.max(0, state.position / state.duration * 100)) : 0;
  return <div className="mini">
    <div className="mini-top" data-tauri-drag-region><span>YTM Desktop</span><button aria-label="Hide mini player" onClick={() => getCurrentWindow().hide()}><CloseIcon size={14}/></button></div>
    <div className="mini-main">
      <div className="mini-art">{t?.thumbnailUrl ? <img src={t.thumbnailUrl} onError={e => { e.currentTarget.style.display = "none"; }}/> : <MusicIcon size={28}/>}</div>
      <div className="mini-copy">
        <b>{t?.title || "Nothing playing"}</b><small>{t?.artist || state.notice}</small>
        <div className="mini-controls">
          <button aria-label="Previous" onClick={() => control("previous")}><PreviousIcon size={16}/></button>
          <button aria-label={state.isPlaying ? "Pause" : "Play"} className="primary" onClick={() => control("play_pause")}>{state.isPlaying ? <PauseIcon size={15}/> : <PlayIcon size={15}/>}</button>
          <button aria-label="Next" onClick={() => control("next")}><NextIcon size={16}/></button>
          <VolumeIcon className="mini-volume-icon" size={15}/><input className="mini-volume" type="range" min="0" max="1" step="0.01" value={state.volume} onChange={e => control("volume", Number(e.target.value))}/>
        </div>
        <div className="mini-progress"><div style={{ width: `${pct}%` }}/></div>
      </div>
    </div>
  </div>;
}

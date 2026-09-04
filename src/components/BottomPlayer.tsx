import type { PlayerState } from "../types/player";
import { MiniIcon, MusicIcon, NextIcon, PauseIcon, PlayIcon, PreviousIcon, VolumeIcon } from "./Icons";

const time = (s: number) => `${Math.floor(s / 60)}:${Math.floor(s % 60).toString().padStart(2, "0")}`;
const hasError = (notice: string) => /failed|could not|error|unavailable|403|network/i.test(notice);

export function BottomPlayer({ state, onControl, onMini }: { state: PlayerState; onControl: (a: string, v?: number) => void; onMini: () => void }) {
  const t = state.current;
  const error = hasError(state.notice);
  return <>
    {error && <div className="player-alert" title={state.notice}>{state.notice}</div>}
    <footer className="bottom-player">
      <div className="now">
        <div className="now-cover">{t?.thumbnailUrl ? <img src={t.thumbnailUrl} /> : <MusicIcon size={20}/>}</div>
        <div><b>{t?.title || "Nothing playing"}</b><small>{t?.artist || state.notice}</small></div>
      </div>
      <div className="transport">
        <div className="transport-buttons">
          <button aria-label="Previous" onClick={() => onControl("previous")}><PreviousIcon size={17}/></button>
          <button aria-label={state.isPlaying ? "Pause" : "Play"} className="primary" onClick={() => onControl("play_pause")}>{state.isPlaying ? <PauseIcon size={16}/> : <PlayIcon size={16}/>}</button>
          <button aria-label="Next" onClick={() => onControl("next")}><NextIcon size={17}/></button>
        </div>
        <div className="seek"><span>{time(state.position)}</span><input type="range" min="0" max={Math.max(1, state.duration)} value={state.position} onChange={e => onControl("seek", Number(e.target.value))}/><span>{time(state.duration)}</span></div>
      </div>
      <div className="player-tools">
        <button aria-label="Open mini player" onClick={onMini}><MiniIcon size={17}/></button>
        <VolumeIcon size={17}/>
        <input className="volume" type="range" min="0" max="1" step="0.01" value={state.volume} onChange={e => onControl("volume", Number(e.target.value))}/>
      </div>
    </footer>
  </>;
}

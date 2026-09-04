import { useState } from "react";
import { getCurrentWindow } from "@tauri-apps/api/window";
import { Titlebar } from "./components/Titlebar";
import { Sidebar, type Page } from "./components/Sidebar";
import { BottomPlayer } from "./components/BottomPlayer";
import { HomeView } from "./views/HomeView";
import { SearchView } from "./views/SearchView";
import { LibraryView } from "./views/LibraryView";
import { SettingsView } from "./views/SettingsView";
import { MiniPlayer } from "./windows/MiniPlayer";
import { usePlayer } from "./hooks/usePlayer";
import { useAudioEngine } from "./hooks/useAudioEngine";
import { api } from "./lib/tauri";
import type { Track } from "./types/music";

function MainApp() {
  const [page, setPage] = useState<Page>("home");
  const { state, control, queue } = usePlayer();
  useAudioEngine(state);
  const play = (track: Track) => queue(track);

  return <div className="app">
    <Titlebar />
    <div className="body">
      <Sidebar page={page} onPage={setPage} />
      <main>
        {page === "home" && <HomeView onPlay={play} />}
        {page === "search" && <SearchView onPlay={play} />}
        {page === "library" && <LibraryView onPlay={play} />}
        {page === "settings" && <SettingsView />}
      </main>
    </div>
    <BottomPlayer state={state} onControl={control} onMini={() => api.openMini()} />
  </div>;
}

export default function App() {
  return getCurrentWindow().label === "mini" ? <MiniPlayer /> : <MainApp />;
}

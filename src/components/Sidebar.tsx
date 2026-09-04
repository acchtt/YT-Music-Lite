import { HomeIcon, LibraryIcon, SearchIcon, SettingsIcon } from "./Icons";

export type Page = "home" | "search" | "library" | "settings";

const nav = [
  ["home", HomeIcon, "Home"],
  ["search", SearchIcon, "Search"],
  ["library", LibraryIcon, "Library"],
] as const;

export function Sidebar({ page, onPage }: { page: Page; onPage: (p: Page) => void }) {
  return <aside className="sidebar">
    <nav>{nav.map(([id, Icon, label]) =>
      <button key={id} className={page === id ? "active" : ""} onClick={() => onPage(id)}>
        <span className="nav-icon"><Icon size={17} /></span><span className="nav-label">{label}</span>
      </button>
    )}</nav>
    <div className="sidebar-spacer" />
    <button className={page === "settings" ? "active" : ""} onClick={() => onPage("settings")}>
      <span className="nav-icon"><SettingsIcon size={17} /></span><span className="nav-label">Settings</span>
    </button>
  </aside>;
}

import { getCurrentWindow } from "@tauri-apps/api/window";
import { CloseIcon, MaximizeIcon, MinimizeIcon } from "./Icons";

export function Titlebar() {
  const window = getCurrentWindow();
  return <div className="titlebar" data-tauri-drag-region>
    <div className="brand">
      <span className="brand-mark">Y</span>
      <span className="brand-name">YTM Desktop</span>
      <span className="version">0.3.1</span>
    </div>
    <div className="window-actions">
      <button className="window-button" aria-label="Minimize" onClick={() => window.minimize()}><MinimizeIcon size={15}/></button>
      <button className="window-button" aria-label="Maximize" onClick={() => window.toggleMaximize()}><MaximizeIcon size={14}/></button>
      <button className="window-button close" aria-label="Close" onClick={() => window.close()}><CloseIcon size={15}/></button>
    </div>
  </div>;
}

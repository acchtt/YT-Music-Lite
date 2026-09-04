import type { ReactNode, SVGProps } from "react";

type IconProps = Omit<SVGProps<SVGSVGElement>, "children"> & { size?: number };

type BaseProps = IconProps & { children: ReactNode; filled?: boolean };

function IconBase({ size = 18, children, className = "", filled = false, ...props }: BaseProps) {
  return (
    <svg
      className={`ui-icon ${className}`.trim()}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill={filled ? "currentColor" : "none"}
      stroke={filled ? "none" : "currentColor"}
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...props}
    >
      {children}
    </svg>
  );
}

export const HomeIcon = (p: IconProps) => (
  <IconBase {...p}><path d="m3 10 9-7 9 7"/><path d="M5 9v11h14V9"/><path d="M9 20v-6h6v6"/></IconBase>
);

export const SearchIcon = (p: IconProps) => (
  <IconBase {...p}><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></IconBase>
);

export const LibraryIcon = (p: IconProps) => (
  <IconBase {...p}><path d="M4 4v16"/><path d="M8 4v16"/><path d="m13 5 5-1 2 15-5 1z"/></IconBase>
);

export const SettingsIcon = (p: IconProps) => (
  <IconBase {...p}>
    <circle cx="12" cy="12" r="3"/>
    <path d="M19.4 15a1.7 1.7 0 0 0 .35 1.88l.05.05-2.82 2.83-.06-.06A1.7 1.7 0 0 0 15 19.4a1.7 1.7 0 0 0-1 .6 1.7 1.7 0 0 0-.4 1v.1h-4V21a1.7 1.7 0 0 0-1.4-1.6 1.7 1.7 0 0 0-1.88.35l-.06.05-2.82-2.82.06-.06A1.7 1.7 0 0 0 3.8 15a1.7 1.7 0 0 0-.6-1 1.7 1.7 0 0 0-1-.4h-.1v-4h.1A1.7 1.7 0 0 0 3.8 8.2a1.7 1.7 0 0 0-.35-1.88l-.05-.06 2.82-2.82.06.05A1.7 1.7 0 0 0 8.2 3.8a1.7 1.7 0 0 0 1-.6 1.7 1.7 0 0 0 .4-1v-.1h4v.1A1.7 1.7 0 0 0 15 3.8a1.7 1.7 0 0 0 1.88-.35l.06-.05 2.82 2.82-.05.06A1.7 1.7 0 0 0 19.4 8.2a1.7 1.7 0 0 0 .6 1 1.7 1.7 0 0 0 1 .4h.1v4H21a1.7 1.7 0 0 0-1.6 1.4Z"/>
  </IconBase>
);

export const PreviousIcon = (p: IconProps) => (
  <IconBase {...p}><path d="M6 6v12"/><path d="m18 6-9 6 9 6z" fill="currentColor" stroke="none"/></IconBase>
);

export const NextIcon = (p: IconProps) => (
  <IconBase {...p}><path d="M18 6v12"/><path d="m6 6 9 6-9 6z" fill="currentColor" stroke="none"/></IconBase>
);

export const PlayIcon = ({ className = "", ...p }: IconProps) => (
  <IconBase {...p} className={`icon-play ${className}`.trim()}><path d="m8 5 11 7-11 7z" fill="currentColor" stroke="none"/></IconBase>
);

export const PauseIcon = (p: IconProps) => (
  <IconBase {...p}>
    <rect x="7" y="5" width="3.5" height="14" rx="1" fill="currentColor" stroke="none"/>
    <rect x="13.5" y="5" width="3.5" height="14" rx="1" fill="currentColor" stroke="none"/>
  </IconBase>
);

export const VolumeIcon = (p: IconProps) => (
  <IconBase {...p}><path d="M4 10v4h4l5 4V6l-5 4H4Z"/><path d="M16 9a4 4 0 0 1 0 6"/><path d="M18.5 6.5a7.5 7.5 0 0 1 0 11"/></IconBase>
);

export const MiniIcon = (p: IconProps) => (
  <IconBase {...p}><rect x="3" y="5" width="18" height="14" rx="2"/><rect x="12" y="12" width="6" height="4" rx="1"/></IconBase>
);

export const MusicIcon = (p: IconProps) => (
  <IconBase {...p}><path d="M9 18V6l9-2v12"/><circle cx="6.5" cy="18" r="2.5" fill="currentColor" stroke="none"/><circle cx="15.5" cy="16" r="2.5" fill="currentColor" stroke="none"/></IconBase>
);

export const MinimizeIcon = (p: IconProps) => (
  <IconBase {...p}><path d="M6 12h12"/></IconBase>
);

export const MaximizeIcon = (p: IconProps) => (
  <IconBase {...p}><rect x="6" y="6" width="12" height="12" rx="1.5"/></IconBase>
);

export const CloseIcon = (p: IconProps) => (
  <IconBase {...p}><path d="m7 7 10 10"/><path d="m17 7-10 10"/></IconBase>
);

export const StatusIcon = (p: IconProps) => (
  <IconBase {...p}><circle cx="12" cy="12" r="8"/><path d="M12 8v4"/><path d="M12 16h.01"/></IconBase>
);

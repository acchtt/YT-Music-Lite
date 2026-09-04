export type Track = { videoId:string; title:string; artist:string; album:string; durationSeconds:number; thumbnailUrl:string };
export type Playlist = { playlistId:string; title:string; description:string; trackCount:number; thumbnailUrl:string };
export type Album = { browseId:string; title:string; artist:string; year:string; thumbnailUrl:string; tracks:Track[] };
export type Artist = { channelId:string; name:string; thumbnailUrl:string };
export type HomeItem = { kind:"track"; track:Track } | { kind:"playlist"; playlist:Playlist };
export type HomeSection = { title:string; items:HomeItem[] };
export type SearchResults = { tracks:Track[]; albums:Album[]; artists:Artist[]; playlists:Playlist[] };
export type AuthStatus = { configured:boolean; valid:boolean; sourcePath?:string; message?:string };

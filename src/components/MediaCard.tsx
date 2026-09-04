import type { SyntheticEvent } from "react";
import type { Track, Playlist, Album, Artist } from "../types/music";
import { MusicIcon, PlayIcon } from "./Icons";

const fallback="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='400' height='400'%3E%3Crect width='100%25' height='100%25' fill='%231a1a20'/%3E%3C/svg%3E";
const imageProps=(src:string)=>({src:src||fallback,onError:(e:SyntheticEvent<HTMLImageElement>)=>{if(e.currentTarget.src!==fallback)e.currentTarget.src=fallback;}});
export function TrackCard({item,onPlay}:{item:Track;onPlay:(t:Track)=>void}){return <button className="media-card" onClick={()=>onPlay(item)}><div className="cover-wrap"><img {...imageProps(item.thumbnailUrl)}/>{!item.thumbnailUrl&&<span className="cover-fallback"><MusicIcon size={28}/></span>}<span className="play-badge"><PlayIcon size={16}/></span></div><b>{item.title}</b><small>{item.artist}</small></button>}
export function PlaylistCard({item}:{item:Playlist}){return <div className="media-card"><div className="cover-wrap"><img {...imageProps(item.thumbnailUrl)}/></div><b>{item.title}</b><small>{item.trackCount?`${item.trackCount} tracks`:"Playlist"}</small></div>}
export function AlbumCard({item}:{item:Album}){return <div className="media-card"><div className="cover-wrap"><img {...imageProps(item.thumbnailUrl)}/></div><b>{item.title}</b><small>{item.artist}{item.year?` · ${item.year}`:""}</small></div>}
export function ArtistCard({item}:{item:Artist}){return <div className="media-card artist-card"><div className="cover-wrap"><img {...imageProps(item.thumbnailUrl)}/></div><b>{item.name}</b><small>Artist</small></div>}

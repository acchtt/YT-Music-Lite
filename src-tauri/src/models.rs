use serde::{Deserialize, Serialize};
use ytmusic_api::{AlbumInfo, ArtistInfo, HomeSection, HomeSectionItem, PlaylistInfo, RelatedArtist, SearchResults, Track};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct TrackVm { pub video_id:String, pub title:String, pub artist:String, pub album:String, pub duration_seconds:f64, pub thumbnail_url:String }
impl From<Track> for TrackVm { fn from(v:Track)->Self{Self{video_id:v.video_id,title:v.title,artist:v.artist,album:v.album,duration_seconds:v.duration_seconds,thumbnail_url:v.thumbnail_url}} }

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PlaylistVm { pub playlist_id:String, pub title:String, pub description:String, pub track_count:u32, pub thumbnail_url:String }
impl From<PlaylistInfo> for PlaylistVm { fn from(v:PlaylistInfo)->Self{Self{playlist_id:v.playlist_id,title:v.title,description:v.description,track_count:v.track_count,thumbnail_url:v.thumbnail_url}} }

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AlbumVm { pub browse_id:String, pub title:String, pub artist:String, pub year:String, pub thumbnail_url:String, pub tracks:Vec<TrackVm> }
impl From<AlbumInfo> for AlbumVm { fn from(v:AlbumInfo)->Self{Self{browse_id:v.browse_id,title:v.title,artist:v.artist,year:v.year,thumbnail_url:v.thumbnail_url,tracks:v.tracks.into_iter().map(Into::into).collect()}} }

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ArtistVm { pub channel_id:String, pub name:String, pub thumbnail_url:String }
impl From<RelatedArtist> for ArtistVm { fn from(v:RelatedArtist)->Self{Self{channel_id:v.channel_id,name:v.name,thumbnail_url:v.thumbnail_url}} }
impl From<ArtistInfo> for ArtistVm { fn from(v:ArtistInfo)->Self{Self{channel_id:v.channel_id,name:v.name,thumbnail_url:v.thumbnail_url}} }

#[derive(Debug, Clone, Serialize)]
#[serde(tag="kind", rename_all="lowercase")]
pub enum HomeItemVm { Track{track:TrackVm}, Playlist{playlist:PlaylistVm} }
#[derive(Debug, Clone, Serialize)]
pub struct HomeSectionVm { pub title:String, pub items:Vec<HomeItemVm> }
impl From<HomeSection> for HomeSectionVm { fn from(v:HomeSection)->Self{Self{title:v.title,items:v.items.into_iter().map(|i|match i{HomeSectionItem::Track(t)=>HomeItemVm::Track{track:t.into()},HomeSectionItem::Playlist(p)=>HomeItemVm::Playlist{playlist:p.into()}}).collect()}} }

#[derive(Debug, Clone, Serialize)]
pub struct SearchResultsVm { pub tracks:Vec<TrackVm>, pub albums:Vec<AlbumVm>, pub artists:Vec<ArtistVm>, pub playlists:Vec<PlaylistVm> }
impl From<SearchResults> for SearchResultsVm { fn from(v:SearchResults)->Self{Self{tracks:v.tracks.into_iter().map(Into::into).collect(),albums:v.albums.into_iter().map(Into::into).collect(),artists:v.artists.into_iter().map(Into::into).collect(),playlists:v.playlists.into_iter().map(Into::into).collect()}} }

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all="camelCase")]
pub struct AuthStatus { pub configured:bool, pub valid:bool, pub source_path:Option<String>, pub message:Option<String> }

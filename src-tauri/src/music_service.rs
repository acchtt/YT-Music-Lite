use std::{path::{Path, PathBuf}, sync::Arc};
use tokio::sync::RwLock;
use ytmusic_api::{BrowserAuth, InnerTubeClient};
use crate::models::*;

#[derive(Default)]
pub struct MusicServiceState { client: RwLock<Option<Arc<InnerTubeClient>>>, auth_path: RwLock<Option<PathBuf>> }
impl MusicServiceState {
  pub async fn configure(&self, path:PathBuf)->Result<AuthStatus,String>{
    let client=InnerTubeClient::from_auth_path(&path).map_err(|e|e.to_string())?;
    let client=Arc::new(client);
    client.get_account_info().await.map_err(|e|format!("Session could not be validated: {e}"))?;
    *self.client.write().await=Some(client);
    *self.auth_path.write().await=Some(path.clone());
    Ok(AuthStatus{configured:true,valid:true,source_path:Some(path.display().to_string()),message:Some("YouTube Music session connected.".into())})
  }
  pub async fn configure_json(&self, raw:&str, source_path:PathBuf)->Result<AuthStatus,String>{
    let auth=BrowserAuth::from_json_str(raw).map_err(|e|e.to_string())?;
    let client=InnerTubeClient::new(auth).map_err(|e|e.to_string())?;
    let client=Arc::new(client);
    client.get_account_info().await.map_err(|e|format!("Session could not be validated: {e}"))?;
    *self.client.write().await=Some(client);
    *self.auth_path.write().await=Some(source_path.clone());
    Ok(AuthStatus{configured:true,valid:true,source_path:Some(source_path.display().to_string()),message:Some("Signed in through YTM Desktop.".into())})
  }
  pub async fn load_without_validation(&self,path:PathBuf)->Result<(),String>{let c=InnerTubeClient::from_auth_path(&path).map_err(|e|e.to_string())?;*self.client.write().await=Some(Arc::new(c));*self.auth_path.write().await=Some(path);Ok(())}
  pub async fn clear(&self){*self.client.write().await=None;*self.auth_path.write().await=None;}
  pub async fn status(&self)->AuthStatus{
    let path=self.auth_path.read().await.clone(); let client=self.client.read().await.clone();
    match client {None=>AuthStatus{configured:false,valid:false,source_path:path.map(|p|p.display().to_string()),message:Some("No browser session configured.".into())},Some(c)=>match c.get_account_info().await{Ok(_)=>AuthStatus{configured:true,valid:true,source_path:path.map(|p|p.display().to_string()),message:Some("YouTube Music session connected.".into())},Err(e)=>AuthStatus{configured:true,valid:false,source_path:path.map(|p|p.display().to_string()),message:Some(format!("Session invalid or expired: {e}"))}}}
  }
  async fn client(&self)->Result<Arc<InnerTubeClient>,String>{self.client.read().await.clone().ok_or_else(||"YouTube Music is not connected. Open Settings and sign in.".into())}
  pub async fn home(&self)->Result<Vec<HomeSectionVm>,String>{Ok(self.client().await?.get_home().await.map_err(|e|e.to_string())?.into_iter().map(Into::into).collect())}
  pub async fn search(&self,q:&str)->Result<SearchResultsVm,String>{if q.trim().is_empty(){return Ok(SearchResultsVm{tracks:vec![],albums:vec![],artists:vec![],playlists:vec![]})}Ok(self.client().await?.search_all(q,30,None).await.map_err(|e|e.to_string())?.into())}
  pub async fn playlists(&self,limit:usize)->Result<Vec<PlaylistVm>,String>{Ok(self.client().await?.get_library_playlists(limit).await.map_err(|e|e.to_string())?.into_iter().map(Into::into).collect())}
  pub async fn albums(&self,limit:usize)->Result<Vec<AlbumVm>,String>{Ok(self.client().await?.get_library_albums(limit).await.map_err(|e|e.to_string())?.into_iter().map(Into::into).collect())}
  pub async fn artists(&self,limit:usize)->Result<Vec<ArtistVm>,String>{Ok(self.client().await?.get_library_artists(limit).await.map_err(|e|e.to_string())?.into_iter().map(Into::into).collect())}
  pub async fn liked(&self,limit:usize)->Result<Vec<TrackVm>,String>{Ok(self.client().await?.get_liked_songs(limit).await.map_err(|e|e.to_string())?.into_iter().map(Into::into).collect())}
  pub async fn history(&self)->Result<Vec<TrackVm>,String>{Ok(self.client().await?.get_history().await.map_err(|e|e.to_string())?.into_iter().map(Into::into).collect())}
  pub async fn playlist_tracks(&self,id:&str)->Result<Vec<TrackVm>,String>{Ok(self.client().await?.get_playlist_tracks(id).await.map_err(|e|e.to_string())?.into_iter().map(Into::into).collect())}
  pub async fn lyrics(&self,id:&str)->Result<Option<String>,String>{self.client().await?.get_lyrics(id).await.map_err(|e|e.to_string())}
}
pub fn normalize_auth_path(path:&str)->Result<PathBuf,String>{let p=Path::new(path.trim());if path.trim().is_empty(){return Err("Choose a browser.json path.".into())}if !p.exists(){return Err(format!("File does not exist: {}",p.display()))}Ok(p.to_path_buf())}

use std::{
    collections::HashSet,
    path::PathBuf,
    time::{SystemTime, UNIX_EPOCH},
};

use rusqlite::{params, Connection};
use tauri::{AppHandle, Manager};

use crate::models::TrackVm;

pub struct DiscoveryState {
    db_path: PathBuf,
}

impl DiscoveryState {
    pub fn new(app: &AppHandle) -> Result<Self, String> {
        let dir = app
            .path()
            .app_data_dir()
            .map_err(|error| error.to_string())?;
        std::fs::create_dir_all(&dir).map_err(|error| error.to_string())?;
        let state = Self {
            db_path: dir.join("listening.db"),
        };
        state.initialize()?;
        Ok(state)
    }

    fn connection(&self) -> Result<Connection, String> {
        Connection::open(&self.db_path).map_err(|error| error.to_string())
    }

    fn initialize(&self) -> Result<(), String> {
        let connection = self.connection()?;
        connection
            .execute_batch(
                "PRAGMA journal_mode=WAL;
             PRAGMA synchronous=NORMAL;
             CREATE TABLE IF NOT EXISTS listening_events (
               id INTEGER PRIMARY KEY,
               video_id TEXT NOT NULL,
               title TEXT NOT NULL,
               artist TEXT NOT NULL,
               album TEXT NOT NULL,
               duration_seconds REAL NOT NULL,
               thumbnail_url TEXT NOT NULL,
               event_type TEXT NOT NULL,
               occurred_at INTEGER NOT NULL
             );
             CREATE INDEX IF NOT EXISTS idx_listening_artist ON listening_events(artist);
             CREATE INDEX IF NOT EXISTS idx_listening_video ON listening_events(video_id);",
            )
            .map_err(|error| error.to_string())
    }

    pub fn record(&self, track: &TrackVm, event_type: &str) -> Result<(), String> {
        if !matches!(event_type, "play" | "complete" | "skip" | "like") {
            return Err("Unsupported listening event.".into());
        }
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default()
            .as_secs() as i64;
        self.connection()?
            .execute(
                "INSERT INTO listening_events
             (video_id,title,artist,album,duration_seconds,thumbnail_url,event_type,occurred_at)
             VALUES (?1,?2,?3,?4,?5,?6,?7,?8)",
                params![
                    track.video_id,
                    track.title,
                    track.artist,
                    track.album,
                    track.duration_seconds,
                    track.thumbnail_url,
                    event_type,
                    now
                ],
            )
            .map_err(|error| error.to_string())?;
        Ok(())
    }

    pub fn top_artists(&self, limit: usize) -> Result<Vec<String>, String> {
        let connection = self.connection()?;
        let mut statement = connection
            .prepare(
            "SELECT artist,
              SUM(CASE event_type WHEN 'complete' THEN 5 WHEN 'like' THEN 6 WHEN 'play' THEN 2 WHEN 'skip' THEN -3 ELSE 0 END) score
             FROM listening_events WHERE artist <> '' GROUP BY artist
             HAVING score > 0 ORDER BY score DESC, MAX(occurred_at) DESC LIMIT ?1"
            )
            .map_err(|error| error.to_string())?;
        let rows = statement
            .query_map([limit as i64], |row| row.get(0))
            .map_err(|error| error.to_string())?;
        rows.collect::<Result<Vec<String>, _>>()
            .map_err(|error| error.to_string())
    }

    pub fn seen_ids(&self) -> Result<HashSet<String>, String> {
        let connection = self.connection()?;
        let mut statement = connection
            .prepare("SELECT DISTINCT video_id FROM listening_events")
            .map_err(|error| error.to_string())?;
        let rows = statement
            .query_map([], |row| row.get(0))
            .map_err(|error| error.to_string())?;
        rows.collect::<Result<HashSet<String>, _>>()
            .map_err(|error| error.to_string())
    }

    pub fn ranked_tracks(&self, limit: usize) -> Result<Vec<TrackVm>, String> {
        let connection = self.connection()?;
        let mut statement = connection
            .prepare(
            "SELECT video_id,title,artist,album,duration_seconds,thumbnail_url
             FROM listening_events GROUP BY video_id
             ORDER BY SUM(CASE event_type WHEN 'complete' THEN 5 WHEN 'like' THEN 6 WHEN 'play' THEN 2 WHEN 'skip' THEN -3 ELSE 0 END) DESC,
                      MAX(occurred_at) DESC LIMIT ?1"
            )
            .map_err(|error| error.to_string())?;
        let rows = statement
            .query_map([limit as i64], |row| {
                Ok(TrackVm {
                    video_id: row.get(0)?,
                    title: row.get(1)?,
                    artist: row.get(2)?,
                    album: row.get(3)?,
                    duration_seconds: row.get(4)?,
                    thumbnail_url: row.get(5)?,
                })
            })
            .map_err(|error| error.to_string())?;
        rows.collect::<Result<Vec<TrackVm>, _>>()
            .map_err(|error| error.to_string())
    }
}

use std::{
    io::Cursor,
    sync::{
        mpsc::{self, Receiver, Sender},
        Arc, Mutex,
    },
    thread,
    time::Duration,
};

use rodio::{Decoder, OutputStream, OutputStreamHandle, Sink};

#[derive(Debug, Clone)]
pub struct NativeAudioStatus {
    pub position: f64,
    pub is_playing: bool,
    pub ended: bool,
    pub volume: f64,
}

impl Default for NativeAudioStatus {
    fn default() -> Self {
        Self {
            position: 0.0,
            is_playing: false,
            ended: false,
            volume: 0.8,
        }
    }
}

type Reply = Sender<Result<(), String>>;

enum AudioCommand {
    Load {
        bytes: Arc<[u8]>,
        autoplay: bool,
        volume: f32,
        reply: Reply,
    },
    Toggle { reply: Reply },
    Seek { seconds: f64, reply: Reply },
    Volume { volume: f32, reply: Reply },
    Stop { reply: Reply },
}

#[derive(Clone)]
pub struct NativeAudioState {
    tx: Sender<AudioCommand>,
    status: Arc<Mutex<NativeAudioStatus>>,
}

impl Default for NativeAudioState {
    fn default() -> Self {
        let (tx, rx) = mpsc::channel();
        let status = Arc::new(Mutex::new(NativeAudioStatus::default()));
        let worker_status = status.clone();

        thread::Builder::new()
            .name("ytm-native-audio".into())
            .spawn(move || audio_worker(rx, worker_status))
            .expect("failed to start native audio worker");

        Self { tx, status }
    }
}

impl NativeAudioState {
    fn request(&self, build: impl FnOnce(Reply) -> AudioCommand) -> Result<(), String> {
        let (reply_tx, reply_rx) = mpsc::channel();
        self.tx
            .send(build(reply_tx))
            .map_err(|_| "Native audio worker is not available.".to_string())?;
        reply_rx
            .recv_timeout(Duration::from_secs(12))
            .map_err(|_| "Native audio worker did not respond in time.".to_string())?
    }

    pub fn load(&self, bytes: Arc<[u8]>, autoplay: bool, volume: f64) -> Result<(), String> {
        let volume = volume.clamp(0.0, 1.0) as f32;
        self.request(|reply| AudioCommand::Load {
            bytes,
            autoplay,
            volume,
            reply,
        })
    }

    pub fn toggle(&self) -> Result<(), String> {
        self.request(|reply| AudioCommand::Toggle { reply })
    }

    pub fn seek(&self, seconds: f64) -> Result<(), String> {
        if !seconds.is_finite() {
            return Err("Seek position is invalid.".into());
        }
        self.request(|reply| AudioCommand::Seek {
            seconds: seconds.max(0.0),
            reply,
        })
    }

    pub fn set_volume(&self, volume: f64) -> Result<(), String> {
        if !volume.is_finite() {
            return Err("Volume is invalid.".into());
        }
        self.request(|reply| AudioCommand::Volume {
            volume: volume.clamp(0.0, 1.0) as f32,
            reply,
        })
    }

    pub fn stop(&self) -> Result<(), String> {
        self.request(|reply| AudioCommand::Stop { reply })
    }

    pub fn status(&self) -> NativeAudioStatus {
        self.status
            .lock()
            .map(|status| status.clone())
            .unwrap_or_default()
    }
}

fn audio_worker(rx: Receiver<AudioCommand>, status: Arc<Mutex<NativeAudioStatus>>) {
    // OutputStream is deliberately owned by this dedicated thread. cpal streams are
    // not Send/Sync on every platform, while the Tauri state itself remains Send + Sync.
    let mut output_stream: Option<OutputStream> = None;
    let mut output_handle: Option<OutputStreamHandle> = None;
    let mut sink: Option<Sink> = None;

    loop {
        match rx.recv_timeout(Duration::from_millis(100)) {
            Ok(command) => {
                let result = match command {
                    AudioCommand::Load {
                        bytes,
                        autoplay,
                        volume,
                        reply,
                    } => {
                        let result = load_track(
                            &mut output_stream,
                            &mut output_handle,
                            &mut sink,
                            bytes,
                            autoplay,
                            volume,
                        );
                        if result.is_ok() {
                            if let Ok(mut current) = status.lock() {
                                current.position = 0.0;
                                current.is_playing = autoplay;
                                current.ended = false;
                                current.volume = volume as f64;
                            }
                        }
                        let _ = reply.send(result.clone());
                        result
                    }
                    AudioCommand::Toggle { reply } => {
                        let result = match sink.as_ref() {
                            Some(current) if !current.empty() => {
                                if current.is_paused() {
                                    current.play();
                                } else {
                                    current.pause();
                                }
                                Ok(())
                            }
                            _ => Err("No native audio track is loaded.".into()),
                        };
                        let _ = reply.send(result.clone());
                        result
                    }
                    AudioCommand::Seek { seconds, reply } => {
                        let result = match sink.as_ref() {
                            Some(current) if !current.empty() => current
                                .try_seek(Duration::from_secs_f64(seconds))
                                .map_err(|e| format!("Native audio seek failed: {e}")),
                            _ => Err("No native audio track is loaded.".into()),
                        };
                        let _ = reply.send(result.clone());
                        result
                    }
                    AudioCommand::Volume { volume, reply } => {
                        let result = match sink.as_ref() {
                            Some(current) => {
                                current.set_volume(volume);
                                if let Ok(mut current_status) = status.lock() {
                                    current_status.volume = volume as f64;
                                }
                                Ok(())
                            }
                            None => {
                                if let Ok(mut current_status) = status.lock() {
                                    current_status.volume = volume as f64;
                                }
                                Ok(())
                            }
                        };
                        let _ = reply.send(result.clone());
                        result
                    }
                    AudioCommand::Stop { reply } => {
                        if let Some(current) = sink.take() {
                            current.stop();
                        }
                        if let Ok(mut current_status) = status.lock() {
                            current_status.position = 0.0;
                            current_status.is_playing = false;
                            current_status.ended = false;
                        }
                        let result = Ok(());
                        let _ = reply.send(result.clone());
                        result
                    }
                };

                if let Err(error) = result {
                    eprintln!("Native audio command failed: {error}");
                }
            }
            Err(mpsc::RecvTimeoutError::Timeout) => {}
            Err(mpsc::RecvTimeoutError::Disconnected) => break,
        }

        if let Some(current) = sink.as_ref() {
            if let Ok(mut current_status) = status.lock() {
                current_status.position = current.get_pos().as_secs_f64();
                current_status.ended = current.empty();
                current_status.is_playing = !current.is_paused() && !current.empty();
            }
        }
    }
}

fn load_track(
    output_stream: &mut Option<OutputStream>,
    output_handle: &mut Option<OutputStreamHandle>,
    sink: &mut Option<Sink>,
    bytes: Arc<[u8]>,
    autoplay: bool,
    volume: f32,
) -> Result<(), String> {
    if output_handle.is_none() {
        let (stream, handle) = OutputStream::try_default()
            .map_err(|e| format!("Could not open the Windows audio output device: {e}"))?;
        *output_stream = Some(stream);
        *output_handle = Some(handle);
    }

    if let Some(previous) = sink.take() {
        previous.stop();
    }

    // Rodio 0.20's Decoder::new auto-detects enabled Symphonia formats. This avoids
    // the older Decoder::new_mp4 API, which requires an Mp4Type hint in 0.20.x.
    let decoder = Decoder::new(Cursor::new(bytes))
        .map_err(|e| format!("Native AAC/MP4 decoder could not open this track: {e}"))?;

    let new_sink = Sink::try_new(
        output_handle
            .as_ref()
            .ok_or_else(|| "Windows audio output is not initialized.".to_string())?,
    )
    .map_err(|e| format!("Could not create the native audio player: {e}"))?;

    new_sink.set_volume(volume);
    new_sink.append(decoder);
    if autoplay {
        new_sink.play();
    } else {
        new_sink.pause();
    }

    *sink = Some(new_sink);
    Ok(())
}

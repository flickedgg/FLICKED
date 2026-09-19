//! Discord Rich Presence: the "Playing FLICKED" box on the player's profile.
//!
//! The frontend decides what to show and calls `set_presence` / `clear_presence`
//! when it changes. This side owns the connection to the local Discord app:
//! it connects on first use, and if Discord is closed or restarted the next
//! update simply tries again. Nothing here can fail the launcher.

use std::sync::Mutex;

use discord_rich_presence::{
    activity::{Activity, Assets, Party, Timestamps},
    DiscordIpc, DiscordIpcClient,
};
use serde::Deserialize;

const APP_ID: &str = "1550904200404410428";

const LOGO: &str = "flicked";

#[derive(Default)]
pub struct Presence(Mutex<Option<DiscordIpcClient>>);

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PresenceData {
    details: String,
    state: String,
    party: Option<[i32; 2]>,
    started_at: Option<i64>,
}

fn apply(slot: &mut Option<DiscordIpcClient>, data: &PresenceData) -> Result<(), Box<dyn std::error::Error>> {
    if slot.is_none() {
        let mut client = DiscordIpcClient::new(APP_ID);
        client.connect()?;
        *slot = Some(client);
    }
    let client = slot.as_mut().expect("connected above");

    let mut activity = Activity::new()
        .details(&data.details)
        .state(&data.state)
        .assets(Assets::new().large_image(LOGO).large_text("FLICKED alpha"));
    if let Some(size) = data.party {
        activity = activity.party(Party::new().size(size));
    }
    if let Some(start) = data.started_at {
        activity = activity.timestamps(Timestamps::new().start(start));
    }
    client.set_activity(activity)?;
    Ok(())
}

// async: Tauri runs async commands off the UI thread, so a slow pipe never freezes the window
#[tauri::command]
pub async fn set_presence(presence: tauri::State<'_, Presence>, data: PresenceData) -> Result<(), String> {
    let Ok(mut slot) = presence.0.lock() else { return Ok(()) };
    if let Err(e) = apply(&mut slot, &data) {
        *slot = None;
        eprintln!("discord presence: {e}");
    }
    Ok(())
}

#[tauri::command]
pub async fn clear_presence(presence: tauri::State<'_, Presence>) -> Result<(), String> {
    let Ok(mut slot) = presence.0.lock() else { return Ok(()) };
    if let Some(client) = slot.as_mut() {
        if client.clear_activity().is_err() {
            *slot = None;
        }
    }
    Ok(())
}

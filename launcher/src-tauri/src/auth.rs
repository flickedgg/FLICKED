//! Signing in with Steam, from the launcher's side.
//!
//! The backend decides who you are; this side only carries the result. The whole
//! flow from here:
//!
//!   1. listen on a free loopback port
//!   2. open the system browser at the backend's /auth/steam/login?port=...
//!   3. the player signs in on steamcommunity.com, and Steam sends them back to
//!      the backend, which verifies it and redirects to our listener with a code
//!   4. swap that code for a session token, in a request we make ourselves
//!   5. keep the token in the OS keychain
//!
//! The token never reaches the webview. The UI asks "who am I" and gets a name,
//! never a credential, so a scripting bug in the interface cannot leak a session.

use std::time::Duration;

use serde::{Deserialize, Serialize};
use tauri::{AppHandle, Emitter};
use tauri_plugin_opener::OpenerExt;
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};

const API: &str = "http://localhost:5165";

const KEYCHAIN_SERVICE: &str = "dev.flicked.launcher";
const KEYCHAIN_USER: &str = "session-token";

const LOGIN_TIMEOUT: Duration = Duration::from_secs(300);

#[derive(Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct Account {
    pub id: i64,
    pub name: String,
    pub steam_id: Option<String>,
    pub avatar_url: Option<String>,
    pub rating: i64,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SessionResponse {
    token: String,
    player_id: i64,
    name: String,
    steam_id: Option<String>,
    avatar_url: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct MeResponse {
    id: i64,
    name: String,
    steam_id: Option<String>,
    avatar_url: Option<String>,
    rating: i64,
}

fn entry() -> Result<keyring::Entry, String> {
    keyring::Entry::new(KEYCHAIN_SERVICE, KEYCHAIN_USER).map_err(|e| e.to_string())
}

fn read_token() -> Option<String> {
    entry().ok()?.get_password().ok()
}

#[tauri::command]
pub async fn steam_login(app: AppHandle) -> Result<Account, String> {
    let listener = TcpListener::bind("127.0.0.1:0")
        .await
        .map_err(|e| format!("Could not open a local port: {e}"))?;
    let port = listener.local_addr().map_err(|e| e.to_string())?.port();

    app.opener()
        .open_url(format!("{API}/auth/steam/login?port={port}"), None::<&str>)
        .map_err(|e| format!("Could not open the browser: {e}"))?;

    let code = tokio::time::timeout(LOGIN_TIMEOUT, wait_for_code(&listener))
        .await
        .map_err(|_| "Sign-in timed out.".to_string())??;

    let client = reqwest::Client::new();
    let response = client
        .post(format!("{API}/auth/exchange"))
        .json(&serde_json::json!({ "code": code }))
        .send()
        .await
        .map_err(|e| format!("Could not reach the server: {e}"))?;

    if !response.status().is_success() {
        return Err("That sign-in did not complete. Please try again.".into());
    }

    let session: SessionResponse = response
        .json()
        .await
        .map_err(|e| format!("Unexpected reply from the server: {e}"))?;

    entry()?
        .set_password(&session.token)
        .map_err(|e| format!("Could not save the session: {e}"))?;

    let account = Account {
        id: session.player_id,
        name: session.name,
        steam_id: session.steam_id,
        avatar_url: session.avatar_url,
        rating: 0,
    };

    let _ = app.emit("flicked://signed-in", account.clone());
    Ok(account)
}

async fn wait_for_code(listener: &TcpListener) -> Result<String, String> {
    loop {
        let (mut stream, _) = listener
            .accept()
            .await
            .map_err(|e| format!("Local listener failed: {e}"))?;

        let Some(target) = read_request_target(&mut stream).await else {
            continue;
        };

        if let Some(code) = code_from_target(&target) {
            reply(&mut stream, "Signed in. You can close this tab and go back to FLICKED.").await;
            return Ok(code);
        }

        reply(&mut stream, "Waiting for Steam...").await;
    }
}

async fn read_request_target(stream: &mut TcpStream) -> Option<String> {
    let mut buffer = [0u8; 2048];
    let read = stream.read(&mut buffer).await.ok()?;
    let head = String::from_utf8_lossy(&buffer[..read]);
    head.lines().next()?.split_whitespace().nth(1).map(String::from)
}

fn code_from_target(target: &str) -> Option<String> {
    let (path, query) = target.split_once('?')?;
    if path != "/callback" {
        return None;
    }
    query
        .split('&')
        .filter_map(|pair| pair.split_once('='))
        .find(|(key, _)| *key == "code")
        .filter(|(_, value)| {
            !value.is_empty()
                && value.len() <= 128
                && value.chars().all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
        })
        .map(|(_, value)| value.to_string())
}

async fn reply(stream: &mut TcpStream, message: &str) {
    let body = format!(
        "<!doctype html><meta charset=utf-8>\
         <title>FLICKED</title>\
         <body style=\"background:#0c0c0d;color:#b4b4b8;font:16px system-ui;display:grid;place-items:center;height:100vh;margin:0\">\
         <p>{message}</p>"
    );
    let response = format!(
        "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
        body.len(),
        body
    );
    let _ = stream.write_all(response.as_bytes()).await;
    let _ = stream.flush().await;
}

#[tauri::command]
pub async fn current_account() -> Result<Option<Account>, String> {
    let Some(token) = read_token() else {
        return Ok(None);
    };

    let response = reqwest::Client::new()
        .get(format!("{API}/auth/me"))
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| format!("Could not reach the server: {e}"))?;

    if response.status() == reqwest::StatusCode::UNAUTHORIZED {
        let _ = entry().and_then(|e| e.delete_credential().map_err(|e| e.to_string()));
        return Ok(None);
    }

    if !response.status().is_success() {
        return Err("The server is not answering right now.".into());
    }

    let me: MeResponse = response.json().await.map_err(|e| e.to_string())?;
    Ok(Some(Account {
        id: me.id,
        name: me.name,
        steam_id: me.steam_id,
        avatar_url: me.avatar_url,
        rating: me.rating,
    }))
}

#[tauri::command]
pub async fn logout() -> Result<(), String> {
    if let Some(token) = read_token() {
        let _ = reqwest::Client::new()
            .post(format!("{API}/auth/logout"))
            .bearer_auth(&token)
            .send()
            .await;
    }
    if let Ok(entry) = entry() {
        match entry.delete_credential() {
            Ok(()) | Err(keyring::Error::NoEntry) => {}
            Err(e) => return Err(format!("Could not clear the session: {e}")),
        }
    }
    Ok(())
}

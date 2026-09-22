//! Calls that need the session token.
//!
//! Public endpoints (news, leaderboard) are fetched straight from the webview in
//! src/lib/api.ts. Anything tied to *you* goes through here instead, because the
//! token lives in the OS keychain and is never handed to JavaScript.
//!
//! Each endpoint gets its own command rather than one general "call this path"
//! command. A general one would let any script in the webview borrow the token for
//! any request, which is exactly what keeping it in Rust is meant to prevent.

use reqwest::{Method, StatusCode, Url};
use serde_json::Value;

use crate::auth::{read_token, API};

/// One authenticated request.
///
/// `Ok(None)` means "not signed in", which covers both having no token and the
/// server rejecting the one we have. `Err` carries a message worth showing: the
/// API answers a refused action with plain text ("You are already friends."),
/// so that text is passed through rather than replaced with a status code.
async fn send(method: Method, url: Url) -> Result<Option<Value>, String> {
    let Some(token) = read_token() else {
        return Ok(None);
    };

    let response = reqwest::Client::new()
        .request(method, url)
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| format!("Could not reach the server: {e}"))?;

    if response.status() == StatusCode::UNAUTHORIZED {
        return Ok(None);
    }

    if !response.status().is_success() {
        let status = response.status();
        let body = response.text().await.unwrap_or_default();
        return Err(if body.trim().is_empty() {
            format!("Request failed ({status}).")
        } else {
            body
        });
    }

    // 204 No Content has no body, so an empty reply is success, not a parse error.
    let body = response.text().await.map_err(|e| e.to_string())?;
    if body.trim().is_empty() {
        return Ok(Some(Value::Null));
    }
    serde_json::from_str(&body).map(Some).map_err(|e| e.to_string())
}

async fn get(path: &str) -> Result<Option<Value>, String> {
    send(Method::GET, url(path)?).await
}

/// `API` plus a path we wrote ourselves, so parsing can only fail if that
/// constant is wrong, which would be a bug rather than bad input.
fn url(path: &str) -> Result<Url, String> {
    Url::parse(&format!("{API}{path}")).map_err(|e| e.to_string())
}

/* Ids come from the webview, so they are checked before going into a URL.
   Nothing here can reach another host (the base is fixed), but a negative or
   zero id is never valid and is better refused early than sent. */
fn player_url(prefix: &str, player_id: i64, suffix: &str) -> Result<Url, String> {
    if player_id <= 0 {
        return Err("Unknown player.".into());
    }
    url(&format!("{prefix}{player_id}{suffix}"))
}

/// The signed-in player's own match history.
#[tauri::command]
pub async fn my_matches() -> Result<Option<Value>, String> {
    get("/api/matches").await
}

/// People you are friends with.
#[tauri::command]
pub async fn friends() -> Result<Option<Value>, String> {
    get("/api/friends").await
}

/// Pending requests, both the ones you sent and the ones waiting for you.
#[tauri::command]
pub async fn friend_requests() -> Result<Option<Value>, String> {
    get("/api/friends/requests").await
}

/// Find a player by name or Steam ID. Each result carries your relationship to
/// them, so the interface knows whether to offer Add, Accept or nothing.
#[tauri::command]
pub async fn search_players(query: String) -> Result<Option<Value>, String> {
    let target = Url::parse_with_params(
        &format!("{API}/api/friends/search"),
        &[("q", query.as_str())],
    )
    .map_err(|e| e.to_string())?;
    send(Method::GET, target).await
}

#[tauri::command]
pub async fn add_friend(player_id: i64) -> Result<Option<Value>, String> {
    send(Method::POST, player_url("/api/friends/requests/", player_id, "")?).await
}

#[tauri::command]
pub async fn accept_friend(player_id: i64) -> Result<Option<Value>, String> {
    send(Method::POST, player_url("/api/friends/requests/", player_id, "/accept")?).await
}

/// Declines a request sent to you, or cancels one you sent: the same row either way.
#[tauri::command]
pub async fn remove_friend_request(player_id: i64) -> Result<Option<Value>, String> {
    send(Method::DELETE, player_url("/api/friends/requests/", player_id, "")?).await
}

#[tauri::command]
pub async fn remove_friend(player_id: i64) -> Result<Option<Value>, String> {
    send(Method::DELETE, player_url("/api/friends/", player_id, "")?).await
}

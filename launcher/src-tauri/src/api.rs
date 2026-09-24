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

    read_reply(response).await
}

/// Ok(None) for "not signed in", Err with the server's own words when it refused.
async fn read_reply(response: reqwest::Response) -> Result<Option<Value>, String> {
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

/// Same as send(), with a JSON body.
async fn send_json(method: Method, url: Url, body: Value) -> Result<Option<Value>, String> {
    let Some(token) = read_token() else {
        return Ok(None);
    };

    let response = reqwest::Client::new()
        .request(method, url)
        .bearer_auth(&token)
        .json(&body)
        .send()
        .await
        .map_err(|e| format!("Could not reach the server: {e}"))?;

    read_reply(response).await
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

/* The friends panel's whole state, polled.
   
   Takes the ETag from the last answer and hands it back to the server, which
   replies 304 with no body when nothing has changed. That is the usual case, so
   the usual poll costs a request and no parsing, no IPC payload and no re-render
   in the webview.

   Three outcomes, kept distinct because the caller does something different for
   each: `Ok(None)` is "not signed in" as everywhere else in this file,
   `{"unchanged": true}` is 304, and anything else is a new state carrying the
   ETag to send next time. */
#[tauri::command]
pub async fn friends_state(etag: Option<String>) -> Result<Option<Value>, String> {
    let Some(token) = read_token() else {
        return Ok(None);
    };

    let mut request = reqwest::Client::new()
        .get(url("/api/friends/state")?)
        .bearer_auth(&token);

    // Only a value the server itself gave us is ever sent back.
    if let Some(tag) = etag.filter(|t| !t.is_empty()) {
        request = request.header(reqwest::header::IF_NONE_MATCH, tag);
    }

    let response = request
        .send()
        .await
        .map_err(|e| format!("Could not reach the server: {e}"))?;

    if response.status() == StatusCode::NOT_MODIFIED {
        return Ok(Some(serde_json::json!({ "unchanged": true })));
    }

    let tag = response
        .headers()
        .get(reqwest::header::ETAG)
        .and_then(|v| v.to_str().ok())
        .map(str::to_owned);

    let Some(mut state) = read_reply(response).await? else {
        return Ok(None);
    };

    if let (Some(object), Some(tag)) = (state.as_object_mut(), tag) {
        object.insert("etag".into(), Value::String(tag));
    }

    Ok(Some(state))
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

/* Matchmaking.

   Every one of these is about the signed-in player, so they go through here
   rather than the webview: the backend works out who is queueing from the token,
   and the interface cannot queue, accept or vote on anybody else's behalf. */

/// Where this player is: idle, searching, found, vote, connecting or live.
#[tauri::command]
pub async fn queue_state() -> Result<Option<Value>, String> {
    get("/api/queue").await
}

#[tauri::command]
pub async fn queue_join(mode: String) -> Result<Option<Value>, String> {
    send_json(Method::POST, url("/api/queue")?, serde_json::json!({ "mode": mode })).await
}

#[tauri::command]
pub async fn queue_leave() -> Result<Option<Value>, String> {
    send(Method::DELETE, url("/api/queue")?).await
}

#[tauri::command]
pub async fn queue_accept() -> Result<Option<Value>, String> {
    send(Method::POST, url("/api/queue/accept")?).await
}

#[tauri::command]
pub async fn queue_decline() -> Result<Option<Value>, String> {
    send(Method::POST, url("/api/queue/decline")?).await
}

#[tauri::command]
pub async fn queue_vote(map: String) -> Result<Option<Value>, String> {
    send_json(Method::POST, url("/api/queue/vote")?, serde_json::json!({ "map": map })).await
}

/* Joining the match server.

   Steam's own URL scheme, which is how every CS2 community server is joined:
   steam://connect/host:port, or with the server's game password appended. Steam
   hands it to CS2 if it is already running, and starts it if it is not.

   The address comes from the backend, but is checked here anyway: it ends up in
   a URL handed to the operating system, and "it came from our own API" is a
   weaker guarantee than looking. */
#[tauri::command]
pub async fn connect_to_match(
    app: tauri::AppHandle,
    address: String,
    password: Option<String>,
) -> Result<(), String> {
    use tauri_plugin_opener::OpenerExt;

    let (host, port) = address.split_once(':').ok_or("That server address is malformed.")?;

    let host_ok = !host.is_empty()
        && host.len() <= 253
        && host.chars().all(|c| c.is_ascii_alphanumeric() || c == '.' || c == '-');
    let port_ok = port.parse::<u16>().map(|p| p > 0).unwrap_or(false);
    if !host_ok || !port_ok {
        return Err("That server address is malformed.".into());
    }

    let url = match password.as_deref() {
        // a password with a slash or space in it would break the URL
        Some(p) if !p.is_empty() && p.chars().all(|c| c.is_ascii_graphic() && c != '/') =>
            format!("steam://connect/{address}/{p}"),
        _ => format!("steam://connect/{address}"),
    };

    app.opener()
        .open_url(url, None::<&str>)
        .map_err(|e| format!("Could not hand the server to Steam: {e}"))
}

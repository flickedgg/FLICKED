//! Calls that need the session token.
//!
//! Public endpoints (news, leaderboard) are fetched straight from the webview in
//! src/lib/api.ts. Anything tied to *you* goes through here instead, because the
//! token lives in the OS keychain and is never handed to JavaScript.
//!
//! Each endpoint gets its own command rather than one general "call this path"
//! command. A general one would let any script in the webview borrow the token for
//! any request, which is exactly what keeping it in Rust is meant to prevent.

use serde_json::Value;

use crate::auth::{read_token, API};

/// GET with the session token attached. The reply is passed through as JSON:
/// the shape is already agreed between the API and the TypeScript types, and
/// modelling it a third time in Rust would just be another thing to keep in step.
async fn authed_get(path: &str) -> Result<Option<Value>, String> {
    let Some(token) = read_token() else {
        return Ok(None); // not signed in: the caller shows a sign-in prompt
    };

    let response = reqwest::Client::new()
        .get(format!("{API}{path}"))
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| format!("Could not reach the server: {e}"))?;

    // The backend rejects an expired or revoked token the same way it rejects a
    // missing one, so this is "signed out" rather than an error to show.
    if response.status() == reqwest::StatusCode::UNAUTHORIZED {
        return Ok(None);
    }

    if !response.status().is_success() {
        return Err(format!("{path} failed: {}", response.status()));
    }

    response.json::<Value>().await.map(Some).map_err(|e| e.to_string())
}

/// The signed-in player's own match history. The backend works out whose it is
/// from the token, so there is no player id to pass, or to tamper with.
#[tauri::command]
pub async fn my_matches() -> Result<Option<Value>, String> {
    authed_get("/api/matches").await
}

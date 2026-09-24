mod api;
mod auth;
mod presence;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .manage(presence::Presence::default())
        .invoke_handler(tauri::generate_handler![
            presence::set_presence,
            presence::clear_presence,
            auth::steam_login,
            auth::current_account,
            auth::logout,
            api::my_matches,
            api::social_state,
            api::news,
            api::leaderboard,
            api::search_players,
            api::add_friend,
            api::accept_friend,
            api::remove_friend_request,
            api::remove_friend,
            api::party_invite,
            api::party_accept_invite,
            api::party_decline_invite,
            api::party_cancel_invite,
            api::party_remove_member,
            api::queue_state,
            api::queue_join,
            api::queue_leave,
            api::queue_accept,
            api::queue_decline,
            api::queue_vote,
            api::connect_to_match,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

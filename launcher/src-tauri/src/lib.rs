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
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

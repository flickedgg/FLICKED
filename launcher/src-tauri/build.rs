fn main() {
    // Without this, Cargo does not know the compiled-in API address came from
    // the environment, and a rebuild after changing it would keep the old one.
    println!("cargo:rerun-if-env-changed=FLICKED_API");
    tauri_build::build()
}

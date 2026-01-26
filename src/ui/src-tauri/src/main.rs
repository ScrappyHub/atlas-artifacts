#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize)]
pub struct IpcRequest {
    pub request_id: String,
    pub command: String,
    pub user_id: String,
    pub payload: serde_json::Value,
}

#[tauri::command]
fn agent_request(req: IpcRequest) -> Result<serde_json::Value, String> {
    // Windows named pipe path
    let pipe_path = r"\\.\pipe\atlas-update";

    let mut client = named_pipe::PipeClient::connect(pipe_path)
        .map_err(|e| format!("pipe connect failed: {e}"))?;

    let req_json = serde_json::to_string(&req).map_err(|e| e.to_string())?;
    use std::io::Write;
    client.write_all(req_json.as_bytes()).map_err(|e| e.to_string())?;

    // Closing write-end signals server we're done
    drop(client);

    // Reconnect to read response (simple pattern). If you prefer single duplex stream,
    // we can adjust the server and client code next step.
    let mut client = named_pipe::PipeClient::connect(pipe_path)
        .map_err(|e| format!("pipe reconnect failed: {e}"))?;

    use std::io::Read;
    let mut buf = String::new();
    client.read_to_string(&mut buf).map_err(|e| e.to_string())?;

    let val: serde_json::Value = serde_json::from_str(&buf).map_err(|e| e.to_string())?;
    Ok(val)
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![agent_request])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

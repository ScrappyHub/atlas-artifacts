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
    let pipe_path = r"\\.\pipe\atlas-update";

    let mut client = named_pipe::PipeClient::connect(pipe_path)
        .map_err(|e| format!("pipe connect failed: {e}"))?;

    let req_json = serde_json::to_vec(&req).map_err(|e| e.to_string())?;
    let len = req_json.len() as u32;
    let len_bytes = len.to_le_bytes();

    use std::io::{Read, Write};

    client.write_all(&len_bytes).map_err(|e| e.to_string())?;
    client.write_all(&req_json).map_err(|e| e.to_string())?;
    client.flush().map_err(|e| e.to_string())?;

    // Read response frame
    let mut rlen_bytes = [0u8; 4];
    client.read_exact(&mut rlen_bytes).map_err(|e| e.to_string())?;
    let rlen = u32::from_le_bytes(rlen_bytes) as usize;
    if rlen == 0 || rlen > 10_000_000 {
        return Err(format!("invalid response length: {}", rlen));
    }

    let mut payload = vec![0u8; rlen];
    client.read_exact(&mut payload).map_err(|e| e.to_string())?;

    let val: serde_json::Value = serde_json::from_slice(&payload).map_err(|e| e.to_string())?;
    Ok(val)
}

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![agent_request])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

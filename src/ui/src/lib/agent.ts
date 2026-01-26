import { invoke } from "@tauri-apps/api/tauri";

export type AgentResponse = {
  request_id: string;
  ok: boolean;
  error?: { code: string; message: string } | null;
  data?: any;
};

function uuid(): string {
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export async function callAgent(command: string, payload: any = {}): Promise<AgentResponse> {
  const req = {
    request_id: uuid(),
    command,
    user_id: "local-user",
    payload: payload ?? {}
  };

  const res = await invoke("agent_request", { req });
  return res as AgentResponse;
}

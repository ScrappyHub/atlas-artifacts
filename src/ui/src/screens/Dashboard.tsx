import { useEffect, useState } from "react";
import { callAgent } from "../lib/agent";

export default function Dashboard() {
  const [status, setStatus] = useState<any>(null);
  const [license, setLicense] = useState<any>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const s = await callAgent("get_status");
        if (!s.ok) throw new Error(s.error?.message ?? "status failed");
        setStatus(s.data);

        const l = await callAgent("get_license_status");
        if (!l.ok) throw new Error(l.error?.message ?? "license failed");
        setLicense(l.data);
      } catch (e: any) {
        setErr(e?.message ?? String(e));
      }
    })();
  }, []);

  return (
    <div style={{ padding: 16, fontFamily: "system-ui, sans-serif" }}>
      <h1>Atlas Update</h1>

      {err && (
        <div style={{ padding: 12, border: "1px solid #ccc", marginBottom: 12 }}>
          <strong>Error:</strong> {err}
          <div style={{ marginTop: 8, fontSize: 12, opacity: 0.8 }}>
            Make sure Atlas.Agent is running (and the named pipe is available).
          </div>
        </div>
      )}

      <section style={{ marginBottom: 16 }}>
        <h2>Status</h2>
        <pre style={{ padding: 12, border: "1px solid #ddd", overflowX: "auto" }}>
{JSON.stringify(status, null, 2)}
        </pre>
      </section>

      <section>
        <h2>License</h2>
        <pre style={{ padding: 12, border: "1px solid #ddd", overflowX: "auto" }}>
{JSON.stringify(license, null, 2)}
        </pre>
      </section>
    </div>
  );
}

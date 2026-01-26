import { useEffect, useState } from "react";
import { callAgent } from "../lib/agent";

export default function Dashboard() {
  const [status, setStatus] = useState<any>(null);
  const [license, setLicense] = useState<any>(null);
  const [scanResult, setScanResult] = useState<any>(null);
  const [runs, setRuns] = useState<any[]>([]);
  const [runDetails, setRunDetails] = useState<any>(null);
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function loadBase() {
    const s = await callAgent("get_status");
    if (!s.ok) throw new Error(s.error?.message ?? "status failed");
    setStatus(s.data);

    const l = await callAgent("get_license_status");
    if (!l.ok) throw new Error(l.error?.message ?? "license failed");
    setLicense(l.data);

    const h = await callAgent("get_run_history");
    if (h.ok) setRuns(h.data?.runs ?? []);
  }

  useEffect(() => {
    (async () => {
      try {
        await loadBase();
      } catch (e: any) {
        setErr(e?.message ?? String(e));
      }
    })();
  }, []);

  async function scanNow() {
    setErr(null);
    setBusy(true);
    try {
      const res = await callAgent("request_scan");
      if (!res.ok) throw new Error(res.error?.message ?? "scan failed");
      setScanResult(res.data);

      const h = await callAgent("get_run_history");
      if (h.ok) setRuns(h.data?.runs ?? []);

      if (res.data?.run_id) {
        const d = await callAgent("get_run_details", { run_id: res.data.run_id });
        if (d.ok) setRunDetails(d.data?.run ?? null);
      }
    } catch (e: any) {
      setErr(e?.message ?? String(e));
    } finally {
      setBusy(false);
    }
  }

  async function openRun(run_id: string) {
    setErr(null);
    try {
      const d = await callAgent("get_run_details", { run_id });
      if (!d.ok) throw new Error(d.error?.message ?? "get_run_details failed");
      setRunDetails(d.data?.run ?? null);
    } catch (e: any) {
      setErr(e?.message ?? String(e));
    }
  }

  return (
    <div style={{ padding: 16, fontFamily: "system-ui, sans-serif", maxWidth: 1100 }}>
      <h1>Atlas Update</h1>

      {err && (
        <div style={{ padding: 12, border: "1px solid #ccc", marginBottom: 12 }}>
          <strong>Error:</strong> {err}
          <div style={{ marginTop: 8, fontSize: 12, opacity: 0.8 }}>
            Ensure Atlas.Agent is running and winget is available.
          </div>
        </div>
      )}

      <div style={{ display: "flex", gap: 12, marginBottom: 16 }}>
        <button onClick={scanNow} disabled={busy} style={{ padding: "8px 12px" }}>
          {busy ? "Scanning..." : "Scan Now"}
        </button>
      </div>

      <section style={{ marginBottom: 16 }}>
        <h2>Status</h2>
        <pre style={{ padding: 12, border: "1px solid #ddd", overflowX: "auto" }}>
{JSON.stringify(status, null, 2)}
        </pre>
      </section>

      <section style={{ marginBottom: 16 }}>
        <h2>License</h2>
        <pre style={{ padding: 12, border: "1px solid #ddd", overflowX: "auto" }}>
{JSON.stringify(license, null, 2)}
        </pre>
      </section>

      <section style={{ marginBottom: 16 }}>
        <h2>Latest Scan</h2>
        <pre style={{ padding: 12, border: "1px solid #ddd", overflowX: "auto" }}>
{JSON.stringify(scanResult, null, 2)}
        </pre>
      </section>

      <section style={{ display: "flex", gap: 16 }}>
        <div style={{ flex: 1 }}>
          <h2>Run History</h2>
          <div style={{ border: "1px solid #ddd" }}>
            {runs.length === 0 && <div style={{ padding: 12 }}>No runs yet.</div>}
            {runs.map((r) => (
              <div key={r.run_id} style={{ padding: 12, borderTop: "1px solid #eee" }}>
                <div style={{ display: "flex", justifyContent: "space-between" }}>
                  <code>{r.run_id}</code>
                  <span>{r.outcome}</span>
                </div>
                <div style={{ fontSize: 12, opacity: 0.8 }}>{r.created_at} • {r.run_type}</div>
                <button style={{ marginTop: 8 }} onClick={() => openRun(r.run_id)}>
                  Open
                </button>
              </div>
            ))}
          </div>
        </div>

        <div style={{ flex: 1 }}>
          <h2>Run Details</h2>
          <pre style={{ padding: 12, border: "1px solid #ddd", overflowX: "auto", minHeight: 240 }}>
{JSON.stringify(runDetails, null, 2)}
          </pre>
        </div>
      </section>
    </div>
  );
}

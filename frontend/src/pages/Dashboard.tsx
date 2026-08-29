import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { api, type Project, type Lookup } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { statusBadge, formatDate } from "../lib/ui";

export default function Dashboard() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const [projects, setProjects] = useState<Project[]>([]);
  const [statusMap, setStatusMap] = useState<Record<string, Lookup>>({});
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const [paged, statuses] = await Promise.all([api.projects({ pageSize: 100 }), api.projectStatuses()]);
        setProjects(paged.data);
        setStatusMap(Object.fromEntries(statuses.map((s) => [s.id, s])));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const total = projects.length;
  const codeOf = (p: Project) => statusMap[p.statusId ?? ""]?.code;
  const active = projects.filter((p) => codeOf(p) === "ACTIVE").length;
  const completed = projects.filter((p) => codeOf(p) === "COMPLETED").length;
  const avg = total ? Math.round(projects.reduce((s, p) => s + Number(p.completionPercentage), 0) / total) : 0;

  const kpis = [
    { label: "Total Projects", value: total, sub: "All workspaces" },
    { label: "Active", value: active, sub: "In progress" },
    { label: "Completed", value: completed, sub: "Delivered" },
    { label: "Avg Progress", value: `${avg}%`, sub: "Across portfolio" }
  ];

  const recent = [...projects].sort((a, b) => +new Date(b.createdAt) - +new Date(a.createdAt)).slice(0, 5);

  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">Welcome back{user?.displayName ? `, ${user.displayName}` : ""}</h1>
          <div className="page-sub">Here's how your construction projects are performing.</div>
        </div>
        <div className="head-actions">
          <Link className="btn primary" to="/projects">View Projects</Link>
        </div>
      </div>

      <div className="kpis">
        {kpis.map((k) => (
          <div className="kpi" key={k.label}>
            <div className="label">{k.label}</div>
            <div className="value">{loading ? <span className="skeleton" style={{ display: "inline-block", width: 48, height: 24 }} /> : k.value}</div>
            <div className="sub">{k.sub}</div>
          </div>
        ))}
      </div>

      <div className="card">
        <div className="card-pad" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: "1px solid var(--border)" }}>
          <strong>Recent Projects</strong>
          <Link className="btn-ghost btn" to="/projects">View all</Link>
        </div>
        {loading ? (
          <div className="card-pad muted">Loading…</div>
        ) : recent.length === 0 ? (
          <div className="empty">No projects yet.</div>
        ) : (
          <table>
            <thead>
              <tr><th>Project</th><th>Status</th><th>Progress</th><th>Due Date</th></tr>
            </thead>
            <tbody>
              {recent.map((p) => {
                const b = statusBadge(codeOf(p));
                const pct = Math.round(Number(p.completionPercentage));
                return (
                  <tr key={p.id} className="clickable" onClick={() => navigate(`/projects/${p.id}`)}>
                    <td>
                      <div className="cell-title">{p.name}</div>
                      <div className="cell-code">{p.code}</div>
                    </td>
                    <td><span className={`badge ${b.cls}`}><span className="dot" />{b.label}</span></td>
                    <td>
                      <div className="progress">
                        <span className="pct">{pct}%</span>
                        <div className="bar"><span style={{ width: `${pct}%` }} /></div>
                      </div>
                    </td>
                    <td>{formatDate(p.plannedEndDate)}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

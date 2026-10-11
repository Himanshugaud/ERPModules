import { useEffect, useState } from "react";
import { api, ApiError, type AttachmentCategory, type ProjectAttachment, type UserItem } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { fileIcon, formatDateTime, formatFileSize } from "../lib/ui";

const SECTIONS: { category: AttachmentCategory; title: string }[] = [
  { category: "REQUIREMENT", title: "Requirement Attachments" },
  { category: "PLANNING", title: "Planning Attachments" },
  { category: "PROCUREMENT", title: "Procurement Documents" },
  { category: "TRANSPORT", title: "Transport Documents" },
  { category: "OTHER", title: "Other Attachments" }
];

// Read-only view: every artifact on the project, grouped by stage, each with a download button.
export default function ProjectArtifacts({ projectId, users = [] }: { projectId: string; users?: UserItem[] }) {
  const { user } = useAuth();
  const canRead = (user?.permissions ?? []).includes("document.read");
  const [items, setItems] = useState<ProjectAttachment[]>([]);
  const [loading, setLoading] = useState(canRead);
  const [error, setError] = useState<string | null>(null);
  const [open, setOpen] = useState<Record<string, boolean>>(() => Object.fromEntries(SECTIONS.map((s) => [s.category, true])));
  const allOpen = SECTIONS.every((s) => open[s.category]);
  const setAll = (value: boolean) => setOpen(Object.fromEntries(SECTIONS.map((s) => [s.category, value])));
  const userMap = Object.fromEntries(users.map((u) => [u.id, u]));

  useEffect(() => {
    if (!canRead) return;
    setLoading(true);
    api.projectAttachments(projectId)
      .then((list) => { setItems(list); setError(null); })
      .catch((err) => setError(err instanceof ApiError ? err.message : "Unable to load attachments."))
      .finally(() => setLoading(false));
  }, [projectId, canRead]);

  async function download(a: ProjectAttachment) {
    setError(null);
    try { await api.downloadAttachment(a); } catch (err) { setError(err instanceof ApiError ? err.message : "Download failed."); }
  }

  if (!canRead) return <div className="muted" style={{ fontSize: 13 }}>You don't have access to view attachments.</div>;
  if (loading) return <div className="skeleton" style={{ height: 60 }} />;

  return (
    <div>
      {error && <div className="form-error">{error}</div>}
      <div style={{ textAlign: "right", marginBottom: 8 }}>
        <button type="button" className="btn-link" onClick={() => setAll(!allOpen)}>{allOpen ? "Collapse all" : "Expand all"}</button>
      </div>
      <div className="artifact-grid">
        {SECTIONS.map((s) => {
          const docs = items.filter((a) => a.category === s.category);
          const isOpen = open[s.category];
          return (
            <div key={s.category} className="artifact-section">
              <button
                type="button"
                className="artifact-head"
                aria-expanded={isOpen}
                onClick={() => setOpen((o) => ({ ...o, [s.category]: !o[s.category] }))}
              >
                <span><span className={`chev ${isOpen ? "open" : ""}`}>▸</span> 📁 {s.title}</span>
                <span className={`badge ${docs.length ? "blue" : "gray"}`}>{docs.length}</span>
              </button>
              {isOpen && (docs.length === 0 ? (
                <div className="muted" style={{ fontSize: 13, padding: "10px 12px" }}>No documents</div>
              ) : (
                docs.map((a) => (
                  <div key={a.id} className="attach-row">
                    <div style={{ display: "flex", gap: 10, alignItems: "center", minWidth: 0 }}>
                      <span className="file-ico">{fileIcon(a.fileName)}</span>
                      <div style={{ minWidth: 0 }}>
                        <div className="attach-name" title={a.fileName}>{a.fileName}</div>
                        <div className="muted" style={{ fontSize: 12 }}>
                          {formatFileSize(a.fileSize)} · {userMap[a.uploadedBy ?? ""]?.displayName ?? "—"} · {formatDateTime(a.createdAt)}
                          {a.description ? ` · ${a.description}` : ""}
                        </div>
                      </div>
                    </div>
                    <button type="button" className="btn btn-sm" onClick={() => download(a)}>⭳ Download</button>
                  </div>
                ))
              ))}
            </div>
          );
        })}
      </div>
    </div>
  );
}

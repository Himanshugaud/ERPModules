import { useEffect, useRef, useState } from "react";
import { api, ApiError, type AttachmentCategory, type ProjectAttachment, type UserItem } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { formatDateTime, formatFileSize } from "../lib/ui";

export const CATEGORY_LABELS: Record<AttachmentCategory, string> = {
  REQUIREMENT: "Requirement Attachments",
  PLANNING: "Planning Attachments",
  PROCUREMENT: "Procurement Documents",
  TRANSPORT: "Transport Documents",
  OTHER: "Other Attachments"
};

// Mirrors the backend rule: each category is uploaded by the team that owns that workflow stage.
const UPLOAD_PERMISSIONS: Record<AttachmentCategory, string[]> = {
  REQUIREMENT: ["project.create"],
  PLANNING: ["plan.manage"],
  PROCUREMENT: ["purchaseorder.create", "purchaseorder.update", "goodsreceipt.create"],
  TRANSPORT: ["transfer.dispatch", "transfer.receive", "transfer.create"],
  OTHER: []
};

export function uploadableCategories(permissions: string[]): AttachmentCategory[] {
  if (!permissions.includes("document.create")) return [];
  return (Object.keys(UPLOAD_PERMISSIONS) as AttachmentCategory[]).filter(
    (c) => UPLOAD_PERMISSIONS[c].length === 0 || UPLOAD_PERMISSIONS[c].some((p) => permissions.includes(p))
  );
}

export const MAX_UPLOAD_MB = 25;

export function validateFile(file: File): string | null {
  if (file.size === 0) return `${file.name} is empty.`;
  if (file.size > MAX_UPLOAD_MB * 1024 * 1024) return `${file.name} is larger than ${MAX_UPLOAD_MB} MB.`;
  return null;
}

type Props = {
  projectId: string;
  /** Limit the list (and the upload choices) to these categories. */
  categories?: AttachmentCategory[];
  /** Category preselected in the upload form. */
  defaultCategory?: AttachmentCategory;
  users?: UserItem[];
  title?: string;
  /** Planning files can be removed while the plan is still a draft. */
  deletable?: boolean;
  onChanged?: () => void;
};

export default function AttachmentsPanel({ projectId, categories, defaultCategory, users = [], title, deletable, onChanged }: Props) {
  const { user } = useAuth();
  const permissions = user?.permissions ?? [];
  const canRead = permissions.includes("document.read");
  const allowed = uploadableCategories(permissions).filter((c) => !categories || categories.includes(c));
  const [items, setItems] = useState<ProjectAttachment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [category, setCategory] = useState<AttachmentCategory>(
    defaultCategory && allowed.includes(defaultCategory) ? defaultCategory : allowed[0] ?? "OTHER"
  );
  const [description, setDescription] = useState("");
  const fileInput = useRef<HTMLInputElement>(null);
  const userMap = Object.fromEntries(users.map((u) => [u.id, u]));

  async function load() {
    if (!canRead) { setLoading(false); return; }
    setLoading(true);
    try {
      const all = await api.projectAttachments(projectId);
      setItems(categories ? all.filter((a) => categories.includes(a.category)) : all);
      setError(null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to load attachments.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { load(); }, [projectId]);

  async function onFiles(files: FileList | null) {
    if (!files || files.length === 0) return;
    setError(null);
    for (const f of Array.from(files)) {
      const problem = validateFile(f);
      if (problem) { setError(problem); return; }
    }
    setUploading(true);
    try {
      for (const f of Array.from(files)) await api.uploadAttachment(projectId, category, f, description);
      setDescription("");
      await load();
      onChanged?.();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Upload failed.");
    } finally {
      setUploading(false);
      if (fileInput.current) fileInput.current.value = "";
    }
  }

  async function download(a: ProjectAttachment) {
    setError(null);
    try { await api.downloadAttachment(a); } catch (err) { setError(err instanceof ApiError ? err.message : "Download failed."); }
  }

  async function remove(a: ProjectAttachment) {
    if (!window.confirm(`Delete ${a.fileName}?`)) return;
    setError(null);
    try { await api.deleteAttachment(a.id); await load(); onChanged?.(); }
    catch (err) { setError(err instanceof ApiError ? err.message : "Unable to delete the file."); }
  }

  const grouped = items.reduce<Record<string, ProjectAttachment[]>>((acc, a) => {
    (acc[a.category] ??= []).push(a);
    return acc;
  }, {});

  return (
    <div>
      {title && <div style={{ fontWeight: 600, marginBottom: 8 }}>{title}</div>}
      {error && <div className="form-error">{error}</div>}

      {allowed.length > 0 && (
        <div className="attach-upload">
          {allowed.length > 1 && (
            <select value={category} onChange={(e) => setCategory(e.target.value as AttachmentCategory)} aria-label="Attachment category">
              {allowed.map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
            </select>
          )}
          <input value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Description (optional)" maxLength={200} />
          <input ref={fileInput} type="file" multiple hidden onChange={(e) => onFiles(e.target.files)} />
          <button type="button" className="btn btn-sm primary" disabled={uploading} onClick={() => fileInput.current?.click()}>
            {uploading ? <span className="spinner" /> : "⭱ Upload file"}
          </button>
        </div>
      )}

      {loading ? (
        <div className="skeleton" style={{ height: 40 }} />
      ) : !canRead ? (
        <div className="muted" style={{ fontSize: 13 }}>You don't have access to view attachments.</div>
      ) : items.length === 0 ? (
        <div className="muted" style={{ fontSize: 13, padding: "6px 0" }}>No attachments yet.</div>
      ) : (
        Object.entries(grouped).map(([cat, list]) => (
          <div key={cat} style={{ marginTop: 10 }}>
            <div className="meta-label">📁 {CATEGORY_LABELS[cat as AttachmentCategory] ?? cat}</div>
            <div className="attach-list">
              {list.map((a) => (
                <div key={a.id} className="attach-row">
                  <div style={{ minWidth: 0 }}>
                    <button type="button" className="btn-link attach-name" onClick={() => download(a)} title="Download">⭳ {a.fileName}</button>
                    <div className="muted" style={{ fontSize: 12 }}>
                      {formatFileSize(a.fileSize)} · {userMap[a.uploadedBy ?? ""]?.displayName ?? "—"} · {formatDateTime(a.createdAt)}
                      {a.description ? ` · ${a.description}` : ""}
                    </div>
                  </div>
                  {deletable && a.category === "PLANNING" && (
                    <button type="button" className="btn btn-sm danger" onClick={() => remove(a)}>Delete</button>
                  )}
                </div>
              ))}
            </div>
          </div>
        ))
      )}
    </div>
  );
}

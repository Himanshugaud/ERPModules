import { useEffect, useRef, useState, type DragEvent } from "react";
import { api, ApiError, type ProjectAttachment } from "../api/client";
import { formatDateTime, formatFileSize, fileIcon } from "../lib/ui";
import { MAX_UPLOAD_MB, validateFile } from "./AttachmentsPanel";

type Row = {
  key: string;
  name: string;
  size: number;
  status: "uploading" | "done" | "error";
  attachment?: ProjectAttachment;
  error?: string;
};

type Props = {
  /** Id of the requirement (draft); files are stored against it until it becomes a project. */
  draftId: string;
  readOnly?: boolean;
  disabled?: boolean;
  onCountChange?: (count: number) => void;
};

// Files upload as soon as they are added, so a saved draft always has its documents.
export default function RequirementFiles({ draftId, readOnly, disabled, onCountChange }: Props) {
  const [rows, setRows] = useState<Row[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    api.draftAttachments(draftId)
      .then((list) => {
        if (cancelled) return;
        setRows(list.slice().reverse().map((a) => ({ key: a.id, name: a.fileName, size: a.fileSize, status: "done" as const, attachment: a })));
      })
      .catch(() => { if (!cancelled) setRows([]); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [draftId]);

  useEffect(() => { onCountChange?.(rows.filter((r) => r.status === "done").length); }, [rows]);

  const patch = (key: string, next: Partial<Row>) => setRows((rs) => rs.map((r) => (r.key === key ? { ...r, ...next } : r)));

  async function addFiles(files: FileList | File[] | null) {
    if (!files || disabled || readOnly) return;
    setError(null);
    for (const file of Array.from(files)) {
      const problem = validateFile(file);
      const key = `${file.name}-${file.size}-${Date.now()}-${Math.random()}`;
      if (problem) { setError(problem); continue; }
      setRows((rs) => [...rs, { key, name: file.name, size: file.size, status: "uploading" }]);
      try {
        const attachment = await api.uploadDraftAttachment(draftId, file);
        patch(key, { status: "done", attachment, key: attachment.id });
      } catch (err) {
        patch(key, { status: "error", error: err instanceof ApiError ? err.message : "Upload failed" });
      }
    }
    if (input.current) input.current.value = "";
  }

  async function remove(row: Row) {
    if (row.status === "error") { setRows((rs) => rs.filter((r) => r.key !== row.key)); return; }
    if (!row.attachment || !window.confirm(`Remove "${row.name}" from this requirement?`)) return;
    try {
      await api.deleteAttachment(row.attachment.id);
      setRows((rs) => rs.filter((r) => r.key !== row.key));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to remove the file.");
    }
  }

  async function download(a: ProjectAttachment) {
    try { await api.downloadAttachment(a); } catch (err) { setError(err instanceof ApiError ? err.message : "Download failed."); }
  }

  const onDrop = (e: DragEvent) => { e.preventDefault(); setDragging(false); addFiles(e.dataTransfer.files); };

  return (
    <div>
      {error && <div className="form-error">{error}</div>}

      {!readOnly && (
        <div
          className={`dropzone ${dragging ? "drag" : ""} ${disabled ? "disabled" : ""}`}
          onDragOver={(e) => { e.preventDefault(); if (!disabled) setDragging(true); }}
          onDragLeave={() => setDragging(false)}
          onDrop={onDrop}
          onClick={() => !disabled && input.current?.click()}
          role="button"
          tabIndex={0}
          onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") input.current?.click(); }}
        >
          <div className="dropzone-icon">⭱</div>
          <div><strong>Drop files here</strong> or <span className="dropzone-link">browse</span></div>
          <div className="muted" style={{ fontSize: 12 }}>Drawings, BOQ, client documents · up to {MAX_UPLOAD_MB} MB each · uploaded immediately</div>
          <input ref={input} type="file" multiple hidden onChange={(e) => addFiles(e.target.files)} />
        </div>
      )}

      {loading ? (
        <div className="skeleton" style={{ height: 36, marginTop: 8 }} />
      ) : rows.length === 0 ? (
        <div className="muted" style={{ fontSize: 13, marginTop: 8 }}>{readOnly ? "No attachments." : "No files added yet."}</div>
      ) : (
        <div className="attach-list" style={{ marginTop: 10 }}>
          {rows.map((r) => (
            <div key={r.key} className="attach-row">
              <div style={{ display: "flex", gap: 10, alignItems: "center", minWidth: 0 }}>
                <span className="file-ico">{fileIcon(r.name)}</span>
                <div style={{ minWidth: 0 }}>
                  {r.attachment ? (
                    <button type="button" className="btn-link attach-name" onClick={() => download(r.attachment!)} title="Download">{r.name}</button>
                  ) : (
                    <div className="attach-name">{r.name}</div>
                  )}
                  <div className="muted" style={{ fontSize: 12 }}>
                    {formatFileSize(r.size)}
                    {r.attachment ? ` · ${formatDateTime(r.attachment.createdAt)}` : ""}
                    {r.status === "uploading" && " · uploading…"}
                    {r.status === "error" && <span style={{ color: "var(--red)" }}> · {r.error}</span>}
                  </div>
                </div>
              </div>
              <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                {r.status === "uploading" && <span className="spinner" />}
                {r.status === "done" && <span className="badge green">Saved</span>}
                {!readOnly && r.status !== "uploading" && (
                  <button type="button" className="icon-btn" title="Remove" onClick={() => remove(r)}>✕</button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

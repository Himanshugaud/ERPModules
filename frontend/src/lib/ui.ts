export function initials(name?: string, fallback = "U"): string {
  if (!name) return fallback;
  const parts = name.trim().split(/\s+/);
  const first = parts[0]?.[0] ?? "";
  const second = parts.length > 1 ? parts[parts.length - 1][0] : "";
  return (first + second).toUpperCase() || fallback;
}

export function statusBadge(code?: string): { cls: string; label: string } {
  switch (code) {
    case "ACTIVE":
      return { cls: "green", label: "Active" };
    case "PLANNING":
      return { cls: "blue", label: "Planning" };
    case "INVENTORY_CHECK":
      return { cls: "amber", label: "Inventory Check" };
    case "SHIPMENT_IN_TRANSIT":
      return { cls: "blue", label: "Shipment In Transit" };
    case "SHIPMENT_COMPLETED":
      return { cls: "green", label: "Shipment Completed" };
    case "ON_HOLD":
      return { cls: "amber", label: "On Hold" };
    case "COMPLETED":
      return { cls: "gray", label: "Completed" };
    case "CANCELLED":
      return { cls: "red", label: "Cancelled" };
    default:
      return { cls: "gray", label: code ?? "—" };
  }
}

export function taskStatusBadge(code?: string): { cls: string; label: string } {
  switch (code) {
    case "TODO":
      return { cls: "gray", label: "To Do" };
    case "IN_PROGRESS":
      return { cls: "blue", label: "In Progress" };
    case "BLOCKED":
      return { cls: "red", label: "Blocked" };
    case "IN_REVIEW":
      return { cls: "amber", label: "In Review" };
    case "DONE":
      return { cls: "green", label: "Done" };
    case "CANCELLED":
      return { cls: "gray", label: "Cancelled" };
    default:
      return { cls: "gray", label: code ?? "—" };
  }
}

export function formatDate(iso?: string): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "—";
  return d.toLocaleDateString(undefined, { day: "2-digit", month: "short", year: "numeric" });
}

export function formatDateTime(iso?: string): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "—";
  return d.toLocaleString(undefined, { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
}


export function formatFileSize(bytes?: number): string {
  if (!bytes || bytes < 0) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export function fileIcon(name: string): string {
  const ext = name.split(".").pop()?.toLowerCase() ?? "";
  if (["pdf"].includes(ext)) return "📕";
  if (["png", "jpg", "jpeg", "gif", "webp", "svg", "bmp"].includes(ext)) return "🖼";
  if (["xls", "xlsx", "csv"].includes(ext)) return "📊";
  if (["doc", "docx", "txt", "rtf", "md"].includes(ext)) return "📄";
  if (["dwg", "dxf", "rvt", "skp"].includes(ext)) return "📐";
  if (["zip", "rar", "7z"].includes(ext)) return "🗜";
  return "📎";
}

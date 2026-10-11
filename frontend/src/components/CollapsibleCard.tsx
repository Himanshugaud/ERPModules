import { useState, type ReactNode } from "react";

type Props = {
  /** Remembers the open/closed state across visits (per section key). */
  storageKey: string;
  title: string;
  count?: number;
  subtitle?: string;
  right?: ReactNode;
  defaultOpen?: boolean;
  /** Body without padding (e.g. a full-width table). */
  flush?: boolean;
  children: ReactNode;
};

function readOpen(key: string, fallback: boolean): boolean {
  try {
    const v = localStorage.getItem(`erp_section_${key}`);
    return v === null ? fallback : v === "1";
  } catch {
    return fallback;
  }
}

export default function CollapsibleCard({ storageKey, title, count, subtitle, right, defaultOpen = true, flush, children }: Props) {
  const [open, setOpen] = useState(() => readOpen(storageKey, defaultOpen));

  function toggle() {
    setOpen((o) => {
      try { localStorage.setItem(`erp_section_${storageKey}`, o ? "0" : "1"); } catch { /* ignore */ }
      return !o;
    });
  }

  return (
    <div className="card" style={{ marginTop: 18 }}>
      <div className="section-head" style={{ borderBottom: open ? "1px solid var(--border)" : "none" }}>
        <button type="button" className="section-toggle" onClick={toggle} aria-expanded={open}>
          <span className={`chev ${open ? "open" : ""}`}>▸</span>
          <span>
            <strong>{title}</strong>
            {count !== undefined && <span className="muted" style={{ marginLeft: 8 }}>({count})</span>}
            {subtitle && <span className="muted section-sub">{subtitle}</span>}
          </span>
        </button>
        {right && <div className="section-right">{right}</div>}
      </div>
      {/* Kept mounted so collapsing doesn't discard state or refetch. */}
      <div hidden={!open} className={flush ? undefined : "card-pad"}>{children}</div>
    </div>
  );
}

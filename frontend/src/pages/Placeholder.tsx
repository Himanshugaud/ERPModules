export default function Placeholder({ title }: { title: string }) {
  return (
    <>
      <div className="page-head">
        <div>
          <h1 className="page-title">{title}</h1>
          <div className="page-sub">This module is coming soon.</div>
        </div>
      </div>
      <div className="card">
        <div className="empty">
          <div style={{ fontSize: 32, marginBottom: 8 }}>🚧</div>
          {title} will be available in a future release.
        </div>
      </div>
    </>
  );
}

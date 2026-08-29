import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { ApiError } from "../api/client";

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [organizationCode, setOrg] = useState("DEMO");
  const [email, setEmail] = useState("demo@demo.local");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await login(organizationCode.trim(), email.trim());
      navigate("/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Unable to sign in.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-wrap">
      <form className="login-card" onSubmit={onSubmit}>
        <div className="login-brand">
          <div className="brand-logo">C</div>
          <div>
            <div className="brand-name">Construction ERP</div>
            <div className="brand-ver">v1.0.0</div>
          </div>
        </div>
        <div className="login-title">Sign in</div>
        <div className="login-sub">Access your construction projects.</div>

        {error && <div className="form-error">{error}</div>}

        <div className="field">
          <label>Organization Code</label>
          <input value={organizationCode} onChange={(e) => setOrg(e.target.value)} placeholder="DEMO" required />
        </div>
        <div className="field">
          <label>Email</label>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder="you@company.com" required />
        </div>

        <button className="btn primary" style={{ width: "100%", justifyContent: "center" }} disabled={loading}>
          {loading ? <span className="spinner" /> : "Sign in"}
        </button>

        <div className="login-hint">Uses Microsoft Entra ID in production. Demo: DEMO / demo@demo.local</div>
      </form>
    </div>
  );
}

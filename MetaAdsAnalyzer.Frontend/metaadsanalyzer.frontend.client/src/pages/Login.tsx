import { type FormEvent, useState } from 'react'
import { Link, Navigate, useNavigate, useLocation } from 'react-router-dom'
import { loginAccount } from '../api/client'
import { useAuth } from '../context/UserContext'
import './Pages.css'

export function Login() {
  const navigate = useNavigate()
  const location = useLocation()
  const { setSession, isAuthenticated } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [err, setErr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const from = (location.state as { from?: string } | null)?.from ?? '/app'

  if (isAuthenticated) {
    return <Navigate to={from} replace />
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setErr(null)
    setBusy(true)
    try {
      const r = await loginAccount(email.trim(), password)
      setSession(r.accessToken, r.userId, r.email)
      navigate(from, { replace: true })
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : 'Giriş başarısız')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="page auth-page auth-page--framed">
      <h1 className="page-title">Giriş</h1>
      <p className="page-lead">E-posta ve şifre veya Meta OAuth ile devam edin.</p>

      <form className="panel auth-form" onSubmit={onSubmit}>
        {err && <p className="error-banner">{err}</p>}
        <label>
          E-posta
          <input
            type="email"
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </label>
        <label>
          Şifre
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </label>
        <button type="submit" className="btn primary" disabled={busy}>
          {busy ? 'Giriş…' : 'Giriş yap'}
        </button>
        <p className="muted small">
          Hesabınız yok mu? <Link to="/register">Kayıt ol</Link>
        </p>
        <p className="muted small">
          <Link to="/connect">Meta ile bağlan</Link>
        </p>
      </form>
    </div>
  )
}

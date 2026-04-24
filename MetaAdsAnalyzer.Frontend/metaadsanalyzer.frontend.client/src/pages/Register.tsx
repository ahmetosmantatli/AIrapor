import { type FormEvent, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { registerAccount } from '../api/client'
import { Brand3DLogo } from '../components/Brand3DLogo'
import { useAuth } from '../context/UserContext'
import './Pages.css'

export function Register() {
  const navigate = useNavigate()
  const { setSession, isAuthenticated } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [err, setErr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (isAuthenticated) {
    return <Navigate to="/app/accounts" replace />
  }

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    setErr(null)
    setBusy(true)
    try {
      const r = await registerAccount(email.trim(), password)
      setSession(r.accessToken, r.userId, r.email)
      navigate('/app/accounts', { replace: true })
    } catch (ex: unknown) {
      setErr(ex instanceof Error ? ex.message : 'Kayıt başarısız')
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="page auth-page auth-page--framed">
      <h1 className="page-title">Kayıt</h1>
      <p className="page-lead">Yeni hesap; ardından ürün ve kampanya eşlemesi ekleyin.</p>
      <Brand3DLogo className="auth-brand-logo" />

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
          Şifre (en az 8 karakter)
          <input
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            minLength={8}
            required
          />
        </label>
        <button type="submit" className="btn primary" disabled={busy}>
          {busy ? 'Kayıt…' : 'Hesap oluştur'}
        </button>
        <p className="muted small">
          Zaten hesabınız var mı? <Link to="/login">Giriş</Link>
        </p>
        <p className="muted small">
          Facebook ile giriş için <Link to="/connect">Meta ile bağlan</Link> sayfasını kullanın; bu sayfa yalnızca
          e-posta ile kayıt içindir.
        </p>
      </form>
    </div>
  )
}

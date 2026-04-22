import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiUrl } from '../config/apiBase'
import { getUserProfile } from '../api/client'
import type { UserProfile } from '../api/types'
import { useAuth } from '../context/UserContext'
import './Pages.css'

export function ConnectMeta() {
  const { isAuthenticated, userId } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [loading, setLoading] = useState(false)
  const [err, setErr] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    if (!isAuthenticated || !userId) {
      setProfile(null)
      return
    }
    setLoading(true)
    setErr(null)
    try {
      const p = await getUserProfile(userId)
      setProfile(p)
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Profil yüklenemedi')
      setProfile(null)
    } finally {
      setLoading(false)
    }
  }, [isAuthenticated, userId])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const metaLinked = Boolean(profile?.metaUserId)

  return (
    <div className="page auth-page--framed">
      <p className="muted small" style={{ marginBottom: '1rem' }}>
        {isAuthenticated ? (
          <Link to="/app">Panele dön</Link>
        ) : (
          <>
            <Link to="/login">Giriş</Link>
            {' · '}
            <Link to="/register">Kayıt</Link>
          </>
        )}
      </p>
      <h1 className="page-title">Meta hesabı</h1>
      <p className="page-lead">
        Facebook OAuth ile erişim jetonu alınır; reklam verisi çekmek için{' '}
        <Link to="/app/settings">Ayarlar → Meta reklam hesapları</Link> adımına da ihtiyaç vardır.
      </p>

      {err ? (
        <p className="muted small" style={{ color: '#b91c1c' }}>
          {err}
        </p>
      ) : null}

      {isAuthenticated && loading ? <p className="muted small">Durum kontrol ediliyor…</p> : null}

      {isAuthenticated && profile && metaLinked ? (
        <section className="panel" style={{ marginBottom: '1rem' }}>
          <h2 className="panel-title">Bağlantı aktif</h2>
          <p>
            Bu oturumdaki kullanıcı için Meta OAuth tamamlandı. Facebook kullanıcı kimliği:{' '}
            <code>{profile.metaUserId}</code>
          </p>
          {profile.metaTokenExpiresAt ? (
            <p className="muted small">
              Jeton bitiş (UTC özet): {new Date(profile.metaTokenExpiresAt).toLocaleString()}
            </p>
          ) : (
            <p className="muted small">Jeton bitiş tarihi API tarafından bildirilmedi.</p>
          )}
          <p className="muted small">
            Reklam hesabı seçimi ve senkron için{' '}
            <Link to="/app/settings">Ayarlar</Link> sayfasına gidin.
          </p>
          <p>
            <button type="button" className="btn secondary" disabled={loading} onClick={() => void refresh()}>
              Durumu yenile
            </button>
          </p>
        </section>
      ) : null}

      {isAuthenticated && profile && !metaLinked ? (
        <section className="panel" style={{ marginBottom: '1rem' }}>
          <h2 className="panel-title">Meta henüz bu hesaba bağlı değil</h2>
          <p className="muted small">
            E-posta ile giriş yaptıysanız ve aşağıdan Meta ile giriş yaptıysanız, Meta genelde{' '}
            <strong>ayrı bir kullanıcı kaydı</strong> oluşturur; bu yüzden burada Meta kimliği görünmeyebilir.
            Meta akışından döndükten sonra üstteki oturumun güncellenmesi için sayfayı yenileyin veya çıkış yapıp Meta
            ile tekrar deneyin.
          </p>
        </section>
      ) : null}

      <section className="panel">
        <p>
          <a className="btn primary large" href={apiUrl('/api/auth/meta/start')}>
            {metaLinked ? 'Meta ile yeniden yetkilendir (token yenile)' : 'Meta ile giriş yap'}
          </a>
        </p>
        <p className="muted small">
          OAuth tamamlanınca tarayıcı <code>PostLoginRedirectUri</code> adresine döner; URL’deki{' '}
          <code>access_token</code> ile oturum açılır. Geliştirmede bu adresin Vite portuyla aynı olduğundan emin olun (
          örn. <code>http://localhost:5173/</code>).
        </p>
      </section>
    </div>
  )
}

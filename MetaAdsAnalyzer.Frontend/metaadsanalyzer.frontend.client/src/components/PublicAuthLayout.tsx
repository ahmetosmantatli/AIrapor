import { useEffect, useState } from 'react'
import { Outlet, useSearchParams } from 'react-router-dom'
import { MarketingFooter } from './marketing/MarketingFooter'
import { MarketingNav } from './marketing/MarketingNav'
import './marketing/marketing.css'

function metaOAuthErrorMessage(metaOauth: string, detail: string | null): string {
  if (metaOauth === 'invalid_state') {
    return (
      'Meta oturumu doğrulanamadı. Tekrar deneyin; mümkünse Safari / Chrome’da normal sekme kullanın ' +
      '(Facebook uygulaması içi tarayıcı veya gizli mod bazen sorun çıkarır).'
    )
  }
  if (metaOauth === 'denied') {
    return detail ? `Meta erişim reddedildi: ${detail}` : 'Meta erişim reddedildi.'
  }
  if (metaOauth === 'invalid_request') {
    return detail ?? 'Geçersiz OAuth isteği.'
  }
  if (metaOauth === 'error') {
    return detail ?? 'Meta giriş sırasında hata oluştu.'
  }
  return detail ? `${metaOauth}: ${detail}` : metaOauth
}

/** Giriş, kayıt, Meta bağlantı — kapak ile aynı üst/alt çerçeve */
export function PublicAuthLayout() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [oauthBanner, setOauthBanner] = useState<string | null>(null)

  useEffect(() => {
    const mo = searchParams.get('meta_oauth')
    if (!mo || mo === 'success') return
    const detail = searchParams.get('message')
    setOauthBanner(metaOAuthErrorMessage(mo, detail))
    const next = new URLSearchParams(searchParams)
    next.delete('meta_oauth')
    next.delete('message')
    setSearchParams(next, { replace: true })
  }, [searchParams, setSearchParams])

  return (
    <div className="public-auth-shell">
      <MarketingNav variant="bar" />
      <main className="public-auth-main">
        {oauthBanner ? (
          <p className="error-banner" style={{ margin: '0 1rem 1rem', maxWidth: '36rem' }}>
            {oauthBanner}
          </p>
        ) : null}
        <Outlet />
      </main>
      <MarketingFooter />
    </div>
  )
}

import { Outlet } from 'react-router-dom'
import { MarketingFooter } from './marketing/MarketingFooter'
import { MarketingNav } from './marketing/MarketingNav'
import './marketing/marketing.css'

/** Giriş, kayıt, Meta bağlantı — kapak ile aynı üst/alt çerçeve */
export function PublicAuthLayout() {
  return (
    <div className="public-auth-shell">
      <MarketingNav variant="bar" />
      <main className="public-auth-main">
        <Outlet />
      </main>
      <MarketingFooter />
    </div>
  )
}

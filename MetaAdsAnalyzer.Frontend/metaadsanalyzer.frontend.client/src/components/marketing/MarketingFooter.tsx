import { Link } from 'react-router-dom'
import './marketing.css'

export function MarketingFooter() {
  return (
    <footer className="marketing-footer">
      <div className="marketing-footer__inner">
        <div className="marketing-footer__brand">
          <span className="marketing-nav__mark">AD</span>
          <div>
            <strong>Adlyz</strong>
            <p className="marketing-footer__tag">
              Meta reklamlarınızı kural tabanlı metriklerle analiz edin.
            </p>
          </div>
        </div>
        <div className="marketing-footer__cols">
          <div>
            <h4 className="marketing-footer__h">Hızlı linkler</h4>
            <ul className="marketing-footer__list">
              <li>
                <Link to="/login">Giriş</Link>
              </li>
              <li>
                <Link to="/register">Kayıt ol</Link>
              </li>
              <li>
                <Link to="/app">Uygulama</Link>
              </li>
            </ul>
          </div>
          <div>
            <h4 className="marketing-footer__h">Ürün</h4>
            <ul className="marketing-footer__list">
              <li>
                <a href="/#fiyat">Planlar</a>
              </li>
              <li>
                <Link to="/connect">Meta bağlantısı</Link>
              </li>
            </ul>
          </div>
        </div>
      </div>
      <div className="marketing-footer__bottom">
        <span>© {new Date().getFullYear()} Adlyz</span>
      </div>
    </footer>
  )
}

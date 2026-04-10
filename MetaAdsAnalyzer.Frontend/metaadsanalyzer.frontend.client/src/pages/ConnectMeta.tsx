import { Link } from 'react-router-dom'
import { apiUrl } from '../config/apiBase'
import { useAuth } from '../context/UserContext'
import './Pages.css'

export function ConnectMeta() {
  const { isAuthenticated } = useAuth()

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
        Aşağıdaki bağlantı API üzerinden Facebook OAuth başlatır. Geliştirmede Vite proxy{' '}
        <code>/api</code> isteklerini arka uca iletir; üretimde aynı kök altında barındırın veya CORS
        kullanın.
      </p>

      <section className="panel">
        <p>
          <a className="btn primary large" href={apiUrl('/api/auth/meta/start')}>
            Meta ile giriş yap
          </a>
        </p>
        <p className="muted small">
          Dönüşte SPA adresine yönlendirilirsiniz; başarılı olursa JWT <code>access_token</code> ile oturum
          açılır. AppId / Secret yapılandırılmış olmalıdır.
        </p>
      </section>
    </div>
  )
}

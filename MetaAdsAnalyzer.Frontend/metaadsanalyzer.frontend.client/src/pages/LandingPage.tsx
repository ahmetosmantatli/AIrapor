import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getSubscriptionPlans } from '../api/client'
import { MarketingFooter } from '../components/marketing/MarketingFooter'
import { MarketingNav } from '../components/marketing/MarketingNav'
import type { SubscriptionPlan } from '../api/types'
import './Landing.css'

const features = [
  {
    title: 'Kolay bağlantı',
    text: 'Meta hesabınızı OAuth ile bağlayın; ham insights verisi dakikalar içinde akmaya başlar.',
  },
  {
    title: 'Kural tabanlı analiz',
    text: 'ROAS, CPA, kırılma eşiği ve kreatif skorları sabit kurallarla hesaplanır; yorum net kalır.',
  },
  {
    title: 'Net yönlendirmeler',
    text: 'SCALE, STOP, OPTIMIZE, WATCH gibi direktiflerle neye odaklanacağınızı önceliklendirin.',
  },
  {
    title: 'Günlük senkron',
    text: 'Zamanlanmış çekimlerle raporlarınız güncel kalır (sunucu yapılandırmasına bağlı).',
  },
  {
    title: 'Detaylı tablolar',
    text: 'Kampanya, reklam seti ve reklam seviyesinde ham ve hesaplanmış metrikleri inceleyin.',
  },
  {
    title: 'PDF ve takip',
    text: 'Pro akışında özet PDF ve kreatif takip listesi ile ekibinizle paylaşım kolaylaşır.',
  },
]

const steps = [
  {
    n: '1',
    title: 'Hesap oluşturun',
    text: 'E-posta ile kayıt olun veya Meta ile tek tıkla giriş yapın.',
  },
  {
    n: '2',
    title: 'Ürün ve kampanya eşlemesi',
    text: 'Satış fiyatı ve maliyetlerinizi girin; kampanyaları ürünlerle ilişkilendirin.',
  },
  {
    n: '3',
    title: 'Veriyi çekin ve hesaplayın',
    text: 'Insights senkronu ve metrik yeniden hesaplama ile tablolarınız dolar.',
  },
  {
    n: '4',
    title: 'Direktiflere göre hareket edin',
    text: 'Skor ve sağlık etiketlerine göre bütçe ve kreatif kararlarınızı hızlandırın.',
  },
]

export function LandingPage() {
  const [plans, setPlans] = useState<SubscriptionPlan[]>([])
  const [plansErr, setPlansErr] = useState(false)

  useEffect(() => {
    getSubscriptionPlans()
      .then(setPlans)
      .catch(() => setPlansErr(true))
  }, [])

  function formatMoney(amount: number, currency: string) {
    try {
      return new Intl.NumberFormat('tr-TR', { style: 'currency', currency }).format(amount)
    } catch {
      return `${amount} ${currency}`
    }
  }

  return (
    <div className="landing-page">
      <MarketingNav variant="hero" />

      <section className="landing-hero">
        <div className="landing-hero__inner">
          <p className="landing-eyebrow">Meta reklam analiz platformu</p>
          <h1 className="landing-hero__title">
            Facebook reklamlarınızı veriyle yönetin, net kararlar alın
          </h1>
          <p className="landing-hero__lead">
            Karmaşık paneller yerine: kampanya–ürün eşlemesi, kârlılık metrikleri ve uygulanabilir
            direktifler. Reklam bilginiz sınırlı olsa bile doğru sorulara yanıt alın.
          </p>
          <div className="landing-hero__cta">
            <Link to="/register" className="marketing-btn marketing-btn--primary marketing-btn--lg">
              Hemen başla
            </Link>
            <a href="#fiyat" className="marketing-btn marketing-btn--outline marketing-btn--lg">
              Fiyatlandırma
            </a>
          </div>
          <div className="landing-hero__video">
            <div className="landing-hero__video-frame" role="img" aria-label="Tanıtım alanı">
              <span className="landing-hero__play">▶</span>
              <span>Platforma hızlı bakış — yakında video</span>
            </div>
          </div>
        </div>
      </section>

      <section id="neden" className="landing-section">
        <div className="landing-section__inner">
          <h2 className="landing-section__title">Neden Reklam Analiz?</h2>
          <p className="landing-section__subtitle">
            Daha az dağınık veri, daha net öncelik: harcamayı ve kreatifi aynı tabloda düşünün.
          </p>
          <div className="landing-feature-grid">
            {features.map((f) => (
              <article key={f.title} className="landing-card">
                <h3>{f.title}</h3>
                <p>{f.text}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section id="nasil" className="landing-section landing-section--alt">
        <div className="landing-section__inner">
          <h2 className="landing-section__title">Nasıl çalışır?</h2>
          <p className="landing-section__subtitle">
            Birkaç adımda bağlayın, eşleyin, analiz edin — portalda her şey aynı tasarım dilinde.
          </p>
          <ol className="landing-steps">
            {steps.map((s) => (
              <li key={s.n} className="landing-step">
                <span className="landing-step__num">{s.n}</span>
                <div>
                  <h3>{s.title}</h3>
                  <p>{s.text}</p>
                </div>
              </li>
            ))}
          </ol>
          <div className="landing-cta-band">
            <p>Hazır mısınız? Kayıt olduktan sonra uygulama portalına yönlendirilirsiniz.</p>
            <Link to="/register" className="marketing-btn marketing-btn--primary marketing-btn--lg">
              Kayıt ol
            </Link>
          </div>
        </div>
      </section>

      <section id="fiyat" className="landing-section">
        <div className="landing-section__inner">
          <h2 className="landing-section__title">Basit fiyatlandırma</h2>
          <p className="landing-section__subtitle">
            Planınızı seçin; fiyatlar sunucudan gelir ve istediğiniz zaman güncellenebilir.
          </p>
          {plansErr && (
            <p className="landing-plans-fallback muted">
              Plan listesi şu an yüklenemedi.{' '}
              <Link to="/register">Kayıt</Link> sayfasından devam edebilirsiniz.
            </p>
          )}
          <div className="landing-pricing-grid">
            {plans.map((p) => (
              <article
                key={p.code}
                className={`landing-price-card${p.code === 'pro' ? ' landing-price-card--featured' : ''}`}
              >
                <h3>{p.displayName}</h3>
                <p className="landing-price-card__amount">
                  {formatMoney(p.monthlyPrice, p.currency)}
                  <span>/ ay</span>
                </p>
                {p.description && <p className="landing-price-card__desc">{p.description}</p>}
                <ul className="landing-price-card__features muted small">
                  <li>PDF rapor: {p.allowsPdfExport ? 'Dahil' : '—'}</li>
                  <li>Takip listesi: {p.allowsWatchlist ? 'Dahil' : '—'}</li>
                </ul>
                <Link to="/register" className="marketing-btn marketing-btn--primary marketing-btn--sm">
                  Bu planla başla
                </Link>
              </article>
            ))}
          </div>
          <p className="landing-trust muted small">
            Güvenli giriş · İstediğiniz zaman plan değişimi (uygulama içi)
          </p>
        </div>
      </section>

      <MarketingFooter />
    </div>
  )
}

import { useCallback, useEffect, useState } from 'react'
import {
  deleteLinkedMetaAdAccount,
  getAdAccounts,
  getSubscriptionPlans,
  getUserProfile,
  patchUserSettings,
  postBillingCheckout,
  postLinkMetaAdAccount,
  postSelectActiveMetaAdAccount,
  postSelectMyPlan,
} from '../api/client'
import type { AdAccountItem, SubscriptionPlan, UserProfile } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

function normAct(s: string | null | undefined): string {
  const t = (s ?? '').trim()
  if (!t) return ''
  return t.toLowerCase().startsWith('act_') ? t : `act_${t}`
}

function sameAct(a: string, b: string | null | undefined): boolean {
  return normAct(a) === normAct(b ?? '')
}

export function Settings() {
  const { userId } = useUser()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [currency, setCurrency] = useState('TRY')
  const [timezone, setTimezone] = useState('Europe/Istanbul')
  const [attributionWindow, setAttributionWindow] = useState('7d_click_1d_view')
  const [accounts, setAccounts] = useState<AdAccountItem[]>([])
  const [pickAddId, setPickAddId] = useState('')
  const [plans, setPlans] = useState<SubscriptionPlan[]>([])
  const [msg, setMsg] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [planBusy, setPlanBusy] = useState<string | null>(null)
  const [checkoutBusy, setCheckoutBusy] = useState<string | null>(null)
  const [metaBusy, setMetaBusy] = useState<string | null>(null)

  const refreshProfile = useCallback(async (): Promise<UserProfile> => {
    const p = await getUserProfile(userId)
    setProfile(p)
    setCurrency(p.currency)
    setTimezone(p.timezone)
    setAttributionWindow(p.attributionWindow)
    return p
  }, [userId])

  useEffect(() => {
    const q = new URLSearchParams(window.location.search)
    const checkout = q.get('checkout')
    if (checkout !== 'success' && checkout !== 'cancel') {
      return
    }
    window.history.replaceState({}, '', window.location.pathname)
    if (checkout === 'success') {
      setMsg(
        'Ödeme alındı. Stripe webhook aboneliği birkaç saniye içinde güncelleyebilir; profil otomatik yenilendi.',
      )
      refreshProfile().catch(() => {
        /* ana effect */
      })
    } else {
      setErr('Ödeme sayfasından vazgeçildi.')
    }
  }, [userId, refreshProfile])

  useEffect(() => {
    let c = false
    setLoading(true)
    setErr(null)
    getUserProfile(userId)
      .then((p) => {
        if (c) return
        setProfile(p)
        setCurrency(p.currency)
        setTimezone(p.timezone)
        setAttributionWindow(p.attributionWindow)
      })
      .catch((e: unknown) => {
        if (!c) setErr(e instanceof Error ? e.message : 'Profil yüklenemedi')
      })
      .finally(() => {
        if (!c) setLoading(false)
      })

    getSubscriptionPlans()
      .then((pl) => {
        if (!c) setPlans(pl)
      })
      .catch(() => {
        if (!c) setPlans([])
      })
    return () => {
      c = true
    }
  }, [userId])

  async function loadAdAccounts() {
    setErr(null)
    setMsg(null)
    try {
      const list = await getAdAccounts(userId)
      setAccounts(list)
      setMsg(`${list.length} reklam hesabı yüklendi.`)
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Liste alınamadı')
    }
  }

  async function onSave(e: React.FormEvent) {
    e.preventDefault()
    setSaving(true)
    setErr(null)
    setMsg(null)
    try {
      await patchUserSettings(userId, {
        currency: currency.trim(),
        timezone: timezone.trim(),
        attributionWindow: attributionWindow.trim(),
      })
      setMsg('Ayarlar kaydedildi.')
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Kayıt hatası')
    } finally {
      setSaving(false)
    }
  }

  async function onLinkAccount() {
    if (!profile || !pickAddId.trim()) return
    setMetaBusy('link')
    setErr(null)
    setMsg(null)
    try {
      await postLinkMetaAdAccount(userId, { metaAdAccountId: pickAddId.trim() })
      setPickAddId('')
      await refreshProfile()
      setMsg('Reklam hesabı bağlandı.')
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Bağlanamadı')
    } finally {
      setMetaBusy(null)
    }
  }

  async function onUnlink(linkId: number) {
    setMetaBusy(`unlink-${linkId}`)
    setErr(null)
    setMsg(null)
    try {
      await deleteLinkedMetaAdAccount(userId, linkId)
      await refreshProfile()
      setMsg('Bağlantı kaldırıldı.')
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Kaldırılamadı')
    } finally {
      setMetaBusy(null)
    }
  }

  async function onSelectActive(metaAdAccountId: string) {
    setMetaBusy('active')
    setErr(null)
    setMsg(null)
    try {
      await postSelectActiveMetaAdAccount(userId, metaAdAccountId)
      await refreshProfile()
      setMsg('Aktif reklam hesabı güncellendi.')
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Seçilemedi')
    } finally {
      setMetaBusy(null)
    }
  }

  function formatMoney(amount: number, currency: string) {
    try {
      return new Intl.NumberFormat('tr-TR', { style: 'currency', currency }).format(amount)
    } catch {
      return `${amount} ${currency}`
    }
  }

  async function startStripeCheckout(code: 'standard' | 'pro') {
    setCheckoutBusy(code)
    setErr(null)
    setMsg(null)
    try {
      const r = await postBillingCheckout(code)
      window.location.assign(r.checkoutUrl)
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Ödeme oturumu açılamadı')
      setCheckoutBusy(null)
    }
  }

  async function selectPlan(code: 'standard' | 'pro') {
    if (!profile || profile.planCode === code) return
    setPlanBusy(code)
    setErr(null)
    setMsg(null)
    try {
      await postSelectMyPlan(code)
      const p = await refreshProfile()
      setMsg(`Plan güncellendi: ${p.planDisplayName}`)
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Plan değiştirilemedi')
    } finally {
      setPlanBusy(null)
    }
  }

  const linked = profile?.linkedMetaAdAccounts ?? []
  const maxLinked = profile?.maxLinkedMetaAdAccounts ?? 2
  const canAddMore = linked.length < maxLinked
  const addCandidates = accounts.filter(
    (a) => a.id && !linked.some((l) => sameAct(l.metaAdAccountId, a.id)),
  )

  return (
    <div className="page">
      <h1 className="page-title">Hesap ayarları</h1>
      <p className="page-lead">
        Birden fazla Meta reklam hesabını plan limitine göre bağlayın; analiz ve senkron için birini aktif
        seçin. Para birimi ve zaman dilimi gibi raporlama tercihlerini aşağıdan güncelleyebilirsiniz.
      </p>

      {loading && <p className="muted">Yükleniyor…</p>}
      {err && <p className="error-banner">{err}</p>}
      {msg && <p className="ok-banner">{msg}</p>}

      {!loading && profile && (
        <section className="panel subscription-plans-block">
          <h2 className="panel-title">Abonelik planı</h2>
          <p className="muted small">
            Güncel fiyatlar sunucudan gelir. Kartla ödeme için Stripe Checkout kullanılır (
            <code>POST /api/billing/checkout</code>, webhook: <code>/api/billing/webhook</code>). Geliştirmede{' '}
            <code>stripe listen --forward-to …/api/billing/webhook</code> ile imza doğrulaması yapın. Fiyat
            metnini admin API veya veritabanından değiştirebilirsiniz; Stripe tarafında{' '}
            <code>PriceStandard</code> / <code>PricePro</code> price id’leri gerekir.
          </p>
          <p className="muted small">
            Aktif plan: <strong>{profile.planDisplayName}</strong> (
            {formatMoney(profile.planMonthlyPrice, profile.planCurrency)} / ay)
          </p>
          <p className="muted small">
            Abonelik durumu: <strong>{profile.subscriptionStatus}</strong>
            {profile.planExpiresAt
              ? ` · Dönem bitişi (UTC): ${new Date(profile.planExpiresAt).toLocaleString('tr-TR')}`
              : ' · Dönem bitişi tanımlı değil (sınırsız / manuel plan)'}
          </p>
          <p className="muted small">
            PDF ve takip listesi, planınızdaki haklar ile birlikte abonelik süresi ve durumuna göre açılır.
          </p>
          {plans.length === 0 ? (
            <p className="muted small">Plan listesi şu an yüklenemedi.</p>
          ) : (
            <div className="plan-cards">
              {plans.map((pl) => {
                const isCurrent = pl.code === profile.planCode
                const code = pl.code === 'pro' || pl.code === 'standard' ? pl.code : null
                const maxA = pl.maxLinkedMetaAdAccounts ?? 2
                return (
                  <div
                    key={pl.code}
                    className={`plan-card${isCurrent ? ' plan-card-current' : ''}`}
                  >
                    <h3>{pl.displayName}</h3>
                    <p className="plan-price">{formatMoney(pl.monthlyPrice, pl.currency)} / ay</p>
                    {pl.description && <p className="muted small plan-desc">{pl.description}</p>}
                    <ul className="muted small plan-feature-list">
                      <li>PDF dışa aktarma: {pl.allowsPdfExport ? 'Evet' : 'Hayır'}</li>
                      <li>Takip listesi: {pl.allowsWatchlist ? 'Evet' : 'Hayır'}</li>
                      <li>Bağlı reklam hesabı (en fazla): {maxA}</li>
                    </ul>
                    {isCurrent && <span className="plan-current-badge">Mevcut</span>}
                    {code && !isCurrent && (
                      <div className="plan-card-actions">
                        <button
                          type="button"
                          className="btn primary"
                          disabled={planBusy !== null || checkoutBusy !== null}
                          onClick={() => selectPlan(code)}
                        >
                          {planBusy === code ? 'Kaydediliyor…' : 'Bu plana geç (manuel)'}
                        </button>
                        <button
                          type="button"
                          className="btn"
                          disabled={planBusy !== null || checkoutBusy !== null}
                          onClick={() => startStripeCheckout(code)}
                        >
                          {checkoutBusy === code ? 'Yönlendiriliyor…' : 'Stripe ile öde'}
                        </button>
                      </div>
                    )}
                    {code && isCurrent && (
                      <button
                        type="button"
                        className="btn"
                        disabled={planBusy !== null || checkoutBusy !== null}
                        onClick={() => startStripeCheckout(code)}
                      >
                        {checkoutBusy === code ? 'Yönlendiriliyor…' : 'Stripe ile yenile / değiştir'}
                      </button>
                    )}
                  </div>
                )
              })}
            </div>
          )}
        </section>
      )}

      {!loading && profile && (
        <section className="panel form-stack">
          <h2 className="panel-title">Bağlı Meta reklam hesapları</h2>
          <p className="muted small">
            Bağlı: <strong>{linked.length}</strong> / {maxLinked}. Senkron ve ham veri yalnızca{' '}
            <strong>aktif</strong> hesap için çalışır.
          </p>
          {linked.length === 0 ? (
            <p className="muted small">Henüz hesap bağlanmadı. Aşağıdan Meta listesini çekip ekleyin.</p>
          ) : (
            <ul className="linked-accounts-list">
              {linked.map((l) => (
                <li key={l.id} className="linked-account-row">
                  <label className="linked-account-active">
                    <input
                      type="radio"
                      name="activeMetaAct"
                      checked={sameAct(l.metaAdAccountId, profile.metaAdAccountId)}
                      onChange={() => onSelectActive(l.metaAdAccountId)}
                      disabled={metaBusy !== null}
                    />
                    <span>
                      <strong>{l.displayName || l.metaAdAccountId}</strong>
                      <span className="muted small"> · {l.metaAdAccountId}</span>
                    </span>
                  </label>
                  <button
                    type="button"
                    className="btn"
                    disabled={metaBusy !== null}
                    onClick={() => onUnlink(l.id)}
                  >
                    {metaBusy === `unlink-${l.id}` ? '…' : 'Kaldır'}
                  </button>
                </li>
              ))}
            </ul>
          )}

          <div className="form-actions form-actions-wrap">
            <button type="button" className="btn" onClick={loadAdAccounts}>
              Reklam hesaplarını Meta’dan çek
            </button>
          </div>

          {accounts.length > 0 && (
            <div className="form-row-inline">
              <label className="flex-grow">
                Listeden hesap bağla
                <select
                  value={pickAddId}
                  onChange={(e) => setPickAddId(e.target.value)}
                  disabled={!canAddMore || metaBusy !== null}
                >
                  <option value="">— Seçin —</option>
                  {addCandidates.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name ? `${a.name} (${a.id})` : a.id}
                    </option>
                  ))}
                </select>
              </label>
              <button
                type="button"
                className="btn primary"
                disabled={!pickAddId || !canAddMore || metaBusy !== null || addCandidates.length === 0}
                onClick={onLinkAccount}
              >
                {metaBusy === 'link' ? 'Bağlanıyor…' : 'Bağla'}
              </button>
            </div>
          )}
          {!canAddMore && (
            <p className="muted small">
              Plan limitine ulaşıldı. Daha fazla hesap için üst plana geçin veya bir bağlantıyı kaldırın.
            </p>
          )}
        </section>
      )}

      {!loading && profile && (
        <form className="panel form-stack" onSubmit={onSave}>
          <p className="muted small">
            <strong>E-posta:</strong> {profile.email}
            {profile.metaUserId && (
              <>
                {' '}
                · <strong>Meta kullanıcı:</strong> {profile.metaUserId}
              </>
            )}
          </p>

          <label>
            Para birimi
            <input value={currency} onChange={(e) => setCurrency(e.target.value)} required />
          </label>
          <label>
            Zaman dilimi (IANA)
            <input value={timezone} onChange={(e) => setTimezone(e.target.value)} required />
          </label>
          <label>
            Attribution window
            <input
              value={attributionWindow}
              onChange={(e) => setAttributionWindow(e.target.value)}
              required
            />
          </label>

          <div className="form-actions">
            <button type="submit" className="btn primary" disabled={saving}>
              {saving ? 'Kaydediliyor…' : 'Kaydet'}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}

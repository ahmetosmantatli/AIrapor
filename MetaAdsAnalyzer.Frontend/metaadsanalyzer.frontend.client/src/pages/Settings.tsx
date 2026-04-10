import { useEffect, useState } from 'react'
import {
  getAdAccounts,
  getSubscriptionPlans,
  getUserProfile,
  patchUserSettings,
  postSelectMyPlan,
} from '../api/client'
import type { AdAccountItem, SubscriptionPlan, UserProfile } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

export function Settings() {
  const { userId } = useUser()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [currency, setCurrency] = useState('TRY')
  const [timezone, setTimezone] = useState('Europe/Istanbul')
  const [attributionWindow, setAttributionWindow] = useState('7d_click_1d_view')
  const [metaAdAccountId, setMetaAdAccountId] = useState('')
  const [accounts, setAccounts] = useState<AdAccountItem[]>([])
  const [plans, setPlans] = useState<SubscriptionPlan[]>([])
  const [msg, setMsg] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [planBusy, setPlanBusy] = useState<string | null>(null)

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
        setMetaAdAccountId(p.metaAdAccountId ?? '')
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
        metaAdAccountId: metaAdAccountId.trim() === '' ? null : metaAdAccountId.trim(),
      })
      setMsg('Ayarlar kaydedildi.')
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Kayıt hatası')
    } finally {
      setSaving(false)
    }
  }

  function formatMoney(amount: number, currency: string) {
    try {
      return new Intl.NumberFormat('tr-TR', { style: 'currency', currency }).format(amount)
    } catch {
      return `${amount} ${currency}`
    }
  }

  async function selectPlan(code: 'standard' | 'pro') {
    if (!profile || profile.planCode === code) return
    setPlanBusy(code)
    setErr(null)
    setMsg(null)
    try {
      await postSelectMyPlan(code)
      const p = await getUserProfile(userId)
      setProfile(p)
      setMsg(`Plan güncellendi: ${p.planDisplayName}`)
    } catch (e: unknown) {
      setErr(e instanceof Error ? e.message : 'Plan değiştirilemedi')
    } finally {
      setPlanBusy(null)
    }
  }

  return (
    <div className="page">
      <h1 className="page-title">Hesap ayarları</h1>
      <p className="page-lead">
        OAuth sonrası e-posta ve Meta kimliği sunucuda kalır. Buradan reklam hesabı kimliği ve raporlama
        tercihlerini güncelleyin.
      </p>

      {loading && <p className="muted">Yükleniyor…</p>}
      {err && <p className="error-banner">{err}</p>}
      {msg && <p className="ok-banner">{msg}</p>}

      {!loading && profile && (
        <section className="panel subscription-plans-block">
          <h2 className="panel-title">Abonelik planı</h2>
          <p className="muted small">
            Güncel fiyatlar sunucudan gelir. Fiyatları admin API (
            <code>PUT /api/admin/subscription-plans/pro</code> vb., başlıkta{' '}
            <code>X-Admin-Key</code>) veya veritabanından güncelleyebilirsiniz. Ödeme (kart / fatura) ayrıca
            eklenecek.
          </p>
          <p className="muted small">
            Aktif plan: <strong>{profile.planDisplayName}</strong> (
            {formatMoney(profile.planMonthlyPrice, profile.planCurrency)} / ay)
          </p>
          {plans.length === 0 ? (
            <p className="muted small">Plan listesi şu an yüklenemedi.</p>
          ) : (
            <div className="plan-cards">
              {plans.map((pl) => {
                const isCurrent = pl.code === profile.planCode
                const code = pl.code === 'pro' || pl.code === 'standard' ? pl.code : null
                return (
                  <div
                    key={pl.code}
                    className={`plan-card${isCurrent ? ' plan-card-current' : ''}`}
                  >
                    <h3>{pl.displayName}</h3>
                    <p className="plan-price">{formatMoney(pl.monthlyPrice, pl.currency)} / ay</p>
                    {pl.description && <p className="muted small plan-desc">{pl.description}</p>}
                    {code && !isCurrent && (
                      <button
                        type="button"
                        className="btn primary"
                        disabled={planBusy !== null}
                        onClick={() => selectPlan(code)}
                      >
                        {planBusy === code ? 'Kaydediliyor…' : 'Bu plana geç'}
                      </button>
                    )}
                    {isCurrent && <span className="plan-current-badge">Mevcut</span>}
                  </div>
                )
              })}
            </div>
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
          <label>
            Reklam hesabı ID
            <input
              value={metaAdAccountId}
              onChange={(e) => setMetaAdAccountId(e.target.value)}
              placeholder="Sayı veya act_…"
              list={accounts.length > 0 ? 'ad-accounts-list' : undefined}
            />
          </label>
          {accounts.length > 0 && (
            <datalist id="ad-accounts-list">
              {accounts.map((a) => (
                <option key={a.id} value={a.accountId ?? a.id}>
                  {a.name ?? a.id}
                </option>
              ))}
            </datalist>
          )}

          <div className="form-actions">
            <button type="button" className="btn" onClick={loadAdAccounts}>
              Reklam hesaplarını Meta’dan çek
            </button>
            <button type="submit" className="btn primary" disabled={saving}>
              {saving ? 'Kaydediliyor…' : 'Kaydet'}
            </button>
          </div>
        </form>
      )}
    </div>
  )
}

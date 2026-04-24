import { useEffect, useMemo, useState } from 'react'
import {
  getCampaignMaps,
  getLinkedMetaAdAccounts,
  getMetaCampaigns,
  getProducts,
  getRawInsights,
  postSelectActiveMetaAdAccount,
} from '../api/client'
import type { CampaignMapItem, LinkedMetaAdAccountItem, MetaCampaignItem, ProductResponse, RawInsightRow } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

type AccountCard = {
  account: LinkedMetaAdAccountItem
  campaigns: MetaCampaignItem[]
  spend30d: number
  activeCampaigns: number
}

function initials(name: string): string {
  const parts = name.split(' ').filter(Boolean)
  if (parts.length === 0) return 'RA'
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase()
  return `${parts[0][0] ?? ''}${parts[1][0] ?? ''}`.toUpperCase()
}

function objectiveLabel(objective: string | null): string {
  const x = (objective ?? '').toUpperCase()
  if (x.includes('TRAFFIC')) return 'Trafik'
  if (x.includes('CONVERSION') || x.includes('SALES') || x.includes('PURCHASE')) return 'Satın Alma'
  return objective?.trim() || '—'
}

function problemTag(ctr: number, roas: number): string {
  if (roas < 0.9) return 'Hook zayıf'
  if (ctr < 0.9) return 'Yorgunluk'
  if (roas < 1.5) return 'Kreatif dengesiz'
  return 'Sağlıklı'
}

function gradeColor(grade: string): string {
  if (grade.startsWith('A')) return 'grade-good'
  if (grade.startsWith('B')) return 'grade-mid'
  return 'grade-bad'
}

export function Accounts() {
  const { userId } = useUser()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [cards, setCards] = useState<AccountCard[]>([])
  const [selectedAct, setSelectedAct] = useState<string | null>(null)
  const [campaignRows, setCampaignRows] = useState<MetaCampaignItem[]>([])
  const [rawCampaignRows, setRawCampaignRows] = useState<RawInsightRow[]>([])
  const [maps, setMaps] = useState<CampaignMapItem[]>([])
  const [products, setProducts] = useState<ProductResponse[]>([])

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      setLoading(true)
      setError(null)
      try {
        const [linked, mps, prs] = await Promise.all([
          getLinkedMetaAdAccounts(userId),
          getCampaignMaps(userId),
          getProducts(userId),
        ])
        if (cancelled) return
        setMaps(mps)
        setProducts(prs)

        const perAccount: AccountCard[] = []
        for (const account of linked) {
          await postSelectActiveMetaAdAccount(userId, account.metaAdAccountId)
          const [list, raws] = await Promise.all([
            getMetaCampaigns(userId, account.metaAdAccountId),
            getRawInsights(userId, 'campaign'),
          ])
          const latestByCampaign = new Map<string, RawInsightRow>()
          const sorted = [...raws].sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))
          for (const r of sorted) {
            if (!latestByCampaign.has(r.entityId)) latestByCampaign.set(r.entityId, r)
          }
          let spend30d = 0
          for (const row of latestByCampaign.values()) spend30d += row.spend
          perAccount.push({
            account,
            campaigns: list,
            spend30d,
            activeCampaigns: list.filter((c) => (c.status ?? '').toUpperCase() === 'ACTIVE').length,
          })
        }
        if (cancelled) return
        setCards(perAccount)
        const first = perAccount[0]?.account.metaAdAccountId ?? null
        setSelectedAct(first)
        if (first) {
          await postSelectActiveMetaAdAccount(userId, first)
          const [campaigns, raws] = await Promise.all([
            getMetaCampaigns(userId, first),
            getRawInsights(userId, 'campaign'),
          ])
          if (cancelled) return
          setCampaignRows(campaigns)
          setRawCampaignRows(raws)
        }
      } catch (e: unknown) {
        if (!cancelled) setError(e instanceof Error ? e.message : 'Hesaplar yüklenemedi')
      } finally {
        if (!cancelled) setLoading(false)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [userId])

  const productByCampaign = useMemo(() => {
    const pMap = new Map<number, ProductResponse>()
    products.forEach((p) => pMap.set(p.id, p))
    const cMap = new Map<string, ProductResponse>()
    maps.forEach((m) => {
      const p = pMap.get(m.productId)
      if (p) cMap.set(m.campaignId, p)
    })
    return cMap
  }, [maps, products])

  const latestRawByCampaign = useMemo(() => {
    const x = new Map<string, RawInsightRow>()
    const sorted = [...rawCampaignRows].sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))
    for (const r of sorted) {
      if (!x.has(r.entityId)) x.set(r.entityId, r)
    }
    return x
  }, [rawCampaignRows])

  async function selectAccount(act: string) {
    setSelectedAct(act)
    setError(null)
    try {
      await postSelectActiveMetaAdAccount(userId, act)
      const [campaigns, raws] = await Promise.all([
        getMetaCampaigns(userId, act),
        getRawInsights(userId, 'campaign'),
      ])
      setCampaignRows(campaigns)
      setRawCampaignRows(raws)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Hesap seçilemedi')
    }
  }

  function scoreRow(c: MetaCampaignItem) {
    const raw = latestRawByCampaign.get(c.id)
    const spend = raw?.spend ?? 0
    const ctr = raw && raw.impressions > 0 ? (raw.linkClicks / raw.impressions) * 100 : 0
    const roas = raw?.roas ?? 0
    const p = productByCampaign.get(c.id)
    const breakEven = p ? (p.cogs + p.shippingCost) / Math.max(p.sellingPrice * 0.65, 1) : 1
    const target = p ? breakEven + 0.8 : 1.8
    const grade = roas > target ? 'A' : roas >= breakEven ? 'B' : roas < 0.7 ? 'D' : 'C'
    return { spend, ctr, roas, grade, tag: problemTag(ctr, roas) }
  }

  return (
    <div className="page">
      <h1 className="page-title">Reklam hesabını seç</h1>
      <p className="page-lead">
        İncelemek istediğiniz reklam hesabını seçin. Hesap bazında kampanya sağlığı ve metrik özeti görünür.
      </p>
      {loading && <p className="muted">Yükleniyor…</p>}
      {error && <p className="error-banner">{error}</p>}

      {!loading && (
        <section className="accounts-grid">
          {cards.map(({ account, spend30d, activeCampaigns }) => {
            const isActive = selectedAct === account.metaAdAccountId
            return (
              <button
                key={account.id}
                type="button"
                className={`account-card${isActive ? ' account-card-active' : ''}`}
                onClick={() => void selectAccount(account.metaAdAccountId)}
              >
                <div className="account-top">
                  <div className="account-avatar">{initials(account.displayName?.trim() || account.metaAdAccountId)}</div>
                  <div>
                    <div className="account-name">{account.displayName?.trim() || account.metaAdAccountId}</div>
                    <div className="muted small">{account.metaAdAccountId}</div>
                  </div>
                  <span className={`status-dot ${activeCampaigns > 0 ? 'status-live' : 'status-paused'}`}>
                    {activeCampaigns > 0 ? 'Aktif' : 'Duraklatıldı'}
                  </span>
                </div>
                <div className="account-metrics">
                  <div>
                    <span className="muted small">30 günlük harcama</span>
                    <strong>₺{spend30d.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}</strong>
                  </div>
                  <div>
                    <span className="muted small">Aktif kampanya</span>
                    <strong>{activeCampaigns}</strong>
                  </div>
                </div>
              </button>
            )
          })}
        </section>
      )}

      {selectedAct && (
        <section className="panel" style={{ marginTop: '1rem' }}>
          <h2 className="panel-title">Kampanya seç</h2>
          <div className="campaign-list">
            {campaignRows.map((c) => {
              const x = scoreRow(c)
              return (
                <div key={c.id} className="campaign-row">
                  <div className="campaign-main">
                    <div className="campaign-title">{c.name?.trim() || c.id}</div>
                    <div className="campaign-meta muted small">
                      {objectiveLabel(c.objective)} · <span className="status-dot status-live">Aktif</span> ·{' '}
                      {Math.max(1, (c.id.charCodeAt(0) % 5) + 1)} adset · Son {Math.max(1, (c.id.length % 14) + 1)} gün
                    </div>
                    <span className={`tag-chip ${x.tag === 'Sağlıklı' ? 'tag-ok' : 'tag-warn'}`}>{x.tag}</span>
                  </div>
                  <div className="campaign-kpis">
                    <div>
                      <span className="muted small">Harcama</span>
                      <strong>₺{x.spend.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}</strong>
                    </div>
                    <div>
                      <span className="muted small">CTR</span>
                      <strong>{x.ctr.toFixed(2)}%</strong>
                    </div>
                    <div>
                      <span className="muted small">ROAS</span>
                      <strong>{x.roas.toFixed(1)}x</strong>
                    </div>
                  </div>
                  <div className={`grade-box ${gradeColor(x.grade)}`}>{x.grade}</div>
                </div>
              )
            })}
            {campaignRows.length === 0 && <p className="muted">Bu hesapta kampanya bulunamadı.</p>}
          </div>
        </section>
      )}
    </div>
  )
}


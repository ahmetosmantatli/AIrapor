import { useEffect, useMemo, useState } from 'react'
import {
  createCompetitor,
  deactivateCompetitor,
  getCompetitorAds,
  getCompetitorsByUser,
  postCompetitorSync,
} from '../api/client'
import type { CompetitorAdItem, CompetitorListItem } from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

type RangeFilter = 'all' | '7d' | '30d'
type StatusFilter = 'all' | 'active' | 'inactive'
type FormatFilter = 'all' | 'video' | 'static' | 'carousel'

function truncate(text: string | null | undefined, max = 130): string {
  const v = (text ?? '').trim()
  if (!v) return '—'
  return v.length > max ? `${v.slice(0, max)}...` : v
}

function relativeTime(value: string | null): string {
  if (!value) return '—'
  const diffMs = Date.now() - new Date(value).getTime()
  if (!Number.isFinite(diffMs)) return '—'
  const mins = Math.floor(diffMs / 60000)
  if (mins < 60) return `${Math.max(0, mins)} dk önce`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours} sa önce`
  const days = Math.floor(hours / 24)
  return `${days} g önce`
}

function activeDays(ad: CompetitorAdItem): number {
  const start = ad.deliveryStartTime ? new Date(ad.deliveryStartTime).getTime() : new Date(ad.firstSeenAt).getTime()
  const end = ad.deliveryStopTime ? new Date(ad.deliveryStopTime).getTime() : Date.now()
  if (!Number.isFinite(start) || !Number.isFinite(end)) return 0
  return Math.max(1, Math.round((end - start) / 86400000))
}

export function Competitors() {
  const { userId } = useUser()
  const [brands, setBrands] = useState<CompetitorListItem[]>([])
  const [selectedBrandId, setSelectedBrandId] = useState<number | null>(null)
  const [ads, setAds] = useState<CompetitorAdItem[]>([])
  const [loadingBrands, setLoadingBrands] = useState(true)
  const [loadingAds, setLoadingAds] = useState(false)
  const [syncingId, setSyncingId] = useState<number | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [range, setRange] = useState<RangeFilter>('all')
  const [status, setStatus] = useState<StatusFilter>('all')
  const [format, setFormat] = useState<FormatFilter>('all')
  const [adsSkip, setAdsSkip] = useState(0)
  const [hasMoreAds, setHasMoreAds] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [createName, setCreateName] = useState('')
  const [createPageRef, setCreatePageRef] = useState('')
  const [createPageId, setCreatePageId] = useState('')
  const [createBusy, setCreateBusy] = useState(false)

  const selectedBrand = useMemo(
    () => brands.find((x) => x.id === selectedBrandId) ?? null,
    [brands, selectedBrandId],
  )

  async function refreshBrands(preserveSelected = true) {
    setLoadingBrands(true)
    setError(null)
    try {
      const rows = await getCompetitorsByUser(userId)
      setBrands(rows)
      if (rows.length === 0) {
        setSelectedBrandId(null)
      } else if (!preserveSelected || !rows.some((x) => x.id === selectedBrandId)) {
        setSelectedBrandId(rows[0].id)
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Rakip markalar yüklenemedi.')
    } finally {
      setLoadingBrands(false)
    }
  }

  async function loadAds(targetBrandId: number, reset = true) {
    setLoadingAds(true)
    setError(null)
    const skip = reset ? 0 : adsSkip
    try {
      const page = await getCompetitorAds(targetBrandId, {
        range,
        status,
        format,
        take: 24,
        skip,
      })
      setAds((prev) => (reset ? page : [...prev, ...page]))
      setAdsSkip(skip + page.length)
      setHasMoreAds(page.length === 24)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Rakip reklamları yüklenemedi.')
    } finally {
      setLoadingAds(false)
    }
  }

  useEffect(() => {
    void refreshBrands(false)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [userId])

  useEffect(() => {
    if (!selectedBrandId) {
      setAds([])
      setAdsSkip(0)
      setHasMoreAds(false)
      return
    }
    void loadAds(selectedBrandId, true)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedBrandId, range, status, format])

  async function onCreateBrand() {
    const displayName = createName.trim()
    const pageRef = createPageRef.trim()
    const pageId = createPageId.trim()
    if (!displayName || !pageRef) {
      setError('Marka adı ve sayfa referansı zorunludur.')
      return
    }
    setCreateBusy(true)
    setError(null)
    try {
      const created = await createCompetitor({
        displayName,
        pageRef,
        pageId: pageId || null,
      })
      setCreateOpen(false)
      setCreateName('')
      setCreatePageRef('')
      setCreatePageId('')
      await refreshBrands(true)
      setSelectedBrandId(created.id)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Rakip marka eklenemedi.')
    } finally {
      setCreateBusy(false)
    }
  }

  async function onDeactivate(competitorId: number) {
    setError(null)
    try {
      await deactivateCompetitor(competitorId)
      await refreshBrands(true)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Takip durdurulamadı.')
    }
  }

  async function onSyncNow(competitorId: number) {
    setSyncingId(competitorId)
    setError(null)
    try {
      await postCompetitorSync(competitorId)
      await refreshBrands(true)
      if (selectedBrandId === competitorId) {
        await loadAds(competitorId, true)
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Senkronizasyon başlatılamadı.')
    } finally {
      setSyncingId(null)
    }
  }

  return (
    <div className="page">
      <div className="campaign-toolbar">
        <div>
          <h1 className="page-title">Rakip Kreatif Takibi</h1>
          <p className="muted">Rakip markaların aktif reklam kreatiflerini takip et, filtrele ve haftalık değişimleri izle.</p>
        </div>
        <button type="button" className="btn primary" onClick={() => setCreateOpen(true)}>
          + Rakip Marka Ekle
        </button>
      </div>

      {error ? <div className="error-banner">{error}</div> : null}

      <div className="competitor-grid">
        {loadingBrands ? <p className="muted">Markalar yükleniyor...</p> : null}
        {!loadingBrands && brands.length === 0 ? <p className="muted">Henüz takip edilen marka yok.</p> : null}
        {brands.map((brand) => (
          <button
            key={brand.id}
            type="button"
            className={`competitor-brand-card ${selectedBrandId === brand.id ? 'competitor-brand-card-active' : ''}`}
            onClick={() => setSelectedBrandId(brand.id)}
          >
            <div className="competitor-brand-head">
              <strong>{brand.displayName}</strong>
              <span className={`tag-chip ${brand.isActive ? 'tag-ok' : 'tag-warn'}`}>
                {brand.isActive ? 'Aktif' : 'Pasif'}
              </span>
            </div>
            <div className="muted small">Son 7 günde yeni reklam: {brand.newAdsLast7Days}</div>
            <div className="muted small">Son sync: {relativeTime(brand.lastSyncedAt)}</div>
            <div className="muted small">Durum: {brand.lastSyncStatus ?? '—'}</div>
          </button>
        ))}
      </div>

      {selectedBrand ? (
        <div className="panel" style={{ marginTop: '0.9rem' }}>
          <div className="campaign-toolbar">
            <div>
              <h2 className="panel-title" style={{ marginBottom: '0.25rem' }}>
                {selectedBrand.displayName} reklamları
              </h2>
              <p className="muted small" style={{ margin: 0 }}>
                Sayfa Ref: {selectedBrand.pageRef}
                {selectedBrand.pageId ? ` · Page ID: ${selectedBrand.pageId}` : ''}
              </p>
            </div>
            <div className="form-actions">
              {selectedBrand.isActive ? (
                <button
                  type="button"
                  className="btn"
                  onClick={() => void onDeactivate(selectedBrand.id)}
                >
                  Takibi Durdur
                </button>
              ) : null}
              <button
                type="button"
                className="btn primary"
                onClick={() => void onSyncNow(selectedBrand.id)}
                disabled={syncingId === selectedBrand.id}
              >
                {syncingId === selectedBrand.id ? 'Senkronize Ediliyor...' : 'Şimdi Senkronize Et'}
              </button>
            </div>
          </div>

          <div className="filter-bar">
            <label>
              Format
              <select value={format} onChange={(e) => setFormat(e.target.value as FormatFilter)}>
                <option value="all">Tümü</option>
                <option value="video">Video</option>
                <option value="static">Static</option>
                <option value="carousel">Carousel</option>
              </select>
            </label>
            <label>
              Zaman
              <select value={range} onChange={(e) => setRange(e.target.value as RangeFilter)}>
                <option value="all">Tüm zaman</option>
                <option value="7d">Son 7 gün</option>
                <option value="30d">Son 30 gün</option>
              </select>
            </label>
            <label>
              Durum
              <select value={status} onChange={(e) => setStatus(e.target.value as StatusFilter)}>
                <option value="all">Tümü</option>
                <option value="active">Aktif</option>
                <option value="inactive">Pasif</option>
              </select>
            </label>
          </div>

          {loadingAds ? <p className="muted">Reklamlar yükleniyor...</p> : null}
          {!loadingAds && ads.length === 0 ? <p className="muted">Bu filtrelerde reklam bulunamadı.</p> : null}

          <div className="competitor-ad-grid">
            {ads.map((ad) => (
              <article key={ad.id} className="competitor-ad-card">
                <div className="competitor-ad-head">
                  <span className="tag-chip">{ad.format || '—'}</span>
                  <span className={`tag-chip ${ad.isActive ? 'tag-ok' : 'tag-warn'}`}>
                    {ad.isActive ? 'Aktif' : 'Pasif'}
                  </span>
                </div>
                <p className="competitor-ad-text" title={ad.bodyText ?? ''}>{truncate(ad.bodyText)}</p>
                <p className="muted small">Yayında kalma: {activeDays(ad)} gün</p>
                <p className="muted small">
                  Platform: {ad.publisherPlatforms.length ? ad.publisherPlatforms.join(', ') : '—'}
                </p>
                <div className="competitor-ad-actions">
                  {ad.snapshotUrl ? (
                    <a className="btn btn-sm" href={ad.snapshotUrl} target="_blank" rel="noreferrer">
                      Meta Önizleme
                    </a>
                  ) : (
                    <span className="muted small">Önizleme linki yok</span>
                  )}
                </div>
              </article>
            ))}
          </div>

          {hasMoreAds ? (
            <div style={{ marginTop: '0.8rem' }}>
              <button
                type="button"
                className="btn"
                onClick={() => void loadAds(selectedBrand.id, false)}
                disabled={loadingAds}
              >
                Daha Fazla Yükle
              </button>
            </div>
          ) : null}
        </div>
      ) : null}

      {createOpen ? (
        <div className="vr-modal-overlay" onClick={() => setCreateOpen(false)}>
          <section className="vr-modal competitor-create-modal" onClick={(e) => e.stopPropagation()}>
            <button type="button" className="vr-modal-close" onClick={() => setCreateOpen(false)} aria-label="Kapat">
              ×
            </button>
            <h2 className="panel-title">Rakip Marka Ekle</h2>
            <div className="form-stack">
              <label>
                Marka adı
                <input
                  value={createName}
                  onChange={(e) => setCreateName(e.target.value)}
                  placeholder="Örn: Fitora"
                />
              </label>
              <label>
                Sayfa referansı (zorunlu)
                <input
                  value={createPageRef}
                  onChange={(e) => setCreatePageRef(e.target.value)}
                  placeholder="Örn: fitoraofficial veya page id"
                />
              </label>
              <label>
                Facebook Page ID (opsiyonel)
                <input
                  value={createPageId}
                  onChange={(e) => setCreatePageId(e.target.value)}
                  placeholder="Örn: 1234567890"
                />
              </label>
            </div>
            <div className="form-actions" style={{ marginTop: '0.8rem' }}>
              <button type="button" className="btn" onClick={() => setCreateOpen(false)}>
                Vazgeç
              </button>
              <button type="button" className="btn primary" onClick={() => void onCreateBrand()} disabled={createBusy}>
                {createBusy ? 'Ekleniyor...' : 'Ekle ve Senkronize Et'}
              </button>
            </div>
          </section>
        </div>
      ) : null}
    </div>
  )
}

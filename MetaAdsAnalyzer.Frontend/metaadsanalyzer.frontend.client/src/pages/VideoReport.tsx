import { useCallback, useEffect, useMemo, useState } from 'react'
import { Button } from '@/components/ui/button'
import {
  downloadVideoReportPdf,
  getActiveDirectives,
  getLinkedMetaAdAccounts,
  getMetaAdsets,
  getMetaAds,
  getMetaCampaigns,
  getRawInsights,
  getUserProfile,
  getVideoAssets,
  postDirectivesEvaluate,
  postInsightsSync,
  postMetricsRecompute,
  postSelectActiveMetaAdAccount,
  postVideoReportAggregate,
} from '../api/client'
import type {
  DirectiveItem,
  LinkedMetaAdAccountItem,
  MetaAdListItem,
  MetaAdsetItem,
  MetaCampaignItem,
  RawInsightRow,
  VideoAssetRow,
  VideoReportAggregateResponse,
} from '../api/types'
import { useUser } from '../context/UserContext'
import './Pages.css'

const PRESETS = [
  { value: 'last_7d', label: 'Son 7 gün' },
  { value: 'last_14d', label: 'Son 14 gün' },
  { value: 'last_30d', label: 'Son 30 gün' },
  { value: 'last_90d', label: 'Son 90 gün' },
]

const EFF_ACTIVE = 'ACTIVE'

function normEff(s: string | null | undefined): string {
  return (s ?? '').trim().toUpperCase()
}

function isActiveEffective(ad: MetaAdListItem): boolean {
  return normEff(ad.effectiveStatus) === EFF_ACTIVE
}

type VideoGroup = {
  groupKey: string
  videoId: string | null
  ads: MetaAdListItem[]
  thumbnailUrl: string | null
  displayName: string
  statusLine: string
  totalSpend: number
  cardTags: string[]
}

function buildSpendByAdId(raws: RawInsightRow[]): Map<string, number> {
  const latest = new Map<string, RawInsightRow>()
  for (const r of raws) {
    if (r.level !== 'ad') continue
    const prev = latest.get(r.entityId)
    if (!prev || r.fetchedAt > prev.fetchedAt) latest.set(r.entityId, r)
  }
  const m = new Map<string, number>()
  for (const r of latest.values()) {
    m.set(r.entityId, r.spend)
  }
  return m
}

function buildStatusLine(ads: MetaAdListItem[]): string {
  const counts = new Map<string, number>()
  for (const a of ads) {
    const k = normEff(a.effectiveStatus) || 'BİLİNMİYOR'
    counts.set(k, (counts.get(k) ?? 0) + 1)
  }
  if (counts.size === 1) {
    const only = [...counts.keys()][0]
    if (only === 'ACTIVE') return 'Durum: Aktif'
    if (only === 'PAUSED') return 'Durum: Duraklatıldı'
    if (only === 'ARCHIVED') return 'Durum: Arşivlendi'
    if (only === 'BİLİNMİYOR') return 'Durum: —'
    return `Durum: ${only}`
  }
  return [...counts.entries()]
    .map(([st, n]) => `${st}: ${n} reklam`)
    .join(' · ')
}

function buildVideoGroups(
  ads: MetaAdListItem[],
  spendByAdId: Map<string, number>,
  assetRows: VideoAssetRow[],
): VideoGroup[] {
  const tagByVideo = new Map<string, string[]>()
  for (const row of assetRows) {
    tagByVideo.set(row.videoId, row.problemTags ?? [])
  }

  const buckets = new Map<string, MetaAdListItem[]>()
  for (const ad of ads) {
    const vk = ad.videoId?.trim() ? `v:${ad.videoId.trim()}` : `ad:${ad.id}`
    if (!buckets.has(vk)) buckets.set(vk, [])
    buckets.get(vk)!.push(ad)
  }

  const groups: VideoGroup[] = []
  for (const [groupKey, list] of buckets) {
    const videoId = list[0]?.videoId?.trim() ? list[0].videoId!.trim() : null
    const withThumb = list.find((a) => a.thumbnailUrl)
    const thumbnailUrl = withThumb?.thumbnailUrl ?? null
    const displayName =
      list.map((a) => a.creativeName?.trim() || a.videoTitle?.trim()).find(Boolean) ||
      list[0]?.name?.trim() ||
      (videoId ? `Video ${videoId}` : `Reklam ${list[0].id}`)

    let totalSpend = 0
    for (const a of list) {
      totalSpend += spendByAdId.get(a.id) ?? 0
    }

    const cardTags = videoId ? (tagByVideo.get(videoId) ?? []) : []

    groups.push({
      groupKey,
      videoId,
      ads: list,
      thumbnailUrl,
      displayName,
      statusLine: buildStatusLine(list),
      totalSpend,
      cardTags,
    })
  }

  groups.sort((a, b) => b.totalSpend - a.totalSpend)
  return groups
}

function severityRank(s: string): number {
  if (s === 'critical') return 0
  if (s === 'warning') return 1
  return 2
}

function mergeDirectives(dirs: DirectiveItem[]): DirectiveItem[] {
  const sorted = [...dirs].sort((a, b) => severityRank(a.severity) - severityRank(b.severity))
  const byMsg = new Map<string, DirectiveItem>()
  for (const d of sorted) {
    if (!byMsg.has(d.message)) byMsg.set(d.message, d)
  }
  return [...byMsg.values()].sort((a, b) => severityRank(a.severity) - severityRank(b.severity))
}

function pctFmt(v: number | null | undefined, digits = 1): string {
  if (v == null || !Number.isFinite(v)) return '—'
  return `${v.toFixed(digits)}%`
}

function numFmt(v: number | null | undefined, digits = 2): string {
  if (v == null || !Number.isFinite(v)) return '—'
  return v.toFixed(digits)
}

/** API / eski yanıtlar için güvenli varsayılanlar (null alanlar UI’yi düşürmez). */
function sanitizeVideoAggregate(raw: VideoReportAggregateResponse): VideoReportAggregateResponse {
  return {
    spend: Number.isFinite(raw.spend) ? raw.spend : 0,
    impressions: Number.isFinite(raw.impressions) ? raw.impressions : 0,
    reach: Number.isFinite(raw.reach) ? raw.reach : 0,
    linkClicks: Number.isFinite(raw.linkClicks) ? raw.linkClicks : 0,
    purchases: Number.isFinite(raw.purchases) ? raw.purchases : 0,
    purchaseValue: Number.isFinite(raw.purchaseValue) ? raw.purchaseValue : 0,
    ctrLinkPct: Number.isFinite(raw.ctrLinkPct) ? raw.ctrLinkPct : 0,
    linkCvrPct: raw.linkCvrPct != null && Number.isFinite(raw.linkCvrPct) ? raw.linkCvrPct : null,
    thumbstopPct: raw.thumbstopPct != null && Number.isFinite(raw.thumbstopPct) ? raw.thumbstopPct : null,
    holdPct: raw.holdPct != null && Number.isFinite(raw.holdPct) ? raw.holdPct : null,
    completionPct: raw.completionPct != null && Number.isFinite(raw.completionPct) ? raw.completionPct : null,
    roas: raw.roas != null && Number.isFinite(raw.roas) ? raw.roas : null,
    breakEvenRoas: raw.breakEvenRoas != null && Number.isFinite(raw.breakEvenRoas) ? raw.breakEvenRoas : null,
    targetRoas: raw.targetRoas != null && Number.isFinite(raw.targetRoas) ? raw.targetRoas : null,
    creativeScore:
      raw.creativeScore != null && Number.isFinite(raw.creativeScore) ? Math.round(raw.creativeScore) : null,
    narrativeLines: Array.isArray(raw.narrativeLines) ? raw.narrativeLines : [],
    problemTags: Array.isArray(raw.problemTags) ? raw.problemTags : [],
    hasInsightRows: raw.hasInsightRows !== false,
    diagnosticMessage: raw.diagnosticMessage ?? null,
  }
}

export function VideoReport() {
  const { userId } = useUser()
  const [tab, setTab] = useState<'active' | 'past'>('active')
  const [linked, setLinked] = useState<LinkedMetaAdAccountItem[]>([])
  const [activeAct, setActiveAct] = useState<string | null>(null)
  const [accountsLoading, setAccountsLoading] = useState(true)
  const [accountsError, setAccountsError] = useState<string | null>(null)

  const [campaigns, setCampaigns] = useState<MetaCampaignItem[]>([])
  const [campaignsLoading, setCampaignsLoading] = useState(false)
  const [campaignsError, setCampaignsError] = useState<string | null>(null)
  const [selectedCampaignId, setSelectedCampaignId] = useState<string | null>(null)

  const [adsets, setAdsets] = useState<MetaAdsetItem[]>([])
  const [adsetsLoading, setAdsetsLoading] = useState(false)
  const [adsetsError, setAdsetsError] = useState<string | null>(null)
  const [selectedAdsetId, setSelectedAdsetId] = useState<string | null>(null)

  const [ads, setAds] = useState<MetaAdListItem[]>([])
  const [adsLoading, setAdsLoading] = useState(false)
  const [adsError, setAdsError] = useState<string | null>(null)
  const [rawsForSpend, setRawsForSpend] = useState<RawInsightRow[]>([])
  const [videoAssetRows, setVideoAssetRows] = useState<VideoAssetRow[]>([])
  const [filter, setFilter] = useState('')
  const [preset, setPreset] = useState('last_7d')
  const [busyGroupKey, setBusyGroupKey] = useState<string | null>(null)
  const [stepLog, setStepLog] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selectedGroup, setSelectedGroup] = useState<VideoGroup | null>(null)
  const [aggregate, setAggregate] = useState<VideoReportAggregateResponse | null>(null)
  const [mergedDirectives, setMergedDirectives] = useState<DirectiveItem[]>([])
  const [pdfAllowed, setPdfAllowed] = useState<boolean | null>(null)

  const spendByAdId = useMemo(() => buildSpendByAdId(rawsForSpend), [rawsForSpend])

  const filteredAdsForTab = useMemo(() => {
    if (tab === 'active') return ads.filter(isActiveEffective)
    return ads.filter((a) => !isActiveEffective(a))
  }, [ads, tab])

  const videoGroups = useMemo(() => {
    let list = buildVideoGroups(filteredAdsForTab, spendByAdId, videoAssetRows)
    const q = filter.trim().toLowerCase()
    if (q) {
      list = list.filter((g) => {
        const blob = [
          g.displayName,
          g.videoId,
          ...g.ads.map((a) => [a.id, a.name, a.creativeName, a.videoTitle].filter(Boolean).join(' ')),
        ]
          .join(' ')
          .toLowerCase()
        return blob.includes(q)
      })
    }
    list.sort((a, b) => {
      if (b.totalSpend !== a.totalSpend) return b.totalSpend - a.totalSpend
      return b.ads.length - a.ads.length
    })
    return list
  }, [filteredAdsForTab, spendByAdId, filter, tab, videoAssetRows])

  const loadCampaignsForAct = useCallback(
    async (act: string) => {
      setCampaignsLoading(true)
      setCampaignsError(null)
      setSelectedCampaignId(null)
      setSelectedAdsetId(null)
      setAdsets([])
      setAdsetsError(null)
      setAds([])
      setAdsError(null)
      setVideoAssetRows([])
      try {
        const list = await getMetaCampaigns(userId, act)
        setCampaigns(list)
      } catch (e: unknown) {
        setCampaignsError(e instanceof Error ? e.message : 'Kampanyalar yüklenemedi')
        setCampaigns([])
      } finally {
        setCampaignsLoading(false)
      }
    },
    [userId],
  )

  const loadAdsetsForCampaign = useCallback(
    async (act: string, campaignId: string) => {
      setAdsetsLoading(true)
      setAdsetsError(null)
      setSelectedAdsetId(null)
      setAds([])
      setAdsError(null)
      setVideoAssetRows([])
      try {
        const list = await getMetaAdsets(userId, campaignId, act)
        setAdsets(list)
      } catch (e: unknown) {
        setAdsetsError(e instanceof Error ? e.message : 'Reklam setleri yüklenemedi')
        setAdsets([])
      } finally {
        setAdsetsLoading(false)
      }
    },
    [userId],
  )

  const loadAdsForAdset = useCallback(
    async (act: string, adsetId: string) => {
      setAdsLoading(true)
      setAdsError(null)
      try {
        const list = await getMetaAds(userId, act, { adsetId })
        setAds(list)
        try {
          const assets = await getVideoAssets(userId, act)
          setVideoAssetRows(assets)
        } catch {
          setVideoAssetRows([])
        }
      } catch (e: unknown) {
        setAdsError(e instanceof Error ? e.message : 'Reklamlar yüklenemedi')
        setAds([])
        setVideoAssetRows([])
      } finally {
        setAdsLoading(false)
      }
    },
    [userId],
  )

  const refreshSpendMap = useCallback(async () => {
    try {
      const raws = await getRawInsights(userId, 'ad')
      setRawsForSpend(raws)
    } catch {
      setRawsForSpend([])
    }
  }, [userId])

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      setAccountsLoading(true)
      setAccountsError(null)
      try {
        const [profile, linkedRows] = await Promise.all([
          getUserProfile(userId),
          getLinkedMetaAdAccounts(userId),
        ])
        if (cancelled) return
        setLinked(linkedRows)
        setPdfAllowed(profile.planAllowsPdfExport === true)
        const preferred =
          profile.metaAdAccountId &&
          linkedRows.some((l) => l.metaAdAccountId === profile.metaAdAccountId)
            ? profile.metaAdAccountId
            : linkedRows[0]?.metaAdAccountId ?? null
        setActiveAct(preferred)
        if (preferred) {
          await loadCampaignsForAct(preferred)
        }
      } catch (e: unknown) {
        if (!cancelled) {
          setAccountsError(e instanceof Error ? e.message : 'Hesaplar yüklenemedi')
        }
      } finally {
        if (!cancelled) setAccountsLoading(false)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [userId, loadCampaignsForAct])

  useEffect(() => {
    if (!activeAct || ads.length === 0) {
      setRawsForSpend([])
      return
    }
    void refreshSpendMap()
  }, [activeAct, ads.length, refreshSpendMap])

  async function onAccountChange(nextAct: string) {
    setError(null)
    setStepLog(null)
    setSelectedGroup(null)
    setAggregate(null)
    setMergedDirectives([])
    setActiveAct(nextAct)
    try {
      await postSelectActiveMetaAdAccount(userId, nextAct)
      setSelectedCampaignId(null)
      setSelectedAdsetId(null)
      setAdsets([])
      setAds([])
      await loadCampaignsForAct(nextAct)
    } catch (e: unknown) {
      setAccountsError(e instanceof Error ? e.message : 'Hesap seçilemedi')
    }
  }

  function selectGroup(g: VideoGroup) {
    setSelectedGroup((prev) => (prev?.groupKey === g.groupKey ? prev : g))
  }

  useEffect(() => {
    setAggregate(null)
    setMergedDirectives([])
    setError(null)
  }, [selectedGroup?.groupKey])

  useEffect(() => {
    setSelectedGroup(null)
    setAggregate(null)
    setMergedDirectives([])
    setError(null)
    setStepLog(null)
  }, [selectedCampaignId, selectedAdsetId])

  async function runSelectedVideoAnalysis() {
    const group = selectedGroup
    if (!group || !activeAct || !selectedAdsetId) {
      setError('Önce reklam seti ve video seçin; reklam hesabının doğru olduğundan emin olun.')
      return
    }
    const adIds = group.ads.map((a) => a.id.trim()).filter(Boolean)
    const videoId = group.videoId?.trim() || null
    if (import.meta.env.DEV) {
      // eslint-disable-next-line no-console
      console.info('[VideoReport] Analiz gövdesi:', {
        video_id: videoId,
        ad_ids: adIds,
        metaAdAccountId: activeAct,
        preset,
      })
    }
    setBusyGroupKey(group.groupKey)
    setError(null)
    setStepLog(null)
    setAggregate(null)
    setMergedDirectives([])
    try {
      const sync = await postInsightsSync(userId, 'ad', preset, {
        adIds,
        metaAdAccountId: activeAct,
      })
      setStepLog(
        `Insights: ${sync.rowsFetched} satır çekildi, ${sync.rowsUpserted} kayıt güncellendi (${adIds.length} reklam, ad_id).`,
      )
      const recompute = await postMetricsRecompute(userId, { adIds })
      setStepLog(
        (s) =>
          `${s} Metrik: ${recompute.computedRows} hesaplandı (yalnızca seçilen reklamlar).`,
      )
      const ev = await postDirectivesEvaluate(userId, { adIds })
      setStepLog(
        (s) =>
          `${s} Direktif: ${ev.entitiesEvaluated} varlık, ${ev.directivesCreated} kural çıktısı.`,
      )

      const dList = await getActiveDirectives(userId)
      const dirs = dList.filter((d) => d.entityType === 'ad' && adIds.includes(d.entityId))
      setMergedDirectives(mergeDirectives(dirs))

      const aggRaw = await postVideoReportAggregate({ userId, adIds, metaAdAccountId: activeAct })
      setAggregate(sanitizeVideoAggregate(aggRaw))
      if (import.meta.env.DEV) {
        // eslint-disable-next-line no-console
        console.info('[VideoReport] Özet yanıtı:', {
          hasInsightRows: aggRaw.hasInsightRows,
          diagnosticMessage: aggRaw.diagnosticMessage,
        })
      }

      await refreshSpendMap()
      try {
        const assets = await getVideoAssets(userId, activeAct)
        setVideoAssetRows(assets)
      } catch {
        /* ignore */
      }
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusyGroupKey(null)
    }
  }

  return (
    <div className="page">
      <h1 className="page-title">AI Video Rapor</h1>
      <p className="page-lead">
        Önce <strong>reklam hesabı</strong>, ardından <strong>kampanya</strong> ve <strong>reklam seti</strong> seçin.
        Set içindeki reklamlar aynı <strong>video_id</strong> altında gruplanır; kartı seçip{' '}
        <strong>Bu Videoyu Analiz Et</strong> ile yalnızca o videoya ait reklamlar için insights senkronu, metrik ve
        direktif çalışır.
      </p>

      <section className="panel">
        {accountsLoading && <p className="muted">Hesaplar yükleniyor…</p>}
        {accountsError && <p className="error-banner">{accountsError}</p>}
        {!accountsLoading && linked.length === 0 && (
          <p className="muted">
            Bağlı reklam hesabı yok. Meta ile giriş yapın veya Ayarlar üzerinden hesap bağlayın.
          </p>
        )}
        {!accountsLoading && linked.length > 0 && (
          <div className="form-stack" style={{ maxWidth: '28rem' }}>
            <label>
              Reklam hesabı
              <select
                value={activeAct ?? ''}
                onChange={(e) => void onAccountChange(e.target.value)}
                disabled={!!busyGroupKey}
              >
                {linked.map((a) => (
                  <option key={a.id} value={a.metaAdAccountId}>
                    {a.displayName?.trim() || a.metaAdAccountId}
                  </option>
                ))}
              </select>
            </label>
            <label className="muted small" style={{ flexDirection: 'row', alignItems: 'center', gap: '0.5rem' }}>
              Analiz dönemi (preset)
              <select value={preset} onChange={(e) => setPreset(e.target.value)} disabled={!!busyGroupKey}>
                {PRESETS.map((p) => (
                  <option key={p.value} value={p.value}>
                    {p.label}
                  </option>
                ))}
              </select>
            </label>
            <div className="form-actions">
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={
                  !!busyGroupKey ||
                  !activeAct ||
                  campaignsLoading ||
                  (selectedAdsetId ? adsLoading : selectedCampaignId ? adsetsLoading : campaignsLoading)
                }
                onClick={() => {
                  if (!activeAct) return
                  if (selectedAdsetId) void loadAdsForAdset(activeAct, selectedAdsetId)
                  else if (selectedCampaignId) void loadAdsetsForCampaign(activeAct, selectedCampaignId)
                  else void loadCampaignsForAct(activeAct)
                }}
              >
                Listeyi yenile
              </Button>
            </div>
          </div>
        )}
      </section>

      {activeAct && !accountsLoading && (
        <section className="panel" style={{ marginTop: '1rem' }}>
          <div className="form-stack" style={{ maxWidth: '28rem', marginBottom: '1rem' }}>
            <label>
              Kampanya
              <select
                value={selectedCampaignId ?? ''}
                disabled={!!busyGroupKey || campaignsLoading || !activeAct}
                onChange={(e) => {
                  const v = e.target.value
                  if (!activeAct) return
                  if (!v) {
                    setSelectedCampaignId(null)
                    setAdsets([])
                    setSelectedAdsetId(null)
                    setAds([])
                    setVideoAssetRows([])
                    return
                  }
                  setSelectedCampaignId(v)
                  void loadAdsetsForCampaign(activeAct, v)
                }}
              >
                <option value="">Kampanya seçin…</option>
                {campaigns.map((c) => (
                  <option key={c.id} value={c.id}>
                    {(c.name?.trim() || c.id) + (c.status ? ` · ${c.status}` : '')}
                  </option>
                ))}
              </select>
            </label>
            {campaignsLoading && <p className="muted">Kampanyalar yükleniyor…</p>}
            {campaignsError && <p className="error-banner">{campaignsError}</p>}
            {!campaignsLoading && !campaignsError && campaigns.length === 0 && (
              <p className="muted">Bu hesapta kampanya bulunamadı.</p>
            )}

            <label>
              Reklam seti
              <select
                value={selectedAdsetId ?? ''}
                disabled={!!busyGroupKey || !selectedCampaignId || adsetsLoading}
                onChange={(e) => {
                  const v = e.target.value
                  if (!activeAct) return
                  if (!v) {
                    setSelectedAdsetId(null)
                    setAds([])
                    setVideoAssetRows([])
                    return
                  }
                  setSelectedAdsetId(v)
                  void loadAdsForAdset(activeAct, v)
                }}
              >
                <option value="">{selectedCampaignId ? 'Reklam seti seçin…' : 'Önce kampanya seçin'}</option>
                {adsets.map((s) => (
                  <option key={s.id} value={s.id}>
                    {(s.name?.trim() || s.id) + (s.status ? ` · ${s.status}` : '')}
                  </option>
                ))}
              </select>
            </label>
            {adsetsLoading && <p className="muted">Reklam setleri yükleniyor…</p>}
            {adsetsError && <p className="error-banner">{adsetsError}</p>}
            {!adsetsLoading &&
              !adsetsError &&
              selectedCampaignId &&
              adsets.length === 0 && (
                <p className="muted">Bu kampanyada reklam seti yok veya erişim yok.</p>
              )}
          </div>

          {!selectedAdsetId && (
            <p className="muted" style={{ marginBottom: '0.75rem' }}>
              Video kartlarını görmek için bir reklam seti seçin.
            </p>
          )}

          <div className="video-report-tablist" role="tablist" aria-label="Video listesi">
            <button
              type="button"
              role="tab"
              aria-selected={tab === 'active'}
              disabled={!selectedAdsetId}
              className={`video-report-tab${tab === 'active' ? ' video-report-tab-active' : ''}`}
              onClick={() => setTab('active')}
            >
              Aktif videolar
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={tab === 'past'}
              disabled={!selectedAdsetId}
              className={`video-report-tab${tab === 'past' ? ' video-report-tab-active' : ''}`}
              onClick={() => setTab('past')}
            >
              Geçmiş videolar
            </button>
          </div>

          {selectedAdsetId && adsLoading && <p className="muted">Reklamlar yükleniyor…</p>}
          {selectedAdsetId && adsError && <p className="error-banner">{adsError}</p>}
          {selectedAdsetId && !adsLoading && !adsError && videoGroups.length === 0 && (
            <p className="muted">
              {tab === 'active'
                ? 'ACTIVE durumunda reklam yok veya bu hesapta video içerikli reklam bulunamadı.'
                : 'Duraklatılmış / arşiv veya diğer durumda reklam yok.'}
            </p>
          )}
          {selectedAdsetId && !adsLoading && videoGroups.length > 0 && (
            <>
              <label className="muted small" style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
                Ara
                <input value={filter} onChange={(e) => setFilter(e.target.value)} placeholder="Kreatif, video ID…" />
              </label>
              <p className="muted small" style={{ marginTop: '0.5rem' }}>
                Sıralama: hesaptaki son bilinen harcamaya göre (yüksek → düşük). Karttaki etiketler{' '}
                <code>video_assets</code> özetinden gelir (reklam listesi yenilendiğinde güncellenir).
              </p>
              <div className="video-report-grid">
                {videoGroups.map((g) => (
                  <div
                    key={g.groupKey}
                    role="button"
                    tabIndex={0}
                    className={`video-ad-card${busyGroupKey === g.groupKey ? ' video-ad-card-busy' : ''}${selectedGroup?.groupKey === g.groupKey ? ' video-ad-card-selected' : ''}`}
                    onClick={() => !busyGroupKey && selectGroup(g)}
                    onKeyDown={(e) => {
                      if (!busyGroupKey && (e.key === 'Enter' || e.key === ' ')) {
                        e.preventDefault()
                        selectGroup(g)
                      }
                    }}
                  >
                    <div className="video-ad-thumb-wrap">
                      {g.thumbnailUrl ? (
                        <img src={g.thumbnailUrl} alt="" className="video-ad-thumb" loading="lazy" />
                      ) : (
                        <div className="video-ad-thumb-fallback" aria-hidden />
                      )}
                    </div>
                    <div className="video-ad-card-body">
                      <div className="video-ad-title">{g.displayName}</div>
                      <div className="video-ad-meta muted small">
                        {g.ads.length} reklamda kullanılıyor
                        {g.totalSpend > 0 && <> · ~{g.totalSpend.toFixed(2)} harcama (önbellek)</>}
                      </div>
                      <div className="video-ad-sub muted small">{g.statusLine}</div>
                      {g.videoId && (
                        <div className="video-ad-id muted small" title={g.videoId}>
                          video_id: {g.videoId}
                        </div>
                      )}
                      {g.cardTags.length > 0 && (
                        <div className="video-ad-tags" style={{ marginTop: '0.35rem' }}>
                          {g.cardTags.map((t) => (
                            <span key={t} className="health-pill" style={{ marginRight: '0.25rem', fontSize: '0.65rem' }}>
                              {t}
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </section>
      )}

      {selectedGroup && (
        <section className="panel" style={{ marginTop: '1.25rem' }}>
          <h2 className="panel-title">Seçilen video</h2>
          <p className="muted small">
            <strong>{selectedGroup.displayName}</strong> · {selectedGroup.ads.length} reklam ·
            {selectedGroup.videoId ? ` video_id ${selectedGroup.videoId}` : ' görsel / tek reklam grubu'}
          </p>
          <div className="form-actions" style={{ marginTop: '0.75rem', flexWrap: 'wrap', gap: '0.5rem' }}>
            <Button
              type="button"
              disabled={!!busyGroupKey}
              onClick={() => void runSelectedVideoAnalysis()}
            >
              Bu Videoyu Analiz Et
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={
                !aggregate?.hasInsightRows ||
                pdfAllowed !== true ||
                !!busyGroupKey
              }
              onClick={() =>
                void downloadVideoReportPdf({
                  userId,
                  adIds: selectedGroup.ads.map((a) => a.id),
                  metaAdAccountId: activeAct ?? undefined,
                  videoId: selectedGroup.videoId ?? undefined,
                  displayName: selectedGroup.displayName,
                })
              }
            >
              PDF İndir
            </Button>
          </div>
          {pdfAllowed === false && (
            <p className="muted small" style={{ marginTop: '0.5rem' }}>
              PDF dışa aktarma Pro planda.
            </p>
          )}
        </section>
      )}

      {error && <p className="error-banner">{error}</p>}
      {stepLog && <p className="ok-banner">{stepLog}</p>}

      {aggregate && selectedGroup && (
        <section className="panel" style={{ marginTop: '1.25rem' }}>
          <h2 className="panel-title">Birleşik analiz sonucu</h2>
          {aggregate.hasInsightRows === false && aggregate.diagnosticMessage && (
            <p className="error-banner" style={{ marginTop: '0.5rem' }}>
              {aggregate.diagnosticMessage}
            </p>
          )}
          <p className="muted small">
            Harcama ağırlıklı oranlar (Graph ham satırı; video_p95 vb. boş olabilir): Thumbstop {pctFmt(aggregate.thumbstopPct)} ·
            Hold {pctFmt(aggregate.holdPct)} · Completion {pctFmt(aggregate.completionPct)}
          </p>
          <div className="muted small" style={{ marginTop: '0.75rem' }}>
            <strong>Birleşik metrikler</strong>: Harcama {numFmt(aggregate.spend)} · Gösterim {aggregate.impressions} ·
            Erişim {aggregate.reach} · Tıklama {aggregate.linkClicks} · Satın alma {aggregate.purchases} · Satış cirosu{' '}
            {numFmt(aggregate.purchaseValue)}
            {aggregate.roas != null && Number.isFinite(aggregate.roas) && <> · ROAS {numFmt(aggregate.roas)}</>}
            {aggregate.breakEvenRoas != null && Number.isFinite(aggregate.breakEvenRoas) && (
              <> · Kâr eşiği ROAS {numFmt(aggregate.breakEvenRoas)}</>
            )}
            {aggregate.targetRoas != null && Number.isFinite(aggregate.targetRoas) && (
              <> · Hedef ROAS {numFmt(aggregate.targetRoas)}</>
            )}
          </div>
          {aggregate.creativeScore != null && (
            <p className="muted small" style={{ marginTop: '0.5rem' }}>
              Kreatif skor: <strong>{aggregate.creativeScore}</strong>/100
            </p>
          )}
          {(aggregate.problemTags ?? []).length > 0 && (
            <div style={{ marginTop: '0.75rem' }}>
              <strong className="muted small">Problem etiketleri</strong>
              <div style={{ marginTop: '0.35rem' }}>
                {(aggregate.problemTags ?? []).map((t) => (
                  <span key={t} className="health-pill" style={{ marginRight: '0.35rem' }}>
                    {t}
                  </span>
                ))}
              </div>
            </div>
          )}
          {(aggregate.narrativeLines ?? []).length > 0 && (
            <ul className="muted small" style={{ marginTop: '0.75rem' }}>
              {(aggregate.narrativeLines ?? []).map((line, i) => (
                <li key={i} style={{ marginBottom: '0.35rem' }}>
                  {line}
                </li>
              ))}
            </ul>
          )}
          {mergedDirectives.length > 0 && (
            <>
              <h3 className="panel-title" style={{ marginTop: '1rem', fontSize: '1rem' }}>
                Direktifler
              </h3>
              <ul className="creative-grid" style={{ marginTop: '0.5rem' }}>
                {mergedDirectives.map((d) => (
                  <li key={d.id} className="creative-card">
                    <div className="creative-top">
                      {d.score != null && <span className="score-badge">{d.score}</span>}
                      {d.healthStatus && <span className="health-pill">{d.healthStatus}</span>}
                      <span className="muted small" style={{ fontSize: '0.7rem' }}>
                        reklam {d.entityId}
                      </span>
                    </div>
                    <p className="creative-msg">{d.message}</p>
                  </li>
                ))}
              </ul>
            </>
          )}
        </section>
      )}
    </div>
  )
}

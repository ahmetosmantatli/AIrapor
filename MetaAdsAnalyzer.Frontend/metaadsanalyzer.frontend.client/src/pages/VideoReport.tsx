import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import {
  getActiveDirectives,
  getLinkedMetaAdAccounts,
  getMetaAdsets,
  getMetaAds,
  getMetaCampaigns,
  getCampaignMaps,
  getRawInsights,
  getUserProfile,
  postDirectivesEvaluate,
  postInsightsRefresh,
  postInsightsSync,
  postMetricsRecompute,
  postSelectActiveMetaAdAccount,
  postVideoReportAggregate,
  createProduct,
  createCampaignMap,
} from '../api/client'
import type {
  DirectiveItem,
  LinkedMetaAdAccountItem,
  MetaAdListItem,
  MetaAdsetItem,
  MetaCampaignItem,
  RawInsightRow,
  VideoReportAggregateResponse,
} from '../api/types'
import { useUser } from '../context/UserContext'
import { addAnalyzedAd, listAnalyzedAds, updateRecommendationStatus, type AnalyzedAdItem } from '../features/analyzedAdsStore'
import './Pages.css'

const PRESETS = [
  { value: 'last_7d', label: 'Son 7 gün' },
  { value: 'last_14d', label: 'Son 14 gün' },
  { value: 'last_30d', label: 'Son 30 gün' },
  { value: 'last_90d', label: 'Son 90 gün' },
]

function normEff(s: string | null | undefined): string {
  return (s ?? '').trim().toUpperCase()
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

type CampaignAdsetBundle = {
  adset: MetaAdsetItem
  ads: MetaAdListItem[]
}

type AdAnalysisResult = {
  adId: string
  adName: string
  adsetId: string
  adsetName: string
  score: number | null
  aggregate: VideoReportAggregateResponse
  directives: DirectiveItem[]
  selectedGroup: VideoGroup
  savedAnalyzedItem: AnalyzedAdItem | null
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

type FunnelStep = { label: string; value: number; lossPct: number | null; lostCount: number }

function buildFunnel(agg: VideoReportAggregateResponse, isVideo: boolean): FunnelStep[] {
  const steps = isVideo
    ? [
        { label: 'Gösterim', value: agg.impressions },
        { label: '3s İzleme', value: agg.videoPlay3s },
        { label: '%50 İzleme', value: agg.videoP50 },
        { label: 'ThruPlay', value: agg.thruPlay },
        { label: 'Tıklama', value: agg.linkClicks },
        { label: 'Sepet', value: agg.addToCart },
        { label: 'Ödeme Başlat', value: agg.initiateCheckout },
        { label: 'Satın Alma', value: agg.purchases },
      ]
    : [
        { label: 'Gösterim', value: agg.impressions },
        { label: 'Tıklama', value: agg.linkClicks },
        { label: 'Sepet', value: agg.addToCart },
        { label: 'Ödeme Başlat', value: agg.initiateCheckout },
        { label: 'Satın Alma', value: agg.purchases },
      ]
  return steps.map((s, i) => {
    if (i === 0) return { ...s, lossPct: null, lostCount: 0 }
    const prev = steps[i - 1].value
    if (prev <= 0) return { ...s, lossPct: null, lostCount: 0 }
    const lost = Math.max(0, prev - s.value)
    return { ...s, lostCount: lost, lossPct: (lost / prev) * 100 }
  })
}

function statusIcon(ok: boolean, warn: boolean): string {
  if (ok) return '✅'
  if (warn) return '⚠️'
  return '❌'
}

function directiveTypeLabel(value: string | null | undefined): string {
  const t = (value ?? '').trim().toUpperCase()
  if (t === 'OPTIMIZE') return 'Optimize Et'
  if (t === 'SCALE') return 'Ölçekle'
  if (t === 'STOP') return 'Durdur'
  if (t === 'WATCH') return 'İzle'
  return value ?? 'İzle'
}

function scoreLetter(score: number | null | undefined): string {
  const s = score ?? 0
  if (s >= 80) return 'A'
  if (s >= 60) return 'B'
  if (s >= 40) return 'C'
  return 'D'
}

function diagnosisText(aggregate: VideoReportAggregateResponse): string {
  const first = (aggregate.narrativeLines ?? []).find((x) => x.trim().length > 0)
  if (first) return first
  if ((aggregate.problemTags ?? []).length > 0) {
    return `Bu video setinde öne çıkan konu: ${aggregate.problemTags.join(', ')}.`
  }
  return 'Seçilen video seti için metrikler işlendi. Hook/hold/completion verisine göre aksiyon önerileri üretildi.'
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
    addToCart: Number.isFinite(raw.addToCart) ? raw.addToCart : 0,
    initiateCheckout: Number.isFinite(raw.initiateCheckout) ? raw.initiateCheckout : 0,
    videoPlay3s: Number.isFinite(raw.videoPlay3s) ? raw.videoPlay3s : 0,
    videoP25: Number.isFinite(raw.videoP25) ? raw.videoP25 : 0,
    videoP50: Number.isFinite(raw.videoP50) ? raw.videoP50 : 0,
    videoP75: Number.isFinite(raw.videoP75) ? raw.videoP75 : 0,
    videoP100: Number.isFinite(raw.videoP100) ? raw.videoP100 : 0,
    thruPlay: Number.isFinite(raw.thruPlay) ? raw.thruPlay : 0,
    ctrLinkPct: Number.isFinite(raw.ctrLinkPct) ? raw.ctrLinkPct : 0,
    linkCvrPct: raw.linkCvrPct != null && Number.isFinite(raw.linkCvrPct) ? raw.linkCvrPct : null,
    thumbstopPct: raw.thumbstopPct != null && Number.isFinite(raw.thumbstopPct) ? raw.thumbstopPct : null,
    holdPct: raw.holdPct != null && Number.isFinite(raw.holdPct) ? raw.holdPct : null,
    completionPct: raw.completionPct != null && Number.isFinite(raw.completionPct) ? raw.completionPct : null,
    roas: raw.roas != null && Number.isFinite(raw.roas) ? raw.roas : null,
    cpa: raw.cpa != null && Number.isFinite(raw.cpa) ? raw.cpa : null,
    breakEvenRoas: raw.breakEvenRoas != null && Number.isFinite(raw.breakEvenRoas) ? raw.breakEvenRoas : null,
    targetRoas: raw.targetRoas != null && Number.isFinite(raw.targetRoas) ? raw.targetRoas : null,
    maxCpa: raw.maxCpa != null && Number.isFinite(raw.maxCpa) ? raw.maxCpa : null,
    targetCpa: raw.targetCpa != null && Number.isFinite(raw.targetCpa) ? raw.targetCpa : null,
    netProfitPerOrder:
      raw.netProfitPerOrder != null && Number.isFinite(raw.netProfitPerOrder) ? raw.netProfitPerOrder : null,
    netMarginPct: raw.netMarginPct != null && Number.isFinite(raw.netMarginPct) ? raw.netMarginPct : null,
    hasProductMap: raw.hasProductMap === true,
    dataQuality: {
      insufficientImpressions: raw.dataQuality?.insufficientImpressions === true,
      lowPurchases: raw.dataQuality?.lowPurchases === true,
      earlyData: raw.dataQuality?.earlyData === true,
      learningPhase: raw.dataQuality?.learningPhase === true,
      insufficientSpend: raw.dataQuality?.insufficientSpend === true,
      warnings: Array.isArray(raw.dataQuality?.warnings) ? raw.dataQuality.warnings : [],
    },
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
  const [linked, setLinked] = useState<LinkedMetaAdAccountItem[]>([])
  const [activeAct, setActiveAct] = useState<string | null>(null)
  const [accountsLoading, setAccountsLoading] = useState(true)
  const [accountsError, setAccountsError] = useState<string | null>(null)

  const [campaigns, setCampaigns] = useState<MetaCampaignItem[]>([])
  const [campaignsLoading, setCampaignsLoading] = useState(false)
  const [campaignsError, setCampaignsError] = useState<string | null>(null)
  const [selectedCampaignId, setSelectedCampaignId] = useState<string | null>(null)

  const [adsetsLoading, setAdsetsLoading] = useState(false)
  const [adsetsError, setAdsetsError] = useState<string | null>(null)
  const [analysisByAdset, setAnalysisByAdset] = useState<AdAnalysisResult[]>([])
  const [analysisLoading, setAnalysisLoading] = useState(false)
  const [analysisPreparedText, setAnalysisPreparedText] = useState('')
  const [activeAnalysis, setActiveAnalysis] = useState<AdAnalysisResult | null>(null)
  const [campaignPreparing, setCampaignPreparing] = useState(false)
  const [campaignLastAnalyzedAt, setCampaignLastAnalyzedAt] = useState<string | null>(null)

  const [preset, setPreset] = useState('last_7d')
  const [stepLog, setStepLog] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [resultModalOpen, setResultModalOpen] = useState(false)
  const [appliedRecIds, setAppliedRecIds] = useState<Set<string>>(new Set())
  const [skippedRecIds, setSkippedRecIds] = useState<Set<string>>(new Set())
  const [showDataQualityBanner, setShowDataQualityBanner] = useState(true)
  const [profitabilityModalOpen, setProfitabilityModalOpen] = useState(false)
  const [pendingCampaignAnalyze, setPendingCampaignAnalyze] = useState(false)
  const [savingProfitability, setSavingProfitability] = useState(false)
  const [salePriceInput, setSalePriceInput] = useState('')
  const [cogsInput, setCogsInput] = useState('')
  const [shippingInput, setShippingInput] = useState('')
  const [feePctInput, setFeePctInput] = useState('2.9')
  const [profitabilityErrors, setProfitabilityErrors] = useState<string[]>([])

  const aggregate = activeAnalysis?.aggregate ?? null
  const mergedDirectives = activeAnalysis?.directives ?? []
  const selectedGroup = activeAnalysis?.selectedGroup ?? null
  const savedAnalyzedItem = activeAnalysis?.savedAnalyzedItem ?? null

  const loadCampaignsForAct = useCallback(
    async (act: string) => {
      setCampaignsLoading(true)
      setCampaignsError(null)
      setSelectedCampaignId(null)
      setAdsetsError(null)
      setAnalysisByAdset([])
      setActiveAnalysis(null)
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

  useEffect(() => {
    let cancelled = false
    if (!selectedCampaignId) {
      setCampaignLastAnalyzedAt(null)
      return
    }
    ;(async () => {
      try {
        const rows = await listAnalyzedAds(userId)
        if (cancelled) return
        const latest = rows
          .filter((x) => x.campaignId === selectedCampaignId)
          .sort((a, b) => b.analyzedAt.localeCompare(a.analyzedAt))[0]
        setCampaignLastAnalyzedAt(latest?.analyzedAt ?? null)
      } catch {
        if (!cancelled) setCampaignLastAnalyzedAt(null)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [selectedCampaignId, userId])

  const loadCampaignBundles = useCallback(
    async (act: string, campaignId: string): Promise<CampaignAdsetBundle[]> => {
      const adsetList = await getMetaAdsets(userId, campaignId, act)
      const bundles = await Promise.all(
        adsetList.map(async (adset) => {
          const ads = await getMetaAds(userId, act, { adsetId: adset.id })
          return { adset, ads } satisfies CampaignAdsetBundle
        }),
      )
      return bundles.filter((b) => b.ads.length > 0)
    },
    [userId],
  )

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

  async function onAccountChange(nextAct: string) {
    setError(null)
    setStepLog(null)
    setActiveAnalysis(null)
    setAnalysisByAdset([])
    setActiveAct(nextAct)
    try {
      await postSelectActiveMetaAdAccount(userId, nextAct)
      setSelectedCampaignId(null)
      await loadCampaignsForAct(nextAct)
    } catch (e: unknown) {
      setAccountsError(e instanceof Error ? e.message : 'Hesap seçilemedi')
    }
  }

  async function ensureCampaignProfitabilityMapOrPrompt(): Promise<boolean> {
    if (!selectedCampaignId) return true
    try {
      const maps = await getCampaignMaps(userId)
      const hasMap = maps.some((m) => m.campaignId === selectedCampaignId)
      if (hasMap) return true
      setPendingCampaignAnalyze(true)
      setProfitabilityModalOpen(true)
      return false
    } catch {
      return true
    }
  }

  useEffect(() => {
    if (!campaignPreparing) {
      setAnalysisPreparedText('')
      return
    }
    const full = 'Kampanya analiziniz hazırlanıyor'
    let i = 0
    const tm = window.setInterval(() => {
      i += 1
      setAnalysisPreparedText(full.slice(0, i))
      if (i >= full.length) window.clearInterval(tm)
    }, 52)
    return () => window.clearInterval(tm)
  }, [campaignPreparing])

  const runAdsetAnalysis = useCallback(
    async (bundle: CampaignAdsetBundle, ad: MetaAdListItem): Promise<AdAnalysisResult | null> => {
      if (!activeAct || !selectedCampaignId) return null
      const adId = ad.id?.trim()
      if (!adId) return null

      await postInsightsSync(userId, 'ad', preset, { adIds: [adId], metaAdAccountId: activeAct })
      await postMetricsRecompute(userId, { adIds: [adId] })
      await postDirectivesEvaluate(userId, { adIds: [adId] })

      const dList = await getActiveDirectives(userId)
      const dirs = dList.filter((d) => d.entityType === 'ad' && d.entityId === adId)
      const mergedDirs = mergeDirectives(dirs)
      const aggRaw = await postVideoReportAggregate({ userId, adIds: [adId], metaAdAccountId: activeAct })
      const aggregate = sanitizeVideoAggregate(aggRaw)
      const firstAd = ad

      let savedAnalyzedItem: AnalyzedAdItem | null = null
      if (firstAd) {
        savedAnalyzedItem = await addAnalyzedAd({
          userId,
          adId: firstAd.id,
          adName: firstAd.name?.trim() || firstAd.creativeName?.trim() || firstAd.id,
          thumbnailUrl: firstAd.thumbnailUrl ?? null,
          campaignId: selectedCampaignId,
          campaignName: campaigns.find((x) => x.id === selectedCampaignId)?.name ?? selectedCampaignId,
          adsetId: bundle.adset.id,
          adsetName: bundle.adset.name ?? bundle.adset.id,
          aggregate,
          directives: mergedDirs,
        })
      }

      const selectedGroup: VideoGroup = {
        groupKey: `ad:${adId}`,
        videoId: firstAd?.videoId?.trim() || null,
        ads: [firstAd],
        thumbnailUrl: firstAd?.thumbnailUrl ?? null,
        displayName: firstAd?.name?.trim() || firstAd?.creativeName?.trim() || adId,
        statusLine: buildStatusLine([firstAd]),
        totalSpend: aggregate.spend,
        cardTags: [],
      }

      return {
        adId,
        adName: firstAd?.name?.trim() || firstAd?.creativeName?.trim() || adId,
        adsetId: bundle.adset.id,
        adsetName: bundle.adset.name?.trim() || bundle.adset.id,
        score: aggregate.creativeScore,
        aggregate,
        directives: mergedDirs,
        selectedGroup,
        savedAnalyzedItem,
      }
    },
    [activeAct, campaigns, preset, selectedCampaignId, userId],
  )

  const runCampaignAnalysisCore = useCallback(async () => {
    if (!activeAct || !selectedCampaignId) {
      setError('Önce reklam hesabı ve kampanya seçin.')
      return
    }
    setError(null)
    setStepLog(null)
    setAnalysisByAdset([])
    setActiveAnalysis(null)
    setAnalysisLoading(true)
    setCampaignPreparing(true)
    try {
      const latestRows = await getRawInsights(userId, 'ad', { limit: 1, campaignId: selectedCampaignId })
      const latestAt = latestRows[0]?.fetchedAt ? new Date(latestRows[0].fetchedAt).getTime() : 0
      const olderThan4h = !latestAt || Date.now() - latestAt > 4 * 60 * 60 * 1000
      if (olderThan4h) {
        await postInsightsRefresh(userId, activeAct)
      }

      setAdsetsLoading(true)
      setAdsetsError(null)
      const bundles = await loadCampaignBundles(activeAct, selectedCampaignId)
      if (bundles.length === 0) {
        setError('Seçilen kampanyada analiz edilecek reklam bulunamadı.')
        return
      }
      const minWait = new Promise<void>((resolve) => {
        window.setTimeout(resolve, 25000)
      })
      const results: AdAnalysisResult[] = []
      for (const bundle of bundles) {
        for (const ad of bundle.ads) {
          const one = await runAdsetAnalysis(bundle, ad)
          if (one) results.push(one)
        }
      }
      await minWait
      setAnalysisByAdset(results)
      setActiveAnalysis(results[0] ?? null)
      setStepLog(`Kampanya analizi tamamlandı. ${results.length} reklam raporlandı.`)
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Kampanya analizi başarısız')
    } finally {
      setAdsetsLoading(false)
      setAnalysisLoading(false)
      setCampaignPreparing(false)
    }
  }, [activeAct, loadCampaignBundles, runAdsetAnalysis, selectedCampaignId, userId])

  async function runCampaignAnalysis() {
    const ok = await ensureCampaignProfitabilityMapOrPrompt()
    if (!ok) return
    await runCampaignAnalysisCore()
  }

  function parseOptionalNumber(raw: string): number | null | 'invalid' {
    const v = raw.trim()
    if (!v) return null
    const n = Number(v.replace(',', '.'))
    if (!Number.isFinite(n)) return 'invalid'
    return n
  }

  function validateProfitabilityInputs(): string[] {
    const errs: string[] = []
    const sale = parseOptionalNumber(salePriceInput)
    const cogs = parseOptionalNumber(cogsInput)
    const shipping = parseOptionalNumber(shippingInput)
    const fee = parseOptionalNumber(feePctInput)

    if (sale === 'invalid') errs.push('Satış fiyatı yalnızca sayısal değer olmalıdır.')
    if (cogs === 'invalid') errs.push('Ürün maliyeti yalnızca sayısal değer olmalıdır.')
    if (shipping === 'invalid') errs.push('Kargo maliyeti yalnızca sayısal değer olmalıdır.')
    if (fee === 'invalid') errs.push('Ödeme komisyonu yalnızca sayısal değer olmalıdır.')

    if (typeof sale === 'number' && sale <= 0) errs.push('Satış fiyatı 0’dan büyük olmalıdır.')
    if (typeof cogs === 'number' && cogs < 0) errs.push('Ürün maliyeti negatif olamaz.')
    if (typeof shipping === 'number' && shipping < 0) errs.push('Kargo maliyeti negatif olamaz.')
    if (typeof fee === 'number' && (fee < 0 || fee > 100)) errs.push('Ödeme komisyonu %0 ile %100 arasında olmalıdır.')

    return errs
  }

  function onApplyRecommendation(recId: string, message: string) {
    setAppliedRecIds((prev) => {
      const next = new Set(prev)
      next.add(recId)
      return next
    })
    setSkippedRecIds((prev) => {
      const next = new Set(prev)
      next.delete(recId)
      return next
    })
    setStepLog(`Öneri uygulandı olarak işaretlendi: ${message}`)
  }

  function onSkipRecommendation(recId: string, message: string) {
    setSkippedRecIds((prev) => {
      const next = new Set(prev)
      next.add(recId)
      return next
    })
    setAppliedRecIds((prev) => {
      const next = new Set(prev)
      next.delete(recId)
      return next
    })
    setStepLog(`Öneri atlandı: ${message}`)
  }

  const isVideoAggregate = Boolean(selectedGroup?.videoId?.trim())
  const funnelSteps = aggregate ? buildFunnel(aggregate, isVideoAggregate) : []
  const timelineValues = aggregate
    ? [
        { pct: 25, value: aggregate.videoP25 },
        { pct: 50, value: aggregate.videoP50 },
        { pct: 75, value: aggregate.videoP75 },
        { pct: 100, value: aggregate.videoP100 },
      ]
    : []
  const biggestDrop = useMemo(() => {
    if (!aggregate) return null
    const allZero = aggregate.videoP25 <= 0 && aggregate.videoP50 <= 0 && aggregate.videoP75 <= 0 && aggregate.videoP100 <= 0
    if (allZero || aggregate.videoP25 <= 0) return null
    const pairs = [
      { key: '%25→%50', markerPct: 50, drop: Math.max(0, aggregate.videoP25 - aggregate.videoP50) },
      { key: '%50→%75', markerPct: 75, drop: Math.max(0, aggregate.videoP50 - aggregate.videoP75) },
      { key: '%75→%100', markerPct: 100, drop: Math.max(0, aggregate.videoP75 - aggregate.videoP100) },
    ]
    const picked = pairs.sort((a, b) => b.drop - a.drop)[0]
    return { ...picked, leavePct: (picked.drop / aggregate.videoP25) * 100 }
  }, [aggregate])

  return (
    <div className="page">
      <h1 className="page-title">AI Video Rapor</h1>
      <p className="page-lead">
        Reklam hesabını ve kampanyayı seçin. <strong>Kampanyayı Analiz Et</strong> ile kampanyanın tüm reklam setleri
        ve içlerindeki reklamlar otomatik analiz edilir.
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
                disabled={analysisLoading}
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
              <select value={preset} onChange={(e) => setPreset(e.target.value)} disabled={analysisLoading}>
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
                disabled={analysisLoading || !activeAct || campaignsLoading}
                onClick={() => {
                  if (!activeAct) return
                  void loadCampaignsForAct(activeAct)
                }}
              >
                Listeyi yenile
              </Button>
            </div>
            <label>
              Kampanya
              <select
                value={selectedCampaignId ?? ''}
                onChange={(e) => setSelectedCampaignId(e.target.value || null)}
                disabled={analysisLoading || campaignsLoading}
              >
                <option value="">Kampanya seçin</option>
                {campaigns.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name?.trim() || c.id}
                  </option>
                ))}
              </select>
            </label>
            <Button
              type="button"
              className="primary"
              disabled={!activeAct || !selectedCampaignId || analysisLoading}
              onClick={() => void runCampaignAnalysis()}
            >
              Kampanyayı Analiz Et
            </Button>
          </div>
        )}
      </section>

      {activeAct && !accountsLoading && (
        <section className="panel" style={{ marginTop: '1rem' }}>
          <p className="muted small">
            {selectedCampaignId
              ? `Seçilen kampanya: ${campaigns.find((x) => x.id === selectedCampaignId)?.name ?? selectedCampaignId}`
              : 'Yukarıdan kampanya seçin.'}
          </p>
          {selectedCampaignId && (
            <p className="muted small" style={{ textAlign: 'right', marginTop: '-0.35rem' }}>
              Son analiz tarihi:{' '}
              <strong>
                {campaignLastAnalyzedAt
                  ? new Date(campaignLastAnalyzedAt).toLocaleString('tr-TR')
                  : '—'}
              </strong>
            </p>
          )}

          {adsetsLoading && <p className="muted">Kampanya içi reklam setleri hazırlanıyor…</p>}
          {adsetsError && <p className="error-banner">{adsetsError}</p>}

          {campaignPreparing && (
            <div className="campaign-row" style={{ marginTop: '0.75rem' }}>
              <div className="campaign-main">
                <div className="campaign-title">{campaigns.find((x) => x.id === selectedCampaignId)?.name ?? selectedCampaignId}</div>
                <div className="campaign-meta muted small">{analysisPreparedText}</div>
              </div>
              <div className="vr-spinner" aria-hidden="true" />
            </div>
          )}

          {!campaignPreparing && analysisByAdset.length > 0 && (
            <div className="campaign-list" style={{ marginTop: '0.75rem' }}>
              <div className="campaign-row">
                <div className="campaign-main">
                  <div className="campaign-title">{campaigns.find((x) => x.id === selectedCampaignId)?.name ?? selectedCampaignId}</div>
                  <div className="campaign-meta muted small">
                    Kampanya analizi tamamlandı · {analysisByAdset.length} reklam analiz edildi
                  </div>
                </div>
              </div>
              {analysisByAdset.map((row) => (
                <button
                  type="button"
                  key={row.adId}
                  className={`analyzed-row ${activeAnalysis?.adId === row.adId ? 'account-card-active' : ''}`}
                  onClick={() => {
                    setActiveAnalysis(row)
                    setAppliedRecIds(new Set())
                    setSkippedRecIds(new Set())
                    setShowDataQualityBanner(true)
                    setResultModalOpen(true)
                  }}
                >
                  <div className="analyzed-thumb-wrap">
                    {row.selectedGroup.thumbnailUrl ? (
                      <img src={row.selectedGroup.thumbnailUrl} alt="" className="analyzed-thumb" />
                    ) : (
                      <div className="analyzed-thumb-fallback" />
                    )}
                  </div>
                  <div className="analyzed-main">
                    <div className="analyzed-title">{row.adName}</div>
                    <div className="muted small">
                      {campaigns.find((x) => x.id === selectedCampaignId)?.name ?? selectedCampaignId} · {row.adsetName}
                    </div>
                    <div className="muted small">
                      reklam {row.adId} · skor {row.score ?? '—'}/100
                    </div>
                    <div className="muted small analyzed-apply-badge">Raporu açmak için tıklayın</div>
                  </div>
                  <div className="analyzed-kpis">
                    <div><span className="muted small">ROAS</span><strong>{numFmt(row.aggregate.roas)}x</strong></div>
                    <div><span className="muted small">Hook</span><strong>{pctFmt(row.aggregate.thumbstopPct)}</strong></div>
                    <div><span className="muted small">Hold</span><strong>{pctFmt(row.aggregate.holdPct)}</strong></div>
                  </div>
                  <div className={`grade-box ${scoreLetter(row.score) === 'A' ? 'grade-good' : scoreLetter(row.score) === 'B' ? 'grade-mid' : 'grade-bad'}`}>
                    {scoreLetter(row.score)}
                  </div>
                </button>
              ))}
            </div>
          )}
        </section>
      )}

      {error && <p className="error-banner">{error}</p>}
      {stepLog && <p className="ok-banner">{stepLog}</p>}

      {aggregate && selectedGroup && resultModalOpen && (
        <div className="vr-modal-overlay" role="dialog" aria-modal="true" aria-label="AI video analiz sonucu">
          <div className="vr-modal">
            <button type="button" className="vr-modal-close" onClick={() => setResultModalOpen(false)} aria-label="Kapat">
              ×
            </button>
            {showDataQualityBanner && aggregate.dataQuality.warnings.length > 0 && (
              <div className="vr-quality-banner">
                <strong>⚠ Veri kalitesi uyarıları</strong>
                <ul>
                  {aggregate.dataQuality.warnings.map((w) => (
                    <li key={w}>{w}</li>
                  ))}
                </ul>
                <div className="form-actions">
                  <button type="button" className="btn" onClick={() => setShowDataQualityBanner(false)}>
                    Gizle
                  </button>
                  <button type="button" className="btn primary" onClick={() => setShowDataQualityBanner(false)}>
                    Yine de göster
                  </button>
                </div>
              </div>
            )}
            <div className="vr-modal-main">
              <div className="vr-modal-left">
                {selectedGroup.thumbnailUrl ? (
                  <img src={selectedGroup.thumbnailUrl} alt="" className="vr-modal-thumb" />
                ) : (
                  <div className="vr-modal-thumb-fallback" />
                )}
              </div>
              <div className="vr-modal-right">
                <div className="vr-diagnosis-box">
                  <strong>AI TEŞHİSİ</strong>
                  <p>{diagnosisText(aggregate)}</p>
                </div>
                <div className="vr-recs">
                  <h3>ÖNERİLER</h3>
                  {(mergedDirectives.length > 0 ? mergedDirectives : []).slice(0, 4).map((d, idx) => {
                    const recId = `dir-${d.id}`
                    const isApplied = appliedRecIds.has(recId)
                    const isSkipped = skippedRecIds.has(recId)
                    const persistedRec = savedAnalyzedItem?.recommendations?.[idx]
                    return (
                    <div key={d.id} className="vr-rec-row">
                      <span className={`vr-priority ${d.severity === 'critical' ? 'vr-priority-high' : 'vr-priority-mid'}`}>
                        {d.severity === 'critical' ? 'ÖNCELİKLİ' : 'ORTA'}
                      </span>
                      <span className="vr-priority vr-priority-mid">{directiveTypeLabel(d.directiveType)}</span>
                      <p>{d.symptom ?? d.message}</p>
                      {d.reason ? <p className="vr-reason">{d.reason}</p> : null}
                      <p className="vr-action">{d.action ?? 'İncele ve uygun aksiyonu uygula.'}</p>
                      {(isApplied || isSkipped) && (
                        <span className={`vr-priority ${isApplied ? 'vr-priority-applied' : 'vr-priority-skipped'}`}>
                          {isApplied ? 'UYGULANDI' : 'ATLANDI'}
                        </span>
                      )}
                      <div className="vr-rec-actions">
                        <button
                          type="button"
                          className="btn"
                          onClick={() => {
                            onSkipRecommendation(recId, d.message)
                            if (persistedRec?.id) {
                              void updateRecommendationStatus(savedAnalyzedItem?.id ?? '', persistedRec.id, 'skipped')
                            }
                          }}
                        >
                          Atla
                        </button>
                        <button
                          type="button"
                          className="btn primary"
                          onClick={() => {
                            onApplyRecommendation(recId, d.message)
                            if (persistedRec?.id) {
                              void updateRecommendationStatus(savedAnalyzedItem?.id ?? '', persistedRec.id, 'applied')
                            }
                          }}
                        >
                          Uygula
                        </button>
                      </div>
                    </div>
                  )})}
                  {mergedDirectives.length === 0 && (
                    <div className="vr-rec-row">
                      {(() => {
                        const recId = 'fallback-rec'
                        const isApplied = appliedRecIds.has(recId)
                        const isSkipped = skippedRecIds.has(recId)
                        const persistedRec = savedAnalyzedItem?.recommendations?.[0]
                        return (
                          <>
                      <span className="vr-priority vr-priority-mid">ORTA</span>
                      <span className="vr-priority vr-priority-mid">Optimize Et</span>
                      <p>Hook ve hold performansı hedefin altında.</p>
                      <p className="vr-reason">Mevcut kreatif kombinasyonu izleyiciyi yeterince taşımıyor.</p>
                      <p className="vr-action">Hazırla: 2-3 yeni kreatif varyantını test akışına al.</p>
                      {(isApplied || isSkipped) && (
                        <span className={`vr-priority ${isApplied ? 'vr-priority-applied' : 'vr-priority-skipped'}`}>
                          {isApplied ? 'UYGULANDI' : 'ATLANDI'}
                        </span>
                      )}
                      <div className="vr-rec-actions">
                        <button
                          type="button"
                          className="btn"
                          onClick={() => {
                            onSkipRecommendation(recId, 'Hook/Hold kreatif varyant önerisi')
                            if (persistedRec?.id) {
                              void updateRecommendationStatus(savedAnalyzedItem?.id ?? '', persistedRec.id, 'skipped')
                            }
                          }}
                        >
                          Atla
                        </button>
                        <button
                          type="button"
                          className="btn primary"
                          onClick={() => {
                            onApplyRecommendation(recId, 'Hook/Hold kreatif varyant önerisi')
                            if (persistedRec?.id) {
                              void updateRecommendationStatus(savedAnalyzedItem?.id ?? '', persistedRec.id, 'applied')
                            }
                          }}
                        >
                          Uygula
                        </button>
                      </div>
                          </>
                        )
                      })()}
                    </div>
                  )}
                </div>
              </div>
            </div>
            <div className="vr-funnel">
              <h3>Dönüşüm Hunisi</h3>
              <div className="vr-funnel-list">
                {funnelSteps.map((step, i) => {
                  const width = Math.max(28, 100 - i * (isVideoAggregate ? 8 : 12))
                  const highDrop = (step.lossPct ?? 0) > 70
                  return (
                    <div key={`${step.label}-${i}`} className="vr-funnel-item-wrap">
                      <div
                        className={`vr-funnel-item ${highDrop ? 'vr-funnel-item-risk' : ''}`}
                        style={{ width: `${width}%` }}
                        title={
                          step.lossPct == null
                            ? `${step.label}`
                            : `Bu adımda ${step.lostCount.toLocaleString('tr-TR')} kişi kaybedildi`
                        }
                      >
                        <span>{step.label}</span>
                        <strong>{step.value.toLocaleString('tr-TR')}</strong>
                        <em>{step.lossPct == null ? '—' : `Düşüş ${step.lossPct.toFixed(1)}%`}</em>
                      </div>
                      {i < funnelSteps.length - 1 && <span className="vr-funnel-arrow">↓</span>}
                    </div>
                  )
                })}
              </div>
              {aggregate.purchases > 0 && aggregate.addToCart === 0 && (
                <p className="muted small" style={{ marginTop: '0.65rem', color: '#f59e0b' }}>
                  Satın alma verisi farklı attribution window'dan gelebilir. Sepet verisi bu pencerede görünmüyor olabilir.
                </p>
              )}
            </div>

            {isVideoAggregate && (
              <div className="vr-timeline">
                <h3>Video Zaman Çizgisi</h3>
                <div className="vr-timeline-bar" />
                <div className="vr-timeline-points">
                  {timelineValues.map((p) => (
                    <div key={p.pct} className="vr-timeline-point" style={{ left: `${p.pct}%` }}>
                      <span>{p.pct}%</span>
                      <strong>{p.value.toLocaleString('tr-TR')}</strong>
                      <em className="muted small">
                        {aggregate.impressions > 0 ? `%${((p.value / aggregate.impressions) * 100).toFixed(1)} izledi` : '%0.0 izledi'}
                      </em>
                    </div>
                  ))}
                </div>
                {biggestDrop ? (
                  <>
                    <div
                      className="vr-drop-arrow"
                      style={{ left: `${biggestDrop.markerPct}%` }}
                      title={`En fazla izleyici kaybi burada — izleyicilerin %${biggestDrop.leavePct.toFixed(1)}'i bu noktada ayrildi`}
                    >
                      ↓
                    </div>
                    <p className="vr-drop-marker">En buyuk kayip: {biggestDrop.key}</p>
                  </>
                ) : (
                  <p className="vr-drop-marker">Yeterli video izlenme verisi yok</p>
                )}
              </div>
            )}

            <div className="vr-metric-content">
              <h3>Metrik Özeti</h3>
              <table className="data-table compact">
                <thead>
                  <tr>
                    <th>Metrik Adı</th>
                    <th>Değer</th>
                    <th>Sektör Ortalaması</th>
                    <th>Durum</th>
                  </tr>
                </thead>
                <tbody>
                  <tr><td>İlk 3 Saniye İzleme (Hook Rate)</td><td>{pctFmt(aggregate.thumbstopPct)}</td><td>Ort: %25 · İyi: &gt;%30</td><td>{statusIcon((aggregate.thumbstopPct ?? 0) > 20, (aggregate.thumbstopPct ?? 0) >= 10 && (aggregate.thumbstopPct ?? 0) <= 20)}</td></tr>
                  <tr><td>İçerik Tutma Gücü (Hold Rate)</td><td>{pctFmt(aggregate.holdPct)}</td><td>Ort: %20 · İyi: &gt;%25</td><td>{statusIcon((aggregate.holdPct ?? 0) > 15, (aggregate.holdPct ?? 0) >= 8 && (aggregate.holdPct ?? 0) <= 15)}</td></tr>
                  <tr><td>Link Tıklama Oranı (CTR)</td><td>{pctFmt(aggregate.ctrLinkPct)}</td><td>Ort: %1.2 · İyi: &gt;%2</td><td>{statusIcon((aggregate.ctrLinkPct ?? 0) > 1, (aggregate.ctrLinkPct ?? 0) >= 0.5 && (aggregate.ctrLinkPct ?? 0) <= 1)}</td></tr>
                  <tr><td>Satın Alma Dönüşümü (CVR)</td><td>{pctFmt(aggregate.linkCvrPct)}</td><td>Ort: %1.5 · İyi: &gt;%3</td><td>{statusIcon((aggregate.linkCvrPct ?? 0) > 1, (aggregate.linkCvrPct ?? 0) >= 0.5 && (aggregate.linkCvrPct ?? 0) <= 1)}</td></tr>
                  <tr><td>Reklam maliyeti</td><td>₺{numFmt(aggregate.cpa)}</td><td>—</td><td>{statusIcon((aggregate.targetCpa ?? 0) > 0 && (aggregate.cpa ?? Number.MAX_VALUE) < (aggregate.targetCpa ?? 0), (aggregate.maxCpa ?? 0) > 0 && (aggregate.cpa ?? Number.MAX_VALUE) < (aggregate.maxCpa ?? 0))}</td></tr>
                  <tr><td>Yatırım Getirisi (ROAS)</td><td>{numFmt(aggregate.roas)}x</td><td>Ort: 2.5x · İyi: &gt;4x</td><td>{statusIcon((aggregate.targetRoas ?? 0) > 0 && (aggregate.roas ?? 0) > (aggregate.targetRoas ?? 0), (aggregate.breakEvenRoas ?? 0) > 0 && (aggregate.roas ?? 0) > (aggregate.breakEvenRoas ?? 0))}</td></tr>
                  <tr>
                    <td>Basabas ROAS</td>
                    <td className={!aggregate.hasProductMap ? 'vr-locked-cell' : ''} title={!aggregate.hasProductMap ? 'Maliyet bilgisi eksik — Ürünler/Kampanyalar eşlemesi gerekli' : undefined}>
                      {!aggregate.hasProductMap ? '🔒 Kilitli' : <span style={{ color: '#ef4444' }}>{`${numFmt(aggregate.breakEvenRoas)}x`}</span>}
                    </td>
                    <td>—</td>
                    <td>{aggregate.hasProductMap ? '✅' : '🔒'}</td>
                  </tr>
                  <tr>
                    <td>Hedef ROAS</td>
                    <td className={!aggregate.hasProductMap ? 'vr-locked-cell' : ''} title={!aggregate.hasProductMap ? 'Maliyet bilgisi eksik — Ürünler/Kampanyalar eşlemesi gerekli' : undefined}>
                      {!aggregate.hasProductMap ? '🔒 Kilitli' : `${numFmt(aggregate.targetRoas)}x`}
                    </td>
                    <td>—</td>
                    <td>{aggregate.hasProductMap ? '✅' : '🔒'}</td>
                  </tr>
                  <tr>
                    <td>Maksimum CPA</td>
                    <td className={!aggregate.hasProductMap ? 'vr-locked-cell' : ''} title={!aggregate.hasProductMap ? 'Maliyet bilgisi eksik — Ürünler/Kampanyalar eşlemesi gerekli' : undefined}>
                      {!aggregate.hasProductMap ? '🔒 Kilitli' : `₺${numFmt(aggregate.maxCpa)}`}
                    </td>
                    <td>—</td>
                    <td>{aggregate.hasProductMap ? '✅' : '🔒'}</td>
                  </tr>
                  <tr>
                    <td>Hedef CPA</td>
                    <td className={!aggregate.hasProductMap ? 'vr-locked-cell' : ''} title={!aggregate.hasProductMap ? 'Maliyet bilgisi eksik — Ürünler/Kampanyalar eşlemesi gerekli' : undefined}>
                      {!aggregate.hasProductMap ? '🔒 Kilitli' : `₺${numFmt(aggregate.targetCpa)}`}
                    </td>
                    <td>—</td>
                    <td>{aggregate.hasProductMap ? '✅' : '🔒'}</td>
                  </tr>
                  <tr>
                    <td>Net kar / sipariş</td>
                    <td className={!aggregate.hasProductMap ? 'vr-locked-cell' : ''} title={!aggregate.hasProductMap ? 'Maliyet bilgisi eksik — kampanya ürün eşlemesi gerekli' : undefined}>
                      {!aggregate.hasProductMap ? '🔒 Kilitli' : `₺${numFmt(aggregate.netProfitPerOrder)}`}
                    </td>
                    <td>—</td>
                    <td>{aggregate.hasProductMap ? '✅' : '🔒'}</td>
                  </tr>
                  <tr>
                    <td>Net kar marjı</td>
                    <td className={!aggregate.hasProductMap ? 'vr-locked-cell' : ''} title={!aggregate.hasProductMap ? 'Maliyet bilgisi eksik — kampanya ürün eşlemesi gerekli' : undefined}>
                      {!aggregate.hasProductMap ? '🔒 Kilitli' : `${numFmt(aggregate.netMarginPct)}%`}
                    </td>
                    <td>—</td>
                    <td>{aggregate.hasProductMap ? '✅' : '🔒'}</td>
                  </tr>
                </tbody>
              </table>
              {!aggregate.hasProductMap && (
                <p className="muted small" style={{ marginTop: '0.55rem' }}>
                  Maliyet bilgisi eksik — Ürünler sayfasından maliyet girin ve Kampanyalar sayfasından eşleyin.{' '}
                  <Link to="/app/products">Ürünler sayfasına git</Link>
                </p>
              )}
            </div>
          </div>
        </div>
      )}

      {profitabilityModalOpen && (
        <div className="vr-modal-overlay" role="dialog" aria-modal="true" aria-label="Karlılık analizi ekle">
          <div className="vr-modal" style={{ maxWidth: '680px' }}>
            <button
              type="button"
              className="vr-modal-close"
              onClick={() => {
                setProfitabilityModalOpen(false)
                setPendingCampaignAnalyze(false)
                setProfitabilityErrors([])
              }}
              aria-label="Kapat"
            >
              ×
            </button>
            <h2 className="panel-title">Karlılık Analizi Ekle (İsteğe Bağlı)</h2>
            <p className="muted small" style={{ marginBottom: '0.9rem' }}>
              Ürün maliyetinizi girerek net kar, break-even ROAS ve hedef CPA hesaplamalarını aktif edebilirsiniz.
            </p>
            <div className="form-stack" style={{ maxWidth: '26rem' }}>
              <label>
                Satış fiyatı (₺)
                <input type="number" min="0" step="0.01" value={salePriceInput} onChange={(e) => setSalePriceInput(e.target.value)} inputMode="decimal" />
              </label>
              <label>
                Ürün maliyeti / COGS (₺)
                <input type="number" min="0" step="0.01" value={cogsInput} onChange={(e) => setCogsInput(e.target.value)} inputMode="decimal" />
              </label>
              <label>
                Kargo maliyeti (₺)
                <input type="number" min="0" step="0.01" value={shippingInput} onChange={(e) => setShippingInput(e.target.value)} inputMode="decimal" />
              </label>
              <label>
                Ödeme komisyonu % (varsayılan 2.9)
                <input type="number" min="0" max="100" step="0.01" value={feePctInput} onChange={(e) => setFeePctInput(e.target.value)} inputMode="decimal" />
              </label>
            </div>
            {profitabilityErrors.length > 0 && (
              <p className="error-banner" style={{ marginTop: '0.75rem' }}>
                {profitabilityErrors[0]}
              </p>
            )}
            <div className="form-actions" style={{ marginTop: '1rem' }}>
              <button
                type="button"
                className="btn"
                disabled={savingProfitability}
                onClick={() => {
                  setProfitabilityModalOpen(false)
                  setPendingCampaignAnalyze(false)
                  setProfitabilityErrors([])
                  if (pendingCampaignAnalyze) void runCampaignAnalysisCore()
                }}
              >
                Atla
              </button>
              <button
                type="button"
                className="btn primary"
                disabled={savingProfitability || !pendingCampaignAnalyze || !selectedCampaignId}
                onClick={() => {
                  void (async () => {
                    if (!pendingCampaignAnalyze || !selectedCampaignId) return
                    setSavingProfitability(true)
                    setError(null)
                    try {
                      const validationErrors = validateProfitabilityInputs()
                      if (validationErrors.length > 0) {
                        setProfitabilityErrors(validationErrors)
                        return
                      }
                      setProfitabilityErrors([])
                      const toNum = (v: string, fallback = 0) => {
                        const n = Number(v.replace(',', '.').trim())
                        return Number.isFinite(n) ? n : fallback
                      }
                      const salePrice = Math.max(1, toNum(salePriceInput, 1))
                      const cogs = Math.max(0, toNum(cogsInput, 0))
                      const shipping = Math.max(0, toNum(shippingInput, 0))
                      const feePct = Math.max(0, toNum(feePctInput, 2.9))
                      const product = await createProduct({
                        userId,
                        name: `Kampanya ${selectedCampaignId} ürünü`,
                        sellingPrice: salePrice,
                        cogs,
                        shippingCost: shipping,
                        paymentFeePct: feePct,
                        returnRatePct: 0,
                        ltvMultiplier: 1,
                        targetMarginPct: 30,
                      })
                      await createCampaignMap({
                        userId,
                        campaignId: selectedCampaignId,
                        productId: product.id,
                      })
                      setProfitabilityModalOpen(false)
                      setPendingCampaignAnalyze(false)
                      setProfitabilityErrors([])
                      await runCampaignAnalysisCore()
                    } catch (e: unknown) {
                      setError(e instanceof Error ? e.message : 'Karlılık bilgisi kaydedilemedi')
                    } finally {
                      setSavingProfitability(false)
                    }
                  })()
                }}
              >
                {savingProfitability ? 'Kaydediliyor…' : 'Kaydet ve Analiz Et'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

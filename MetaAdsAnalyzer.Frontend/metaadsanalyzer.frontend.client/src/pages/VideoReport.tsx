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
import { addAnalyzedAd, updateRecommendationStatus, type AnalyzedAdItem } from '../features/analyzedAdsStore'
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

  const [adsets, setAdsets] = useState<MetaAdsetItem[]>([])
  const [adsetsLoading, setAdsetsLoading] = useState(false)
  const [adsetsError, setAdsetsError] = useState<string | null>(null)
  const [selectedAdsetId, setSelectedAdsetId] = useState<string | null>(null)
  const [adsetSpendById, setAdsetSpendById] = useState<Map<string, number>>(new Map())

  const [ads, setAds] = useState<MetaAdListItem[]>([])
  const [adsLoading, setAdsLoading] = useState(false)
  const [adsError, setAdsError] = useState<string | null>(null)
  const [rawsForSpend, setRawsForSpend] = useState<RawInsightRow[]>([])
  const [preset, setPreset] = useState('last_7d')
  const [busyGroupKey, setBusyGroupKey] = useState<string | null>(null)
  const [stepLog, setStepLog] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [selectedGroup, setSelectedGroup] = useState<VideoGroup | null>(null)
  const [aggregate, setAggregate] = useState<VideoReportAggregateResponse | null>(null)
  const [mergedDirectives, setMergedDirectives] = useState<DirectiveItem[]>([])
  const [resultModalOpen, setResultModalOpen] = useState(false)
  const [pickerOpen, setPickerOpen] = useState(false)
  const [pickerStep, setPickerStep] = useState<'campaign' | 'adset' | 'ad'>('campaign')
  const [pickerAdId, setPickerAdId] = useState<string | null>(null)
  const [appliedRecIds, setAppliedRecIds] = useState<Set<string>>(new Set())
  const [skippedRecIds, setSkippedRecIds] = useState<Set<string>>(new Set())
  const [showDataQualityBanner, setShowDataQualityBanner] = useState(true)
  const [savedAnalyzedItem, setSavedAnalyzedItem] = useState<AnalyzedAdItem | null>(null)
  const [profitabilityModalOpen, setProfitabilityModalOpen] = useState(false)
  const [pendingAnalyzeAdId, setPendingAnalyzeAdId] = useState<string | null>(null)
  const [savingProfitability, setSavingProfitability] = useState(false)
  const [salePriceInput, setSalePriceInput] = useState('')
  const [cogsInput, setCogsInput] = useState('')
  const [shippingInput, setShippingInput] = useState('')
  const [feePctInput, setFeePctInput] = useState('2.9')
  const [profitabilityErrors, setProfitabilityErrors] = useState<string[]>([])

  const spendByAdId = useMemo(() => buildSpendByAdId(rawsForSpend), [rawsForSpend])

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
      try {
        // Adset harcaması campaign/adset listelerinde gelmez; insights sync ile güncel spend doldur.
        await postInsightsSync(userId, 'adset', preset, { metaAdAccountId: act })
        // Adset seviyesinde 0 gelen satırları, ad seviyesinden adset_id ile türeterek güvenli fallback yap.
        await postInsightsSync(userId, 'ad', preset, { metaAdAccountId: act })
        const [list, rawAdsets, rawAds] = await Promise.all([
          getMetaAdsets(userId, campaignId, act),
          getRawInsights(userId, 'adset', { campaignId }),
          getRawInsights(userId, 'ad', { campaignId }),
        ])

        // Her adset için son satırı alıp harcama durumunu çıkar.
        const latest = new Map<string, RawInsightRow>()
        for (const row of [...rawAdsets].sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))) {
          if (!latest.has(row.entityId)) latest.set(row.entityId, row)
        }

        const spendMap = new Map<string, number>()
        for (const [id, row] of latest.entries()) {
          spendMap.set(id, row.spend ?? 0)
        }

        // ad-level fallback: her ad için en güncel satırı alıp adset bazında topla.
        const latestAdRows = new Map<string, RawInsightRow>()
        for (const row of [...rawAds].sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))) {
          if (!latestAdRows.has(row.entityId)) latestAdRows.set(row.entityId, row)
        }
        const adsetSpendFromAds = new Map<string, number>()
        for (const row of latestAdRows.values()) {
          const adsetId = row.metaAdsetId?.trim()
          if (!adsetId) continue
          adsetSpendFromAds.set(adsetId, (adsetSpendFromAds.get(adsetId) ?? 0) + (row.spend ?? 0))
        }

        for (const adset of list) {
          const current = spendMap.get(adset.id)
          const fallback = adsetSpendFromAds.get(adset.id)
          if ((current == null || current <= 0) && fallback != null && fallback > 0) {
            spendMap.set(adset.id, fallback)
          }
        }
        setAdsetSpendById(spendMap)

        // Yalnızca harcamasının kesin olarak 0 olduğu adsetleri gizle.
        // Insight satırı hiç yoksa (undefined) adseti göster; kullanıcı reklamlara inebilsin.
        const filtered = list.filter((x) => {
          const spend = spendMap.get(x.id)
          return spend == null || spend > 0
        })
        setAdsets(filtered)
      } catch (e: unknown) {
        setAdsetsError(e instanceof Error ? e.message : 'Reklam setleri yüklenemedi')
        setAdsets([])
        setAdsetSpendById(new Map())
      } finally {
        setAdsetsLoading(false)
      }
    },
    [userId, preset],
  )

  const loadAdsForAdset = useCallback(
    async (act: string, adsetId: string) => {
      setAdsLoading(true)
      setAdsError(null)
      try {
        const list = await getMetaAds(userId, act, { adsetId })
        setAds(list)
      } catch (e: unknown) {
        setAdsError(e instanceof Error ? e.message : 'Reklamlar yüklenemedi')
        setAds([])
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

  function openPicker() {
    if (!activeAct) return
    setPickerOpen(true)
    setPickerStep('campaign')
    setPickerAdId(null)
  }

  useEffect(() => {
    setAggregate(null)
    setMergedDirectives([])
    setError(null)
    setResultModalOpen(false)
  }, [selectedGroup?.groupKey])

  useEffect(() => {
    setSelectedGroup(null)
    setAggregate(null)
    setMergedDirectives([])
    setError(null)
    setStepLog(null)
    setResultModalOpen(false)
  }, [selectedCampaignId, selectedAdsetId])

  async function runAnalysisForAds(group: VideoGroup, adIds: string[]) {
    if (!activeAct || !selectedAdsetId) {
      setError('Önce reklam seti ve reklam seçin; reklam hesabının doğru olduğundan emin olun.')
      return
    }
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
      setSelectedGroup(group)
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
      const mergedDirs = mergeDirectives(dirs)
      setMergedDirectives(mergedDirs)

      const aggRaw = await postVideoReportAggregate({ userId, adIds, metaAdAccountId: activeAct })
      setAggregate(sanitizeVideoAggregate(aggRaw))
      setShowDataQualityBanner(true)
      setResultModalOpen(true)
      setAppliedRecIds(new Set())
      setSkippedRecIds(new Set())
      setSavedAnalyzedItem(null)
      const firstAd = group.ads[0]
      if (firstAd) {
        const saved = await addAnalyzedAd({
          userId,
          adId: firstAd.id,
          adName: firstAd.name?.trim() || firstAd.creativeName?.trim() || firstAd.id,
          thumbnailUrl: firstAd.thumbnailUrl ?? null,
          campaignId: selectedCampaignId,
          campaignName: campaigns.find((x) => x.id === selectedCampaignId)?.name ?? selectedCampaignId,
          adsetId: selectedAdsetId,
          adsetName: adsets.find((x) => x.id === selectedAdsetId)?.name ?? selectedAdsetId,
          aggregate: sanitizeVideoAggregate(aggRaw),
          directives: mergedDirs,
        })
        setSavedAnalyzedItem(saved)
      }
      if (import.meta.env.DEV) {
        // eslint-disable-next-line no-console
        console.info('[VideoReport] Özet yanıtı:', {
          hasInsightRows: aggRaw.hasInsightRows,
          diagnosticMessage: aggRaw.diagnosticMessage,
        })
      }

      await refreshSpendMap()
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusyGroupKey(null)
    }
  }

  async function runPickedAdAnalysisCore(adId?: string) {
    const targetId = adId ?? pickerAdId
    if (!targetId) return
    const ad = ads.find((x) => x.id === targetId)
    if (!ad) {
      setError('Seçilen reklam bulunamadı. Listeyi yenileyin.')
      return
    }
    const adSpend = spendByAdId.get(ad.id) ?? 0
    if (adSpend <= 0) {
      setStepLog('Bu reklamda harcama verisi 0/bilinmiyor; analiz yine de başlatıldı.')
    }
    const group: VideoGroup = {
      groupKey: `ad:${ad.id}`,
      videoId: ad.videoId?.trim() || null,
      ads: [ad],
      thumbnailUrl: ad.thumbnailUrl ?? null,
      displayName: ad.creativeName?.trim() || ad.videoTitle?.trim() || ad.name?.trim() || `Reklam ${ad.id}`,
      statusLine: buildStatusLine([ad]),
      totalSpend: spendByAdId.get(ad.id) ?? 0,
      cardTags: [],
    }
    setPickerOpen(false)
    await runAnalysisForAds(group, [ad.id])
  }

  async function ensureCampaignProfitabilityMapOrPrompt(targetAdId: string): Promise<boolean> {
    if (!selectedCampaignId) return true
    try {
      const maps = await getCampaignMaps(userId)
      const hasMap = maps.some((m) => m.campaignId === selectedCampaignId)
      if (hasMap) return true
      setPendingAnalyzeAdId(targetAdId)
      setProfitabilityModalOpen(true)
      return false
    } catch {
      return true
    }
  }

  async function runPickedAdAnalysis(adId?: string) {
    const targetId = adId ?? pickerAdId
    if (!targetId) return
    const ok = await ensureCampaignProfitabilityMapOrPrompt(targetId)
    if (!ok) return
    await runPickedAdAnalysisCore(targetId)
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
    const pairs = [
      { key: '%25→%50', prev: aggregate.videoP25, next: aggregate.videoP50 },
      { key: '%50→%75', prev: aggregate.videoP50, next: aggregate.videoP75 },
      { key: '%75→%100', prev: aggregate.videoP75, next: aggregate.videoP100 },
    ].filter((x) => x.prev > 0)
    if (pairs.length === 0) return null
    const withDrop = pairs.map((p) => ({ ...p, dropPct: ((p.prev - p.next) / p.prev) * 100 }))
    return withDrop.sort((a, b) => b.dropPct - a.dropPct)[0]
  }, [aggregate])

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
                disabled={!activeAct || !!busyGroupKey || accountsLoading}
                onClick={openPicker}
              >
                Kampanya Seç
              </Button>
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
          <p className="muted small">
            {selectedCampaignId
              ? `Seçilen kampanya: ${campaigns.find((x) => x.id === selectedCampaignId)?.name ?? selectedCampaignId}`
              : 'Kampanya seçmek için yukarıdaki "Kampanya Seç" butonunu kullanın.'}
            {selectedAdsetId ? ` · Adset: ${adsets.find((x) => x.id === selectedAdsetId)?.name ?? selectedAdsetId}` : ''}
          </p>

          {!selectedAdsetId && <p className="muted">Kampanya Seç → Adset Seç akışıyla reklam seçip analiz başlatın.</p>}
          {selectedAdsetId && adsLoading && <p className="muted">Reklamlar yükleniyor…</p>}
          {selectedAdsetId && adsError && <p className="error-banner">{adsError}</p>}
          <h3 className="panel-title" style={{ marginTop: '0.75rem' }}>Analiz edilen reklamlar</h3>
          <p className="muted small">
            Bu bölüm ayrı sekmeye taşındı. Sol menüden <strong>Analiz edilen reklamlar</strong> sekmesini açın.
          </p>
        </section>
      )}

      {selectedGroup && false && <section />}

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
                      <p className="vr-reason">{d.reason ?? 'Neden bilgisi yok.'}</p>
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
                    </div>
                  ))}
                </div>
                {biggestDrop && (
                  <p className="vr-drop-marker">
                    Izleyicilerin %{biggestDrop.dropPct.toFixed(1)}'i {biggestDrop.key} adımında ayrıldı.
                  </p>
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

      {pickerOpen && (
        <div className="vr-modal-overlay" role="dialog" aria-modal="true" aria-label="Kampanya seçim ekranı">
          <div className="vr-modal">
            <button type="button" className="vr-modal-close" onClick={() => setPickerOpen(false)} aria-label="Kapat">×</button>
            <h2 className="panel-title">Kampanya seç</h2>
            {pickerStep === 'campaign' && (
              <div className="campaign-list">
                {campaignsLoading && <p className="muted">Kampanyalar yükleniyor…</p>}
                {campaignsError && <p className="error-banner">{campaignsError}</p>}
                {campaigns.map((c) => (
                  <button
                    type="button"
                    key={c.id}
                    className="campaign-row campaign-picker-btn"
                    onClick={() => {
                      if (!activeAct) return
                      setSelectedCampaignId(c.id)
                      setSelectedAdsetId(null)
                      setPickerAdId(null)
                      setPickerStep('adset')
                      void loadAdsetsForCampaign(activeAct, c.id)
                    }}
                  >
                    <div className="campaign-main">
                      <div className="campaign-title">{c.name?.trim() || c.id}</div>
                      <div className="campaign-meta muted small">{c.objective ?? '—'} · {c.status ?? '—'}</div>
                    </div>
                  </button>
                ))}
              </div>
            )}
            {pickerStep === 'adset' && (
              <div className="campaign-list">
                <div className="form-actions" style={{ marginBottom: '0.35rem' }}>
                  <Button type="button" variant="outline" size="sm" onClick={() => setPickerStep('campaign')}>← Kampanyalar</Button>
                </div>
                {adsetsLoading && <p className="muted">Reklam setleri yükleniyor…</p>}
                {adsetsError && <p className="error-banner">{adsetsError}</p>}
                {adsets.map((s) => (
                  <button
                    type="button"
                    key={s.id}
                    className="campaign-row campaign-picker-btn"
                    onClick={() => {
                      if (!activeAct) return
                      setSelectedAdsetId(s.id)
                      setPickerAdId(null)
                      setPickerStep('ad')
                      void loadAdsForAdset(activeAct, s.id)
                    }}
                  >
                    <div className="campaign-main">
                      <div className="campaign-title">{s.name?.trim() || s.id}</div>
                      <div className="campaign-meta muted small">
                        {s.status ?? '—'} · campaign {s.campaignId ?? '—'} · harcama{' '}
                        {adsetSpendById.has(s.id) ? (adsetSpendById.get(s.id) ?? 0).toFixed(2) : 'bilinmiyor'}
                      </div>
                    </div>
                  </button>
                ))}
              </div>
            )}
            {pickerStep === 'ad' && (
              <div className="campaign-list">
                <div className="form-actions" style={{ marginBottom: '0.35rem' }}>
                  <Button type="button" variant="outline" size="sm" onClick={() => setPickerStep('adset')}>← Adsetler</Button>
                </div>
                {adsLoading && <p className="muted">Reklamlar yükleniyor…</p>}
                {adsError && <p className="error-banner">{adsError}</p>}
                {ads.map((a) => {
                  const adSpend = spendByAdId.get(a.id) ?? 0
                  return (
                  <div
                    key={a.id}
                    className={`campaign-row ${pickerAdId === a.id ? 'account-card-active' : ''}`}
                    onClick={() => setPickerAdId(a.id)}
                    role="button"
                    tabIndex={0}
                  >
                    <div style={{ display: 'flex', gap: '0.65rem', alignItems: 'center' }}>
                      <div style={{ width: '70px', height: '70px', borderRadius: '8px', overflow: 'hidden', background: 'hsl(var(--muted))' }}>
                        {a.thumbnailUrl ? (
                          <img src={a.thumbnailUrl} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                        ) : (
                          <div style={{ width: '100%', height: '100%' }} />
                        )}
                      </div>
                      <div className="campaign-main">
                        <div className="campaign-title">{a.name?.trim() || a.creativeName?.trim() || a.id}</div>
                        <div className="campaign-meta muted small">
                          reklam {a.id} · video_id {a.videoId?.trim() || '—'} · harcama {adSpend.toFixed(2)}
                        </div>
                      </div>
                    </div>
                    <div className="form-actions" style={{ justifyContent: 'flex-end' }}>
                      <button type="button" className="btn">Atla</button>
                      <button
                        type="button"
                        className="btn primary"
                        disabled={!!busyGroupKey}
                        title={adSpend <= 0 ? 'Harcama verisi 0/bilinmiyor; analiz yapılabilir.' : undefined}
                        onClick={() => void runPickedAdAnalysis(a.id)}
                      >
                        Uygula
                      </button>
                    </div>
                  </div>
                )})}
              </div>
            )}
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
                setPendingAnalyzeAdId(null)
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
                  const adId = pendingAnalyzeAdId
                  setProfitabilityModalOpen(false)
                  setPendingAnalyzeAdId(null)
                  setProfitabilityErrors([])
                  if (adId) void runPickedAdAnalysisCore(adId)
                }}
              >
                Atla
              </button>
              <button
                type="button"
                className="btn primary"
                disabled={savingProfitability || !pendingAnalyzeAdId || !selectedCampaignId}
                onClick={() => {
                  void (async () => {
                    if (!pendingAnalyzeAdId || !selectedCampaignId) return
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
                      const adId = pendingAnalyzeAdId
                      setPendingAnalyzeAdId(null)
                      setProfitabilityErrors([])
                      await runPickedAdAnalysisCore(adId)
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

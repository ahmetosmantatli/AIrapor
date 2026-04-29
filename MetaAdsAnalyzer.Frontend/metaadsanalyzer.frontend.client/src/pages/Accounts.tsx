import { useEffect, useMemo, useState } from 'react'
import {
  getCampaignMaps,
  getLinkedMetaAdAccounts,
  getMetaCampaigns,
  getProducts,
  getRawInsights,
  postInsightsSync,
  postSelectActiveMetaAdAccount,
} from '../api/client'
import type { CampaignMapItem, LinkedMetaAdAccountItem, MetaCampaignItem, ProductResponse, RawInsightRow } from '../api/types'
import { useUser } from '../context/UserContext'
import { listAnalyzedAds, type AnalyzedAdItem, updateRecommendationStatus } from '../features/analyzedAdsStore'
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

function normalizeEntityId(value: string | null | undefined): string {
  return (value ?? '').trim()
}

function extractDigits(value: string): string {
  return value.replace(/\D+/g, '')
}

function sameEntityId(left: string | null | undefined, right: string | null | undefined): boolean {
  const a = normalizeEntityId(left)
  const b = normalizeEntityId(right)
  if (!a || !b) return false
  if (a === b) return true
  const da = extractDigits(a)
  const db = extractDigits(b)
  return da.length >= 6 && db.length >= 6 && da === db
}

type DetailTab = 'funnel' | 'timeline' | 'metrics'
type FunnelStep = { label: string; value: number; lossPct: number | null; lostCount: number }
type CampaignEvaluation = {
  score: number
  scoreLabel: string
  grade: string
  spend: number
  roas: number
  cpa: number | null
  ctr: number
  cvr: number
  analyzedAdsetCount: number
  analyzedAdCount: number
  healthyAdsetCount: number
  weakAdsetCount: number
  warnings: string[]
  suggestions: string[]
}
type CampaignDiagnosis = {
  headline: string
  summary: string
  reasons: string[]
  actions: string[]
  expectedImpact: string
}

function pct(v: number | null | undefined): string {
  if (v == null || !Number.isFinite(v)) return '—'
  return `${v.toFixed(1)}%`
}

function buildFunnel(agg: AnalyzedAdItem['aggregate']): FunnelStep[] {
  const isVideo = Boolean(agg.videoP25 || agg.videoP50 || agg.videoP75 || agg.videoP100)
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

function directiveTypeLabel(value: string | null | undefined): string {
  const t = (value ?? '').trim().toUpperCase()
  if (t === 'OPTIMIZE') return 'Optimize Et'
  if (t === 'SCALE') return 'Ölçekle'
  if (t === 'STOP') return 'Durdur'
  if (t === 'WATCH') return 'İzle'
  return value ?? 'İzle'
}

function statusIcon(ok: boolean, warn: boolean): string {
  if (ok) return '✅'
  if (warn) return '⚠️'
  return '❌'
}

function scoreLabel(score: number | null): { label: string; cls: string } {
  const s = score ?? 0
  if (s >= 80) return { label: 'Winner', cls: 'grade-good' }
  if (s >= 60) return { label: 'Potansiyel', cls: 'grade-mid' }
  if (s >= 40) return { label: 'Zayıf', cls: 'grade-warn' }
  return { label: 'Kapat', cls: 'grade-bad' }
}

function adGrade(a: AnalyzedAdItem): { letter: string; breakdown: string[] } {
  const roas = a.aggregate.roas ?? 0
  const hook = a.aggregate.thumbstopPct ?? 0
  const hold = a.aggregate.holdPct ?? 0
  const completion = a.aggregate.completionPct ?? 0
  const letter = roas >= 3 ? 'A' : roas >= 2 ? 'B' : roas >= 1 ? 'C' : 'D'
  return {
    letter,
    breakdown: [
      `ROAS: ${roas.toFixed(1)}x ${roas >= 2 ? '(iyi)' : '(geliştirilmeli)'}`,
      `Hook: ${pct(hook)} ${hook >= 25 ? '(iyi)' : '(zayıf)'}`,
      `Hold: ${pct(hold)} ${hold >= 35 ? '(iyi)' : '(zayıf)'}`,
      `Completion: ${pct(completion)} ${completion >= 15 ? '(iyi)' : '(zayıf)'}`,
    ],
  }
}

function clampScore(v: number): number {
  return Math.max(0, Math.min(100, Math.round(v)))
}

function clampPct(v: number): number {
  if (!Number.isFinite(v)) return 0
  return Math.max(0, Math.min(100, v))
}

function adsetHealthLabel(roas: number, hook: number, hold: number): 'good' | 'mid' | 'bad' {
  if (roas >= 1.5 && (hook >= 20 || hold >= 15)) return 'good'
  if (roas < 1 && (hook < 15 || hold < 10)) return 'bad'
  return 'mid'
}

function buildCampaignDiagnosis(evalx: CampaignEvaluation): CampaignDiagnosis {
  const reasons: string[] = []
  const actions: string[] = []
  let headline = 'Adlyz Teşhisi'
  let summary = 'Kampanya performansı dengeli, kontrollü optimizasyonla ilerlenebilir.'
  let expectedImpact = '7 günlük izleme sonunda performansın stabil kalması beklenir.'

  const roasGapPct = evalx.roas > 0 ? Math.max(0, ((2.5 - evalx.roas) / 2.5) * 100) : 100
  const weakRatio = evalx.analyzedAdsetCount > 0 ? (evalx.weakAdsetCount / evalx.analyzedAdsetCount) * 100 : 0

  if (evalx.roas < 1) {
    headline = 'Adlyz Teşhisi: Kârlılık riski yüksek'
    summary = 'Harcama var ancak satın alma getirisi maliyeti karşılamıyor.'
    reasons.push(
      `Yatırım getirisi düşük: ROAS ${evalx.roas.toFixed(2)}x (hedef 2.50x).`,
      `Hedefe uzaklık: yaklaşık %${roasGapPct.toFixed(0)}.`,
    )
    actions.push(
      'Düşük performanslı reklam setlerinde bütçeyi %20-%30 azalt.',
      'İlk 3 saniye açılışını yenileyerek tıklama kalitesini artır.',
      'Satış sayfasında fiyat/değer mesajını netleştir (CVR iyileştirme).',
    )
    expectedImpact = '7 gün içinde ROAS’ın 1.0x üstüne çıkması ve CPA’nın düşmesi hedeflenir.'
  } else if (evalx.roas >= 2.5 && evalx.weakAdsetCount <= evalx.healthyAdsetCount) {
    headline = 'Adlyz Teşhisi: Ölçeklemeye uygun kampanya'
    summary = 'Kampanya kârlı çalışıyor, kontrollü bütçe artışıyla büyütülebilir.'
    reasons.push(
      `ROAS güçlü: ${evalx.roas.toFixed(2)}x.`,
      `Sağlıklı adset dağılımı: ${evalx.healthyAdsetCount} iyi / ${evalx.weakAdsetCount} sorunlu.`,
    )
    actions.push(
      'Kazanan reklam setlerinde bütçeyi 24 saatte en fazla %15 artır.',
      'Skoru düşük setlerde kreatif varyant testini ayrı adsette yürüt.',
      'Kârlı setleri bozmadan yeni kitle testlerini düşük bütçeyle başlat.',
    )
    expectedImpact = '7 gün içinde toplam gelir artarken ROAS’ın 2.5x üstünde korunması beklenir.'
  } else {
    headline = 'Adlyz Teşhisi: İyileştirme potansiyeli var'
    summary = 'Kampanya tamamen sorunlu değil, fakat verimlilik alanları net.'
    reasons.push(
      `ROAS seviyesi: ${evalx.roas.toFixed(2)}x.`,
      `Sorunlu adset oranı: %${weakRatio.toFixed(0)}.`,
      `Satın alma dönüşümü: ${evalx.cvr.toFixed(2)}%.`,
    )
    actions.push(
      'Zayıf adsetlerde kreatif/hook güncellemesi yap ve 48 saat izle.',
      'CTR iyi ama dönüşüm düşükse teklif ve landing mesajını yeniden hizala.',
      'Kampanyayı sağlık raporuna göre günlük takip et, skor düşen seti hızlı kes.',
    )
    expectedImpact = '7 gün içinde skorun üst banda taşınması ve ROAS’ın 2.0x+ seviyeye yaklaşması beklenir.'
  }

  return {
    headline,
    summary,
    reasons: reasons.slice(0, 3),
    actions: actions.slice(0, 3),
    expectedImpact,
  }
}

export function Accounts() {
  const { userId } = useUser()
  const [view, setView] = useState<'accounts' | 'campaigns'>('accounts')
  const [campaignSortBy, setCampaignSortBy] = useState<'spend' | 'health' | 'cpa'>('spend')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [cards, setCards] = useState<AccountCard[]>([])
  const [selectedAct, setSelectedAct] = useState<string | null>(null)
  const [campaignRows, setCampaignRows] = useState<MetaCampaignItem[]>([])
  const [rawCampaignRows, setRawCampaignRows] = useState<RawInsightRow[]>([])
  const [maps, setMaps] = useState<CampaignMapItem[]>([])
  const [products, setProducts] = useState<ProductResponse[]>([])
  const [analyzedItems, setAnalyzedItems] = useState<AnalyzedAdItem[]>([])
  const [selectedCampaign, setSelectedCampaign] = useState<MetaCampaignItem | null>(null)
  const [activeAnalyzedAd, setActiveAnalyzedAd] = useState<AnalyzedAdItem | null>(null)
  const [activeRawLatest, setActiveRawLatest] = useState<RawInsightRow | null>(null)
  const [detailTab, setDetailTab] = useState<DetailTab>('funnel')

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
        const analyzed = await listAnalyzedAds(userId)
        if (cancelled) return
        setAnalyzedItems(analyzed)
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

  const latestRawByCampaignId = useMemo(() => {
    const sorted = [...rawCampaignRows].sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))
    const x = new Map<string, RawInsightRow>()
    for (const c of campaignRows) {
      const hit = sorted.find((r) => sameEntityId(r.entityId, c.id))
      if (hit) x.set(c.id, hit)
    }
    return x
  }, [campaignRows, rawCampaignRows])

  async function selectAccount(act: string) {
    setSelectedAct(act)
    setSelectedCampaign(null)
    setActiveAnalyzedAd(null)
    setError(null)
    try {
      await postSelectActiveMetaAdAccount(userId, act)
      const [campaigns, raws] = await Promise.all([
        getMetaCampaigns(userId, act),
        getRawInsights(userId, 'campaign'),
      ])
      setCampaignRows(campaigns)
      setRawCampaignRows(raws)
      setView('campaigns')
    } catch (e: unknown) {
      setError(e instanceof Error ? e.message : 'Hesap seçilemedi')
    }
  }

  function scoreRow(c: MetaCampaignItem) {
    const raw = latestRawByCampaignId.get(c.id)
    const spend = raw?.spend ?? 0
    const ctr = raw && raw.impressions > 0 ? (raw.linkClicks / raw.impressions) * 100 : 0
    const roas = raw?.roas ?? 0
    const cpa = (raw?.cpa ?? 0) > 0
      ? (raw?.cpa ?? Number.POSITIVE_INFINITY)
      : (raw?.purchases ?? 0) > 0
        ? spend / Math.max(1, raw?.purchases ?? 0)
        : Number.POSITIVE_INFINITY
    const p = productByCampaign.get(c.id)
    const breakEven = p ? (p.cogs + p.shippingCost) / Math.max(p.sellingPrice * 0.65, 1) : 1
    const target = p ? breakEven + 0.8 : 1.8
    const grade = roas > target ? 'A' : roas >= breakEven ? 'B' : roas < 0.7 ? 'D' : 'C'
    return { spend, ctr, roas, cpa, grade, tag: problemTag(ctr, roas) }
  }

  const campaignEvaluations = useMemo(() => {
    const map = new Map<string, CampaignEvaluation>()
    for (const c of campaignRows) {
      const base = scoreRow(c)
      const rawCampaign = latestRawByCampaignId.get(c.id)
      const related = analyzedItems.filter((x) => sameEntityId(x.campaignId, c.id))
      const adsetMap = new Map<string, { roasWeightedSum: number; hookWeightedSum: number; holdWeightedSum: number; spendSum: number; count: number }>()
      for (const item of related) {
        const k = item.adsetId || item.adsetName || item.adId
        const prev = adsetMap.get(k)
        const spendWeight = Math.max(1, item.aggregate.spend ?? 0)
        if (!prev) {
          adsetMap.set(k, {
            roasWeightedSum: (item.aggregate.roas ?? 0) * spendWeight,
            hookWeightedSum: (item.aggregate.thumbstopPct ?? 0) * spendWeight,
            holdWeightedSum: (item.aggregate.holdPct ?? 0) * spendWeight,
            spendSum: spendWeight,
            count: 1,
          })
          continue
        }
        prev.roasWeightedSum += (item.aggregate.roas ?? 0) * spendWeight
        prev.hookWeightedSum += (item.aggregate.thumbstopPct ?? 0) * spendWeight
        prev.holdWeightedSum += (item.aggregate.holdPct ?? 0) * spendWeight
        prev.spendSum += spendWeight
        prev.count += 1
      }
      let healthyAdsetCount = 0
      let weakAdsetCount = 0
      for (const v of adsetMap.values()) {
        const roasAvg = v.spendSum > 0 ? v.roasWeightedSum / v.spendSum : 0
        const hookAvg = v.spendSum > 0 ? v.hookWeightedSum / v.spendSum : 0
        const holdAvg = v.spendSum > 0 ? v.holdWeightedSum / v.spendSum : 0
        const health = adsetHealthLabel(roasAvg, hookAvg, holdAvg)
        if (health === 'good') healthyAdsetCount += 1
        if (health === 'bad') weakAdsetCount += 1
      }
      const analyzedAdsetCount = adsetMap.size
      const analyzedAdCount = related.length

      const analyzedSpend = related.reduce((n, x) => n + (x.aggregate.spend ?? 0), 0)
      const analyzedPurchaseValue = related.reduce((n, x) => n + (x.aggregate.purchaseValue ?? 0), 0)
      const analyzedPurchases = related.reduce((n, x) => n + (x.aggregate.purchases ?? 0), 0)
      const analyzedClicks = related.reduce((n, x) => n + (x.aggregate.linkClicks ?? 0), 0)
      const campaignRoasRaw = (rawCampaign?.roas ?? 0) > 0
        ? (rawCampaign?.roas ?? 0)
        : (rawCampaign?.spend ?? 0) > 0
          ? (rawCampaign?.purchaseValue ?? 0) / Math.max(1, rawCampaign?.spend ?? 0)
        : analyzedSpend > 0
          ? analyzedPurchaseValue / analyzedSpend
          : 0
      const cvrRaw =
        (rawCampaign?.linkClicks ?? 0) > 0
          ? ((rawCampaign?.purchases ?? 0) / Math.max(1, rawCampaign?.linkClicks ?? 0)) * 100
          : analyzedClicks > 0
            ? (analyzedPurchases / analyzedClicks) * 100
            : 0
      const cvr = clampPct(cvrRaw)
      const campaignCpa =
        (rawCampaign?.cpa ?? 0) > 0
          ? (rawCampaign?.cpa ?? null)
          : (rawCampaign?.purchases ?? 0) > 0
            ? (rawCampaign?.spend ?? 0) / Math.max(1, rawCampaign?.purchases ?? 0)
            : analyzedPurchases > 0
              ? analyzedSpend / Math.max(1, analyzedPurchases)
              : null

      const roasScore = Math.max(0, Math.min(45, (campaignRoasRaw / 3) * 45))
      const ctrScore = Math.max(0, Math.min(20, (base.ctr / 2) * 20))
      const cvrScore = Math.max(0, Math.min(15, (cvr / 3) * 15))
      const adsetQualityScore = analyzedAdsetCount > 0 ? Math.max(0, Math.min(10, (healthyAdsetCount / analyzedAdsetCount) * 10)) : 0
      const confidenceScore = analyzedAdsetCount >= 3 ? 10 : analyzedAdsetCount > 0 ? 5 : 0
      const score = clampScore(roasScore + ctrScore + cvrScore + adsetQualityScore + confidenceScore)

      const warnings: string[] = []
      if (campaignRoasRaw < 1 && base.spend > 0) warnings.push('Harcama var fakat ROAS 1.0 altı.')
      if (weakAdsetCount > healthyAdsetCount && analyzedAdsetCount > 0) warnings.push('Zayıf adset sayısı sağlıklı adsetlerden fazla.')
      if (base.ctr < 1) warnings.push('CTR düşük; hook ve ilk 3 saniye kreatifleri zayıf olabilir.')
      if (cvrRaw > 100) warnings.push('CVR verisi attribution farkından ötürü normalize edildi (üst sınır %100).')

      const suggestions: string[] = []
      if (campaignRoasRaw >= 2.5) suggestions.push('Kazanan adsetleri kontrollü bütçe artışıyla ölçekleyin.')
      if (campaignRoasRaw < 1.5) suggestions.push('Zayıf adsetlerde kreatif yenileme ve hedefleme daraltma yapın.')
      if (base.ctr < 1.2) suggestions.push('Hook açılışlarını A/B test ederek CTR iyileştirin.')
      if (suggestions.length === 0) suggestions.push('Performans stabil; izleme ve küçük optimizasyonlarla devam edin.')

      const scoreLabel = score >= 80 ? 'Winner' : score >= 60 ? 'Potansiyel' : score >= 40 ? 'Zayıf' : 'Riskli'
      const grade = score >= 80 ? 'A' : score >= 60 ? 'B' : score >= 40 ? 'C' : 'D'

      map.set(c.id, {
        score,
        scoreLabel,
        grade,
        spend: rawCampaign?.spend ?? base.spend,
        roas: campaignRoasRaw,
        cpa: campaignCpa,
        ctr: base.ctr,
        cvr,
        analyzedAdsetCount,
        analyzedAdCount,
        healthyAdsetCount,
        weakAdsetCount,
        warnings,
        suggestions,
      })
    }
    return map
  }, [analyzedItems, campaignRows, latestRawByCampaignId, maps, products])

  const campaignCards = useMemo(() => {
    const latestAnalyzedAtByCampaignId = new Map<string, string>()
    for (const item of analyzedItems) {
      const campaignId = (item.campaignId ?? '').trim()
      if (!campaignId) continue
      const prev = latestAnalyzedAtByCampaignId.get(campaignId)
      if (!prev || item.analyzedAt > prev) {
        latestAnalyzedAtByCampaignId.set(campaignId, item.analyzedAt)
      }
    }

    const rows = campaignRows.map((c) => {
      const base = scoreRow(c)
      const evalx = campaignEvaluations.get(c.id)
      return {
        campaign: c,
        base,
        evalx,
        shownSpend: evalx?.spend ?? base.spend,
        shownCtr: evalx?.ctr ?? base.ctr,
        shownRoas: evalx?.roas ?? base.roas,
        shownCpa: Number.isFinite(base.cpa) ? base.cpa : null,
        shownGrade: evalx?.grade ?? base.grade,
        latestAnalyzedAt: latestAnalyzedAtByCampaignId.get(c.id) ?? null,
      }
    })
    if (campaignSortBy === 'spend') {
      return rows.sort((a, b) => b.shownSpend - a.shownSpend)
    }
    if (campaignSortBy === 'cpa') {
      return rows.sort((a, b) => {
        const av = a.shownCpa ?? Number.POSITIVE_INFINITY
        const bv = b.shownCpa ?? Number.POSITIVE_INFINITY
        return av - bv
      })
    }
    return rows.sort((a, b) => {
      const as = a.evalx?.score ?? 0
      const bs = b.evalx?.score ?? 0
      return bs - as
    })
  }, [analyzedItems, campaignEvaluations, campaignRows, campaignSortBy])

  const campaignHealthSummary = useMemo(() => {
    let healthy = 0
    let watch = 0
    let issue = 0
    for (const card of campaignCards) {
      if (card.shownGrade === 'A' || card.shownGrade === 'B') healthy += 1
      else if (card.shownGrade === 'C') watch += 1
      else issue += 1
    }
    return { healthy, watch, issue }
  }, [campaignCards])

  const selectedCampaignAdsets = useMemo(() => {
    if (!selectedCampaign) return []
    const selectedCampaignName = (selectedCampaign.name ?? '').trim().toLowerCase()
    const rows = analyzedItems
      .filter((x) => {
        if (sameEntityId(x.campaignId, selectedCampaign.id)) return true
        if (!x.campaignId && selectedCampaignName) {
          return (x.campaignName ?? '').trim().toLowerCase() === selectedCampaignName
        }
        return false
      })
      .sort((a, b) => b.analyzedAt.localeCompare(a.analyzedAt))
    return rows
  }, [analyzedItems, selectedCampaign])

  const activeAggregate = useMemo(() => {
    if (!activeAnalyzedAd) return null
    const base = activeAnalyzedAd.aggregate
    const raw = activeRawLatest
    if (!raw) return base
    return {
      ...base,
      // Detail modal should prioritize latest raw Meta values for funnel consistency.
      spend: raw.spend > 0 ? raw.spend : base.spend,
      impressions: raw.impressions > 0 ? raw.impressions : base.impressions,
      linkClicks: raw.linkClicks > 0 ? raw.linkClicks : base.linkClicks,
      purchases: raw.purchases > 0 ? raw.purchases : base.purchases,
      purchaseValue: raw.purchaseValue > 0 ? raw.purchaseValue : base.purchaseValue,
      addToCart: raw.addToCart > 0 ? raw.addToCart : base.addToCart,
      initiateCheckout: raw.initiateCheckout > 0 ? raw.initiateCheckout : base.initiateCheckout,
      videoPlay3s: raw.videoPlay3s > 0 ? raw.videoPlay3s : base.videoPlay3s,
      thruPlay: raw.videoThruplay > 0 ? raw.videoThruplay : base.thruPlay,
      videoP25: raw.videoP25 > 0 ? raw.videoP25 : base.videoP25,
      videoP50: raw.videoP50 > 0 ? raw.videoP50 : base.videoP50,
      videoP75: raw.videoP75 > 0 ? raw.videoP75 : base.videoP75,
      videoP100: raw.videoP100 > 0 ? raw.videoP100 : base.videoP100,
      roas: raw.roas ?? base.roas ?? null,
      cpa: raw.cpa ?? base.cpa ?? null,
    }
  }, [activeAnalyzedAd, activeRawLatest])

  const timelineAggregate = useMemo(() => {
    if (!activeAnalyzedAd) return null
    return activeAggregate ?? activeAnalyzedAd.aggregate
  }, [activeAnalyzedAd, activeAggregate])

  const timelineValues = useMemo(() => {
    if (!timelineAggregate) return []
    return [
      { pct: 25, value: timelineAggregate.videoP25 },
      { pct: 50, value: timelineAggregate.videoP50 },
      { pct: 75, value: timelineAggregate.videoP75 },
      { pct: 100, value: timelineAggregate.videoP100 },
    ]
  }, [timelineAggregate])

  useEffect(() => {
    let cancelled = false
    if (!activeAnalyzedAd) {
      setActiveRawLatest(null)
      return
    }
    ;(async () => {
      try {
        await postInsightsSync(userId, 'ad', 'last_30d', {
          adIds: [activeAnalyzedAd.adId],
          metaAdAccountId: selectedAct ?? undefined,
        }).catch(() => undefined)
        const rows = await getRawInsights(userId, 'ad', { adId: activeAnalyzedAd.adId })
        if (cancelled) return
        const latest = rows
          .filter((r) => sameEntityId(r.entityId, activeAnalyzedAd.adId))
          .sort((a, b) => b.fetchedAt.localeCompare(a.fetchedAt))[0] ?? null
        setActiveRawLatest(latest)
      } catch {
        if (!cancelled) setActiveRawLatest(null)
      }
    })()
    return () => {
      cancelled = true
    }
  }, [activeAnalyzedAd, selectedAct, userId])

  return (
    <div className="page">
      <h1 className="page-title">{view === 'accounts' ? 'Reklam hesabını seç' : 'Kampanya seç'}</h1>
      <p className="page-lead">
        {view === 'accounts'
          ? 'İncelemek istediğiniz reklam hesabını seçin.'
          : 'Seçilen reklam hesabındaki kampanyalara girip analiz akışına devam edin.'}
      </p>
      {loading && <p className="muted">Yükleniyor…</p>}
      {error && <p className="error-banner">{error}</p>}

      {!loading && view === 'accounts' && (
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

      {!loading && view === 'campaigns' && selectedAct && (
        <section className="panel" style={{ marginTop: '1rem' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '0.75rem', marginBottom: '0.6rem' }}>
            <h2 className="panel-title" style={{ marginBottom: 0 }}>
              {cards.find((c) => c.account.metaAdAccountId === selectedAct)?.account.displayName?.trim() || selectedAct}
            </h2>
            <button
              type="button"
              className="btn"
              onClick={() => {
                setView('accounts')
                setSelectedCampaign(null)
              }}
            >
              Geri
            </button>
          </div>
          <p className="muted small" style={{ marginTop: '-0.2rem', marginBottom: '0.45rem' }}>
            CPA = Bir satın alma için ortalama maliyet (Cost Per Acquisition). `—` görünmesi, Meta'nın o kampanya için hesaplanabilir satın alma/CPA değeri döndürmemesidir.
          </p>
          <div className="campaign-toolbar">
            <div className="campaign-health-summary">
              <span>Sağlıklı: <strong>{campaignHealthSummary.healthy}</strong></span>
              <span>İzlenmeli: <strong>{campaignHealthSummary.watch}</strong></span>
              <span>Sorunlu: <strong>{campaignHealthSummary.issue}</strong></span>
            </div>
            <div className="campaign-sort-controls">
              <button type="button" className={`btn btn-sm ${campaignSortBy === 'spend' ? 'primary' : ''}`} onClick={() => setCampaignSortBy('spend')}>
                Harcama
              </button>
              <button type="button" className={`btn btn-sm ${campaignSortBy === 'health' ? 'primary' : ''}`} onClick={() => setCampaignSortBy('health')}>
                Sağlık Raporu
              </button>
              <button type="button" className={`btn btn-sm ${campaignSortBy === 'cpa' ? 'primary' : ''}`} onClick={() => setCampaignSortBy('cpa')}>
                CPA
              </button>
            </div>
          </div>
          <div className="campaign-list">
            {campaignCards.map(({ campaign: c, base: x, evalx, shownSpend, shownCtr, shownRoas, shownGrade, shownCpa, latestAnalyzedAt }) => {
              return (
                <button key={c.id} type="button" className="campaign-row" onClick={() => setSelectedCampaign(c)}>
                  <div className="campaign-main">
                    <div className="campaign-title">{c.name?.trim() || c.id}</div>
                    <div className="campaign-meta muted small">
                      {objectiveLabel(c.objective)} · <span className="status-dot status-live">Aktif</span>
                    </div>
                    <div className="muted small">
                      Son analiz: {latestAnalyzedAt ? new Date(latestAnalyzedAt).toLocaleString('tr-TR') : 'Henüz analiz yok'}
                    </div>
                    <span className={`tag-chip ${x.tag === 'Sağlıklı' ? 'tag-ok' : 'tag-warn'}`}>{x.tag}</span>
                  </div>
                  <div className="campaign-kpis">
                    <div>
                      <span className="muted small">Harcama</span>
                      <strong>₺{shownSpend.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}</strong>
                    </div>
                    <div>
                      <span className="muted small">CTR</span>
                      <strong>{shownCtr.toFixed(2)}%</strong>
                    </div>
                    <div>
                      <span className="muted small">ROAS</span>
                      <strong>{shownRoas.toFixed(1)}x</strong>
                    </div>
                    <div>
                      <span className="muted small">CPA</span>
                      <strong>{shownCpa == null ? '—' : `₺${shownCpa.toFixed(2)}`}</strong>
                    </div>
                    <div>
                      <span className="muted small">Kampanya Skoru</span>
                      <strong>{evalx?.score ?? 0}/100</strong>
                    </div>
                  </div>
                  <div className={`grade-box ${gradeColor(shownGrade)}`}>{shownGrade}</div>
                </button>
              )
            })}
            {campaignRows.length === 0 && <p className="muted">Bu hesapta kampanya bulunamadı.</p>}
          </div>
        </section>
      )}

      {selectedCampaign && (
        <div className="vr-modal-overlay" role="dialog" aria-modal="true" aria-label="Adset listesi">
          <div className="vr-modal">
            <button type="button" className="vr-modal-close" onClick={() => setSelectedCampaign(null)} aria-label="Kapat">×</button>
            <h2 className="panel-title">Reklam setleri</h2>
            <p className="muted small" style={{ marginBottom: '0.7rem' }}>
              Kampanya: {selectedCampaign.name?.trim() || selectedCampaign.id}
            </p>
            {(() => {
              const evalx = campaignEvaluations.get(selectedCampaign.id)
              if (!evalx) return null
              return (
                <div className="dashboard-mini-stats" style={{ marginBottom: '0.75rem' }}>
                  <div><span>Skor</span><strong>{evalx.score}/100 · {evalx.scoreLabel}</strong></div>
                  <div><span>ROAS / CTR / CVR</span><strong>{evalx.roas.toFixed(2)}x · {evalx.ctr.toFixed(2)}% · {evalx.cvr.toFixed(2)}%</strong></div>
                  <div><span>Adset Sağlığı</span><strong>{evalx.healthyAdsetCount} iyi / {evalx.weakAdsetCount} zayıf</strong></div>
                  <div><span>Analiz Kapsamı</span><strong>{evalx.analyzedAdsetCount} adset · {evalx.analyzedAdCount || 0} reklam</strong></div>
                </div>
              )
            })()}
            {(() => {
              const evalx = campaignEvaluations.get(selectedCampaign.id)
              if (!evalx || evalx.warnings.length === 0) return null
              return (
                <div className="impact-banner impact-banner-orange" style={{ marginBottom: '0.6rem' }}>
                  {evalx.warnings[0]}
                </div>
              )
            })()}
            {(() => {
              const evalx = campaignEvaluations.get(selectedCampaign.id)
              if (!evalx) return null
              const diagnosis = buildCampaignDiagnosis(evalx)
              return (
                <div className="panel" style={{ marginBottom: '0.7rem' }}>
                  <h3 className="panel-title" style={{ marginBottom: '0.35rem' }}>{diagnosis.headline}</h3>
                  <p className="muted small" style={{ margin: '0 0 0.5rem' }}>{diagnosis.summary}</p>
                  <div className="dashboard-mini-stats" style={{ marginBottom: '0.5rem' }}>
                    <div><span>Durum Özeti</span><strong>ROAS {evalx.roas.toFixed(2)}x · CPA {evalx.cpa == null ? '—' : `₺${evalx.cpa.toFixed(2)}`}</strong></div>
                    <div><span>İlk dikkat ve ilgi</span><strong>CTR %{evalx.ctr.toFixed(2)}</strong></div>
                    <div><span>Satın alma dönüşümü</span><strong>CVR %{evalx.cvr.toFixed(2)}</strong></div>
                    <div><span>Kapsam</span><strong>{evalx.analyzedAdsetCount} adset / {evalx.analyzedAdCount} reklam</strong></div>
                  </div>
                  <p className="muted small" style={{ margin: '0.15rem 0' }}><strong>Teşhis nedenleri</strong></p>
                  {diagnosis.reasons.map((r) => (
                    <p key={r} className="muted small" style={{ margin: '0.15rem 0' }}>• {r}</p>
                  ))}
                  <p className="muted small" style={{ margin: '0.45rem 0 0.15rem' }}><strong>Öncelikli aksiyon planı</strong></p>
                  {diagnosis.actions.map((a) => (
                    <p key={a} className="muted small" style={{ margin: '0.15rem 0' }}>• {a}</p>
                  ))}
                  <p className="muted small" style={{ margin: '0.45rem 0 0', color: 'var(--text)' }}>
                    <strong>Beklenen etki:</strong> {diagnosis.expectedImpact}
                  </p>
                </div>
              )
            })()}
            <div className="analyzed-list">
              {selectedCampaignAdsets.map((item) => {
                const g = adGrade(item)
                return (
                  <button
                    key={item.id}
                    type="button"
                    className="analyzed-row"
                    onClick={() => {
                      setActiveAnalyzedAd(item)
                    }}
                  >
                    <div className="analyzed-thumb-wrap">
                      {item.thumbnailUrl ? (
                        <img src={item.thumbnailUrl} alt="" className="analyzed-thumb" />
                      ) : (
                        <div className="analyzed-thumb-fallback" />
                      )}
                    </div>
                    <div className="analyzed-main">
                      <div className="analyzed-title">{item.adName}</div>
                      <div className="muted small">
                        {item.campaignName ?? item.campaignId ?? 'kampanya —'} · {item.adsetName ?? item.adsetId ?? 'adset —'} · reklam {item.adId}
                      </div>
                      <div className="muted small">Analiz: {new Date(item.analyzedAt).toLocaleString('tr-TR')}</div>
                    </div>
                    <div className="analyzed-kpis">
                      <div><span className="muted small">ROAS</span><strong>{(item.aggregate.roas ?? 0).toFixed(1)}x</strong></div>
                      <div><span className="muted small">Hook</span><strong>{pct(item.aggregate.thumbstopPct)}</strong></div>
                      <div><span className="muted small">Hold</span><strong>{pct(item.aggregate.holdPct)}</strong></div>
                    </div>
                    <div className={`grade-box ${g.letter === 'A' ? 'grade-good' : g.letter === 'B' ? 'grade-mid' : 'grade-bad'}`}>{g.letter}</div>
                  </button>
                )
              })}
              {selectedCampaignAdsets.length === 0 && (
                <p className="muted">Bu kampanya için analiz edilen kayıt bulunamadı.</p>
              )}
            </div>
          </div>
        </div>
      )}

      {activeAnalyzedAd && (
        <div className="vr-modal-overlay" role="dialog" aria-modal="true" aria-label="Detay rapor">
          <div className="vr-modal">
            <button type="button" className="vr-modal-close" onClick={() => setActiveAnalyzedAd(null)} aria-label="Kapat">×</button>
            <div className="vr-modal-main">
              <div className="vr-modal-left">
                {activeAnalyzedAd.thumbnailUrl ? <img src={activeAnalyzedAd.thumbnailUrl} alt="" className="vr-modal-thumb" /> : <div className="vr-modal-thumb-fallback" />}
              </div>
              <div className="vr-modal-right">
                <div className="vr-diagnosis-box">
                  <strong>DERECELENDİRME: {adGrade(activeAnalyzedAd).letter}</strong>
                  <ul className="muted small" style={{ margin: '0.5rem 0 0', paddingLeft: '1rem' }}>
                    {adGrade(activeAnalyzedAd).breakdown.map((b) => <li key={b}>{b}</li>)}
                  </ul>
                </div>
                <div className="analyzed-top-meta">
                  <span className={`grade-pill ${scoreLabel(activeAnalyzedAd.aggregate.creativeScore).cls}`}>
                    {scoreLabel(activeAnalyzedAd.aggregate.creativeScore).label}
                  </span>
                  <span className="muted small">Skor: {activeAnalyzedAd.aggregate.creativeScore ?? '—'}/100</span>
                  <span className="muted small">Analiz: {new Date(activeAnalyzedAd.analyzedAt).toLocaleString('tr-TR')}</span>
                  <span className="muted small">Gösterim: {activeAnalyzedAd.aggregate.impressions.toLocaleString('tr-TR')}</span>
                </div>
              </div>
            </div>
            <div className="analyzed-tabs">
              <button type="button" className={`btn ${detailTab === 'funnel' ? 'primary' : ''}`} onClick={() => setDetailTab('funnel')}>Dönüşüm Hunisi</button>
              <button type="button" className={`btn ${detailTab === 'timeline' ? 'primary' : ''}`} onClick={() => setDetailTab('timeline')}>Video Zaman Çizgisi</button>
              <button type="button" className={`btn ${detailTab === 'metrics' ? 'primary' : ''}`} onClick={() => setDetailTab('metrics')}>Metrik Özeti</button>
            </div>
            {detailTab === 'funnel' && (
              <div className="vr-funnel">
                <h3>Dönüşüm Hunisi</h3>
                <table className="data-table compact">
                  <thead>
                    <tr>
                      <th>Adım</th>
                      <th>Değer</th>
                      <th>Kayıp</th>
                    </tr>
                  </thead>
                  <tbody>
                    {buildFunnel(activeAggregate ?? activeAnalyzedAd.aggregate).map((step, i) => (
                      <tr key={`${step.label}-${i}`}>
                        <td>{step.label}</td>
                        <td>{step.value.toLocaleString('tr-TR')}</td>
                        <td>
                          {step.lossPct == null
                            ? '—'
                            : `${step.lostCount.toLocaleString('tr-TR')} (${step.lossPct.toFixed(1)}%)`}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {detailTab === 'metrics' && (
              <div className="vr-metric-content">
                <h3>Metrik Özeti</h3>
                <table className="data-table compact"><tbody>
                  <tr><td>Hook Rate</td><td>{pct((activeAggregate ?? activeAnalyzedAd.aggregate).thumbstopPct)}</td><td>Ort: %25</td><td>{statusIcon(((activeAggregate ?? activeAnalyzedAd.aggregate).thumbstopPct ?? 0) > 20, ((activeAggregate ?? activeAnalyzedAd.aggregate).thumbstopPct ?? 0) >= 10)}</td></tr>
                  <tr><td>Hold Rate</td><td>{pct((activeAggregate ?? activeAnalyzedAd.aggregate).holdPct)}</td><td>Ort: %20</td><td>{statusIcon(((activeAggregate ?? activeAnalyzedAd.aggregate).holdPct ?? 0) > 15, ((activeAggregate ?? activeAnalyzedAd.aggregate).holdPct ?? 0) >= 8)}</td></tr>
                  <tr><td>CTR</td><td>{pct((activeAggregate ?? activeAnalyzedAd.aggregate).ctrLinkPct)}</td><td>Ort: %1.2</td><td>{statusIcon(((activeAggregate ?? activeAnalyzedAd.aggregate).ctrLinkPct ?? 0) > 1, ((activeAggregate ?? activeAnalyzedAd.aggregate).ctrLinkPct ?? 0) >= 0.5)}</td></tr>
                  <tr><td>CVR</td><td>{pct((activeAggregate ?? activeAnalyzedAd.aggregate).linkCvrPct)}</td><td>Ort: %1.5</td><td>{statusIcon(((activeAggregate ?? activeAnalyzedAd.aggregate).linkCvrPct ?? 0) > 1, ((activeAggregate ?? activeAnalyzedAd.aggregate).linkCvrPct ?? 0) >= 0.5)}</td></tr>
                  <tr><td>ROAS</td><td>{((activeAggregate ?? activeAnalyzedAd.aggregate).roas ?? 0).toFixed(2)}x</td><td>Ort: 2.5x</td><td>{statusIcon(((activeAggregate ?? activeAnalyzedAd.aggregate).targetRoas ?? 0) > 0 && ((activeAggregate ?? activeAnalyzedAd.aggregate).roas ?? 0) > ((activeAggregate ?? activeAnalyzedAd.aggregate).targetRoas ?? 0), ((activeAggregate ?? activeAnalyzedAd.aggregate).breakEvenRoas ?? 0) > 0 && ((activeAggregate ?? activeAnalyzedAd.aggregate).roas ?? 0) > ((activeAggregate ?? activeAnalyzedAd.aggregate).breakEvenRoas ?? 0))}</td></tr>
                </tbody></table>
              </div>
            )}
            {detailTab === 'timeline' && (
              <div className="vr-timeline">
                <h3>Video Zaman Çizgisi</h3>
                <div className="vr-timeline-bar" />
                <div className="vr-timeline-points">
                  {timelineValues.map((p) => (
                    <div key={p.pct} className="vr-timeline-point" style={{ left: `${p.pct}%` }}>
                      <span>{p.pct}%</span>
                      <strong>{p.value.toLocaleString('tr-TR')}</strong>
                      <em className="muted small">
                        {timelineAggregate && timelineAggregate.impressions > 0
                          ? `%${((p.value / timelineAggregate.impressions) * 100).toFixed(1)} izledi`
                          : '%0.0 izledi'}
                      </em>
                    </div>
                  ))}
                </div>
                {!timelineAggregate || timelineValues.every((x) => x.value <= 0) ? (
                  <p className="vr-drop-marker">Yeterli video izlenme verisi yok</p>
                ) : null}
              </div>
            )}
            <div className="vr-recs">
              <h3>Tanılar ve öneriler</h3>
              {activeAnalyzedAd.recommendations.map((r) => {
                const isLocked = r.status !== 'pending'
                return (
                  <div key={r.id} className="vr-rec-row">
                    <span className={`vr-priority ${r.severity === 'critical' ? 'vr-priority-high' : 'vr-priority-mid'}`}>{r.severity === 'critical' ? 'ÖNCELİKLİ' : 'ORTA'}</span>
                    <span className="vr-priority vr-priority-mid">{directiveTypeLabel(r.directiveType)}</span>
                    <p>{r.symptom ?? r.message}</p>
                    <p className="vr-reason">{r.reason ?? 'Neden bilgisi yok.'}</p>
                    <p className="vr-action">{r.action ?? 'İncele ve uygun aksiyonu uygula.'}</p>
                    <div className="vr-rec-actions">
                      <button type="button" className={`btn ${r.status === 'skipped' ? 'vr-btn-skip' : ''}`} disabled={isLocked} onClick={() => { if (isLocked) return; void updateRecommendationStatus(activeAnalyzedAd.id, r.id, 'skipped').then(() => listAnalyzedAds(userId).then(setAnalyzedItems)); }}>
                        {r.status === 'skipped' ? 'Atlandı' : 'Atla'}
                      </button>
                      <button type="button" className={`btn primary ${r.status === 'applied' ? 'vr-btn-apply' : ''}`} disabled={isLocked} onClick={() => { if (isLocked) return; void updateRecommendationStatus(activeAnalyzedAd.id, r.id, 'applied').then(() => listAnalyzedAds(userId).then(setAnalyzedItems)); }}>
                        {r.status === 'applied' ? 'Uygulandı ✓' : 'Uygula'}
                      </button>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>
        </div>
      )}
    </div>
  )
}


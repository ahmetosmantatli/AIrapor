import { createSavedReport, listSavedReports, updateSavedSuggestionStatus } from '../api/client'
import type { DirectiveItem, SavedReportItem, VideoReportAggregateResponse } from '../api/types'
const LEGACY_KEY = 'metaads_analyzed_ads_v1'

function n(v: unknown, fallback = 0): number {
  return typeof v === 'number' && Number.isFinite(v) ? v : fallback
}

function nn(v: unknown): number | null {
  if (v == null) return null
  return n(v, 0)
}

function sanitizeAggregate(raw: unknown): VideoReportAggregateResponse {
  const a = (raw ?? {}) as Partial<VideoReportAggregateResponse>
  return {
    spend: n(a.spend),
    impressions: Math.trunc(n(a.impressions)),
    reach: Math.trunc(n(a.reach)),
    linkClicks: Math.trunc(n(a.linkClicks)),
    purchases: Math.trunc(n(a.purchases)),
    purchaseValue: n(a.purchaseValue),
    addToCart: Math.trunc(n(a.addToCart)),
    initiateCheckout: Math.trunc(n(a.initiateCheckout)),
    videoPlay3s: Math.trunc(n(a.videoPlay3s)),
    videoP25: Math.trunc(n(a.videoP25)),
    videoP50: Math.trunc(n(a.videoP50)),
    videoP75: Math.trunc(n(a.videoP75)),
    videoP100: Math.trunc(n(a.videoP100)),
    thruPlay: Math.trunc(n(a.thruPlay)),
    ctrLinkPct: n(a.ctrLinkPct),
    linkCvrPct: nn(a.linkCvrPct),
    thumbstopPct: nn(a.thumbstopPct),
    holdPct: nn(a.holdPct),
    completionPct: nn(a.completionPct),
    roas: nn(a.roas),
    cpa: nn(a.cpa),
    breakEvenRoas: nn(a.breakEvenRoas),
    targetRoas: nn(a.targetRoas),
    maxCpa: nn(a.maxCpa),
    targetCpa: nn(a.targetCpa),
    hasProductMap: a.hasProductMap === true,
    dataQuality: {
      insufficientImpressions: a.dataQuality?.insufficientImpressions === true,
      lowPurchases: a.dataQuality?.lowPurchases === true,
      earlyData: a.dataQuality?.earlyData === true,
      learningPhase: a.dataQuality?.learningPhase === true,
      insufficientSpend: a.dataQuality?.insufficientSpend === true,
      warnings: Array.isArray(a.dataQuality?.warnings) ? a.dataQuality.warnings : [],
    },
    creativeScore: a.creativeScore == null ? null : Math.round(n(a.creativeScore)),
    narrativeLines: Array.isArray(a.narrativeLines) ? a.narrativeLines : [],
    problemTags: Array.isArray(a.problemTags) ? a.problemTags : [],
    hasInsightRows: a.hasInsightRows !== false,
    diagnosticMessage: a.diagnosticMessage ?? null,
  }
}

export type AnalyzedAdItem = {
  id: string
  adId: string
  adName: string
  thumbnailUrl: string | null
  campaignId: string | null
  campaignName: string | null
  adsetId: string | null
  adsetName: string | null
  analyzedAt: string
  aggregate: VideoReportAggregateResponse
  recommendations: Array<{
    id: string
    directiveType?: string | null
    severity: 'critical' | 'warning' | 'info'
    message: string
    symptom?: string | null
    reason?: string | null
    action?: string | null
    status: 'pending' | 'applied' | 'skipped'
    appliedAt?: string | null
    skippedAt?: string | null
    beforeRoas?: number | null
    beforeSpend?: number | null
    beforePurchases?: number | null
    afterRoas?: number | null
    afterSpend?: number | null
    afterPurchases?: number | null
    impactMeasuredAt?: string | null
    metaChangeDetected?: boolean
    metaChangeMessage?: string | null
  }>
}
function mapSavedReport(x: SavedReportItem): AnalyzedAdItem {
  return {
    id: String(x.id),
    adId: x.adId,
    adName: x.adName ?? x.adId,
    thumbnailUrl: x.thumbnailUrl,
    campaignId: x.campaignId,
    campaignName: x.campaignName,
    adsetId: x.adsetId,
    adsetName: x.adsetName,
    analyzedAt: x.analyzedAt,
    aggregate: sanitizeAggregate({
      spend: x.aggregateSpend ?? 0,
      purchases: x.aggregatePurchases ?? 0,
      roas: x.aggregateRoas ?? null,
      thumbstopPct: x.aggregateHookRate ?? null,
      holdPct: x.aggregateHoldRate ?? null,
    }),
    recommendations: x.suggestions.map((s) => ({
      id: String(s.id),
      directiveType: s.directiveType,
      severity: s.severity === 'critical' ? 'critical' : s.severity === 'warning' ? 'warning' : 'info',
      message: s.message,
      symptom: s.symptom,
      reason: s.reason,
      action: s.action,
      status: s.appliedAt ? 'applied' : s.skippedAt ? 'skipped' : 'pending',
      appliedAt: s.appliedAt,
      skippedAt: s.skippedAt,
      beforeRoas: s.beforeRoas,
      beforeSpend: s.beforeSpend,
      beforePurchases: s.beforePurchases,
      afterRoas: s.afterRoas,
      afterSpend: s.afterSpend,
      afterPurchases: s.afterPurchases,
      impactMeasuredAt: s.impactMeasuredAt,
      metaChangeDetected: s.metaChangeDetected,
      metaChangeMessage: s.metaChangeMessage,
    })),
  }
}

export async function listAnalyzedAds(userId: number): Promise<AnalyzedAdItem[]> {
  try {
    let rows = await listSavedReports(userId)
    let mapped = rows.map(mapSavedReport).sort((a, b) => b.analyzedAt.localeCompare(a.analyzedAt))
    const legacy = readLegacyLocalAnalyzedAds()
    if (legacy.length > 0) {
      const migrated = await migrateLegacyAnalyzedAdsToServer(userId, legacy, mapped)
      if (migrated > 0) {
        rows = await listSavedReports(userId)
        mapped = rows.map(mapSavedReport).sort((a, b) => b.analyzedAt.localeCompare(a.analyzedAt))
      }
    }
    return mergeAnalyzedAds(mapped, readLegacyLocalAnalyzedAds())
  } catch {
    return readLegacyLocalAnalyzedAds()
  }
}

export async function addAnalyzedAd(input: {
  userId: number
  adId: string
  adName: string
  thumbnailUrl: string | null
  campaignId: string | null
  campaignName: string | null
  adsetId: string | null
  adsetName: string | null
  aggregate: VideoReportAggregateResponse
  directives: DirectiveItem[]
}): Promise<AnalyzedAdItem> {
  const suggestions =
    input.directives.length > 0
      ? input.directives.slice(0, 6).map((d) => ({
          suggestionKey: `dir-${d.id}`,
          directiveType: d.directiveType ?? null,
          severity: d.severity,
          message: d.message,
          symptom: d.symptom ?? null,
          reason: d.reason ?? null,
          action: d.action ?? null,
        }))
      : [
          {
            suggestionKey: 'fallback-rec',
            directiveType: 'OPTIMIZE',
            severity: 'warning',
            message: 'Hook ve hold metriklerini destekleyecek 2-3 yeni kreatif varyantı hazırlayın.',
            symptom: 'Hook ve hold performansı hedefin altında.',
            reason: 'Mevcut kreatif kombinasyonu izleyiciyi yeterince taşımıyor.',
            action: 'Hazırla: 2-3 yeni kreatif varyantını test akışına al.',
          },
        ]
  try {
    const created = await createSavedReport({
      userId: input.userId,
      adId: input.adId,
      adName: input.adName,
      thumbnailUrl: input.thumbnailUrl,
      campaignId: input.campaignId,
      campaignName: input.campaignName,
      adsetId: input.adsetId,
      adsetName: input.adsetName,
      aggregateRoas: input.aggregate.roas,
      aggregateHookRate: input.aggregate.thumbstopPct,
      aggregateHoldRate: input.aggregate.holdPct,
      aggregateSpend: input.aggregate.spend,
      aggregatePurchases: input.aggregate.purchases,
      suggestions,
    })
    return mapSavedReport(created)
  } catch {
    return persistLegacyAnalyzedAd(input, suggestions)
  }
}

export async function updateRecommendationStatus(
  itemId: string,
  recId: string,
  status: 'applied' | 'skipped' | 'pending',
): Promise<void> {
  if (status === 'pending') return
  persistLegacyRecommendationStatus(itemId, recId, status)
  const id = Number(recId)
  if (!Number.isFinite(id) || id <= 0) return
  await updateSavedSuggestionStatus(id, status)
}

function readLegacyLocalAnalyzedAds(): AnalyzedAdItem[] {
  try {
    const raw = localStorage.getItem(LEGACY_KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw) as unknown[]
    if (!Array.isArray(parsed)) return []
    return parsed
      .map((x) => {
        const r = x as Partial<AnalyzedAdItem>
        if (!r.id || !r.adId) return null
        return {
          id: String(r.id),
          adId: String(r.adId),
          adName: r.adName ?? String(r.adId),
          thumbnailUrl: r.thumbnailUrl ?? null,
          campaignId: r.campaignId ?? null,
          campaignName: r.campaignName ?? null,
          adsetId: r.adsetId ?? null,
          adsetName: r.adsetName ?? null,
          analyzedAt: r.analyzedAt ?? new Date().toISOString(),
          aggregate: sanitizeAggregate(r.aggregate),
          recommendations: Array.isArray(r.recommendations) ? r.recommendations : [],
        } as AnalyzedAdItem
      })
      .filter((x): x is AnalyzedAdItem => x !== null)
      .sort((a, b) => b.analyzedAt.localeCompare(a.analyzedAt))
  } catch {
    return []
  }
}

function mergeAnalyzedAds(server: AnalyzedAdItem[], legacy: AnalyzedAdItem[]): AnalyzedAdItem[] {
  if (legacy.length === 0) return server
  if (server.length === 0) return legacy

  const merged = [...server]
  const seen = new Set(
    server.map((x) => `${x.adId}|${x.analyzedAt}|${x.recommendations.map((r) => r.message).join('|')}`),
  )
  for (const item of legacy) {
    const key = `${item.adId}|${item.analyzedAt}|${item.recommendations.map((r) => r.message).join('|')}`
    if (!seen.has(key)) {
      merged.push(item)
      seen.add(key)
    }
  }
  return merged.sort((a, b) => b.analyzedAt.localeCompare(a.analyzedAt))
}

function legacyMergeKey(item: Pick<AnalyzedAdItem, 'adId' | 'analyzedAt' | 'recommendations'>): string {
  return `${item.adId}|${item.analyzedAt}|${item.recommendations.map((r) => r.message).join('|')}`
}

async function migrateLegacyAnalyzedAdsToServer(
  userId: number,
  legacy: AnalyzedAdItem[],
  server: AnalyzedAdItem[],
): Promise<number> {
  const serverKeys = new Set(server.map((x) => legacyMergeKey(x)))
  const remaining: AnalyzedAdItem[] = []
  let migratedCount = 0

  for (const item of legacy) {
    const key = legacyMergeKey(item)
    if (serverKeys.has(key)) {
      continue
    }
    try {
      const suggestions = item.recommendations.map((r, idx) => ({
        suggestionKey: `legacy-${idx + 1}`,
        directiveType: r.directiveType ?? null,
        severity: r.severity,
        message: r.message,
        symptom: r.symptom ?? null,
        reason: r.reason ?? null,
        action: r.action ?? null,
      }))

      const created = await createSavedReport({
        userId,
        adId: item.adId,
        adName: item.adName,
        thumbnailUrl: item.thumbnailUrl,
        campaignId: item.campaignId,
        campaignName: item.campaignName,
        adsetId: item.adsetId,
        adsetName: item.adsetName,
        aggregateRoas: item.aggregate.roas,
        aggregateHookRate: item.aggregate.thumbstopPct,
        aggregateHoldRate: item.aggregate.holdPct,
        aggregateSpend: item.aggregate.spend,
        aggregatePurchases: item.aggregate.purchases,
        suggestions: suggestions.length > 0
          ? suggestions
          : [
              {
                suggestionKey: 'legacy-fallback',
                directiveType: 'OPTIMIZE',
                severity: 'warning',
                message: 'Legacy kayıttan taşınan öneri.',
                symptom: null,
                reason: null,
                action: null,
              },
            ],
      })

      for (const [idx, createdSuggestion] of created.suggestions.entries()) {
        const legacyRec = item.recommendations[idx]
        if (!legacyRec) continue
        if (legacyRec.status === 'applied' || legacyRec.status === 'skipped') {
          await updateSavedSuggestionStatus(createdSuggestion.id, legacyRec.status)
        }
      }

      migratedCount += 1
    } catch {
      remaining.push(item)
    }
  }

  try {
    if (remaining.length === 0) {
      localStorage.removeItem(LEGACY_KEY)
    } else {
      localStorage.setItem(LEGACY_KEY, JSON.stringify(remaining))
    }
  } catch {
    // ignore localStorage write failures
  }

  return migratedCount
}

function persistLegacyAnalyzedAd(
  input: {
    userId: number
    adId: string
    adName: string
    thumbnailUrl: string | null
    campaignId: string | null
    campaignName: string | null
    adsetId: string | null
    adsetName: string | null
    aggregate: VideoReportAggregateResponse
  },
  suggestions: Array<{
    suggestionKey: string
    directiveType?: string | null
    severity: string
    message: string
    symptom?: string | null
    reason?: string | null
    action?: string | null
  }>,
): AnalyzedAdItem {
  const nowIso = new Date().toISOString()
  const legacyItem: AnalyzedAdItem = {
    id: `legacy-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    adId: input.adId,
    adName: input.adName,
    thumbnailUrl: input.thumbnailUrl,
    campaignId: input.campaignId,
    campaignName: input.campaignName,
    adsetId: input.adsetId,
    adsetName: input.adsetName,
    analyzedAt: nowIso,
    aggregate: sanitizeAggregate(input.aggregate),
    recommendations: suggestions.map((s, idx) => ({
      id: `legacy-rec-${idx + 1}-${Date.now()}`,
      directiveType: s.directiveType ?? null,
      severity: s.severity === 'critical' ? 'critical' : s.severity === 'warning' ? 'warning' : 'info',
      message: s.message,
      symptom: s.symptom ?? null,
      reason: s.reason ?? null,
      action: s.action ?? null,
      status: 'pending',
      appliedAt: null,
      skippedAt: null,
      beforeRoas: null,
      beforeSpend: null,
      beforePurchases: null,
      afterRoas: null,
      afterSpend: null,
      afterPurchases: null,
      impactMeasuredAt: null,
      metaChangeDetected: true,
      metaChangeMessage: null,
    })),
  }
  try {
    const existing = readLegacyLocalAnalyzedAds()
    localStorage.setItem(LEGACY_KEY, JSON.stringify([legacyItem, ...existing]))
  } catch {
    // ignore localStorage write failures
  }
  return legacyItem
}

function persistLegacyRecommendationStatus(
  itemId: string,
  recId: string,
  status: 'applied' | 'skipped',
): void {
  try {
    const raw = localStorage.getItem(LEGACY_KEY)
    if (!raw) return
    const parsed = JSON.parse(raw) as unknown
    if (!Array.isArray(parsed)) return
    const nowIso = new Date().toISOString()
    let changed = false
    const next = parsed.map((entry) => {
      if (!entry || typeof entry !== 'object') return entry
      const obj = entry as Record<string, unknown>
      if (String(obj.id ?? '') !== itemId) return entry
      const recs = Array.isArray(obj.recommendations) ? obj.recommendations : null
      if (!recs) return entry
      const updatedRecs = recs.map((rec) => {
        if (!rec || typeof rec !== 'object') return rec
        const recObj = rec as Record<string, unknown>
        if (String(recObj.id ?? '') !== recId) return rec
        changed = true
        return {
          ...recObj,
          status,
          appliedAt: status === 'applied' ? nowIso : null,
          skippedAt: status === 'skipped' ? nowIso : null,
        }
      })
      return { ...obj, recommendations: updatedRecs }
    })
    if (changed) {
      localStorage.setItem(LEGACY_KEY, JSON.stringify(next))
    }
  } catch {
    // ignore localStorage parse/write failures
  }
}


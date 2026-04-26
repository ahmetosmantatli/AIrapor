export type AuthResponse = {
  accessToken: string
  userId: number
  email: string
  expiresAtUtc: string
}

export type WatchlistItem = {
  id: number
  level: string
  entityId: string
  createdAt: string
}

export type WatchlistToggleResult = {
  isWatching: boolean
  watchlistItemId: number | null
}

export type HealthResponse = {
  status: string
  database: string
}

export type DirectiveItem = {
  id: number
  entityId: string
  entityType: string
  directiveType: string
  severity: string
  message: string
  symptom?: string | null
  reason?: string | null
  action?: string | null
  score: number | null
  healthStatus: string | null
  triggeredAt: string
}

export type MetricsRecomputeResult = {
  computedRows: number
  skippedNoCampaignMap: number
  skippedNoCampaignKey: number
}

export type DirectiveEvaluateResult = {
  directivesCreated: number
  entitiesEvaluated: number
}

export type InsightsSyncResult = {
  rowsFetched: number
  rowsUpserted: number
  pageCount: number
}

export type InsightsRefreshResult = {
  status: 'updated' | 'skipped' | 'limit'
  message: string
  lastSync: string | null
  dailyCount?: number | null
}

export type LinkedMetaAdAccountItem = {
  id: number
  metaAdAccountId: string
  displayName: string | null
  linkedAt: string
}

export type UserProfile = {
  id: number
  email: string
  metaAdAccountId: string | null
  /** Plan limiti; eski API yanıtlarında olmayabilir. */
  maxLinkedMetaAdAccounts?: number
  linkedMetaAdAccounts?: LinkedMetaAdAccountItem[]
  currency: string
  timezone: string
  attributionWindow: string
  metaUserId: string | null
  metaTokenExpiresAt: string | null
  planCode: string
  planDisplayName: string
  planMonthlyPrice: number
  planCurrency: string
  planAllowsPdfExport: boolean
  planAllowsWatchlist: boolean
  subscriptionStatus: string
  planExpiresAt: string | null
}

export type SubscriptionPlan = {
  code: string
  displayName: string
  description: string | null
  monthlyPrice: number
  currency: string
  sortOrder: number
  updatedAt: string
  allowsPdfExport: boolean
  allowsWatchlist: boolean
  maxLinkedMetaAdAccounts?: number
}

export type AdAccountItem = {
  id: string
  name: string | null
  accountId: string | null
}

/** Graph act_…/campaigns */
export type MetaCampaignItem = {
  id: string
  name: string | null
  status: string | null
  objective: string | null
}

/** Graph {campaignId}/adsets */
export type MetaAdsetItem = {
  id: string
  name: string | null
  status: string | null
  campaignId: string | null
}

/** Graph act_…/ads — reklam ve isteğe bağlı video_id (performans ayrı insights ile gelir). */
export type MetaAdListItem = {
  id: string
  name: string | null
  status: string | null
  effectiveStatus: string | null
  creativeId: string | null
  creativeName: string | null
  videoId: string | null
  videoTitle: string | null
  thumbnailUrl: string | null
}

export type RawInsightRow = {
  id: number
  level: string
  entityId: string
  entityName: string | null
  metaCampaignId: string | null
  metaAdsetId: string | null
  dateStart: string
  dateStop: string
  fetchedAt: string
  spend: number
  impressions: number
  reach: number
  linkClicks: number
  videoPlay3s: number
  video15Sec: number
  videoP100: number
  purchases: number
  purchaseValue: number
  roas: number | null
  cpa: number | null
  thumbstopRatePct: number | null
  holdRatePct: number | null
  completionRatePct: number | null
  creativeScoreTotal: number | null
  computedMetricId: number | null
}

export type VideoReportAggregateResponse = {
  spend: number
  impressions: number
  reach: number
  linkClicks: number
  purchases: number
  purchaseValue: number
  addToCart: number
  initiateCheckout: number
  videoPlay3s: number
  videoP25: number
  videoP50: number
  videoP75: number
  videoP100: number
  thruPlay: number
  ctrLinkPct: number
  linkCvrPct: number | null
  thumbstopPct: number | null
  holdPct: number | null
  completionPct: number | null
  roas: number | null
  cpa: number | null
  breakEvenRoas: number | null
  targetRoas: number | null
  maxCpa: number | null
  targetCpa: number | null
  netProfitPerOrder: number | null
  netMarginPct: number | null
  hasProductMap: boolean
  dataQuality: {
    insufficientImpressions: boolean
    lowPurchases: boolean
    earlyData: boolean
    learningPhase: boolean
    insufficientSpend: boolean
    warnings: string[]
  }
  creativeScore: number | null
  narrativeLines: string[]
  problemTags: string[]
  /** Backend: ham insight yoksa false (404 yerine 200). */
  hasInsightRows?: boolean
  diagnosticMessage?: string | null
}

export type VideoAssetRow = {
  videoId: string
  thumbnailUrl: string | null
  representativeAdName: string | null
  totalSpend: number
  hookRateAvg: number | null
  holdRateAvg: number | null
  completionRateAvg: number | null
  totalRoas: number | null
  problemTags: string[]
}

export type CampaignMapItem = {
  id: number
  campaignId: string
  productId: number
  userId: number
}

export type ProductResponse = {
  id: number
  userId: number
  name: string
  cogs: number
  sellingPrice: number
  shippingCost: number
  paymentFeePct: number
  returnRatePct: number
  ltvMultiplier: number
  targetMarginPct: number
  createdAt: string
}

export type CreateProductPayload = {
  userId: number
  name: string
  cogs: number
  sellingPrice: number
  shippingCost: number
  paymentFeePct: number
  returnRatePct: number
  ltvMultiplier: number
  targetMarginPct: number
}

export type SavedReportSuggestion = {
  id: number
  suggestionKey: string
  directiveType: string | null
  severity: string
  message: string
  symptom: string | null
  reason: string | null
  action: string | null
  appliedAt: string | null
  skippedAt: string | null
  beforeRoas: number | null
  beforeHookRate: number | null
  beforeHoldRate: number | null
  beforeSpend: number | null
  beforePurchases: number | null
  afterRoas: number | null
  afterHookRate: number | null
  afterHoldRate: number | null
  afterSpend: number | null
  afterPurchases: number | null
  impactMeasuredAt: string | null
  metaChangeDetected: boolean
  metaChangeMessage: string | null
}

export type SavedReportItem = {
  id: number
  adId: string
  adName: string | null
  thumbnailUrl: string | null
  campaignId: string | null
  campaignName: string | null
  adsetId: string | null
  adsetName: string | null
  analyzedAt: string
  aggregateRoas: number | null
  aggregateHookRate: number | null
  aggregateHoldRate: number | null
  aggregateSpend: number | null
  aggregatePurchases: number | null
  suggestions: SavedReportSuggestion[]
}

export type SavedReportImpactFeedItem = {
  suggestionId: number
  savedReportId: number
  adId: string
  adName: string | null
  appliedAt: string
  impactMeasuredAt: string | null
  beforeRoas: number | null
  afterRoas: number | null
  metaChangeDetected: boolean
  metaChangeMessage: string | null
}

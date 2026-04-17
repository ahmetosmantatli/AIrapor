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

export type RawInsightRow = {
  id: number
  level: string
  entityId: string
  entityName: string | null
  metaCampaignId: string | null
  dateStart: string
  dateStop: string
  fetchedAt: string
  spend: number
  impressions: number
  linkClicks: number
  purchases: number
  purchaseValue: number
  roas: number | null
  cpa: number | null
  computedMetricId: number | null
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

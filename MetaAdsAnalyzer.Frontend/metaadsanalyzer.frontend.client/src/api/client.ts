import { apiUrl } from '../config/apiBase'
import { TOKEN_KEY, USER_ID_KEY, clearStoredSession } from '../context/UserContext'
import type {
  AdAccountItem,
  AuthResponse,
  CampaignMapItem,
  CreateProductPayload,
  DirectiveEvaluateResult,
  DirectiveItem,
  HealthResponse,
  InsightsSyncResult,
  MetricsRecomputeResult,
  ProductResponse,
  RawInsightRow,
  SubscriptionPlan,
  LinkedMetaAdAccountItem,
  UserProfile,
  WatchlistItem,
  WatchlistToggleResult,
  MetaAdListItem,
  MetaCampaignItem,
  MetaAdsetItem,
  VideoAssetRow,
  VideoReportAggregateResponse,
  SavedReportItem,
  SavedReportSuggestion,
  SavedReportImpactFeedItem,
} from './types'

function assertPositiveUserId(userId: number, context: string): void {
  if (!Number.isFinite(userId) || userId <= 0) {
    throw new Error(
      `Geçersiz kullanıcı kimliği (${context}). Çıkış yapıp yeniden giriş yapın veya Network’te istenen URL’yi kontrol edin.`,
    )
  }
}

async function parseError(res: Response): Promise<string> {
  const t = await res.text()
  try {
    const j = JSON.parse(t) as { message?: string; title?: string }
    return j.message ?? j.title ?? t
  } catch {
    return t || res.statusText
  }
}

async function authFetch(path: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers)
  if (!headers.has('Accept')) headers.set('Accept', 'application/json')
  const t = localStorage.getItem(TOKEN_KEY)
  if (t) headers.set('Authorization', `Bearer ${t}`)
  const fullUrl = apiUrl(path)
  const method = init?.method ?? 'GET'
  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.debug('[api]', method, fullUrl)
  }
  const res = await fetch(fullUrl, { ...init, headers })
  if (!res.ok && import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.warn('[api] HTTP', res.status, method, fullUrl)
  }
  if (res.status === 401) {
    clearStoredSession()
    if (typeof window !== 'undefined') {
      const p = window.location.pathname
      if (!['/login', '/register', '/connect', '/'].includes(p)) {
        window.location.assign('/login')
      }
    }
  }
  return res
}

async function postJson<T>(path: string, body: unknown): Promise<T> {
  const res = await authFetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) {
    const msg = (await parseError(res)) || res.statusText || String(res.status)
    const fullUrl = apiUrl(path)
    if (import.meta.env.DEV) {
      // eslint-disable-next-line no-console
      console.error('[api] POST failed', fullUrl, res.status, msg)
    }
    throw new Error(import.meta.env.DEV ? `${msg} (${fullUrl})` : msg)
  }
  return res.json() as Promise<T>
}

export async function getSubscriptionPlans(): Promise<SubscriptionPlan[]> {
  const res = await fetch(apiUrl('/api/subscription/plans'), { headers: { Accept: 'application/json' } })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<SubscriptionPlan[]>
}

export async function postSelectMyPlan(planCode: 'standard' | 'pro'): Promise<void> {
  const res = await authFetch('/api/subscription/my-plan', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ planCode }),
  })
  if (!res.ok) throw new Error(await parseError(res))
}

export type BillingCheckoutResponse = {
  checkoutUrl: string
}

export async function postBillingCheckout(
  planCode: 'standard' | 'pro',
): Promise<BillingCheckoutResponse> {
  return postJson<BillingCheckoutResponse>('/api/billing/checkout', { planCode })
}

export async function getHealth(): Promise<HealthResponse> {
  const res = await fetch(apiUrl('/api/health'), { headers: { Accept: 'application/json' } })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<HealthResponse>
}

export async function registerAccount(email: string, password: string): Promise<AuthResponse> {
  const res = await fetch(apiUrl('/api/auth/register'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<AuthResponse>
}

export async function loginAccount(email: string, password: string): Promise<AuthResponse> {
  const res = await fetch(apiUrl('/api/auth/login'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<AuthResponse>
}

export async function getAuthMe(): Promise<{ userId: number; email: string | null }> {
  const res = await authFetch('/api/auth/me', {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<{ userId: number; email: string | null }>
}

export async function getActiveDirectives(userId: number): Promise<DirectiveItem[]> {
  const res = await authFetch(`/api/directives/active/by-user/${userId}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<DirectiveItem[]>
}

export async function postMetricsRecompute(
  userId: number,
  opts?: { adIds?: string[] },
): Promise<MetricsRecomputeResult> {
  return postJson<MetricsRecomputeResult>('/api/metrics/recompute', {
    userId,
    ...(opts?.adIds?.length ? { adIds: opts.adIds } : {}),
  })
}

export async function postDirectivesEvaluate(
  userId: number,
  opts?: { adIds?: string[] },
): Promise<DirectiveEvaluateResult> {
  return postJson<DirectiveEvaluateResult>('/api/directives/evaluate', {
    userId,
    ...(opts?.adIds?.length ? { adIds: opts.adIds } : {}),
  })
}

export async function postInsightsSync(
  userId: number,
  level: string,
  datePreset: string,
  opts?: { adId?: string; adIds?: string[]; metaAdAccountId?: string },
): Promise<InsightsSyncResult> {
  return postJson<InsightsSyncResult>('/api/meta/insights/sync', {
    userId,
    level,
    datePreset,
    ...(opts?.adIds?.length ? { adIds: opts.adIds } : {}),
    ...(opts?.adId ? { adId: opts.adId } : {}),
    ...(opts?.metaAdAccountId ? { metaAdAccountId: opts.metaAdAccountId } : {}),
  })
}

export async function getMetaCampaigns(
  userId: number,
  metaAdAccountId?: string,
): Promise<MetaCampaignItem[]> {
  assertPositiveUserId(userId, 'getMetaCampaigns')
  const q = metaAdAccountId
    ? `?metaAdAccountId=${encodeURIComponent(metaAdAccountId)}`
    : ''
  const path = `/api/meta/campaigns/${userId}${q}`
  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.info('[api] getMetaCampaigns', { path, resolvedUrl: apiUrl(path), userId, metaAdAccountId })
  }
  const res = await authFetch(path, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<MetaCampaignItem[]>
}

export async function getMetaAdsets(
  userId: number,
  campaignId: string,
  metaAdAccountId?: string,
): Promise<MetaAdsetItem[]> {
  assertPositiveUserId(userId, 'getMetaAdsets')
  const params = new URLSearchParams({ campaignId })
  if (metaAdAccountId) params.set('metaAdAccountId', metaAdAccountId)
  const path = `/api/meta/adsets/${userId}?${params.toString()}`
  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.info('[api] getMetaAdsets', { path, resolvedUrl: apiUrl(path), userId, campaignId, metaAdAccountId })
  }
  const res = await authFetch(path, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<MetaAdsetItem[]>
}

export async function getMetaAds(
  userId: number,
  metaAdAccountId?: string,
  opts?: { campaignId?: string; adsetId?: string },
): Promise<MetaAdListItem[]> {
  assertPositiveUserId(userId, 'getMetaAds')
  const params = new URLSearchParams()
  if (metaAdAccountId) params.set('metaAdAccountId', metaAdAccountId)
  if (opts?.campaignId) params.set('campaignId', opts.campaignId)
  if (opts?.adsetId) params.set('adsetId', opts.adsetId)
  const q = params.toString() ? `?${params.toString()}` : ''
  const path = `/api/meta/ads/${userId}${q}`
  if (import.meta.env.DEV) {
    // eslint-disable-next-line no-console
    console.info('[api] getMetaAds', { path, resolvedUrl: apiUrl(path), userId, metaAdAccountId, opts })
  }
  const res = await authFetch(path, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<MetaAdListItem[]>
}

export async function getUserProfile(userId: number): Promise<UserProfile> {
  const res = await authFetch(`/api/users/${userId}`, { headers: { Accept: 'application/json' } })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<UserProfile>
}

export async function patchUserSettings(
  userId: number,
  body: Partial<{
    metaAdAccountId: string | null
    currency: string
    timezone: string
    attributionWindow: string
  }>,
): Promise<void> {
  const res = await authFetch(`/api/users/${userId}/settings`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(await parseError(res))
}

export async function getAdAccounts(userId: number): Promise<AdAccountItem[]> {
  const res = await authFetch(`/api/meta/ad-accounts/${userId}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<AdAccountItem[]>
}

export async function getLinkedMetaAdAccounts(userId: number): Promise<LinkedMetaAdAccountItem[]> {
  const res = await authFetch(`/api/users/${userId}/meta-ad-accounts`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<LinkedMetaAdAccountItem[]>
}

export async function postLinkMetaAdAccount(
  userId: number,
  body: { metaAdAccountId: string; displayName?: string | null },
): Promise<LinkedMetaAdAccountItem> {
  return postJson<LinkedMetaAdAccountItem>(`/api/users/${userId}/meta-ad-accounts`, body)
}

export async function deleteLinkedMetaAdAccount(userId: number, linkId: number): Promise<void> {
  const res = await authFetch(`/api/users/${userId}/meta-ad-accounts/${linkId}`, {
    method: 'DELETE',
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
}

export async function postSelectActiveMetaAdAccount(
  userId: number,
  metaAdAccountId: string,
): Promise<void> {
  const res = await authFetch(`/api/users/${userId}/meta-ad-accounts/select-active`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ metaAdAccountId }),
  })
  if (!res.ok) throw new Error(await parseError(res))
}

export async function getRawInsights(
  userId: number,
  level?: string,
  opts?: { campaignId?: string; adId?: string; limit?: number },
): Promise<RawInsightRow[]> {
  const params = new URLSearchParams()
  if (level) params.set('level', level)
  if (opts?.campaignId) params.set('campaignId', opts.campaignId)
  if (opts?.adId) params.set('adId', opts.adId)
  if (typeof opts?.limit === 'number' && Number.isFinite(opts.limit) && opts.limit > 0) {
    params.set('limit', String(Math.trunc(opts.limit)))
  }
  const q = params.toString() ? `?${params.toString()}` : ''
  const res = await authFetch(`/api/raw-insights/by-user/${userId}${q}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<RawInsightRow[]>
}

export async function getProducts(userId: number): Promise<ProductResponse[]> {
  const res = await authFetch(`/api/products/by-user/${userId}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<ProductResponse[]>
}

export async function createProduct(body: CreateProductPayload): Promise<ProductResponse> {
  return postJson<ProductResponse>('/api/products', body)
}

export async function getCampaignMaps(userId: number): Promise<CampaignMapItem[]> {
  const res = await authFetch(`/api/campaign-product-maps/by-user/${userId}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<CampaignMapItem[]>
}

export async function createCampaignMap(body: {
  userId: number
  campaignId: string
  productId: number
}): Promise<CampaignMapItem> {
  return postJson<CampaignMapItem>('/api/campaign-product-maps', body)
}

export async function getWatchlist(): Promise<WatchlistItem[]> {
  const res = await authFetch('/api/watchlist', { headers: { Accept: 'application/json' } })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<WatchlistItem[]>
}

export async function toggleWatchlist(level: string, entityId: string): Promise<WatchlistToggleResult> {
  return postJson<WatchlistToggleResult>('/api/watchlist/toggle', { level, entityId })
}

export async function downloadAnalysisPdf(): Promise<void> {
  const res = await authFetch('/api/reports/analysis.pdf', {
    headers: { Accept: 'application/pdf' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `meta-analiz-${localStorage.getItem(USER_ID_KEY) ?? 'rapor'}.pdf`
  a.click()
  URL.revokeObjectURL(url)
}

export async function postVideoReportAggregate(body: {
  userId: number
  adIds: string[]
  metaAdAccountId?: string
}): Promise<VideoReportAggregateResponse> {
  return postJson<VideoReportAggregateResponse>('/api/video-report/aggregate', body)
}

export async function getVideoAssets(userId: number, metaAdAccountId?: string): Promise<VideoAssetRow[]> {
  const q = metaAdAccountId ? `?metaAdAccountId=${encodeURIComponent(metaAdAccountId)}` : ''
  const res = await authFetch(`/api/video-assets/by-user/${userId}${q}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<VideoAssetRow[]>
}

export async function downloadVideoReportPdf(body: {
  userId: number
  adIds: string[]
  metaAdAccountId?: string
  videoId?: string
  displayName?: string
}): Promise<void> {
  const res = await authFetch('/api/reports/video.pdf', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/pdf' },
    body: JSON.stringify(body),
  })
  if (!res.ok) throw new Error(await parseError(res))
  const blob = await res.blob()
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  const vid = body.videoId?.trim() || 'video'
  a.download = `video-rapor-${vid}.pdf`
  a.click()
  URL.revokeObjectURL(url)
}

export async function createSavedReport(body: {
  userId: number
  adId: string
  adName?: string | null
  thumbnailUrl?: string | null
  campaignId?: string | null
  campaignName?: string | null
  adsetId?: string | null
  adsetName?: string | null
  aggregateRoas?: number | null
  aggregateHookRate?: number | null
  aggregateHoldRate?: number | null
  aggregateSpend?: number | null
  aggregatePurchases?: number | null
  suggestions: Array<{
    suggestionKey: string
    directiveType?: string | null
    severity: string
    message: string
    symptom?: string | null
    reason?: string | null
    action?: string | null
  }>
}): Promise<SavedReportItem> {
  return postJson<SavedReportItem>('/api/saved-reports', body)
}

export async function listSavedReports(userId: number): Promise<SavedReportItem[]> {
  const res = await authFetch(`/api/saved-reports/by-user/${userId}`, { headers: { Accept: 'application/json' } })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<SavedReportItem[]>
}

export async function updateSavedSuggestionStatus(
  suggestionId: number,
  status: 'applied' | 'skipped',
): Promise<SavedReportSuggestion> {
  return postJson<SavedReportSuggestion>(`/api/saved-reports/suggestions/${suggestionId}/status`, { status })
}

export async function listSavedReportImpacts(
  userId: number,
  take = 10,
): Promise<SavedReportImpactFeedItem[]> {
  const res = await authFetch(`/api/saved-reports/impacts/by-user/${userId}?take=${take}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<SavedReportImpactFeedItem[]>
}

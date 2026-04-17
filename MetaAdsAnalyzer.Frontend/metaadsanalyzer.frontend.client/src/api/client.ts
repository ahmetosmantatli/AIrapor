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
} from './types'

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
  const res = await fetch(apiUrl(path), { ...init, headers })
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
  if (!res.ok) throw new Error(await parseError(res))
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

export async function getActiveDirectives(userId: number): Promise<DirectiveItem[]> {
  const res = await authFetch(`/api/directives/active/by-user/${userId}`, {
    headers: { Accept: 'application/json' },
  })
  if (!res.ok) throw new Error(await parseError(res))
  return res.json() as Promise<DirectiveItem[]>
}

export async function postMetricsRecompute(userId: number): Promise<MetricsRecomputeResult> {
  return postJson<MetricsRecomputeResult>('/api/metrics/recompute', { userId })
}

export async function postDirectivesEvaluate(userId: number): Promise<DirectiveEvaluateResult> {
  return postJson<DirectiveEvaluateResult>('/api/directives/evaluate', { userId })
}

export async function postInsightsSync(
  userId: number,
  level: string,
  datePreset: string,
): Promise<InsightsSyncResult> {
  return postJson<InsightsSyncResult>('/api/meta/insights/sync', { userId, level, datePreset })
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

export async function getRawInsights(userId: number, level?: string): Promise<RawInsightRow[]> {
  const q = level ? `?level=${encodeURIComponent(level)}` : ''
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

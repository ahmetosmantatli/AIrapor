const raw = (import.meta.env.VITE_API_BASE_URL ?? '').trim()

/**
 * Üretimde API kökü (sonda / yok), örn. https://api.sizin-domain.com veya http://localhost:5195.
 * Sonda `/api` yazılmamalı — istemci yolları zaten `/api/...` ile başlar.
 * Geliştirmede boş bırakın (Vite `/api` → 5195 proxy).
 */
export const API_BASE_URL = raw.replace(/\/+$/, '')

/** Örn. .../api/api/meta → .../api/meta (Kestrel fallback 404'ü önler). */
function collapseDuplicateApiSegments(url: string): string {
  let u = url
  while (u.includes('/api/api/')) {
    u = u.replace('/api/api/', '/api/')
  }
  return u
}

/** /api/... veya tam URL */
export function apiUrl(path: string): string {
  if (path.startsWith('http://') || path.startsWith('https://')) {
    return collapseDuplicateApiSegments(path)
  }

  const p = path.startsWith('/') ? path : `/${path}`
  const base = API_BASE_URL.replace(/\/+$/, '')
  if (!base) {
    return collapseDuplicateApiSegments(p)
  }

  // Yaygın hata: VITE_API_BASE_URL=http://host:5195/api + path=/api/meta/... → çift /api → 404
  const baseEndsWithApi = /\/api$/i.test(base)
  if (baseEndsWithApi && p.startsWith('/api/')) {
    return collapseDuplicateApiSegments(`${base}${p.slice(4)}`)
  }

  return collapseDuplicateApiSegments(`${base}${p}`)
}

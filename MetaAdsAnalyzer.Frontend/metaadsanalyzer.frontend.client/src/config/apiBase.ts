const raw = (import.meta.env.VITE_API_BASE_URL ?? '').trim()

/** Üretimde API ayrı subdomain’deyse buraya tam kök (sonda / yok). Geliştirmede boş bırakın (Vite proxy). */
export const API_BASE_URL = raw.replace(/\/$/, '')

/** /api/... veya tam URL */
export function apiUrl(path: string): string {
  if (path.startsWith('http://') || path.startsWith('https://')) {
    return path
  }

  const p = path.startsWith('/') ? path : `/${path}`
  return `${API_BASE_URL}${p}`
}

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type ReactNode,
} from 'react'

export const TOKEN_KEY = 'metaads_access_token'
export const USER_ID_KEY = 'metaads_user_id'
export const USER_EMAIL_KEY = 'metaads_user_email'

function readUserIdFromJwt(token: string): number | null {
  try {
    const part = token.split('.')[1]
    if (!part) return null
    const json = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/'))) as {
      uid?: string
      sub?: string
    }
    const raw = json.uid ?? json.sub
    const n = parseInt(String(raw), 10)
    return Number.isFinite(n) && n > 0 ? n : null
  } catch {
    return null
  }
}

function readEmailFromJwt(token: string): string | null {
  try {
    const part = token.split('.')[1]
    if (!part) return null
    const json = JSON.parse(atob(part.replace(/-/g, '+').replace(/_/g, '/'))) as { email?: string }
    return json.email ?? null
  } catch {
    return null
  }
}

export function clearStoredSession(): void {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(USER_ID_KEY)
  localStorage.removeItem(USER_EMAIL_KEY)
}

type Session = {
  token: string | null
  userId: number | null
  email: string | null
}

type UserCtx = {
  token: string | null
  userId: number | null
  email: string | null
  isAuthenticated: boolean
  setSession: (accessToken: string, userId: number, email: string) => void
  logout: () => void
}

const UserContext = createContext<UserCtx | null>(null)

function readInitialSession(): Session {
  try {
    if (typeof window !== 'undefined') {
      const q = new URLSearchParams(window.location.search)
      if (q.get('meta_oauth') === 'success' && q.get('access_token')) {
        const tok = q.get('access_token')!
        const uid = readUserIdFromJwt(tok)
        const em = readEmailFromJwt(tok) ?? ''
        if (uid) {
          localStorage.setItem(TOKEN_KEY, tok)
          localStorage.setItem(USER_ID_KEY, String(uid))
          localStorage.setItem(USER_EMAIL_KEY, em)
          const path = window.location.pathname + window.location.hash
          window.history.replaceState({}, '', path || '/')
          return { token: tok, userId: uid, email: em || null }
        }
      }
    }

    const token = localStorage.getItem(TOKEN_KEY)
    const uidRaw = localStorage.getItem(USER_ID_KEY)
    const email = localStorage.getItem(USER_EMAIL_KEY)
    const uid = uidRaw ? parseInt(uidRaw, 10) : NaN
    if (token && Number.isFinite(uid) && uid > 0) {
      return { token, userId: uid, email }
    }
  } catch {
    /* ignore */
  }
  return { token: null, userId: null, email: null }
}

export function UserProvider({ children }: { children: ReactNode }) {
  const [session, setSessionState] = useState<Session>(() => readInitialSession())

  const setSession = useCallback((accessToken: string, userId: number, email: string) => {
    localStorage.setItem(TOKEN_KEY, accessToken)
    localStorage.setItem(USER_ID_KEY, String(userId))
    localStorage.setItem(USER_EMAIL_KEY, email)
    setSessionState({ token: accessToken, userId, email })
  }, [])

  const logout = useCallback(() => {
    clearStoredSession()
    setSessionState({ token: null, userId: null, email: null })
  }, [])

  const value = useMemo(
    () =>
      ({
        token: session.token,
        userId: session.userId,
        email: session.email,
        isAuthenticated: Boolean(session.token && session.userId),
        setSession,
        logout,
      }) satisfies UserCtx,
    [session.token, session.userId, session.email, setSession, logout],
  )

  return <UserContext.Provider value={value}>{children}</UserContext.Provider>
}

/** Korumalı sayfalar: userId her zaman tanımlıdır. */
export function useUser(): { userId: number; email: string | null; logout: () => void } {
  const ctx = useContext(UserContext)
  if (!ctx) throw new Error('useUser içinde UserProvider gerekli')
  if (!ctx.userId) throw new Error('Oturum yok')
  return { userId: ctx.userId, email: ctx.email, logout: ctx.logout }
}

export function useAuth(): UserCtx {
  const ctx = useContext(UserContext)
  if (!ctx) throw new Error('useAuth içinde UserProvider gerekli')
  return ctx
}

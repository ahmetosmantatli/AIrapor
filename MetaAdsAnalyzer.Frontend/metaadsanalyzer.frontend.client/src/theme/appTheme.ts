import { useEffect, useState } from 'react'

export type AppTheme = 'dark' | 'light'

const THEME_KEY = 'adlyz_theme'

function readStoredTheme(): AppTheme {
  if (typeof window === 'undefined') return 'dark'
  const raw = window.localStorage.getItem(THEME_KEY)
  return raw === 'light' || raw === 'pink' ? 'light' : 'dark'
}

function applyThemeClass(theme: AppTheme): void {
  if (typeof document === 'undefined') return
  document.body.classList.toggle('theme-light', theme === 'light')
}

export function useAppTheme() {
  const [theme, setTheme] = useState<AppTheme>(() => readStoredTheme())

  useEffect(() => {
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(THEME_KEY, theme)
    }
    applyThemeClass(theme)
  }, [theme])

  return {
    theme,
    setTheme,
    toggleTheme: () => setTheme((prev) => (prev === 'dark' ? 'light' : 'dark')),
  }
}

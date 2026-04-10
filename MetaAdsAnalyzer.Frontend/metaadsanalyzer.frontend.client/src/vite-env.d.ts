/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Boş = aynı kök (tek site) veya Vite dev proxy. Örnek: https://api.sizin-domain.com */
  readonly VITE_API_BASE_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

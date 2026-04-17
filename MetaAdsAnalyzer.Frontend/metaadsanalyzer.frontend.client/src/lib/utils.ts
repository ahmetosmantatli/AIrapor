/** Sınıf birleştirme — harici paket yok (Vite, eksik node_modules durumunda da çalışır). */
export function cn(...inputs: (string | undefined | null | false)[]): string {
  return inputs.filter((x): x is string => typeof x === 'string' && x.length > 0).join(' ')
}

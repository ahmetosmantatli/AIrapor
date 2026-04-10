import { cpSync, existsSync, mkdirSync, readdirSync, rmSync, statSync } from 'node:fs'
import { join, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = dirname(fileURLToPath(import.meta.url))
const clientRoot = join(__dirname, '..')
const dist = join(clientRoot, 'dist')
const apiWwwroot = join(clientRoot, '..', '..', 'MetaAdsAnalyzer.API', 'wwwroot')

if (!existsSync(dist) || !statSync(dist).isDirectory() || readdirSync(dist).length === 0) {
  console.error('Önce "npm run build" çalıştırın; dist klasörü boş veya yok.')
  process.exit(1)
}

rmSync(apiWwwroot, { recursive: true, force: true })
mkdirSync(apiWwwroot, { recursive: true })
cpSync(dist, apiWwwroot, { recursive: true })
console.log('SPA kopyalandı:', apiWwwroot)
console.log('API\'yi çalıştırın: cd MetaAdsAnalyzer.API && dotnet run — tek kökten UI + /api')

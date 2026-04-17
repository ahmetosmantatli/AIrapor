# AIrapor (MetaAdsAnalyzer)

ASP.NET Core 8 Web API + React (Vite) + PostgreSQL — Meta reklam insights analizi.

## Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- PostgreSQL 14+ (yerel: Docker `postgres:16`, bulut: Railway / Neon / Azure Database for PostgreSQL)

## Hızlı başlangıç (Windows / Mac)

1. `ConnectionStrings:DefaultConnection` değerini Npgsql biçiminde ayarlayın (örnek: `appsettings.Development.EXAMPLE.json`). Gizli parola için User Secrets:  
   `cd MetaAdsAnalyzer.API` → `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Database=...;Username=...;Password=..."`
2. API: `cd MetaAdsAnalyzer.API` → `dotnet ef database update --project ../MetaAdsAnalyzer.Infrastructure/MetaAdsAnalyzer.Infrastructure.csproj`
3. API çalıştır: `dotnet run --launch-profile http` (varsayılan http://localhost:5195)
4. Frontend: `cd MetaAdsAnalyzer.Frontend/metaadsanalyzer.frontend.client` → `npm ci` → `npm run dev` → http://localhost:5173  
   - **Node.js 20.19+** gerekir (Vite 8). UI: Tailwind + [shadcn/ui](https://ui.shadcn.com/) tabanı (`components.json`, `src/components/ui/`). Yeni bileşen: `npx shadcn@latest add card` (klasörde çalıştırın).

Tek süreç (build’li SPA): `npm run publish:api` sonra yalnızca API’yi çalıştırın; arayüz http://localhost:5195

## Mac / Railway notu

- Veritabanı **PostgreSQL**’dir; Railway’de Postgres eklentisinin verdiği `DATABASE_URL` veya `PGHOST`, `PGUSER`, `PGPASSWORD`, `PGDATABASE` değerlerini tek connection string’e çevirin (Npgsql: `Host=…;Port=…;Database=…;Username=…;Password=…;SSL Mode=Require` gibi).
- Ön uç aynı; `npm run dev` + API’ye proxy yeterli.

## GitHub

Uzak depo: `https://github.com/ahmetosmantatli/AIrapor.git`

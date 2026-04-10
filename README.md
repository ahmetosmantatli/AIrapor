# AIrapor (MetaAdsAnalyzer)

ASP.NET Core 8 Web API + React (Vite) + SQL Server — Meta reklam insights analizi.

## Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- SQL Server (Windows: LocalDB / Express; **Mac:** Docker `mcr.microsoft.com/mssql/server` veya Azure SQL)

## Hızlı başlangıç (Windows / Mac)

1. `ConnectionStrings:DefaultConnection` değerini kendi SQL örneğinize göre `MetaAdsAnalyzer.API/appsettings.json` veya User Secrets ile ayarlayın.
2. API: `cd MetaAdsAnalyzer.API` → `dotnet ef database update --project ../MetaAdsAnalyzer.Infrastructure/MetaAdsAnalyzer.Infrastructure.csproj`
3. API çalıştır: `dotnet run --launch-profile http` (varsayılan http://localhost:5195)
4. Frontend: `cd MetaAdsAnalyzer.Frontend/metaadsanalyzer.frontend.client` → `npm ci` → `npm run dev` → http://localhost:5173

Tek süreç (build’li SPA): `npm run publish:api` sonra yalnızca API’yi çalıştırın; arayüz http://localhost:5195

## Mac notu

- **EF Core + SQL Server**: Mac’te yerel SQL Server yok; Docker ile SQL Server 2022 image veya bulut veritabanı kullanın. Connection string’i buna göre güncelleyin.
- Ön uç aynı; `npm run dev` + API’ye proxy yeterli.

## GitHub

Uzak depo: `https://github.com/ahmetosmantatli/AIrapor.git`

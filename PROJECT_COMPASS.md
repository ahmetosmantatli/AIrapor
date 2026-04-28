# Adlyz Proje Pusulasi (Tek Kaynak Dokuman)

Bu dokumanin amaci, projeyi devralan herhangi bir ekip uyesi veya AI asistaninin
hedeften sapmadan dogru onceliklerle devam etmesini saglamaktir.

## 1) Urun Ne Ise Yarar?

Adlyz, Meta reklam hesaplarindaki kampanya/adset/reklam verilerini toplayip:

- performans analizi yapar,
- AI destekli aksiyon onerileri uretir,
- oneriler "uygulandi" oldugunda onceki metrikleri snapshot olarak saklar,
- zaman icindeki etkileri "before vs current" karsilastirmasi ile gosterir.

Kisaca urun, reklam yoneticisinin "neyi degistirmeliyim?" ve
"degisiklik gercekten ise yaradi mi?" sorularini netlestirmek icin vardir.

## 2) Cekirdek Urun Prensipleri (Degismemesi Gerekenler)

1. **Guvenilir veri**: UI her zaman en guncel ve dogru oldugu bilinen veriyi gostermeli.
2. **Takip edilebilirlik**: Her onemli karar/metrik veri kaynagi ve zaman damgasi ile izlenebilmeli.
3. **Snapshot mantigi**: "Uygulandi" anindaki metrikler immutable (degismez) kalmali.
4. **Tekrarsiz analiz kartlari**: Ayni adId icin analiz listesinde son kayit gosterilmeli.
5. **Maliyet kontrollu API kullanimi**: Gereksiz Meta API cagrisindan kacinilmali.
6. **Anlasilir UX**: Kullanici, hangi verinin eski/yeni oldugunu aninda anlamali.

## 3) Mevcut Teknik Mimari (Ozet)

- **Backend**: ASP.NET Core + EF Core + PostgreSQL
- **Frontend**: React + TypeScript + Vite
- **Veri kaynagi**: Meta Graph API (Insights)
- **Temel moduller**:
  - `MetaInsightsSyncService` (senkronizasyon + cache + log)
  - `MetaInsightsSchedulingService` (gunluk otomatik sync)
  - `VideoReportInsightService` (agregasyon/video metrikleri)
  - `SavedReportsController` (rapor/suggestion/impact akisleri)

## 4) Veri Cekme ve Veri Sirkulasyonu (Source of Truth)

### 4.1 Veri Akisi

1. Scheduler veya manuel refresh endpoint'i Meta'dan insight ceker.
2. Ham veriler `raw_insights` tablosuna yazilir.
3. Turetilmis/yorumlanan metrikler servis seviyesinde hesaplanir.
4. Analiz kayitlari `saved_reports` ve `saved_report_suggestions` ile saklanir.
5. Kullanici "uygulandi" dediginde before snapshot suggestion uzerine yazilir.
6. Sonraki gunlerde otomatik sync ile guncel metrikler "after/current" alanlarina akar.
7. Impact ekrani before/current farki ve degisimin algilanip algilanmadigini gosterir.

### 4.2 Tazelik Kurallari

- Genel insight cache: yaklasik 4 saat.
- Manuel refresh cooldown: yaklasik 1 saat.
- Gunluk manuel sync limiti: kullanici basina limitli.
- Gunluk otomatik sync: sistem tarafindan tetiklenir.

### 4.3 Kritik Veri Durumlari

- Meta bazen detayli video metriklerini donmeyebilir.
- `VideoPlay3s > 0` ama `p25/p50/p75/p100 = 0` oldugunda eksik veri kabul edilir.
- Bu durumda hedefli force-sync ile veri yeniden cekme denemesi yapilir.

## 5) 1K+ Kullaniciya Acmadan Once Gerekenler (Hard Checklist)

## A) Performans ve Olceklenebilirlik

- [ ] `raw_insights` ve impact sorgulari icin indexlerin dogrulanmasi (userId, level, entityId, fetchedAt)
- [ ] En sik endpointlerde p95 latency hedefi belirleme (or: < 500ms read endpointleri)
- [ ] N+1 sorgu ve gereksiz include kullanimlarinin profillemesi
- [ ] Uzun suren sync islerini kuyruk/background job mantigina ayirma (gerekirse)
- [ ] API timeout/retry/backoff stratejilerinin standardize edilmesi

## B) Veri Tutarliligi

- [ ] "Applied snapshot immutable" garanti testleri
- [ ] Ayni suggestion icin cift "applied" race condition korumasi
- [ ] En son analiz kaydi seciminde deterministic kural (AnalyzedAt + Id)
- [ ] DB migrationlarinin local/staging/prod ortamlarda ayni olmasi

## C) Guvenlik

- [ ] JWT dogrulama ve "kendi userId disina erisememe" testleri
- [ ] Secret/token'larin loglara sizmamasini garanti eden log filter
- [ ] Rate limiting (ozellikle refresh/sync endpointleri)
- [ ] CORS, HTTPS, secure headers ve prod config denetimi

## D) Gozlemlenebilirlik (Observability)

- [ ] Her sync istegi icin request-id / correlation-id
- [ ] "META_SYNC_SUMMARY" benzeri standart kapanis loglari
- [ ] Hata siniflandirma: transient vs permanent
- [ ] Dashboard metrikleri: sync success rate, stale data ratio, API error ratio
- [ ] Alarm kurallari: fail oranlari ve veri gecikmesi threshold'lari

## E) Operasyonel Dayaniklilik

- [ ] "DB'de eksik kolon/tablo" durumlari icin startup health check
- [ ] Idempotent manual SQL fallback runbook
- [ ] Rollback plani (deploy bazli)
- [ ] Feature flag ile riskli ozellikleri kapatabilme

## 6) Kalan Ana Hedefler (Product + Tech)

1. **Impact Tracking UX finalizasyonu**
   - kart > ilk popup > detaylar > ikinci popup/ayri detay akisi netlestirilecek.
2. **Before/Current karsilastirma guvenilirligi**
   - hangi metrik hangi kaynaktan geldigini UI'da daha acik gostermek.
3. **Video timeline dayanikliligi**
   - Meta'nin eksik dondugu senaryolarda fallback mesajlarini korumak.
4. **Refresh/sync maliyet optimizasyonu**
   - gereksiz force-sync azaltilacak, stale algilama daha akilli hale getirilecek.
5. **Production hardening (1K+ kullanici)**
   - test, izleme, limitler, alarm ve runbooklar tamamlanacak.

## 7) Kabul Kriterleri (Definition of Done)

Bir ozellik "tamamlandi" sayilmasi icin:

- kod + migration + test + log + dokuman birlikte gelmeli,
- frontend fallback durumlari tanimli olmali,
- endpoint hata durumlarinda kullaniciya anlamli mesaj donmeli,
- prod deploy sonrasi en az bir smoke test adimi gecilmeli.

## 8) AI/Dev Handover Talimati (Bu Dosyayi Kullananlar Icin)

Bu projede calisan AI veya gelistirici:

1. Once bu dokumani oku, sonra kod degisikligine gec.
2. Asla urun prensiplerine ters "hizli ama kirilgan" cozum onermemeli.
3. Veri guvenilirligini UX'ten oncelikli kabul et.
4. "Applied snapshot" mantigini bozacak hicbir degisiklik yapma.
5. Kullanicinin istemedigi UI/akisi varsayma; once mevcut akisi koru.
6. Her kritik degisiklikte:
   - hangi sorunu cozdun,
   - hangi risk kaldi,
   - nasil test ettin,
   net yaz.

## 9) Kisa Yol Haritasi (Fazli)

- **Faz A - Stabilizasyon**: build/deploy hatalarini sifirla, kritik bug fixler.
- **Faz B - Impact UX**: modal akisini tasarima gore finalize et.
- **Faz C - Olceklenme**: query optimizasyonu + limit/rate control + gozlemleme.
- **Faz D - Guvenilir Otomasyon**: sync scheduling ve self-healing runbook.
- **Faz E - 1K Acilis**: load test, SLO takibi, kontrollu rollout.

## 10) Basari Metrikleri

- Sync basari orani >= %98
- Kritik endpoint p95 < 500ms (read)
- Veri stale olma orani (4 saat ustu) < %5
- "Applied but no change detected" false-positive orani dusecek
- Support talebi / aktif hesap orani release sonrasi azalacak

---

Son guncelleme amaci:
Bu dokuman, proje amacina sadakati koruyan "tek kaynak karar rehberi" olarak kullanilsin.

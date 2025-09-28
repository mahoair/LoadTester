# LoadTester

NBomber tabanlı basit bir yük testi aracı.

## Özellikler
- Senaryolar modüler: HTTP adımları ve WebSocket (SignalR) hub bağlanma.
- Profil bazlı varsayılanlar: `baseline`, `stress`, `soak`.
- Ortam değişkenleri ile kolay konfigürasyon ve senaryo aç/kapat: `RUN_HTTP`, `RUN_HUB`.
- Tekil, optimize `HttpClient` kullanımı (SocketsHttpHandler, gzip/deflate).

## Ortam Değişkenleri
- Genel
  - `LOAD_PROFILE` = `baseline` | `stress` | `soak` (default: `baseline`)
  - `RUN_HTTP` = `1`/`0` (default: `1`)
  - `RUN_HUB` = `1`/`0` (default: `1`)
- Uç noktalar
  - `BASE_HTTP` (default: `https://tbfapi.nikayazilim.com/webapi-service/api`)
  - `HUB_WSS` (default: `wss://tbfapi.nikayazilim.com/webapi-service/hub`)
- İçerik/Filtre
  - `MATCH_DATE` (default: UTC gün başı `yyyy-MM-ddT00:00:00.000Z`)
- Yük ve zaman aşımı (profil ile override edilir, env ile aşılabilir)
  - `HTTP_COPIES`, `HTTP_RAMP_S`, `HTTP_HOLD_S`
  - `HUB_COPIES`, `HUB_RAMP_S`, `HUB_HOLD_S`
  - `HTTP_TIMEOUT_S`

Profil varsayılanları:
- baseline: 50 copies, ramp 10 sn, hold 30 sn, timeout 15 sn
- stress: 500 copies, ramp 60 sn, hold 120 sn, timeout 20 sn
- soak: 200 copies, ramp 60 sn, hold 900 sn, timeout 20 sn

## Lokal Çalıştırma (Windows cmd)

Hızlı smoke test (düşük yük):

```cmd
set LOAD_PROFILE=baseline
set RUN_HTTP=1
set RUN_HUB=1
set HTTP_COPIES=1
set HUB_COPIES=1
set HTTP_RAMP_S=1
set HTTP_HOLD_S=5
set HUB_RAMP_S=1
set HUB_HOLD_S=5
set HTTP_TIMEOUT_S=5

dotnet run --project .\LoadTester\LoadTester\LoadTester.csproj -c Debug
```

Sadece HTTP senaryosu:

```cmd
set RUN_HTTP=1
set RUN_HUB=0

dotnet run --project .\LoadTester\LoadTester\LoadTester.csproj -c Release
```

## Docker / Compose

İmaj oluştur ve çalıştır:

```cmd
:: Build image
docker compose build

:: Run with defaults from compose.yaml
docker compose up --abort-on-container-exit
```

`compose.yaml` içinde env değişkenlerini düzenleyerek profili ve yük seviyelerini değiştirebilirsiniz.

## Raporlar

NBomber raporları çalıştırma sonunda şu klasöre kaydedilir:
`LoadTester\reports\<session_id>` ve ayrıca `bin\<cfg>\net9.0\reports\<session_id>`.

## Notlar
- NBomber topluluk sürümü kişisel kullanım içindir (çalıştırmada uyarı görebilirsiniz).
- Ağ ve hedef servis kaynaklarına göre `HTTP_TIMEOUT_S` ve kopya sayıları ayarlanmalıdır.



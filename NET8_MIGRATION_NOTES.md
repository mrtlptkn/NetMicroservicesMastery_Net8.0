# .NET 10 → .NET 8 (LTS) Geçiş Notları

Bu solution, .NET 10 yerine **.NET 8 SDK** ile derlenip çalışacak şekilde güncellenmiştir.
Kaynak kodda (`.cs`) değişiklik gerekmedi: kullanılan tüm dil özellikleri (primary constructor,
collection expression, raw string literal) C# 12 ile, kullanılan tüm API'ler (`IExceptionHandler`,
`MediaTypeNames.Application.ProblemJson`, `PeriodicTimer` vb.) .NET 8 ile uyumludur.

## 1. Hedef Framework

20 projenin tamamında: `<TargetFramework>net10.0</TargetFramework>` → `net8.0`

## 2. NuGet Paket Sürümleri

| Paket | Eski | Yeni | Neden |
|---|---|---|---|
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.0 | 8.0.2 | net8 hattı |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.0 | 8.0.0 | net8 hattı |
| Microsoft.EntityFrameworkCore.Design | 10.0.0 | 8.0.11 | EF Core 10 yalnızca net10 destekler |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.0 | 8.0.11 | EF Core 8 ile eşleşen sağlayıcı |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | 8.0.11 | ASP.NET Core 8 ile eşleşmeli |
| Microsoft.Extensions.Http.Resilience | 9.0.0 | 8.10.0 | Microsoft.Extensions 9.x bağımlılıklarını çekmemesi için |
| AspNetCore.HealthChecks.NpgSql | 9.0.0 | 8.0.2 | net8 hattı |
| AspNetCore.HealthChecks.Redis | 9.0.0 | 8.0.1 | net8 hattı |
| OpenTelemetry.* (Hosting, AspNetCore, Http, Runtime, OTLP) | 1.10.x | 1.9.0 | 1.10+ tüm hedeflerde Microsoft.Extensions.* **9.0.0** ister; bu, net8 projelerindeki 8.x doğrudan referanslarla NU1605 (paket düşürme) hatasına yol açar |
| OpenTelemetry.Exporter.Prometheus.AspNetCore | 1.10.0-beta.1 | 1.9.0-beta.2 | OpenTelemetry 1.9.0 ile eşleşen sürüm |

MassTransit 8.3.4, MediatR, FluentValidation, Serilog 8.x, Polly 8.5, YARP 2.2, Hangfire,
Consul, VaultSharp, RedLock.net, StackExchange.Redis ve Swashbuckle 7.2 zaten net8'i
desteklediği için **değiştirilmedi**. (MassTransit, net8 hedefinde EF Core 8 / Microsoft.Extensions 8 kullanır.)

## 3. Docker

Tüm `Dockerfile`'larda:
- `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/sdk:8.0`
- `mcr.microsoft.com/dotnet/aspnet:10.0` → `mcr.microsoft.com/dotnet/aspnet:8.0`

.NET 8 imajları da varsayılan olarak 8080 portunu dinlediği için `EXPOSE 8080` ve
compose port eşleşmeleri aynen geçerlidir.

Yeni `.dockerignore`: `COPY . .` adımında host'taki `bin/`/`obj/` klasörlerinin imaja
kopyalanıp container içindeki restore çıktısını ezmesini engeller.

## 4. Yeni `global.json`

Makinede birden fazla SDK (ör. 8 ve 10) kurulu olsa bile bu solution'ın **8.0.x SDK** ile
derlenmesini sağlar (`rollForward: latestFeature` → herhangi bir 8.0.xxx SDK kabul edilir).

## 5. Temizlik

Eski `net10.0` derleme çıktıları (`bin/`, `obj/`) ve `.vs/` önbelleği silindi. İlk açılışta
`dotnet restore` çalıştırın.

## 6. Dokunulmayanlar

- `src/Gateway/ApiGateway.rar`: solution'a dahil olmayan eski bir yedek arşivi; içeriği
  değiştirilmedi (içindeki proje hâlâ net10.0 hedefler).

## Doğrulama

```bash
dotnet --list-sdks          # 8.0.xxx görünmeli
dotnet restore
dotnet build NetMicroservicesMastery.sln -c Debug
```

Gereksinim: .NET 8 SDK (8.0.100+) — Visual Studio 2022 17.8+ veya Rider 2023.3+.

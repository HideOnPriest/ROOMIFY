# Roomify

Roomify to planowany system rezerwacji sal konferencyjnych i spotkań. Powstaje na zaliczenie przedmiotu „Programowanie i testowanie aplikacji webowych”. Celem jest pokazanie poprawnego projektowania, implementowania i testowania aplikacji z frontendem, backendem i relacyjną bazą danych.

## Status

WP-00: dostępna specyfikacja bazowa. WP-01.1–WP-01.5: przygotowano repozytorium, szkielety .NET 10 i Angular 22, Compose oraz infrastrukturę testową. WP-01.6 zamknięto po ponownej kontroli Docker Desktop 17.10.2026 ze statusem **READY WITH WARNINGS**: trzy usługi działają, PostgreSQL i API są healthy, proxy Nginx działa, Testcontainers przeszedł 1/1 i HTTP 3/3. W pierwotnym audycie przeszły też Vitest 5/5 i Chromium 2/2. Projekty jednostkowe backendu mają zero przypadków, mutation score jest N/A, a manualne NOT RUN. Funkcje biznesowe, EF Core, Identity, migracje i CI nadal nie istnieją. [Stan implementacji](docs/ai/IMPLEMENTATION_STATUS.md) i [przekazanie do WP-02](docs/ai/WP02_HANDOFF.md) opisują gotowość i ograniczenia.

Cztery dokumenty WP-00 w [docs/ai](docs/ai/) zachowano w kompletnej, niezmienionej wersji. Ich historyczne zapisy o proponowanym stosie i otwartej decyzji cookies/JWT należy czytać wraz z decyzjami WP-01.1 w [rejestrze ADR](docs/decisions/README.md). Zatwierdzenie stosu w WP-01.1 nie zatwierdza automatycznie pozostałych propozycji biznesowych oznaczonych `[P]`.

## Zatwierdzony stos technologiczny

| Obszar | Technologie planowane |
|---|---|
| Backend | .NET 10 LTS, ASP.NET Core Web API, C#, Entity Framework Core 10, ASP.NET Core Identity, uwierzytelnianie cookies |
| Frontend | Angular 22, TypeScript, Standalone Components, Reactive Forms, Angular Signals, RxJS, CSR |
| Dane | PostgreSQL, EF Core Migrations |
| Infrastruktura | Docker, Docker Compose, Git, GitHub Actions |
| Testy backendu | xUnit, Moq, WebApplicationFactory, Testcontainers, Stryker.NET |
| Testy frontendu i E2E | Vitest, Angular TestBed, Playwright |

Backend używa `net10.0`; Application i Infrastructure odwołują się do `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0. Frontend wykorzystuje Angular/CLI 22.2.3, TypeScript 6.0.3, RxJS 7.8.2 i Vitest 5.0.5. Przygotowano konfigurację PostgreSQL 18.6 i Docker Compose; integracja bazy przez EF Core oraz uwierzytelnianie pozostają planowane. Konkretne zależności npm zapisuje `frontend/package-lock.json`.

## Architektura

Modularny monolit oznacza jeden backend z wydzielonymi obszarami funkcjonalnymi. Lekki podział Clean Architecture oddziela reguły biznesowe od HTTP i bazy danych:

- `Roomify.Domain`: encje, reguły, typy wartości i statusy; bez zależności od innych projektów Roomify.
- `Roomify.Application`: przypadki użycia, serwisy, interfejsy i DTO; zależy od Domain.
- `Roomify.Infrastructure`: EF Core, PostgreSQL, Identity, dostęp do danych i migracje; zależy od Application i Domain.
- `Roomify.Api`: endpointy, konfiguracja zależności, middleware, uwierzytelnianie, autoryzacja i błędy HTTP; zależy od Application i Infrastructure.

Angular będzie komunikować się przez HTTP API; backend będzie korzystać z PostgreSQL przez Infrastructure. Szczegóły i diagram: [opis architektury](docs/architecture/OVERVIEW.md).

## Struktura repozytorium

```text
ROOMIFY/
├── backend/
│   ├── Dockerfile
│   ├── .dockerignore
│   ├── healthcheck.sh
│   ├── Roomify.slnx
│   ├── src/
│   │   ├── Roomify.Api/
│   │   ├── Roomify.Application/
│   │   ├── Roomify.Domain/
│   │   └── Roomify.Infrastructure/
│   └── tests/
│       ├── Roomify.Domain.UnitTests/
│       ├── Roomify.Application.UnitTests/
│       └── Roomify.Api.IntegrationTests/
├── frontend/
│   ├── Dockerfile
│   ├── .dockerignore
│   ├── src/app/
│   │   ├── core/
│   │   ├── shared/
│   │   ├── features/
│   │   └── layout/
│   ├── e2e/
│   ├── angular.json
│   ├── package.json
│   └── package-lock.json
├── infra/
│   ├── nginx/default.conf
│   └── postgres/
├── docs/
│   ├── ai/
│   ├── architecture/
│   ├── testing/
│   └── decisions/
├── .github/
│   └── workflows/
├── .editorconfig
├── .gitignore
├── .gitattributes
├── .env.example
├── docker-compose.yml
└── README.md
```

Puste katalogi zawierają `.gitkeep`. Rozwiązanie zawiera cztery projekty produkcyjne i trzy testowe `.csproj`. Lokalne narzędzie Stryker jest przypięte w `.config/dotnet-tools.json`, a konfiguracja Playwright znajduje się w `frontend/playwright.config.ts`. Szczegóły opisują [backend](docs/architecture/BACKEND.md) i [testowanie](docs/testing/README.md).

## Planowane moduły

- Konta i dostęp: logowanie, wylogowanie, profil oraz konta tworzone i dezaktywowane przez administratora; bez publicznej rejestracji.
- Sale: lista, szczegóły, wyszukiwanie i filtrowanie oraz administracyjne dodawanie, edycja i archiwizacja.
- Rezerwacje: tworzenie własnych rezerwacji, przeglądanie, edycja i anulowanie, walidacja terminów oraz ochrona przed konfliktami współbieżnych zapisów.
- Dostępność: kalendarz sali w widoku dnia/tygodnia, filtrowanie po sali i dacie.
- Administracja: zarządzanie kontami, salami i rezerwacjami innych użytkowników. Statystyki pozostają opcją `COULD`, a nie obowiązkową częścią MVP.

Zakres, priorytety i identyfikatory opisują [wymagania WP-00](docs/ai/REQUIREMENTS.md); status reguł biznesowych określa [BUSINESS_RULES.md](docs/ai/BUSINESS_RULES.md).

## Planowane testowanie

Jednostki izolują zachowanie, integracja sprawdza współpracę elementów, mutacje oceniają asercje, E2E sprawdzają ścieżki w przeglądarce, a manualne oceniają m.in. użyteczność. [Infrastruktura testowa](docs/testing/README.md) zawiera polecenia, raportowanie i ograniczenia. Obecne E2E sprawdzają wyłącznie frontend; integracja API z PostgreSQL pozostaje do WP-02. Nie dodano fikcyjnych testów domenowych ani Moq bez rzeczywistej potrzeby.

Z katalogu głównego:

```powershell
dotnet restore backend/Roomify.slnx
dotnet test backend/tests/Roomify.Domain.UnitTests --no-restore
dotnet test backend/tests/Roomify.Application.UnitTests --no-restore
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=false"
# Osobno, wyłącznie przy działającym Dockerze:
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=true"
dotnet tool restore
dotnet tool list --local
dotnet stryker --help
```

Runner jednostkowy zgłasza na razie brak przypadków — nie jest to PASS domeny. Nie uruchamiaj całego `dotnet test backend/Roomify.slnx` jako szybkiej kontroli bez Dockera: obejmuje również test kontenerowy. Prawdziwe mutacje uruchamiaj po implementacji domeny, zgodnie z [MUTATION_TESTS.md](docs/testing/MUTATION_TESTS.md).

## Uruchamianie backendu

Wymagany jest **.NET SDK 10.0.x** z obsługą `net10.0` (dostępny w środowisku WP-01.2: 10.0.301). Sam runtime nie wystarcza do budowania. W katalogu głównym repozytorium wykonaj:

```powershell
dotnet --list-sdks
dotnet restore backend/Roomify.slnx
dotnet build backend/Roomify.slnx --no-restore
dotnet build backend/Roomify.slnx --configuration Release --no-restore
dotnet run --project backend/src/Roomify.Api --launch-profile http --no-build
```

API będzie nasłuchiwać pod `http://localhost:5080`, bez PostgreSQL i uwierzytelniania. W drugim terminalu PowerShell sprawdź:

```powershell
$response = Invoke-WebRequest http://localhost:5080/health
$response.StatusCode
$response.Content
```

Oczekiwany wynik: status `200` i tekst `Healthy`. Zatrzymaj API skrótem `Ctrl+C`. Plik `backend/src/Roomify.Api/Roomify.Api.http` zawiera też gotowe żądanie dla edytora obsługującego pliki HTTP.

Opcjonalny profil `https` udostępnia `https://localhost:7080` i przekierowuje lokalny HTTP na HTTPS. Wymaga poprawnego certyfikatu developerskiego:

```powershell
dotnet dev-certs https --check
dotnet run --project backend/src/Roomify.Api --launch-profile https --no-build
```

Zaufanie certyfikatowi zależy od lokalnego systemu; w razie potrzeby skonfiguruj je przez `dotnet dev-certs https --trust`. Projekt nie zmienia magazynu certyfikatów automatycznie.

Konfiguracja pochodzi z `appsettings.json`, następnie `appsettings.{Environment}.json`, zmiennych środowiskowych i argumentów CLI. Profile lokalne ustawiają `Development`. Poza tym środowiskiem standardowa obsługa wyjątków z Problem Details nie ujawnia szczegółów błędu. Wbudowane logowanie jest widoczne w konsoli; poziomy ustalają pliki `appsettings`.

[.env.example](.env.example) opisuje konfigurację Compose, lecz .NET uruchamiany bezpośrednio sam nie wczytuje plików `.env`. Port API wewnątrz kontenera to `8080`; lokalny profil CLI używa `5080`. Testy HTTP uruchamiają API w pamięci, bez tego profilu i bez bazy.

## Uruchamianie frontendu

Wymagane są Node.js zgodny z Angular CLI 22 (`^22.22.3 || ^24.15.0 || >=26.0.0`) oraz npm. Zweryfikowane środowisko: **Node 24.19.0 i npm 12.2.0**. W katalogu `frontend/` wykonaj:

```powershell
npm ci
npm start
```

Otwórz [stronę startową](http://localhost:4200/home). `/` przekierowuje na `/home`, a nieznany adres wyświetla stronę 404 z linkiem powrotu. Oba widoki działają bez backendu. Zatrzymaj serwer przez `Ctrl+C`.

```powershell
npm run build
npm test
npm run test:ci
npm run test:coverage
npx playwright install chromium
npm run e2e
npm run e2e:report
```

Build produkcyjny trafia do `frontend/dist/roomify/browser/`. `npm test` uruchamia tryb interaktywny, a `test:ci` wykonuje jednostki jednokrotnie. Vitest używa TestBed i jsdom bez sieci i PostgreSQL. Playwright sam uruchamia Angulara na osobnym porcie 4300, używając Chromium bez backendu. Nie trzeba wcześniej wykonywać `npm start`. HTML E2E jest w `frontend/playwright-report/`, artefakty błędów w `frontend/test-results/`, a coverage Vitest w `frontend/coverage/`. Wyniki xUnit można zapisywać przez `--logger trx --results-directory artifacts/test-results`. Raporty są ignorowane przez Git. Trzy scenariusze [manualne](docs/testing/MANUAL_TESTS.md) pozostają NOT RUN.

`provideHttpClient()` przygotowuje przyszłą komunikację z względnymi adresami `/api`. Proxy developerskie kieruje `/api` i `/api/**` do `http://localhost:5080`, zachowując prefiks. Backend nie ma jeszcze endpointów biznesowych pod `/api`; obecne strony nie wykonują do niego żądań. Proxy nie jest częścią produkcyjnego builda — docelowy hosting zapewni jeden origin. Nie dodano CORS, uwierzytelniania ani interceptorów. Przed wdrożeniem operacji zmieniających stan z cookies potrzebna będzie ochrona CSRF. Konfiguracja frontendu jest publiczna i nie może zawierać sekretów.

W tym środowisku npm nie był dostępny w PATH. Kontrole wykonano lokalną kopią w ignorowanym `artifacts/`. Do jej użycia w tej sesji, z katalogu głównego repozytorium:

```powershell
$roomifyRuntime = "$env:USERPROFILE\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin"
$env:PATH = "$roomifyRuntime;$env:PATH"
$roomifyNpm = "$PWD\artifacts\wp013\npm\package\bin\npm-cli.js"
& "$roomifyRuntime\node.exe" $roomifyNpm --prefix frontend run test:ci
```

W czystym checkout używaj standardowej instalacji Node i npm oraz poleceń powyżej. Podział `core/shared/features`, DI, lazy loading i planowane użycie Signals, RxJS oraz Reactive Forms opisuje [FRONTEND.md](docs/architecture/FRONTEND.md).

## Uruchamianie przez Docker Compose

Na Windows zainstaluj [Docker Desktop z Compose](https://docs.docker.com/desktop/setup/install/windows-install/), włącz wymagane WSL 2/wirtualizację i uruchom silnik kontenerów Linux. Na Linux zainstaluj [Docker Engine](https://docs.docker.com/engine/install/) oraz [wtyczkę Compose](https://docs.docker.com/compose/install/linux/). Samo polecenie Compose nie zastępuje działającego silnika Docker. Budowanie wymaga dostępu do rejestrów obrazów, NuGet i npm; lokalne SDK .NET oraz Node nie są wtedy konieczne.

Z katalogu głównego repozytorium:

```powershell
docker version
docker compose version
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
```

Ustaw własne hasło developerskie w lokalnym `.env`, zachowując istniejący plik. `.env` jest ignorowany przez Git i nie trafia do kontekstów budowania. W środowisku wykonania WP-01.4 przygotowano już lokalny plik z wygenerowanym hasłem; jego wartości nie wypisano.

```powershell
docker compose config --quiet
docker compose build
docker compose up -d
docker compose ps
```

Pełne `docker compose config` ujawnia rozwinięte wartości, w tym hasło — do codziennej walidacji używaj `--quiet`. Po uruchomieniu sprawdź:

- Frontend: [http://localhost:4200](http://localhost:4200).
- Bezpośrednia trasa Angulara: [http://localhost:4200/home](http://localhost:4200/home).
- Healthcheck API przez Nginx: [http://localhost:4200/health](http://localhost:4200/health), oczekiwane `200` i `Healthy`.
- `/api/nieistniejacy-endpoint`: oczekiwane 404 backendu, bez dokumentu HTML Angulara.

Nginx serwuje produkcyjny build Angulara i przekazuje `/api` z zachowaniem ścieżki. API nasłuchuje w sieci Compose na `api:8080`, a baza na `db:5432`. Porty hosta są przypisane tylko do `127.0.0.1`: frontend `4200`, diagnostyczne API `5080` i PostgreSQL `5432`. W `.env` odpowiadają im `FRONTEND_PORT`, `API_PORT` i `POSTGRES_HOST_PORT`. Te dwa ostatnie zastępują wcześniejsze `BACKEND_PORT` oraz `POSTGRES_PORT`; nie zmienia to lokalnych profili `dotnet run`.

```powershell
docker compose logs --tail=100
docker compose logs -f api
docker compose build
docker compose up -d
docker compose stop
docker compose down
```

Zwykłe `down` zachowuje nazwany wolumen `roomify_postgres_data`, montowany pod `/var/lib/postgresql` zgodnie z wymaganiami PostgreSQL 18. Nie usuwaj wolumenów ani nie stosuj `down -v` do zwykłego zatrzymywania. Zmiana hasła w `.env` nie zmienia hasła istniejącej bazy.

Gdy Docker nie odpowiada, sprawdź uruchomienie Desktop i wynik `docker version`. Przy zajętym porcie zmień port hosta w `.env` lub zatrzymaj własny proces developerski. Przy unhealthy/502 sprawdź `docker compose ps` i logi; nie kasuj danych. Baza nie jest jeszcze używana przez API — `Database__*` przygotowuje przyszłą konfigurację bez dodawania EF Core.

Szczegóły obrazów, healthchecków, sieci, bezpieczeństwa i trwałości danych opisuje [DOCKER.md](docs/architecture/DOCKER.md). To konfiguracja lokalna, bez TLS i gotowości do publikacji internetowej. Podczas ponownego odbioru WP-01.6 potwierdzono build z cache, działanie usług, healthchecków i proxy oraz brak zmiany logicznej zawartości developerskiej bazy przez test kontenerowy. Nie wykonywano testu odtwarzania danych ani restartu bazy.

## Kolejne etapy

Przeprowadzono **WP-01.6: Audyt techniczny i odbiór środowiska**. [Raport audytu](docs/testing/WP01_AUDIT_REPORT.md) zachowuje pierwotne NOT READY i zawiera ponowny odbiór po uruchomieniu Dockera: **READY WITH WARNINGS**. Blokadę Docker zamknięto rzeczywistymi kontrolami, bez zmiany logiki aplikacji i WP-00.

Środowisko jest gotowe do **WP-02.1**; [WP02_HANDOFF.md](docs/ai/WP02_HANDOFF.md) przekazuje kontekst kolejnemu agentowi. WP-02 nie został rozpoczęty automatycznie.

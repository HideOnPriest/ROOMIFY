# Roomify — audyt techniczny WP-01.6

> **Aktualny odbiór: READY WITH WARNINGS (17.10.2026, ponowna kontrola po uruchomieniu Docker Desktop).** Wyniki pierwotnego audytu w sekcjach 1–8 zachowano jako historię. Aktualizację, zamknięcie blokady Docker i ograniczenia opisuje sekcja 9. Historyczne NOT READY nie jest bieżącą oceną.

## 1. Metadane i zakres

- **Projekt:** Roomify, akademicki system rezerwacji sal konferencyjnych.
- **Etap:** WP-01.6 — audyt WP-01.1–WP-01.5, bez implementowania funkcji.
- **Data:** 17.10.2026, strefa Europe/Warsaw (CEST, UTC+02:00).
- **Katalog:** `D:\Projekty\ROOMIFY`.
- **Commit:** brak. `git rev-parse --verify HEAD` zwrócił kod 128; repozytorium jest zainicjalizowane, lecz nie ma commita ani śledzonych plików. Nie tworzono commita ani push.
- **Dowody:** świeże logi poleceń, TRX i JSON w ignorowanym `artifacts/wp016/`; raport Playwright w `frontend/playwright-report/index.html`.
- **Metoda:** analiza konfiguracji i kodu, wykonanie dostępnych kontroli, następnie drobne poprawki dokumentacji/formatowania oraz ich weryfikacja. Nie zmieniono kodu aplikacji ani testów.

### Wersje wykryte podczas audytu

| Element | Wersja |
|---|---|
| .NET SDK | 10.0.301 |
| Microsoft.NETCore.App / Microsoft.AspNetCore.App | 10.0.9 |
| Angular / CLI | 22.2.3 |
| TypeScript | 6.0.3 |
| Node.js / npm | 24.19.0 / 12.2.0 |
| RxJS / jsdom | 7.8.2 / 30.1.3 |
| Vitest / coverage-v8 | 5.0.5 / 5.0.5 |
| Playwright | 1.64.1, zainstalowany Chromium |
| xUnit / runner Visual Studio | 2.9.3 / 3.1.4 |
| Microsoft.NET.Test.Sdk / coverlet.collector | 17.14.1 / 6.0.4 |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.9 |
| Testcontainers.PostgreSql / Npgsql | 4.16.0 / 10.0.3 |
| Stryker.NET | 5.0.0, lokalny manifest `.config/dotnet-tools.json` |
| Compose | Samodzielny Docker Compose 5.6.0; brak dostępnego silnika Docker |

Node spełnia `engines.node` z zainstalowanego CLI: `^22.22.3 || ^24.15.0 || >=26.0.0`. TypeScript spełnia peer dependency kompilatora: `>=6.0 <6.1`. Sprawdzenie wykonano biblioteką semver na rzeczywistych metadanych pakietów, bez zakładania zgodności na podstawie dokumentacji.

Przypięte obrazy w konfiguracji, których **nie uruchomiono**: SDK `10.0.301-noble`, ASP.NET `10.0.9-noble`, Node `24.19.0-bookworm-slim`, Nginx `1.30.6-alpine`, PostgreSQL `18.6-bookworm`.

## 2. Podsumowanie stanu

| Obszar | Ocena | Dowód / ograniczenie |
|---|---|---|
| Repozytorium | PASS z ostrzeżeniem | Brak zagnieżdżonego Git; poprawne ignorowanie. Brak historii i śledzonych plików. |
| Backend | PASS | Restore, Debug i Release siedmiu projektów bez błędów/ostrzeżeń; lokalne HTTP 200 Healthy bez bazy. |
| Frontend | PASS | npm ci, produkcyjny build, routing i testy UI działają bez backendu. |
| Compose | Konfiguracja PASS, wykonanie BLOCKED | Walidacja działa; ps/build/up nie łączą się z docker_engine. |
| Nginx i PostgreSQL | Statycznie sprawdzone; wykonanie BLOCKED | Nie potwierdzono proxy, gotowości bazy, sieci, wolumenu ani zachowania procesów w kontenerach. |
| Testy | 10 zaliczonych przypadków; 1 blokada | HTTP 3, Vitest 5, E2E 2. PostgreSQL: failed w runnerze z przyczyny środowiskowej. |
| Stryker | CONFIGURED / NOT APPLICABLE | Poprawnie wczytana konfiguracja Domain; brak testów i kodu biznesowego do miarodajnej oceny. |
| Manualne | NOT RUN | Trzy udokumentowane scenariusze, bez deklarowania nowego ręcznego wykonania. |

Najważniejsza blokada odbioru to brak działającego silnika kontenerów. Nie wykryto błędu kompilacji ani niepowodzenia asercji istniejących funkcji, które wymagałoby zmiany kodu.

## 3. Audyt statyczny

### Repozytorium i konfiguracja

Sprawdzono strukturę `backend/`, `frontend/`, `infra/`, `docs/`, `.github/` i `.config/`. Nie znaleziono zagnieżdżonego repozytorium ani plików AGENTS.md/IMPLEMENTATION_STATUS.md w zakresie projektu poza zależnościami i artefaktami.

`git ls-files` zwraca zero plików. W indeksie nie ma sekretów, node_modules, bin, obj ani raportów. Wniosek dotyczy obecnego indeksu; przy braku commita nie istnieje historia do porównania. `git status --porcelain=v1 --untracked-files=all` pokazuje pliki projektu jako untracked. Lokalny `.env` i wyniki testów nie pojawiają się w tej liście.

Wykonano 14 prób `git check-ignore --no-index`: 9 ścieżek artefaktów/sekretów jest ignorowanych, a 5 wymaganych ścieżek pozostaje widocznych, w tym package-lock, `.env.example`, Markdown, przyszłe migracje `.cs` i konfiguracja CI. `.gitattributes` określa normalizację tekstu do LF, wyjątki CRLF dla cmd/bat i pliki binarne. `.editorconfig` definiuje UTF-8, końcową nową linię, wcięcia i zasady Markdown. Dwie konfiguracje JSON miały CRLF; znormalizowano je i potwierdzono niezmienność sparsowanych wartości. Ignorowanych lokalnych plików IDE nie modyfikowano.

Przykład środowiska zawiera wyłącznie placeholder hasła. Lokalna wartość hasła nie występuje w audytowanych plikach przeznaczonych do wersjonowania; nie wypisywano jej. `.env` pozostaje poza kontekstami budowania obrazów. To kontrola konfiguracji i indeksu, bez deklarowania pełnego audytu bezpieczeństwa produktu.

### Architektura backendu

Wykryto dokładnie cztery projekty produkcyjne i trzy testowe, wszystkie `net10.0`, z nullable i implicit usings. Zweryfikowano następujące bezpośrednie referencje i brak cykli:

| Projekt | Referencje |
|---|---|
| Domain | Brak |
| Application | Domain |
| Infrastructure | Application, Domain |
| Api | Application, Infrastructure |
| Domain.UnitTests | Domain |
| Application.UnitTests | Application, Domain |
| Api.IntegrationTests | Api |

API wywołuje obie rejestracje DI, konfiguruje MVC, routing, standardowy host konfiguracji i logowania oraz Health Checks. Brak kontrolerów biznesowych jest zgodny z zakresem. Domain nie zawiera kodu biznesowego. Nie dodano EF Core, Identity, modeli, migracji ani usług pozorujących gotowe funkcjonalności.

### Frontend i komunikacja

Zweryfikowano bootstrap standalone bez NgModule, CSR bez SSR, jawne strict/strictTemplates, SCSS, podział core/shared/features, dwa dynamiczne loadComponent, przekierowanie `/` i końcowy wildcard. `provideHttpClient()` jest zarejestrowany; obecne strony nie wykonują żądań API. W komponentach nie ma wewnętrznych adresów kontenerów ani twardo wpisanego localhost backendu. Signals, RxJS i Reactive Forms są przewidziane do realnych funkcji, bez sztucznego stanu w informacyjnym szkielecie.

Proxy developerskie zachowuje `/api` i `/api/**`. Nie ma niepotrzebnego CORS w backendzie. Nginx ma osobne lokalizacje `/api`, `/api/` i `/health`, proxy_pass bez końcowego ukośnika, nagłówki przekazujące kontekst oraz fallback Angulara poza API. Nie ma reguły podmieniającej błędy backendu dokumentem Angulara. Jest to wynik analizy konfiguracji: rzeczywistych odpowiedzi **Nginx** nie potwierdzono bez Dockera. Samo API przez HTTP zwróciło 404 dla nieistniejącego `/api/...`.

### Compose, obrazy i dane

Konfiguracja zawiera wyłącznie frontend, api i db, prywatną sieć bridge projektu, porty hosta przypisane do 127.0.0.1 i nazwany wolumen `roomify_postgres_data`. PostgreSQL 18 ma montowanie pod `/var/lib/postgresql`. API działa jako użytkownik app i używa 8080, Nginx serwuje zweryfikowaną ścieżkę `dist/roomify/browser/`. Oba Dockerfile są wieloetapowe i mają osobne konteksty z `.dockerignore`. Obraz API buduje wyłącznie graf produkcyjnego projektu API, nie rozwiązanie zawierające wykluczone testy.

Sprawdzono deklaracje healthchecków, parametry czasu i zależności service_healthy. Skrypt API wykorzystuje Bash w wariancie Noble; jego wykonanie w obrazie pozostaje niepotwierdzone. Nie tworzono ani nie usuwano wolumenów. Bez połączenia z silnikiem nie można potwierdzić stanu usług, restartów, gotowości bazy, rzeczywistego montowania ani trwałości danych.

## 4. Wykonane polecenia i wyniki

Wszystkie poniższe wyniki pochodzą z tego audytu, nie z WP-01.5. Pełne argumenty, katalog roboczy, kody wyjścia, czasy i logi zapisuje `artifacts/wp016/commands.json` oraz pliki `.log`.

Na tym hoście `git` oznacza dostarczony z runtime plik wykonywalny, `node` — Node z runtime, a `npm` — wywołanie tego Node z `artifacts/wp013/npm/package/bin/npm-cli.js`. `COMPOSE` oznacza istniejący samodzielny `artifacts/wp014/tools/docker-compose.exe`; nie wykonywano niedostępnego globalnego Docker CLI. Polecenia npm mają katalog roboczy `frontend/`, polecenie mutacyjne — katalog Domain.UnitTests, pozostałe — katalog główny. W poleceniach testowych skrót `<wyniki>` oznacza absolutną ścieżkę `artifacts/wp016/trx`.

| Cel | Wykonane polecenie | Status | Istotny wynik / błąd |
|---|---|---|---|
| Zmiany Git | `git -c safe.directory=D:/Projekty/ROOMIFY status --porcelain=v1 --untracked-files=all` | PASS | Pliki projektu untracked; brak raportów i `.env`. |
| Indeks | `git -c safe.directory=D:/Projekty/ROOMIFY ls-files` | PASS | Zero śledzonych plików. |
| Commit | `git -c safe.directory=D:/Projekty/ROOMIFY rev-parse --verify HEAD` | NOT APPLICABLE | Kod 128: brak commita, nie uszkodzenie kodu. |
| SDK | `dotnet --version` | PASS | 10.0.301. |
| Dostępne SDK | `dotnet --list-sdks` | PASS | Wykryto SDK 10.0.301 oraz istniejący SDK 8.0.422. |
| Dostępne runtime | `dotnet --list-runtimes` | PASS | Microsoft.NETCore.App i Microsoft.AspNetCore.App 10.0.9 dostępne lokalnie. |
| Node | `node --version` | PASS | v24.19.0. |
| npm | `npm --version` | PASS | 12.2.0. |
| Restore | `dotnet restore backend/Roomify.slnx` | PASS | Wszystkie projekty aktualne. |
| Debug | `dotnet build backend/Roomify.slnx --configuration Debug --no-restore` | PASS | 0 ostrzeżeń, 0 błędów. |
| Release | `dotnet build backend/Roomify.slnx --configuration Release --no-restore` | PASS | 0 ostrzeżeń, 0 błędów. |
| Jednostki Domain | `dotnet test backend/tests/Roomify.Domain.UnitTests --no-build --no-restore --logger "trx;LogFileName=domain.trx" --results-directory <wyniki>` | NOT APPLICABLE dla zachowania domeny | Kod 0; wykryto zero przypadków. |
| Jednostki Application | `dotnet test backend/tests/Roomify.Application.UnitTests --no-build --no-restore --logger "trx;LogFileName=application.trx" --results-directory <wyniki>` | NOT APPLICABLE dla zachowania aplikacyjnego | Kod 0; wykryto zero przypadków. |
| Integracja HTTP | `dotnet test backend/tests/Roomify.Api.IntegrationTests --no-build --no-restore --logger "trx;LogFileName=api.trx" --results-directory <wyniki> --filter "RequiresDocker=false"` | PASS | 3 passed, 0 failed, 0 skipped. |
| PostgreSQL | `dotnet test backend/tests/Roomify.Api.IntegrationTests --no-build --no-restore --logger "trx;LogFileName=postgresql.trx" --results-directory <wyniki> --filter "RequiresDocker=true"` | BLOCKED | Kod 1; DockerUnavailableException, timeout połączenia z docker_engine. |
| Odtwarzalna instalacja frontendu | `npm ci` | PASS z ostrzeżeniem | 296 packages, audit bez podatności; cztery skrypty instalacyjne zablokowane przez politykę npm, bez wpływu na wykonane build/testy. |
| Build Angulara | `npm run build` | PASS | Build produkcyjny; osobne fragmenty home i not-found. |
| Vitest i JSON | `npm run test:ci -- --reporters=json --output-file=../artifacts/wp016/vitest.json` | PASS | JSON potwierdza 5 zaliczonych testów. |
| E2E i raporty | `npm run e2e -- --reporter=list,html,json` | PASS | 2 expected, 0 unexpected, 0 flaky, 0 skipped. |
| Wersja Compose | `COMPOSE version` | PASS | 5.6.0. |
| Lokalizacja Docker CLI | `Get-Command docker,docker-compose -ErrorAction SilentlyContinue` | BLOCKED | Nie znaleziono poleceń w PATH; sprawdzenie standardowej lokalizacji Docker Desktop także nie znalazło Docker CLI. |
| Walidacja bez ujawniania sekretów | `COMPOSE config --quiet` | PASS | Kod 0. |
| Stan kontenerów | `COMPOSE ps --all` | BLOCKED | Kod 1; brak połączenia z npipe docker_engine. |
| Budowanie obrazów | `COMPOSE build` | BLOCKED | Kod 1; brak silnika. Dodatkowo brak buildx w lokalnym zestawie narzędzi. |
| Start bez zastępowania istniejących kontenerów | `COMPOSE up -d --no-recreate` | BLOCKED | Kod 1; brak silnika. Nie zmieniono stanu kontenerów. |
| Przywrócenie narzędzia | `dotnet tool restore` | PASS | Przywrócono lokalny dotnet-stryker 5.0.0. |
| Manifest | `dotnet tool list --local` | PASS | Wskazany manifest `.config/dotnet-tools.json`. |
| Dostępność Stryker | `dotnet stryker --help` | PASS | Dostępne opcje i runner VSTest. |
| Wczytanie konfiguracji Domain | `dotnet stryker --config-file stryker-config.json --output <artifacts/wp016/mutation> --skip-version-check` | CONFIGURED / NOT APPLICABLE | Konfiguracja wczytana, wskazano Domain; kod 1 z powodu zera testów. Brak miarodajnego score. |

Ustawienia raportowania E2E na czas procesu: `PLAYWRIGHT_JSON_OUTPUT_FILE=artifacts/wp016/playwright.json` (ścieżka absolutna) i `PLAYWRIGHT_HTML_OPEN=never`. Nie zmieniono konfiguracji testów. Ostrzeżenie FORCE_COLOR/NO_COLOR nie spowodowało błędu testów.

### Dodatkowa kontrola lokalnego API

Wykonano `dotnet run --project backend/src/Roomify.Api --launch-profile http --no-build -- --urls http://127.0.0.1:5080`. Własny proces audytowy obsłużył rzeczywiste GET `/health`: **200, dokładny tekst Healthy**, bez PostgreSQL. GET `/api/nieistniejacy-endpoint` zwrócił **404**, bez dokumentu Angulara. Proces i jego procesy potomne zatrzymano po kontroli. Szczegóły są w `artifacts/wp016/local-api.json` i `local-api.log`. Nie zatrzymywano cudzych procesów.

Sprawdzenia zgodności pakietów, ignorowania, parserów JSON/XML, referencji, sum WP-00 oraz odnośników wykonano skryptami kontrolnymi zapisującymi wyniki do `static.json`, `versions.json`, `format-verification.json` i `summary.json` w tym samym katalogu artefaktów. Dla Git używano safe.directory wyłącznie jako opcji pojedynczych poleceń, bez zmiany konfiguracji globalnej.

## 5. Wyniki testów

Czasy poniżej oznaczają zmierzony czas całego polecenia wraz z runnerem, nie sumę czasów samych asercji.

| Zestaw | Wykryte | Zaliczone | Failed w runnerze | Pominięte | Czas polecenia | Ocena |
|---|---:|---:|---:|---:|---:|---|
| Domain.UnitTests | 0 | 0 | 0 | 0 | 1,072 s | Poprawne ograniczenie etapu, bez dowodu jakości domeny. |
| Application.UnitTests | 0 | 0 | 0 | 0 | 1,041 s | Poprawne ograniczenie etapu. |
| WebApplicationFactory | 3 | 3 | 0 | 0 | 1,336 s | PASS. |
| Testcontainers PostgreSQL | 1 | 0 | 1 | 0 | 11,204 s | BLOCKED: inicjalizacja fixture nie dotarła do SELECT 1. |
| Vitest | 5 | 5 | 0 | 0 | 2,912 s | PASS, bez backendu. |
| Playwright Chromium | 2 | 2 | 0 | 0 | 3,336 s | PASS, bez ponowień; czas runnera 2,838 s. |

Łącznie zarejestrowano **11 prób przypadków: 10 zaliczonych, 1 failed z blokady środowiskowej, 0 pominiętych**. Nie doliczamy kontroli HTTP, parsowania konfiguracji ani pustych projektów jako dodatkowych zaliczonych testów.

Testy HTTP korzystają z rzeczywistego hosta w pamięci, bez osobnego Kestrela; HttpClient, status i treść są objęte asercjami. Filtry RequiresDocker rozdzielają zestawy przed inicjalizacją fixture. Test kontenerowy w kodzie używa osobnego kontenera, losowego portu i zwalniania zasobów, ale brak silnika uniemożliwił potwierdzenie tego w wykonaniu. Nie korzysta z `.env` ani developerskiego wolumenu.

Vitest zachował wszystkie dotychczasowe asercje TestBed. W przeglądarce Chromium potwierdzono przekierowanie na /home, widoczność Roomify, nieznaną trasę, komunikat 404 i link powrotu. HTML Playwright został rzeczywiście wygenerowany; screenshot i trace są ustawione na błędy/ponowienia, więc ich brak przy dwóch stabilnie przechodzących testach jest oczekiwany.

Stryker sprawdził konfigurację i projekt docelowy, lecz nie przeprowadził miarodajnej oceny mutacyjnej. **CONFIGURED oznacza gotowe narzędzie i konfigurację; NOT APPLICABLE oznacza brak kodu/testów do oceny. Żaden z tych stanów nie jest wysokim mutation score.**

Trzy przypadki manualne z MANUAL_TESTS.md pozostają **NOT RUN**. Nie przypisujemy im wyników E2E jako ręcznego wykonania.

## 6. Niezgodności i naprawy

| ID | Opis | Priorytet | Obszar | Rozwiązanie / weryfikacja | Status |
|---|---|---|---|---|---|
| AUD-001 | Brak dostępu do silnika Docker: nie można odebrać obrazów, usług, PostgreSQL i Nginx. | P1 — blokuje pełny odbiór do WP-02 | Środowisko | Zapewnić Docker z kontenerami Linux i buildx, ponowić config/build/up, healthchecki, HTTP przez Nginx oraz kategorię RequiresDocker=true. Nie resetować danych. | OTWARTA / BLOCKED |
| AUD-002 | Brak commita i śledzonych plików uniemożliwia wskazanie wersji audytowanego kodu w historii Git. | P2 | Repozytorium | Uporządkować wersjonowanie w osobno autoryzowanej pracy; w audycie użyto lokalnych sum plików. Bez automatycznego commita/push. | OSTRZEŻENIE, bez zmiany repozytorium |
| AUD-003 | BACKEND.md twierdził, że Docker nie jest skonfigurowany, a 8080 jest tylko planem. | P3 | Dokumentacja | Opisano istniejące Dockerfile/Compose, rozróżniono 8080/5080 i blokadę wykonania; sprawdzono zgodność z kodem i odnośnik do DOCKER.md. | NAPRAWIONA |
| AUD-004 | Wprowadzenie ADR przedstawiało wszystkie mechanizmy jako nadal planowane, a README/dokumentacja testów nie odnotowywały bieżącego audytu. | P3 | Dokumentacja | Wyjaśniono historyczną perspektywę decyzji, dodano aktualny raport i stan WP-01.6. Nie zmieniono zaakceptowanych decyzji ani WP-00. Zweryfikowano odnośniki i brak starego oznaczenia etapu. | NAPRAWIONA |
| AUD-005 | Manifest narzędzi i package.json używały CRLF mimo konwencji LF. | P3 | Format konfiguracji | Zmieniono wyłącznie zakończenia linii; porównano sparsowane JSON przed/po i potwierdzono identyczne wartości. Lokalne pliki IDE pozostały nietknięte. | NAPRAWIONA |

Zablokowane skrypty instalacyjne npm oraz ostrzeżenia kolorów nie spowodowały błędu builda ani testów. Nie osłabiano polityki wykonywania skryptów, nie instalowano nowych zależności i nie zmieniano asercji. Puste projekty jednostkowe, brak mutation score i niewykonane scenariusze manualne są jawnie opisanymi ograniczeniami, a nie fikcyjnymi sukcesami.

### Dokładny zakres zmian audytu

Zmodyfikowano sześć istniejących plików: README.md, docs/architecture/BACKEND.md, docs/decisions/README.md, docs/testing/README.md, .config/dotnet-tools.json oraz frontend/package.json. Dodano niniejszy raport. Nie zmieniono package-lock, projektów, referencji, testów, Dockerfile ani konfiguracji proxy. Kontrola sum potwierdziła zachowanie pozostałych audytowanych plików; dokumenty WP-00 są również bajtowo identyczne z dostarczonym archiwum źródłowym.

## 7. Kryteria akceptacji

| ID | Kryterium | Status | Uzasadnienie |
|---|---|---|---|
| AC-001 | Struktura repozytorium | PASS | Wykryto wymagane katalogi i pliki. |
| AC-002 | Konfiguracja Git | PASS | Indeks, status, ignorowanie i konwencje sprawdzone; brak historii ujawniono. |
| AC-003 | Cztery projekty backendu | PASS | Cztery produkcyjne plus trzy testowe. |
| AC-004 | Architektura referencji | PASS | Dokładne referencje zgodne, brak cykli. |
| AC-005 | Debug | PASS | 0 błędów i ostrzeżeń. |
| AC-006 | Release | PASS | 0 błędów i ostrzeżeń. |
| AC-007 | Lokalny /health | PASS | Rzeczywiste HTTP 200 Healthy bez bazy. |
| AC-008 | Build Angulara | PASS | Produkcyjny build po npm ci. |
| AC-009 | Routing /home | PASS | Chromium i testy Routera. |
| AC-010 | Routing 404 | PASS | Widok i powrót potwierdzone w E2E. |
| AC-011 | Compose config | PASS | Samodzielny Compose, kod 0 bez wypisywania sekretów. |
| AC-012 | Budowanie obrazów | BLOCKED | Brak silnika Docker. |
| AC-013 | Start trzech usług | BLOCKED | Brak silnika Docker. |
| AC-014 | Healthcheck PostgreSQL | BLOCKED | Nie uruchomiono serwera w kontenerze. |
| AC-015 | Proxy Nginx w wykonaniu | BLOCKED | Statyczna konfiguracja sprawdzona, brak kontroli kontenerowej. |
| AC-016 | Jednostki .NET / opis braku przypadków | PASS | Oba runnery uruchomione, zero przypadków udokumentowane. |
| AC-017 | WebApplicationFactory | PASS | 3/3 przypadki zaliczone. |
| AC-018 | Testcontainers PostgreSQL | BLOCKED | DockerUnavailableException, SELECT 1 niewykonany. |
| AC-019 | Vitest | PASS | 5/5 testów. |
| AC-020 | Playwright | PASS | 2/2 testy w Chromium. |
| AC-021 | Raport Playwright | PASS | HTML istnieje i ma wynik tego audytu. |
| AC-022 | Sprawdzenie Stryker | PASS | Narzędzie i konfiguracja sprawdzone, score NOT APPLICABLE. |
| AC-023 | Zgodność dokumentacji | PASS | Poprawiono wykryte opisy; odnośniki i konfigurację sprawdzono ponownie. |
| AC-024 | Brak sekretów śledzonych w Git | PASS | Indeks pusty, .env ignorowany; lokalnej wartości nie znaleziono w plikach projektu. |
| AC-025 | Brak blokujących problemów | FAIL | AUD-001 nadal blokuje pełny odbiór środowiska do WP-02. |
| AC-026 | Raport audytu | PASS | Utworzono WP01_AUDIT_REPORT.md. |
| AC-027 | Zakres funkcjonalny bez zmian | PASS | Nie zmieniono kodu ani testów. |
| AC-028 | Brak operacji destrukcyjnych na danych | PASS | Bez usuwania danych, resetów i czyszczenia wolumenów. |
| AC-029 | Brak zbędnych zależności | PASS | Tylko przywrócenie już przypiętych zależności. |
| AC-030 | Jednoznaczna ocena do WP-02 | PASS | NOT READY, uzasadnienie poniżej. |

Wynik: **24 PASS, 5 BLOCKED, 1 FAIL**. BLOCKED nie jest zaliczeniem. FAIL AC-025 oznacza niespełniony warunek odbioru, a nie wykryty błąd logiki biznesowej.

## 8. Ocena gotowości do WP-02

**NOT READY — pełne środowisko nie zostało odebrane do WP-02.**

Backend i frontend kompilują się, a wszystkie dostępne przypadki techniczne HTTP, Vitest i Chromium przechodzą. Brak Dockera uniemożliwia jednak potwierdzenie pięciu wymaganych kontroli: budowania obrazów, uruchomienia usług, gotowości PostgreSQL, proxy Nginx i Testcontainers. Planowana integracja danych w WP-02 wymaga rzeczywistego działającego PostgreSQL, więc tego braku nie klasyfikujemy jako nieblokującego ostrzeżenia.

Warunkiem ponownego odbioru jest dostępny silnik Docker Linux wraz z narzędziami budowania, niedestrukcyjne powtórzenie wskazanych kontroli i udokumentowanie powodzenia. Nie rozpoczynano WP-02 automatycznie.

## 9. Ponowny odbiór po uruchomieniu Docker Desktop — 17.10.2026

### Zakres i środowisko

Kontrolę ponowiono około 18:22 CEST. Docker Desktop 4.95.0 (242831), klient i silnik 29.8.2, kontekst desktop-linux, Linux amd64, Compose 5.6.0. Trzy kontenery projektu były już uruchomione przez użytkownika. Dowody tego wykonania znajdują się w ignorowanym `artifacts/wp016-recheck/`: log budowania, `results.json`, logi poleceń i dwa pliki TRX. Nie nadpisano dowodów `artifacts/wp016/` ani wcześniejszych wyników w sekcjach 1–8.

### Rzeczywiste wyniki

| Kontrola | Wynik | Dowód i zakres |
|---|---|---|
| `docker compose config --quiet` | PASS, kod 0 | Bez wypisywania rozwiniętej konfiguracji i hasła. |
| `docker compose build` | PASS, kod 0 | Obrazy API i Angular/Nginx zbudowane; warstwy z cache. To nie był build bez cache. |
| `docker compose up -d --no-recreate --wait --wait-timeout 120` | PASS, kod 0 | Istniejące usługi zachowane; bez zastępowania kontenerów i danych. |
| `docker compose ps --all` | PASS | frontend, api i db running; API i db healthy, restart count 0. Frontend nie ma zdefiniowanego healthchecka, dostępność sprawdzono HTTP. |
| `pg_isready` wewnątrz db | PASS, kod 0 | PostgreSQL przyjmuje połączenia TCP; dodatkowo pg_dump i odczyt identyfikatora klastra wykonały się prawidłowo. |
| Skrypt `/app/healthcheck.sh` w api | PASS, kod 0 | Rzeczywiste wykonanie skryptu w obrazie Noble. |
| `nginx -t` we frontendzie | PASS, kod 0 | Poprawna składnia i konfiguracja Nginx. |
| `http://127.0.0.1:5080/health` | PASS | HTTP 200, text/plain, dokładnie `Healthy`, Kestrel. |
| `http://127.0.0.1:4200/health` | PASS | HTTP 200, text/plain, dokładnie `Healthy`, przez Nginx. |
| Nginx `/`, `/home`, `/audit-nonexistent-route` | PASS | HTTP 200, text/html, dokument Angulara z app-root; fallback CSR działa. To kontrola HTTP, nie dodatkowe E2E. |
| Nginx `/api` i `/api/audit-missing?probe=wp01` | PASS | HTTP 404 z pustą treścią, bez HTML Angulara. Log API potwierdza zachowanie prefiksu i query string. |
| `dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=true"` | PASS | 1/1 passed, 0 failed, 0 skipped; całe polecenie 7,781 s. Wykonano SELECT 1 przez Npgsql, a nie tylko start kontenera. Dodatkowo użyto loggera TRX i katalogu wyników. |
| Ten sam projekt, `--no-build --no-restore --filter "RequiresDocker=false"` | PASS | Ponowiono 3/3 przypadki HTTP, 0 failed, 0 skipped; polecenie 1,391 s. |

Nie powtarzano npm ci, Vitest, Playwright ani Stryker: ich poprzednie wykonania i ograniczenia pozostają w sekcjach 4–5, a kod i konfiguracja testów nie zmieniły się. Aktualny komplet odrębnych istniejących przypadków automatycznych to **11/11 zaliczonych**: HTTP 3 i PostgreSQL 1 z ponownego odbioru oraz Vitest 5 i Playwright 2 z wcześniejszego audytu tego samego kodu. Nie oznacza to jednego wspólnego nowego uruchomienia.

### Izolacja danych

Podczas testu zaobserwowano odrębny kontener `1ac7913fd94b`, oznaczony etykietami Testcontainers 4.16.0, z portem hosta **52627**, podczas gdy developerski PostgreSQL pozostał na **5432**. Kontener testowy używał własnego anonimowego wolumenu, bez montowania `roomify_postgres_data`. Nazwa bazy i użytkownika w fixture to `roomify_tests`, hasło jest losowane; fixture nie odczytuje `.env`. Po teście identyfikator kontenera testowego nie występował w `docker ps -aq`.

Przed i po teście porównano logiczny pg_dump developerskiej bazy (bez właścicieli i uprawnień), pomijając wyłącznie losowe znaczniki `\restrict`/`\unrestrict` generowane przez pg_dump. Obie sumy SHA-256 wyniosły `eecd1a3cab6c14d496cdc5600f591e3a8ea96f0ac7d40ba6042195d90d7d2ab5`. Identyfikator klastra pozostał `7697670479764820011`; ID kontenera db, czas startu, liczba restartów i montowanie developerskiego wolumenu również się nie zmieniły. Potwierdza to brak zmiany logicznej zawartości tej bazy przez wykonany test, nie bajtową niezmienność plików silnika ani test odtwarzania kopii zapasowej. Nie wykonano down, usuwania wolumenów, resetu ani zapisu danych audytowych w bazie.

### Aktualizacja kryteriów i ustaleń

AC-012, AC-013, AC-014, AC-015 i AC-018 zmieniają bieżący status z BLOCKED na **PASS**, a AC-025 z FAIL na **PASS**. Pozostałe wyniki zachowują dowody pierwotnego wykonania. Bieżąca macierz ma **30 PASS, 0 BLOCKED, 0 FAIL**, z jawnymi ograniczeniami poniżej. AUD-001 jest **ZAMKNIĘTA** po rzeczywistym odbiorze Dockera. AUD-002 zostaje usunięta po utworzeniu pierwszego lokalnego commita opisanego niżej; pozostałe historyczne naprawy pozostają ważne.

**READY WITH WARNINGS — środowisko WP-01 odebrane, można rozpocząć WP-02.1 po przekazaniu jego zakresu.** Ostrzeżenia: manualne NOT RUN, Domain/Application mają zero testów biznesowych, Stryker CONFIGURED / NOT APPLICABLE. `/health` obecnie potwierdza host API, nie połączenie API z bazą. EF Core, Identity, migracje i CI nadal nie istnieją; nie są deklarowane jako gotowe. Nie implementowano WP-02 i nie zmieniano wymagań WP-00.

### Git i zakres zamknięcia

Sprawdzono `.gitignore`, stan repozytorium i pliki przygotowane do pierwszego lokalnego commita. Wcześniejszy pusty indeks jest faktem historycznym; przed tym odbiorem indeks zawierał już 105 plików inicjalizacyjnych, a HEAD nadal nie istniał. Pierwszy lokalny commit zamknięcia ma komunikat `chore: initialize Roomify architecture and testing infrastructure`; jego rzeczywisty identyfikator podaje raport końcowy i `git log -1`. Nie wpisujemy własnego hasha do zawartości commita.

Końcowa kontrola objęła 107 plików indeksu, zgodność indeksu z plikami roboczymi po normalizacji Git, `git diff --cached --check`, dziesięć prób ignorowania, skan lokalnego hasła i typowych kluczy/tokenów oraz lokalne odnośniki Markdown. Nie wykryto błędów ani sekretów w kontrolowanym zakresie. Cztery pliki WP-00 porównano bajtowo z oryginalnym ZIP — wszystkie identyczne. Generowane raporty, .env, node_modules, bin i obj nie znajdują się w indeksie. To kontrola zawartości pierwszego commita, nie pełny audyt bezpieczeństwa aplikacji.

W tym zamknięciu zmieniono wyłącznie dokumentację: niniejszy raport, README, BACKEND, OVERVIEW, README testów i INTEGRATION_TESTS oraz dodano IMPLEMENTATION_STATUS i WP02_HANDOFF. Pliki WP-00, aplikacji, testów, lockfile i konfiguracji infrastruktury zachowano. Generowane logi/TRX i skrypt kontrolny są w ignorowanym artifacts; nie należą do commita. Nie wykonano git push w ramach zamknięcia WP-01.

# Roomify — przekazanie WP-01 do WP-02

Stan na 17.10.2026: **READY WITH WARNINGS**, środowisko gotowe do WP-02.1; implementacja WP-02 nie została rozpoczęta. Bieżące wyniki są w [sekcji 9 audytu](../testing/WP01_AUDIT_REPORT.md#9-ponowny-odbiór-po-uruchomieniu-docker-desktop--17102026), a stan etapów w [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).

## Kolejność zapoznania się z projektem

1. [PROJECT_CONTEXT](PROJECT_CONTEXT.md), [REQUIREMENTS](REQUIREMENTS.md), [BUSINESS_RULES](BUSINESS_RULES.md), [TEST_STRATEGY](TEST_STRATEGY.md) — niezmienione źródła WP-00.
2. [ADR](../decisions/README.md) — zamrożone w WP-01 wybory architektury, .NET 10, Angular 22, EF Core 10/PostgreSQL oraz Identity z cookies.
3. [Architektura](../architecture/OVERVIEW.md), [backend](../architecture/BACKEND.md), [frontend](../architecture/FRONTEND.md), [Docker](../architecture/DOCKER.md) i [testy](../testing/README.md).
4. Dokładne polecenie WP-02.1 od użytkownika. Nie ma tu odrębnej zatwierdzonej specyfikacji tego podetapu.

Dokumenty specyfikacji są źródłem wymagań, nie samodzielną autoryzacją wykonywania wszystkich opisanych przyszłych działań. Zachowaj oznaczenia `[Z]`/`[P]` oraz identyfikatory FR/BR/NFR. Pięć zatwierdzonych ustaleń: konta tworzy administrator, zasoby to sale, godziny 08:00–20:00, administrator zarządza cudzymi rezerwacjami, maksymalna długość 8 godzin. Wybór technologii nie zatwierdza automatycznie pozostałych propozycji biznesowych. Wymagają doprecyzowania m.in. kontrakt błędów, reset haseł i niezatwierdzone szczegóły reguł.

## Stan kodu i zależności

| Obszar | Istnieje | Jeszcze nie istnieje |
|---|---|---|
| Domain | net10.0, bez zależności Roomify | Encje, typy wartości, reguły i statusy. |
| Application → Domain | Punkt AddApplication, DI.Abstractions 10.0.0 | Przypadki użycia, interfejsy danych, DTO i walidacja. |
| Infrastructure → Application, Domain | Punkt AddInfrastructure, DI.Abstractions 10.0.0 | EF Core, provider Npgsql dla EF, DbContext, mapowania, migracje i Identity. |
| Api → Application, Infrastructure | MVC, DI, Problem Details, logging, /health, public partial Program | Kontrolery biznesowe, auth/authz, CSRF i połączenie z bazą. |
| Angular | Standalone CSR, SCSS, strict, strictTemplates, HttpClient, lazy /home i 404 | Ekrany biznesowe, serwisy API, auth, formularze i kalendarz. |

Rozwiązanie `backend/Roomify.slnx` zawiera cztery projekty produkcyjne i trzy testowe. Domain nie może zależeć od EF/HTTP/Identity. Migracje i implementacje danych należą do Infrastructure; API jest miejscem składania zależności. Obecny Dockerfile buduje projekt API, a nie całe rozwiązanie z testami.

Wersje zweryfikowane: SDK .NET 10.0.301, runtime ASP.NET/.NET 10.0.9; Angular/CLI 22.2.3, TypeScript 6.0.3, Node 24.19.0, npm 12.2.0, RxJS 7.8.2. Testy: xUnit 2.9.3, runner 3.1.4, Test SDK 17.14.1, coverlet 6.0.4, Mvc.Testing 10.0.9, Testcontainers.PostgreSql 4.16.0, Npgsql 10.0.3, Vitest/coverage-v8 5.0.5, Playwright 1.64.1, Stryker.NET 5.0.0. Npgsql jest obecnie zależnością testów, nie skonfigurowanym providerem EF w aplikacji. Przy dodawaniu EF/Identity sprawdź zgodność wersji i zachowaj lockfile npm; Moq nie jest jeszcze potrzebny ani dodany.

## Środowisko i dane

Docker Desktop 4.95.0, Docker Engine 29.8.2 Linux, Compose 5.6.0. Działają `roomify-db-1`, `roomify-api-1`, `roomify-frontend-1`. Obrazy: PostgreSQL 18.6-bookworm, SDK 10.0.301-noble, ASP.NET 10.0.9-noble, Node 24.19.0-bookworm-slim, Nginx 1.30.6-alpine. Ostatni build użył cache; istniejących kontenerów nie odtwarzano.

- Frontend/Nginx: http://127.0.0.1:4200, API diagnostycznie: http://127.0.0.1:5080, PostgreSQL: 127.0.0.1:5432. Porty można zmienić lokalnym .env.
- W sieci Compose używaj `api:8080` i `db:5432`. Lokalny dotnet run nie wczytuje .env i ma osobny profil HTTP 5080.
- Nginx zachowuje /api i query string, /health przekazuje do API, inne trasy mają fallback CSR. Błędy API nie są zastępowane HTML Angulara. Dev proxy Angulara obsługuje /api, ale nie /health.
- `Database__Host/Port/Name/Username/Password` trafiają do API przez Compose, lecz obecny kod ich nie odczytuje. WP-02 musi jawnie zaprojektować i podłączyć konfigurację danych, bez utrwalania haseł w kodzie lub logach.
- Wolumen `roomify_postgres_data` jest developerski i trwały; PostgreSQL 18 montuje go pod /var/lib/postgresql. Nazwa wolumenu jest stała także po zmianie nazwy projektu. Nie stosuj down -v ani resetowania bazy jako naprawy. Zmiana .env nie zmienia hasła istniejącej roli.
- Brak tabel biznesowych i migracji w repozytorium. Rola inicjalizacyjna obrazu ma uprawnienia superuser; to lokalna konfiguracja. Przed użyciem produkcyjnym potrzebne będą ograniczone uprawnienia, TLS i obsługa sekretów.

Obecne /health sprawdza host API, nie połączenie z bazą. Połączenie potwierdzono osobno przez pg_isready/pg_dump i Testcontainers. Po wdrożeniu danych określ semantykę readiness oraz strategię migracji; nie zmieniaj jej przypadkowo wraz z dodaniem DbContext.

## Testy i ich granice

| Zestaw | Ostatni rzeczywisty wynik | Co pokrywa |
|---|---|---|
| Domain.UnitTests | 0 przypadków | Gotowy projekt; brak dowodu jakości domeny. |
| Application.UnitTests | 0 przypadków | Gotowy projekt; brak zachowania do testowania. |
| API, RequiresDocker=false | 3/3, ponowny odbiór | /health: status, treść, content-type oraz dwa nieistniejące endpointy 404; WebApplicationFactory. |
| PostgreSQL, RequiresDocker=true | 1/1, ponowny odbiór | Npgsql SELECT 1 w odrębnym PostgreSQL; nie integracja API/EF. |
| Vitest | 5/5, pierwotny audyt | Layout i routing w TestBed/jsdom. |
| Playwright Chromium | 2/2, pierwotny audyt | Frontend /home, 404 i powrót; bez API i bazy. |
| Stryker | CONFIGURED / NOT APPLICABLE | Pusta domena, brak mutation score; nie deklaruj sukcesu mutacji. |
| Manualne | 3 NOT RUN | Scenariusze są opisane, brak protokołu wykonania. |

Fixture PostgreSQL losuje hasło i port, używa `roomify_tests`, nie czyta .env i nie montuje developerskiego wolumenu. W odbiorze zaobserwowano osobny port 52627 i anonimowy wolumen; kontener testowy usunięto. Znormalizowany dump developerskiej bazy i identyfikator klastra były identyczne przed testem i po nim. Zachowaj tę izolację po dołączeniu WebApplicationFactory do bazy testowej. Przyszłe testy migracji, persystencji, Identity, transakcji i współbieżności muszą używać prawdziwego PostgreSQL, bez zastępowania go EF InMemory.

Podstawowe polecenia z katalogu głównego:

```powershell
docker compose config --quiet
docker compose ps
dotnet restore backend/Roomify.slnx
dotnet build backend/Roomify.slnx --no-restore
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=false"
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=true"
```

W frontend: `npm ci`, `npm run build`, `npm run test:ci`, `npx playwright install chromium`, `npm run e2e`. Playwright startuje własny serwer na 4300. W tym hoście Node jest dostarczany przez runtime Codex, a npm jest lokalną kopią w ignorowanym artifacts/wp013; przenośny projekt używa standardowej instalacji Node/npm. .NET i Docker są dostępne w PATH, Git z runtime wymaga tutaj opcji `-c safe.directory=D:/Projekty/ROOMIFY` z powodu innego właściciela katalogu; nie jest to wymaganie aplikacji.

## Ograniczenia i następne decyzje

Brak CI, seedów, kont użytkowników i publicznej rejestracji. Cookies są wybraną architekturą, lecz nie są zaimplementowane. Przy auth zaprojektuj CSRF, właściwości cookies, zaufanie do forwarded headers i unieważnianie dostępu konta nieaktywnego. Ochrona kolizji rezerwacji musi być wymuszona również przez bazę/transakcję zgodnie z ADR-008; szczegóły mechanizmu nie są jeszcze wybrane. Zachowaj wymagania czasu i status propozycji WP-00; nie dopisuj funkcji poza zleceniem WP-02.1.

## Git i przekazanie

Przed zamknięciem nie istniał lokalny HEAD; pierwszy commit zamknięcia to `chore: initialize Roomify architecture and testing infrastructure` po pozytywnym audycie. Identyfikator sprawdź przez `git log -1`. Commit obejmuje 107 sprawdzonych plików: cały dotychczasowy szkielet oraz dokumentację zamknięcia. .env, node_modules, bin, obj, raporty i artifacts pozostają ignorowane; .env.example jest publicznym wzorcem.

Origin wskazuje `https://github.com/HideOnPriest/ROOMIFY.git`. Wcześniejsza, osobno zlecona operacja publikacji utworzyła na GitHub commit inicjalizacyjny `f9becee1830c10565af132571b67072c93aee34f` z samym .gitignore. Nie oznacza to publikacji całego projektu. Lokalny pierwszy commit ma niezależną historię; przed przyszłym push trzeba sprawdzić i uzgodnić stan zdalny. W tym zamknięciu nie wykonano push ani zmian zdalnych. Nie wymuszaj nadpisania zdalnej historii.

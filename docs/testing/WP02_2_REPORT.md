# WP-02.2 — EF Core i mapowanie PostgreSQL: odbiór

Data: 17.10.2026. **WP-02.2 wykonane, 26/26 AC PASS.** Model EF buduje się i potrafi materializować istniejące encje. Nie utworzono tabel, migracji, Identity ani endpointów biznesowych. Nie wykonano commit ani push. Zastane niezacommitowane zmiany WP-02.1 zachowano; bazowy HEAD nadal fb429d4.

## Pliki i pakiety

Dodane: Persistence/RoomifyDbContext.cs, Configurations/RoomConfiguration.cs, ReservationConfiguration.cs, ReservationStatusStorage.cs w Infrastructure; PersistenceModelTests.cs, PersistenceRegistrationTests.cs, PersistenceMaterializationTests.cs w istniejącym projekcie integracyjnym; [PERSISTENCE.md](../architecture/PERSISTENCE.md) i niniejszy raport.

Zmiany kodu: Infrastructure.csproj (EF Core 10.0.9, Npgsql provider 10.0.3 i podniesienie istniejącego DI.Abstractions do 10.0.9), InfrastructureServiceCollectionExtensions.cs (konfiguracja i Scoped DI), Api/Program.cs (przekazanie IConfiguration), Domain/Reservations/Reservation.cs (niezbędna materializacja i pola), Domain.UnitTests/ReservationTests.cs (dwa dodatkowe testy), komentarz w docker-compose.yml. Zaktualizowano README, IMPLEMENTATION_STATUS i powiązane opisy architektury/testów. Nie zmieniono pakietów testowych, plików WP-00, Domain.csproj, Application ani samych Room/ReservationPeriod.

Pełna lista aktualizowanej dokumentacji poza dwoma nowymi plikami: README.md, docs/ai/IMPLEMENTATION_STATUS.md, docs/ai/WP02_HANDOFF.md (uwaga o historii), docs/architecture/DOMAIN.md, BACKEND.md, DOCKER.md, OVERVIEW.md oraz docs/testing/UNIT_TESTS.md, INTEGRATION_TESTS.md i README.md. Pozostałe zastane zmiany WP-02.1 zachowano.

Nie instalowano Design ani dotnet-ef: brak obecnej potrzeby generowania migracji. Zależności przechodnie są jawne: EF.Relational 10.0.4, EF.Abstractions 10.0.9, Npgsql 10.0.3. Wersje mieszczą się w zakresie providera; restore bez ostrzeżeń. Pierwsze restore ujawniło NU1605 dla starego DI.Abstractions 10.0.0; rozwiązano przez podniesienie wyłącznie tej zależności Infrastructure, bez tłumienia ostrzeżenia.

## Mapowanie i materializacja

Schemat **modelu**: rooms (PK id uuid; wymagane name varchar100, location varchar150, capacity int, is_active bool, created_at timestamptz; description text NULL) oraz reservations (PK id uuid; room_id/user_id uuid, title varchar120, status varchar9, created_at/start_at/end_at timestamptz wymagane; updated_at timestamptz NULL). FK room_id → rooms.id ma Restrict. UserId nie ma relacji do Identity. Status zapisuje jawne `active`/`cancelled` i odrzuca nieznane wartości. Brak globalnych filtrów.

Unikalność rooms.name bez rozróżniania wielkości liter deklaruje indeks z niedeterministyczną kolacją ICU. Zwykłe indeksy reservations obejmują (room_id, start_at, end_at), (user_id, start_at) i (start_at, end_at). Nie implementują exclusion constraint ani ochrony współbieżności. Diagram i wszystkie kolumny opisuje PERSISTENCE.md.

Próba preferowanego ComplexProperty nie powiodła się: obecny konstruktor Reservation ma niemapowalne parametry period i now. Dodatkowym ograniczeniem EF10 jest brak indeksów na polach complex. Zastosowano dopuszczone jawne mapowanie StartAt/EndAt na _startAt/_endAt. Period jest obliczanym niemutowalnym value object, bez duplikacji stanu i bez nowej encji. Prywatny konstruktor skalarny umożliwia standardowe wiązanie EF; waliduje dotychczasowe identyfikatory/tytuł oraz geometrię Period. Odczyt dawnych danych nie podlega regule przyszłości. Nie dodano publicznych setterów ani refleksji. Pierwszy nieudany probe zachowano w artifacts, zamiast przedstawiać go jako sukces.

## Wykonane kontrole

Dowody: ignorowany `artifacts/wp022/commands.json`, logi, trx/domain.trx, application.trx, ef.trx i http.trx. Czasy oznaczają całe polecenia. Użyto .NET SDK 10.0.301; źródła nuget.org i dokumentacja producentów wskazane w PERSISTENCE.md.

| Kontrola | Rzeczywisty wynik |
|---|---|
| dotnet restore backend/Roomify.slnx | PASS, kod 0, 0,735 s. |
| Build Debug --no-restore | PASS, 0 ostrzeżeń i błędów, 1,297 s. |
| Build Release --no-restore | PASS, 0 ostrzeżeń i błędów, 1,781 s. |
| Domain.UnitTests --no-build --no-restore | **87/87 PASS**, 0 failed/skipped, 1,359 s. |
| Application.UnitTests --no-build --no-restore | Kod 0, **0 przypadków**, 1,110 s; nie jest to dowód jakości przypadków użycia. |
| IntegrationTests, Category=PersistenceMetadata | **35/35 PASS**, 0 failed/skipped, 1,968 s. |
| IntegrationTests, FullyQualifiedName~HealthCheckTests | **3/3 PASS**, 0 failed/skipped, 1,328 s. |
| dotnet list Infrastructure.csproj package --include-transitive --no-restore | PASS, kod 0, 0,641 s; rzeczywiste wersje zapisane w packages.log. |
| docker compose config --quiet / ps --all | Kod 0; istniejące trzy kontenery running, db i api healthy; nie przebudowywano/odtwarzano usług. |
| Lista tabel użytkownika z pg_tables | Pusta; brak tabel biznesowych. Wyłącznie SELECT. |
| Logiczny dump developerskiej bazy przed i po kontroli | Identyczny SHA-256 `eecd1a3cab6c14d496cdc5600f591e3a8ea96f0ac7d40ba6042195d90d7d2ab5`; pominięto tylko losowe znaczniki pg_dump restrict/unrestrict. |

Łącznie końcowa regresja: **125/125 zaliczonych przypadków** (87 + 35 + 3), bez sumowania wcześniejszych prób. Domain zachowuje 85 istniejących przypadków i dodaje 2. Testy integracyjne wymagające Dockera, frontend, mutacje i coverage nie były ponownie wykonywane w tym pakiecie. Score 84,31% i pokrycie z WP-02.1 są historyczne, nie nową oceną zmienionego kodu.

35 testów EF obejmuje model i klucze, nazwy/typy/nullability/długości kolumn, relację i indeksy, brak dodatkowych encji/filtrów, converter statusu, generowanie SELECT, DI, pierwszeństwo konfiguracji, hasło z separatorami i Scoped lifetime. Publiczne interceptory EF podają kontrolowany DbDataReader i tłumią otwarcie połączenia: rzeczywisty materializator odtwarza dawne Active/Cancelled, archiwalną salę z właściwym Id, UTC i metadane, a niepoprawny przedział odrzuca. Osobno sprawdzono change tracking po ChangePeriod. Nie jest to InMemory ani deklaracja odczytu/zapisu tabel PostgreSQL.

Test WebApplicationFactory uruchamia bieżący kod API z konfiguracją bazy na niedostępnym porcie 1; /health zwraca 200 Healthy, a DbContext buduje model przy zamkniętym połączeniu. Działające wcześniej kontenery nadal używają starego obrazu; nie traktujemy ich healthchecka jako dowodu nowego DI. Konstrukcja modelu, testy i źródła nie wywołują migracji/EnsureCreated/SaveChanges. Bazy nie modyfikowano.

Końcowa kontrola static.json potwierdziła zgodność referencji siedmiu projektów, brak EF w Domain i zależności Application → Infrastructure, brak pakietów Identity i plików migracji oraz brak wywołań inicjalizacji schematu/zapisu w źródłach. Cztery pliki WP-00 pozostają bajtowo zgodne z oryginalnym ZIP. Sprawdzono lokalne odnośniki Markdown, git diff --check i brak lokalnego hasła w plikach widocznych dla Git. Artefakty/bin/obj pozostają ignorowane, indeks jest pusty, HEAD nie zmienił się. Zmiany istniejących źródeł względem zapisanych sum początku WP-02.2 mieszczą się w opisanym zakresie.

## Kryteria akceptacji

| ID | Wynik | Dowód / granica |
|---|---|---|
| AC-001 | PASS | EF Core 10.0.9 w Infrastructure. |
| AC-002 | PASS | Provider Npgsql 10.0.3, zgodny graf NuGet. |
| AC-003 | PASS | RoomifyDbContext : DbContext. |
| AC-004 | PASS | DbSet Room i Reservation. |
| AC-005 | PASS | Osobne IEntityTypeConfiguration Room. |
| AC-006 | PASS | Osobne IEntityTypeConfiguration Reservation. |
| AC-007 | PASS | Pola start/end i walidowana materializacja Period; bez ComplexProperty z wyjaśnionej przyczyny. |
| AC-008 | PASS | Wszystkie daty timestamptz; UTC w domenie i testach materializacji. |
| AC-009 | PASS | Wymagany FK RoomId do Room, DeleteBehavior.Restrict. |
| AC-010 | PASS | Indeksy sali/użytkownika/okresu, unikalna nazwa z kolacją. |
| AC-011 | PASS | Publiczne API i reguły nowych operacji zachowane; 87 testów domeny. |
| AC-012 | PASS | Domain.csproj i zależności bez zmian, brak EF w Domain. |
| AC-013 | PASS | Application nadal zależy wyłącznie od Domain. |
| AC-014 | PASS | ConnectionStrings:Roomify lub Database:*, bez hasła w kodzie produkcyjnym. |
| AC-015 | PASS | Scoped DI, test tożsamości kontekstu w zakresach. |
| AC-016 | PASS | Nowy host API działa przy niedostępnym serwerze. |
| AC-017 | PASS | Istniejące HTTP 3/3 i dodatkowy test liveness. |
| AC-018 | PASS | 35 testów EF/DI/materializacji bez Dockera. |
| AC-019 | PASS | Debug bez błędów i ostrzeżeń. |
| AC-020 | PASS | Release bez błędów i ostrzeżeń. |
| AC-021 | PASS | Zachowane 85 testów Domain i 2 nowe, 87/87. |
| AC-022 | PASS | Brak migracji, EnsureCreated i tabel biznesowych; lista pg_tables pusta. |
| AC-023 | PASS | Brak pakietów i modelu Identity. |
| AC-024 | PASS | PERSISTENCE.md obejmuje pojęcia, schemat, decyzje, konfigurację i testy. |
| AC-025 | PASS | Tylko odczyt bazy; dump bez zmian, brak operacji na wolumenach. |
| AC-026 | PASS | Raport oparty na logach/TRX i kontroli repozytorium, z jawnymi ograniczeniami. |

## Ograniczenia i WP-02.3

Nie ma wdrożonego schematu, zatem unikalność/relacja/typy są na razie deklaracją modelu, bez testu wykonywania ograniczeń przez serwer. Nie przeprowadzano round-trip do tabel. Kolacja wymaga ICU przy przyszłym wdrożeniu; timestamptz nie przechowuje identyfikatora strefy, Npgsql wymaga offsetu zero, a mikrosekundowa precyzja bazy może zaokrąglić metadane czasu poniżej mikrosekundy. Brak gotowej obsługi konfliktów i uprawnień.

**WP-02.2 zakończono. WP-02.3 nie rozpoczęto.** Kolejny pakiet według obecnego zlecenia wprowadzi Identity, ale nie tworzymy teraz użytkowników, logowania ani ich relacji. Migracje, faktyczny zapis i zabezpieczenie współbieżności wymagają odrębnego zakresu. Zmiany pozostają w katalogu roboczym, bez git add/commit/push.

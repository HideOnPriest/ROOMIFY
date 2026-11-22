# Testy integracyjne

## Cel, narzędzia i odpowiedzialność

Projekt `Roomify.Api.IntegrationTests` na .NET 10 sprawdza współpracę rzeczywistych elementów. Używa xUnit, Microsoft.AspNetCore.Mvc.Testing 10.0.9, Testcontainers.PostgreSql 4.16.0 i Npgsql 10.0.3.

WebApplicationFactory składa host API, DI, routing i Health Checks, ale żądania HttpClient trafiają do TestServer w pamięci. Nie startuje osobny Kestrel, nie rezerwujemy portu TCP i nie potrzebujemy bazy. Publiczna częściowa klasa Program udostępnia punkt wejścia bez zmiany zachowania endpointów. Testowe ustawienia wyłączają zależność od portu HTTPS hosta.

`HealthCheckTests` ma `RequiresDocker=false`. Weryfikuje 200, dokładny tekst Healthy i `text/plain`, a parametryzowany test sprawdza dwa adresy 404. To integracja, ponieważ współdziałają host, konfiguracja, middleware i endpoint, a nie izolowana metoda.

## Fixture PostgreSQL

Fixture to obiekt środowiska testu zarządzany przez xUnit. `PostgreSqlFixture` przez `IAsyncLifetime` uruchamia odrębny `postgres:18.6-bookworm` z własną bazą, użytkownikiem i losowym hasłem. Port hosta jest dynamiczny, a connection string pochodzi z Testcontainers. Nie odczytuje `.env` i nie montuje developerskich wolumenów.

Test z `RequiresDocker=true` otwiera połączenie Npgsql, wykonuje SELECT 1 i wymaga wyniku int równego 1. `await using` zamyka komendę i połączenie, a fixture usuwa kontener przez `DisposeAsync()`, również przy błędzie częściowego startu. Nie wyłączono domyślnego sprzątania Testcontainers. Sam test SELECT 1 nie tworzy tabel; od WP-02.4 osobna klasa sprawdza pełne migracje w odrębnych bazach.

## Osobne uruchamianie i raporty

Po restore, z katalogu głównego:

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=false" --logger "trx;LogFileName=api.trx" --results-directory artifacts/test-results/api
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=true" --logger "trx;LogFileName=postgresql.trx" --results-directory artifacts/test-results/postgresql
```

Filtr działa przed tworzeniem fixture, dlatego testy HTTP nie startują kontenera. PostgreSQL wymaga działającego Dockera Linux, ale nie `docker compose up`. Pokrycie HTTP można uzyskać przez `--collect:"XPlat Code Coverage" --results-directory artifacts/coverage/api`.

## Interpretacja i ograniczenia

TRX zapisuje rzeczywiste wyniki. Brak Dockera daje failed w runnerze i środowiskowe BLOCKED w raporcie pracy; nie ma automatycznego skip ani PASS. W WP-01.5 i pierwotnym audycie WP-01.6 test PostgreSQL napotkał niedostępny `docker_engine`. Po uruchomieniu Docker Desktop 17.10.2026 ponowiono RequiresDocker=true: **1/1 PASS**, rzeczywiste SELECT 1, osobny port 52627, odrębny anonimowy wolumen i usunięcie kontenera potwierdzone. Porównanie logicznego dumpa developerskiej bazy przed i po teście nie wykazało zmiany. Ponowione testy HTTP: **3/3 PASS**. Szczegóły i granice dowodu opisuje [audyt, sekcja 9](WP01_AUDIT_REPORT.md#9-ponowny-odbiór-po-uruchomieniu-docker-desktop--17102026).

W WP-02.2 dodano EF Core/DbContext/DI, a w WP-02.3 Identity/cookies, na tamtych etapach bez migracji i tabel. WP-02.4 wprowadza schemat; operacji API na danych nadal brak. SELECT 1 jest kontrolą infrastruktury, nie reguł biznesowych.

## Metadane, DI i materializacja EF — WP-02.2

Dodano PersistenceModelTests, PersistenceRegistrationTests i PersistenceMaterializationTests z Category=PersistenceMetadata i RequiresDocker=false: **35/35 PASS**. Nie wymagają Dockera ani schematu. Testy używają rzeczywistego providera Npgsql i modelu relacyjnego; odczyty materializacyjne zastępują tylko transport publicznymi interceptorami EF. Nie jest to test SQL na serwerze ani provider InMemory. API /health działa z niedostępną bazą, a połączenie pozostaje zamknięte. Kontekst bez connection stringa również buduje model.

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "Category=PersistenceMetadata"
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "FullyQualifiedName~HealthCheckTests"
```

W WP-02.2 RequiresDocker=false obejmował 38 przypadków. Po WP-02.3 obejmuje **72** (35 EF + 34 Identity/security + 3 dotychczasowe HTTP). Historycznie w WP-02.3 RequiresDocker=true wybierał wyłącznie SELECT 1; od WP-02.4 wybiera 28 przypadków, opisanych poniżej. Nie wykonano EnsureCreated, migracji ani SaveChanges. [PERSISTENCE.md](../architecture/PERSISTENCE.md) opisuje podstawy mapowania.

## Identity i cookies — WP-02.3

Category=IdentityConfiguration wybiera 34 testy, wszystkie PASS: dokładna lista encji, klucze Guid i złożone Identity, Restrict dla historii, wspólny Scoped kontekst w stores, managery, polityka haseł/blokady i rzeczywiste cookie/401/403. ApplicationUser jest inicjalizowany również z kontrolowanym zegarem. Nie ma kont testowych w PostgreSQL. IdentityTestFactory dołącza wyłącznie testowy kontroler z biletem User i efemeryczne Data Protection; standardowy host zwraca dla tych adresów i ścieżek auth/rejestracji 404 w GET i POST. Nie osłabiono security stamp validatora ani ustawień produkcyjnych.

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "Category=IdentityConfiguration"
```

Dotychczasowe 35 przypadków EF i 3 HTTP ponownie przeszły; oczekiwanie dwóch encji rozszerzono do dokładnej listy dziewięciu oraz dodatkowego FK, zachowując wszystkie asercje biznesowych mapowań. Szczegóły, ograniczenia i 30 AC: [IDENTITY.md](../architecture/IDENTITY.md), [WP02_3_REPORT.md](WP02_3_REPORT.md).

## Migracje i integralność PostgreSQL — WP-02.4

18.10.2026: **100/100** testów całego projektu integracyjnego, 0 failed/skipped: 72 dotychczasowe bez Dockera + 28 Docker (SELECT 1 oraz 27 nowych migracji). Domain osobno 87/87; Application 0 przypadków. Testy pracują na PostgreSQL 18.6, bez SQLite/InMemory. [Pełny raport i AC](WP02_4_REPORT.md), [schemat i ERD](../architecture/DATABASE.md).

`PostgreSqlMigrationTests` ma Category=DatabaseMigrations i RequiresDocker=true. Jego fixture startuje własny kontener, a `MigrationDatabase` tworzy osobną bazę roomify_mig_<Guid> na każdy przypadek. Connection string pochodzi wyłącznie z fixture, pooling jest wyłączony; nie ma odczytu .env, wskazania db:5432 ani montowania roomify_postgres_data. Konteksty i połączenia są zwalniane przed usunięciem własnej bazy; fixture usuwa kontener także po niepowodzeniu. Nie wyłączono globalnej równoległości. Fabryki przygotowują techniczne rekordy Identity bez haseł, sale i rezerwacje; czas kontroluje niezmienny zegar UTC 2030-01-01.

MIG-001–005 sprawdzają Up na pustej bazie, dokładną listę tabel, PK/FK, indeksy/CHECK/exclusion/kolację/rozszerzenie i ponowne Migrate zachowujące dane. MIG-006–009 badają odwrócony i zerowy okres, cztery kształty nakładania, dwie granice styku oraz anulowanie i konflikt reaktywacji w SQL. MIG-010 uruchamia dwie transakcje na niezależnych połączeniach ze wspólną barierą, limitem 20 s i dokładnie jednym commit. MIG-011 potwierdza 23P01 oraz nazwę ograniczenia w InnerException EF. MIG-012/013 sprawdzają brak sali/użytkownika, MIG-014 Restrict dla usunięcia obu encji, MIG-015 round-trip UTC/statusu/okresu i powiązanych danych. Dodatkowe przypadki obejmują Down → Up, identyczny termin różnych sal, trzy CHECK sal i unikalność ICU/nieznany status.

W pierwszej pełnej regresji MIG-010 ujawnił 40P01 zamiast pierwotnie oczekiwanego wyłącznie 23P01: serwer przerwał jedną transakcję, druga wykonała commit. Asercja uwzględnia teraz oba prawidłowe mechanizmy odrzucenia, nadal wymagając dokładnie jednego aktywnego rekordu. Osobny MIG-011 nadal bezwarunkowo wymaga 23P01. Końcowa pełna regresja przeszła 100/100; nie ukryto pierwszego wyniku jako sukcesu.

Rollback wywołuje IMigrator do InitialDatabase tylko w jednorazowej bazie testu. Sprawdza usunięcie dziewięciu tabel i kolacji, pustą historię EF, zachowanie współdzielonego btree_gist i ponowne Up. **Nie wolno wykonywać tego rollbacku w bazie developerskiej z danymi.** Nie ma EnsureCreated/EnsureDeleted ani down -v.

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --filter "Category=DatabaseMigrations" --logger "trx;LogFileName=migrations.trx" --results-directory artifacts/wp024/trx
dotnet test backend/tests/Roomify.Api.IntegrationTests --filter "RequiresDocker=true"
dotnet test backend/tests/Roomify.Api.IntegrationTests --logger "trx;LogFileName=regression.trx" --results-directory artifacts/wp024/trx
```

Logiczny dump developerski przed i po obu pełnych przebiegach (w tym nieudanym) pozostał identyczny, wraz z identyfikatorem klastra i pustą listą tabel. Dopiero potem osobna operacja EF zastosowała InitialCreate do sprawdzonej roomify_dev; testy nie są odpowiedzialne za zmianę jej schematu. Po testach brak kontenerów Testcontainers i baz roomify_mig w środowisku developerskim. TRX, logi i wygenerowany SQL są lokalne w ignorowanym artifacts/wp024 i nie trafiają do Git.

## Odbiór WP-02.5 — 18.10.2026

Bieżący pełny zestaw: **114/114 PASS**, 0 failed/skipped; 72 bez Dockera i 42 RequiresDocker=true. Te 42 to **38 DatabaseMigrations + 3 DatabaseAudit + SELECT 1**. Dotychczasowe wyniki WP-02.4 powyżej są historią, nie aktualną liczbą przypadków.

MIG-010 ma teraz Theory z 12 próbami: identyczny termin albo przesunięty o 15/30 minut, po cztery warianty. Oddzielne połączenia i transakcje, wspólna bariera, limit 20 s, brak Task.Delay. ITestOutputHelper zapisuje oba rezultaty i liczbę aktywnych wierszy w TRX. Dwie pełne serie dały 24 commit i 24 odrzucenia z 48 prób zapisu: 9 × 23P01 i 15 × 40P01, zawsze po 1 aktywnym wierszu. Timeout i nieznany błąd powodują FAIL. MIG-011 nadal wymaga 23P01 i nazwy ograniczenia.

PostgreSqlSchemaAuditTests (Category=DatabaseAudit) dodaje trzy testy: porównanie wszystkich kolumn (nazwy/typy/nullability) i uporządkowanych PK z modelem EF oraz fizycznej definicji exclusion; rzeczywiste UserManager/stores, normalizację i hash oraz unikalność emaila; UPDATE własnego okresu i atomowość odrzuconej edycji kolidującej z drugim rekordem. Każdy korzysta z osobnej bazy w PostgreSQL fixture, bez .env i developerskiego wolumenu. Tylko te izolowane bazy otrzymują technicznych użytkowników i hasła; całość jest sprzątana. Produkcyjnych endpointów auth/biznesowych nie dodano.

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --logger "trx;LogFileName=integration-final.trx" --results-directory artifacts/wp025/trx
# Możliwe selekcje zamiast pełnego przebiegu:
dotnet test backend/tests/Roomify.Api.IntegrationTests --filter "Category=DatabaseAudit"
dotnet test backend/tests/Roomify.Api.IntegrationTests --filter "FullyQualifiedName~MIG010"
```

Wykonano pełny przebieg 113/113, potem po dodaniu testu edycji 114/114; obie serie i kody zachowano w osobnych TRX. Pierwszy nowy test audytu nie kompilował się wskutek brakującego using Metadata; log integration.log zachowano, import poprawiono przed uruchomieniem obu poprawnych przebiegów. Końcowy czas runnera 17 s, sesja TRX 18,408 s. Developerki nie migrowano ani nie seedowano: logiczny dump i historia przed/po identyczne. Po testach brak kontenerów testowych. [Pełny odbiór i 30 AC](WP02_ACCEPTANCE_REPORT.md), [macierz FR/BR](REQUIREMENTS_TRACEABILITY.md).

## Tworzenie rezerwacji przez Application — WP-03.1

18.10.2026: **138/138 PASS** w pełnej regresji, 75 RequiresDocker=false i 63 RequiresDocker=true. Zachowano 114 wcześniejszych przypadków, dodano 21 PostgreSQL (Category=ReservationApplication) i 3 kompozycji/awarii połączenia (Category=ApplicationComposition, bez Dockera).

Nowa klasa ReservationApplicationTests wykonuje rzeczywisty serwis, DI, repozytoria EF i InitialCreate. INT-RES-001–009 obejmują poprawny zapis/UTC i SQL EXISTS, 5 układów konfliktu, 2 styki, cancelled, salę nieaktywną/brakującą, 6 równoległych prób, właściwe tłumaczenie 23P01 po nieaktualnym SELECT oraz FK/inny exclusion niemaskowane jako konflikt. Dodatkowo sprawdza inną salę. Każdy przypadek ma własną bazę Testcontainers; techniczne zmiany sal/użytkowników/constraint dotyczą wyłącznie tej prywatnej bazy.

Bariera nowego INT-RES-007 czeka na dwie prawdziwe kontrole dostępności, po czym dopuszcza równoległe zapisy na niezależnych zakresach/DbContext/połączeniach. Limit 20 s, brak Task.Delay. Asercje wymagają 1 Success, 1 aktywnego wiersza i kontrolowanego wyniku zgodnego z rzeczywistym kodem z interceptora: 23P01 właściwego exclusion → Conflict, 40P01 → InfrastructureFailure. Inny kod/timeout nie jest zaliczany. Dwie poprawne serie: 12 scenariuszy, 24 wywołania, 12 sukcesów, 11 konfliktów i 1 odrębny błąd infrastruktury; dane developerki bez zmian.

Pierwszy podzbiór 21/23 (2 FAIL) zachowano osobno; naprawiono obsługę dodatkowego opakowania błędu przejściowego Npgsql oraz normalizację danych testu innego exclusion. Potem 24/24 nowego podzbioru i 280/280 całego backendu. Nie osłabiono istniejących testów ani nie ukryto pierwszego wyniku.

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --filter "Category=ReservationApplication|Category=ApplicationComposition"
dotnet test backend/Roomify.slnx --no-restore --logger "trx;LogFilePrefix=wp031" --results-directory artifacts/wp031/regression
```

Testy nie są odbiorem kontrolera HTTP tworzenia rezerwacji — takiego kontrolera nie dodano. Szczegóły: [architektura](../architecture/RESERVATION_APPLICATION.md), [pełny raport](WP03_1_REPORT.md).

## Zarządzanie rezerwacjami i xmin — WP-03.2

Bieżący zestaw **159/159 PASS**: 75 bez Dockera, 84 PostgreSQL. Nowe **21/21** Category=ReservationManagement obejmuje INT-UPD-001–007, INT-CAN-001–005 i INT-SEC-001–004, a także Up/Down nowej migracji xmin z zachowaniem danych. Są to prawdziwe serwisy, repozytoria, migracje i osobne bazy Testcontainers, nie InMemory/SQLite i nie developerka.

Trzy warianty współbieżności (update/update, update/cancel, cancel/cancel) po trzy przypadki. Bariera jest po dwóch realnych odczytach ze śledzeniem; osobne konteksty/połączenia, limit 20 s, bez Task.Delay. Wymagany jest jeden Success, jeden ConcurrentModification i kompletny zapis zgodny ze zwycięzcą. Timeout/InfrastructureFailure nie jest zaliczany. Nowy podzbiór i końcowa regresja razem 18/18 scenariuszy. Sekwencyjne powtórzenie Cancel daje AlreadyCancelled bez UPDATE i zmiany xmin/UpdatedAt. Odrębny 23P01 dla terminu pozostaje osobną asercją.

Dawne testy metadanych/materializacji i schematu zachowują sprawdzanie 9 biznesowych właściwości, dodają jedną shadow xmin oraz weryfikują systemowy atrybut attnum < 0 zamiast udawać nową kolumnę użytkownika. MIG-001 sprawdza najpierw samą InitialCreate, potem dwie dokładne migracje; MIG-005 zachowuje dane przy ponownym Migrate. Pierwsza pełna regresja 361 PASS/1 FAIL ujawniła stary test z surowym UPDATE i starą wersją EF; dodano Reload i asercję nowego tokenu, zachowując wymóg 23P01 dla dalszej edycji. Logi pierwszego wyniku zachowano.

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --filter "Category=ReservationManagement"
dotnet test backend/Roomify.slnx --no-restore --logger "trx;LogFilePrefix=wp032-final" --results-directory artifacts/wp032/regression-final
```

Developerka ma identyczny dump i historię przed/po. Nowa migracja modelu/historii jest przygotowana i przetestowana, nie stosowana do roomify_dev. [Wyniki, kody i 25 AC](WP03_2_REPORT.md), [RESERVATION_MANAGEMENT.md](../architecture/RESERVATION_MANAGEMENT.md).

## Przyrost WP-03.3 — 18.10.2026

Bieżące wykonanie **187/187**: RequiresDocker=false **76**, RequiresDocker=true **111**. Przyrost: 1 test rejestracji 6 usług i 27 realnego PostgreSQL. INT-ROOM-001–014 obejmują zapis/odczyt/edycję, archiwizację bez utraty historii, aktywację, filtry SQL, 8 sortowań, wyścig nazw, dwa wyścigi xmin, ograniczenia i izolację. Dodatkowo role na realnych repozytoriach, Update DuplicateName, klasyfikacja PK/CHECK i up/down migracji. Fizyczny DELETE testowany wyłącznie w izolacji jest blokowany przez RESTRICT/23001. Dump i historia developerki przed/po identyczne. Wcześniejsze wyniki zachowane jako historia. [Raport](WP03_3_REPORT.md).


## Przyrost WP-03.4 — 18.10.2026

**211/211**: RequiresDocker=false **78**, RequiresDocker=true **133**. Przyrost 2 bez Dockera i 22 PostgreSQL: 15 INT-USR, atomic role/account, partial-email rollback, ConcurrencyStamp, last-admin race, 6 sortowań SQL i rzeczywisty potok cookies. Historyczny bilet bez konta/stamp jest teraz odrzucany 401; wcześniejszy dowód 200/403 przeniesiono na prawdziwe konta Testcontainers i rozszerzono o dezaktywację/reaktywację. To nie test produkcyjnego endpointu logowania. Developerka identyczna przed/po.

[Pełny raport](WP03_4_REPORT.md), [architektura kont i sesji](../architecture/USER_MANAGEMENT.md). Poprzednie wyniki zachowano jako historyczne.

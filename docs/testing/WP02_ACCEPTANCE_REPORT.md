# Odbiór WP-02 — domena i baza danych, WP-02.5

**Data: 18.10.2026. Ocena: READY WITH WARNINGS dla zakresu WP-02.** Wszystkie 30 kryteriów WP-02.5: PASS w opisanym zakresie. Nie oznacza to ukończenia MVP, autoryzacji zasobów ani gotowości do publicznego wdrożenia. WP-03 nie rozpoczęto. Nie wykonano commit ani push.

## Zakres i wynik audytu

Sprawdzono dokumenty WP-00, historyczny handoff WP-01, Domain/Persistence/Identity/Database, wszystkie encje i value object, konfiguracje EF/Identity, InitialCreate/designer/snapshot oraz istniejące testy. Porównano model z faktycznym schematem PostgreSQL i ponownie wykonano testy zamiast polegać na wcześniejszych raportach.

Potwierdzono: tworzenie/edycję/archiwizację Room i zachowanie tożsamości, walidację identyfikatorów/tytułu oraz cykl Active → Cancelled, idempotencję Cancel, niezmienność Period i hermetyzację modelu. Room powstaje aktywny; nie dodano niezamówionej reaktywacji zarchiwizowanej sali. Polityka czasu używa TimeProvider, rzeczywistych chwil UTC i Europe/Warsaw; granice 08–20, 8 h, pełne minuty, przyszłość, jeden lokalny dzień oraz obie zmiany DST przechodzą testy. Wymagań WP-00, produkcyjnego kodu domeny/mapowań/Identity/API ani zastosowanej migracji nie zmieniono.

Baza rzeczywiście chroni wymagane pola, klucze, unikalność nazw/emaili, dozwolone statusy i okresy oraz brak nakładania aktywnych rezerwacji jednej sali. Potwierdzono zachowanie FK Restrict i historii, przypadki identycznych/zawartych/częściowo nakładających się terminów, granice styku, różne sale, cancelled, INSERT i UPDATE. Rozpoznanie konfliktu przez EF zachowuje SqlState 23P01 i nazwę constraint.

[Macierz FR/BR/EDGE/NFR](REQUIREMENTS_TRACEABILITY.md) rozróżnia dowody modelu/bazy od brakujących funkcji produktu. W szczególności brak kontroli aktywności sal/kont i właściciela w przypadkach użycia nie został uznany za zrealizowaną regułę na podstawie samego FK czy testowego kontrolera cookies. Nie dodano endpointów, logowania, UI, seeda, powiadomień ani CI.

## Uzupełnienia i znalezione problemy

| Problem / luka | Priorytet w tym zakresie | Działanie i status |
|---|---|---|
| Bieżący mutation score wynosił 76,64%, poniżej celu; wcześniejszy wynik 84,31% dotyczył innej wersji kodu | P2 | Dodano 23 przypadki DomainContractTests; końcowy wynik 93,46%. CLOSED |
| Brak testów jawnych null dla zegara/okresu, w tym po anulowaniu | P2 | 11 danych Theory sprawdza ArgumentNullException, ParamName i zachowanie stanu. CLOSED |
| Testy nie wymagały niepustego wyjaśnienia błędu domenowego | P3 | 10 danych Theory sprawdza kod/kategorię i niepusty komunikat, bez utrwalania dokładnego brzmienia. CLOSED |
| Walidacja strefy była testowana głównie przez statyczną politykę Reservation | P2 | Dwa przypadki osobno tworzonej ReservationTimePolicy na dniach obu zmian DST. CLOSED |
| Jedna próba współbieżności bez zapisu rzeczywistych kodów | P2 | MIG-010 rozszerzono do 12 deterministycznie parametryzowanych przypadków i ITestOutputHelper; wykonano dwie pełne serie. CLOSED |
| Brak pełnego porównania kolumn/PK rzeczywistego schematu z modelem | P2 | Dodano audyt wszystkich 9 tabel, nazw/typów/nullability kolumn i uporządkowanych PK; snapshot i fizyczny exclusion sprawdzone. CLOSED |
| Brak testu UserManager/stores na tabelach PostgreSQL | P2 | Normalizacja, zapis i weryfikacja hasha oraz odrzucenie duplikatu emaila bezpośrednio przez indeks. CLOSED |
| Brak oddzielnego dowodu EDGE-013 i atomowości odrzuconej edycji | P2 | Jawny UPDATE obu końców bez konfliktu z samym sobą; konfliktujący ChangePeriod/SaveChanges daje 23P01, zapisany okres i UpdatedAt pozostają pierwotne. CLOSED |
| Nowy test audytu początkowo nie kompilował się: brak using Microsoft.EntityFrameworkCore.Metadata | P3 | Naprawiono import; pierwotny log integration.log zachowano. Końcowy build/test PASS. CLOSED |
| 7 mutantów przeżywa, związanych z podwójną walidacją fabryki/konstruktora | P3 | Analiza poniżej; pozostają Survived, bez wykluczania i bez zmiany biznesowej logiki. OPEN jako ograniczenie analizy |
| Przy wyścigu występuje również 40P01, brak serwisu retry | P2 przed przyszłym wdrożeniem operacji | Integralność PASS; świadomie PLANNED obsługa całej transakcji w przyszłym Application/API |

Nie wykryto usterki produkcyjnej wymagającej zmiany modelu lub nowej migracji. Dotychczasowych testów nie usunięto ani nie osłabiono. Zakres migracji, CHECK i exclusion pozostał identyczny; nowe testy zwiększają weryfikowane własności.

## Wykonane testy i czasy

Środowisko: Windows, SDK .NET 10.0.301, net10.0, EF/Identity/Design 10.0.9, Npgsql provider 10.0.3, PostgreSQL 18.6-bookworm w Docker Linux. xUnit 2.9.3, Coverlet collector 6.0.4 i lokalny Stryker.NET 5.0.0. Wymienione numery wynikają z repozytorium i przywróconych narzędzi; pakietów nie aktualizowano.

| Zestaw / kontrola | Łącznie | Passed | Failed | Skipped | Czas / dowód |
|---|---:|---:|---:|---:|---|
| Domain, z Coverlet | 110 | 110 | 0 | 0 | Runner: 76 ms; sesja TRX z kolektorem 1,745 s |
| Application | 0 | 0 | 0 | 0 | Projekt uruchomiono; runner zgłasza brak przypadków, nie PASS funkcji |
| Pełne integracyjne, pierwsze poprawne wykonanie | 113 | 113 | 0 | 0 | Runner: 18 s; sesja TRX 19,699 s |
| Pełne integracyjne po uzupełnieniu EDGE-013 | 114 | 114 | 0 | 0 | Runner: 17 s; sesja TRX 18,408 s |
| W tym: metadane/DI/materializacja EF | 35 | 35 | 0 | 0 | W końcowym zestawie |
| W tym: Identity/cookies/security | 34 | 34 | 0 | 0 | W końcowym zestawie |
| W tym: dotychczasowe HTTP /health i 404 | 3 | 3 | 0 | 0 | WebApplicationFactory/TestServer |
| W tym: PostgreSQL, cały RequiresDocker=true | 42 | 42 | 0 | 0 | 38 migracji, 3 audytu, 1 SELECT 1 |
| W tym: MIG-010 w końcowym zestawie | 12 | 12 | 0 | 0 | To część 38 przypadków migracji, nie dodatkowe testy do sumowania |
| Restore, Debug i Release | — | PASS | — | — | Całe backend/Roomify.slnx; 0 warning/error |
| has-pending-model-changes | — | PASS | — | — | Brak zmian modelu względem ostatniej migracji |

Końcowy zbiór unikalnych przypadków backendu to **224/224 (110 + 114)**; nie sumujemy powtórzeń obu przebiegów jako nowych przypadków. Testy Domain nie mają referencji do EF/Identity/PostgreSQL/Testcontainers i nie potrzebują Dockera. Application nadal nie ma przypadków użycia. Testy HTTP korzystają z TestServer; nie deklarujemy testu realnego Kestrela ani biznesowego przepływu HTTP z PostgreSQL.

### Polecenia odtworzenia

Z katalogu głównego repozytorium:

```powershell
dotnet tool restore
dotnet restore backend/Roomify.slnx
dotnet build backend/Roomify.slnx --configuration Debug --no-restore
dotnet build backend/Roomify.slnx --configuration Release --no-restore
dotnet test backend/tests/Roomify.Domain.UnitTests --no-restore --collect:"XPlat Code Coverage" --logger "trx;LogFileName=domain.trx" --results-directory artifacts/wp025/coverage
dotnet test backend/tests/Roomify.Application.UnitTests --no-restore
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --logger "trx;LogFileName=integration-final.trx" --results-directory artifacts/wp025/trx
dotnet ef migrations has-pending-model-changes --project backend/src/Roomify.Infrastructure --startup-project backend/src/Roomify.Infrastructure
```

Selektywne uruchomienia: `--filter RequiresDocker=false` (72), `--filter RequiresDocker=true` (42), `--filter Category=DatabaseMigrations` (38), `--filter Category=DatabaseAudit` (3), `--filter FullyQualifiedName~MIG010` (12). Wyniki raportowane wyżej pochodzą z pełnych przebiegów, które obejmowały te podzbiory; nie twierdzimy, że każdy filtr uruchamiano osobno.

## Migracje, EF i Identity

Pozostaje jedna migracja: **20261018093445_InitialCreate**. Nie utworzono nowej ani nie edytowano istniejącej migracji/designer/snapshot. Kontrola EF nie wykazała zmian modelu. Nowy test `AppliedMigration_ExactlyMatchesModelColumnsAndPrimaryKeys` porównuje wszystkie kolumny dziewięciu tabel (nazwy, typy i nullability) oraz PK (także kolejność kluczy złożonych) rzeczywistego PostgreSQL z modelem EF. Dawne testy nadal sprawdzają klucze Guid, wymagane pola, skalarne mapowanie Period, literały statusu, timestamptz, relacje, indeksy i Restrict.

Exclusion jest jawnym SQL, więc nie ma go w snapshot jako zwykłego indeksu EF. Nie jest to rozjazd modelu: nowy test sprawdza fizyczną definicję GiST, room_id, zakres '[)' i predicate active. Dotychczasowy MIG-004 sprawdza obecność sześciu CHECK, btree_gist, ICU i indeksów; testy negatywne sprawdzają rzeczywiste odrzucenia 23514/23503/23505/23P01. Powtórne Migrate zachowuje dane. Down i ponowne Up sprawdzono tylko w jednorazowej bazie, z zachowaniem współdzielonego rozszerzenia zgodnie z kontraktem WP-02.4.

`IdentityStores_NormalizePersistAndRejectDuplicateEmailOnRealPostgreSql` używa rzeczywistych UserManager/stores i tego samego DbContext co model biznesowy. Hasło techniczne istnieje wyłącznie jako wejście testu w usuwanej bazie; PostgreSQL przechowuje hash, a hasher poprawnie go weryfikuje. Normalizacja emaila i nazwy oraz fizyczna unikalność normalized_email działają. Nie jest to test logowania HTTP ani dezaktywacji sesji.

## Współbieżność — dwie serie, rzeczywiste kody

Każdy przypadek ma osobną bazę, dwa niezależne połączenia Npgsql i dwie transakcje. Obie są gotowe przed zwolnieniem wspólnej bariery TaskCompletionSource/Interlocked. INSERT-y są wywoływane współbieżnie, nie czekamy z drugim na commit pierwszego. Przesunięcie drugiego godzinnego terminu wynosi 0, 15 albo 30 minut; każdy wariant powtarza się czterokrotnie w serii. Nie ma Task.Delay ani globalnego wyłączenia równoległości. Limit scenariusza to 20 s, komend 10 s; timeout nie jest przechwytywany jako sukces.

| Próba / przesunięcie | Seria 1 — A / B | Seria 2 — A / B | Aktywne rekordy po każdej próbie |
|---|---|---|---:|
| 1 / 15 min | committed / 40P01 | committed / 23P01 | 1 |
| 2 / 30 min | 40P01 / committed | 40P01 / committed | 1 |
| 3 / 0 min | committed / 23P01 | 40P01 / committed | 1 |
| 4 / 15 min | committed / 40P01 | committed / 40P01 | 1 |
| 5 / 30 min | 23P01 / committed | 40P01 / committed | 1 |
| 6 / 0 min | committed / 40P01 | 40P01 / committed | 1 |
| 7 / 15 min | 23P01 / committed | 23P01 / committed | 1 |
| 8 / 30 min | committed / 40P01 | 40P01 / committed | 1 |
| 9 / 0 min | committed / 40P01 | 40P01 / committed | 1 |
| 10 / 15 min | committed / 40P01 | 23P01 / committed | 1 |
| 11 / 30 min | 40P01 / committed | committed / 23P01 | 1 |
| 12 / 0 min | committed / 23P01 | committed / 23P01 | 1 |

Łącznie **24 scenariusze, 48 prób INSERT, 24 commit i 24 odrzucenia**: 9 razy 23P01, 15 razy 40P01, zero timeoutów i podwójnych sukcesów. Seria 1: 4/8, seria 2: 5/7 odpowiednio 23P01/40P01. Po każdym przypadku dokładnie jedna aktywna rezerwacja. To empiryczne potwierdzenie w tych przebiegach, nie statystyczny dowód braku wszystkich możliwych wyścigów; trwałą gwarancję zapewnia constraint PostgreSQL.

23P01 oznacza naruszenie exclusion; 40P01 oznacza wykryty deadlock i przerwanie jednej transakcji. To różne kody, nie traktujemy ich jako identycznej odpowiedzi API. Asercje dopuszczają tylko te dwie przyczyny odrzucenia i sprawdzają liczbę commit/wierszy; sam brak wyjątku nie wystarcza. MIG-011 i test odrzuconej edycji osobno **wymagają dokładnie 23P01 i nazwy ex_reservations_active_period**. Obsługa ponowienia po 40P01 pozostaje do zaprojektowania przy serwisie. [PostgreSQL — wykrywanie deadlocków](https://www.postgresql.org/docs/18/explicit-locking.html#LOCKING-DEADLOCKS).

## Stryker.NET — zachowane oba przebiegi

Nie zmieniano konfiguracji mutacji ani jej wykluczeń. Zakres to istniejący projekt Roomify.Domain: DomainGuard, Room, Reservation, ReservationPeriod, ReservationTimePolicy i błędy/enum. Nie obejmuje Infrastructure, migracji, ModelSnapshot, DI, Program.cs, DTO ani przyszłej autoryzacji. Część plików enum nie generuje mutantów.

| Kategoria | Przed uzupełnieniem testów | Po uzupełnieniu |
|---|---:|---:|
| Wygenerowane | 133 | 133 |
| Killed / wykryte | 82 | 100 |
| Survived / niewykryte | 25 | 7 |
| NoCoverage | 0 | 0 |
| Timeout | 0 | 0 |
| RuntimeError | 0 | 0 |
| CompileError | 1 | 1 |
| Ignored — domyślny filtr narzędzia | 25 | 25 |
| Badane poprawnie kompilujące się | 107 | 107 |
| Mutation score | **76,64%** | **93,46%** |
| Czas Stryker | 28,063 s | 27,251 s |

Score końcowy: `100 / (100 + 7 + 0) × 100 = 93,46%`. CompileError i Ignored nie wchodzą do tego mianownika według definicji narzędzia. Pozostałe 25 ma rzeczywisty statusReason `Removed by block already covered filter`; to nie ręczne uznanie ich za równoważne ani wynik Killed. Nie zmieniano filtrów pomiędzy sesjami i nie dodano ignore-mutations. Jedyny CompileError to mutant **54**, `ReservationPeriod.Duration`: niedozwolone `End + Start` (dodawanie dwóch DateTimeOffset), zamiast odejmowania. Nie jest to usterka kompilacji produkcyjnego kodu. [Definicje stanów i score Stryker](https://stryker-mutator.io/docs/mutation-testing-elements/mutant-states-and-metrics/).

Nowe testy wykryły 18 wcześniej przeżywających mutantów: **9, 14, 23, 31, 35, 41, 53, 62, 64, 70, 72, 79, 86, 96, 101, 111, 115, 127**. Dotyczą usuwania jawnych null guardów, pustych komunikatów i niewłaściwej identyfikacji strefy w samodzielnie tworzonej polityce. Nie zmieniano poprawnej logiki biznesowej dla wyniku. Cel 80% osiągnięty bez zmniejszenia zakresu.

### Wszystkie końcowe niewykryte mutanty

Wszystkie dotyczą `backend/src/Roomify.Domain/Reservations/Reservation.cs`; ID i numery linii są z końcowego JSON.

| ID | Linia / zmiana | Analiza i decyzja |
|---|---|---|
| 17 | 45: usunięcie RequireId(roomId) w konstruktorze skalarnym | Publiczna fabryka wcześniej sprawdza roomId. Mutant nie zmienia poprawnego przepływu fabryki; może osłabiać ochronę materializacji spoza niej. Pozostaje Survived. |
| 18 | 45: pusty kod błędu roomId w konstruktorze | Błąd nie jest osiągany z poprawnie zwalidowanej fabryki; kontrakt błędnych danych materializacji jest poza izolowanym zestawem Stryker. Survived. |
| 19 | 46: usunięcie RequireId(userId) w konstruktorze | Analogicznie do 17; pierwsza walidacja pozostaje w Create. Survived. |
| 20 | 46: pusty kod błędu userId w konstruktorze | Analogicznie do 18. Survived. |
| 21 | 50: pusty kod BR-024 przy walidacji tytułu konstruktora | Create wcześniej normalizuje i waliduje tytuł. Dotyczy ścieżki odtwarzania niepoprawnych danych. Survived. |
| 24 | 61: usunięcie pierwszego RequireId(roomId) z Create | Druga walidacja w konstruktorze nadal odrzuca pusty identyfikator. Może zmienić kolejność błędów przy wielu jednocześnie niepoprawnych wejściach; priorytetu błędów WP-00 nie określa. Survived. |
| 26 | 62: usunięcie pierwszego RequireId(userId) z Create | Analogicznie do 24, dla użytkownika. Survived. |

Nie uznajemy wszystkich siedmiu za bezwarunkowo równoważne: część jest warunkowo redundantna w publicznej fabryce, a ścieżka odtwarzania przez EF może zachowywać się inaczej. Nie dodano testów refleksją prywatnego konstruktora tylko dla wskaźnika. Ograniczenie jest jawne, wszystkie nadal obniżają score. Istniejące testy EF i nowy round-trip są dodatkowym dowodem persystencji, lecz nie zostały zaliczone do wyniku mutacji Domain.

### Odtworzenie i raporty mutacji

Z `backend/tests/Roomify.Domain.UnitTests`:

```powershell
dotnet stryker --config-file stryker-config.json --output ../../../artifacts/wp025/mutation-final --skip-version-check --concurrency 4
```

HTML: `artifacts/wp025/mutation-final/reports/mutation-report.html`, JSON obok. Początkowe raporty zachowano osobno pod `artifacts/wp025/mutation-initial/reports/`; logi `stryker-initial.log` i `stryker-final.log`. Historyczny wynik WP-02.1 84,31% zachowuje odrębny zakres/version kodu; nie zastępuje go ta analiza.

## Coverlet — rzeczywisty zakres pokrycia

Pomiar objął **wyłącznie assembly Roomify.Domain** wykonywane przez 110 testów jednostkowych, a nie całe rozwiązanie. Cobertura wykazała **160/160 wykonywalnych linii (100%) i 38/38 gałęzi (100%)**. Sześć klas z wykonywalnym kodem: DomainGuard, Room, Reservation, ReservationPeriod, ReservationTimePolicy, DomainException. Dla każdej line-rate i branch-rate wynosi 1. Enum nie wnosi wykonywalnych linii.

Coverlet raportuje linie/punkty wykonania i gałęzie; nie dostarcza tu odrębnej metryki procentowej wszystkich instrukcji IL. **Nie nazywamy tego pokryciem całego systemu ani niezależnym pomiarem instrukcji IL.** Miarą wykonywalnego kodu w przyjętym kolektorze jest pokrycie linii; przekracza cel 85%, a branch coverage cel 80%. Niepokrytych istotnych linii/gałęzi w tym pomiarze nie było. Survived przy 100% coverage pokazuje, że sama wykonana linia nie gwarantuje jakości każdej asercji.

Dowód: `artifacts/wp025/coverage/d41577e0-42f3-4785-be86-a21e9d6cffdd/coverage.cobertura.xml` i `artifacts/wp025/coverage/domain.trx`. Nie dodano generatora HTML pokrycia ani nowej zależności tylko dla raportu.

## Ochrona danych i architektura

Przed testami zapisano logiczny dump developerskiej roomify_dev, po końcowym przebiegu porównano ponownie. Normalizowano wyłącznie losowe linie pg_dump `\restrict`/`\unrestrict`; pozostała zawartość jest porównana. **SHA-256 bez zmian: `be2f8674ec8f1819e2ba85f5f90a5dcbdf1cd01f511d08b268904004f984f280`**. Historia bez zmian: `20261018093445_InitialCreate|10.0.9`. W WP-02.5 nie wykonano żadnego database update/rollback do bazy developerskiej, seedowania ani kasowania danych.

Testy używają wyłącznie losowych connection stringów Testcontainers, osobnych baz per przypadek, bez .env i bez roomify_postgres_data. Konteksty/połączenia są zwalniane przed usunięciem prywatnej bazy, fixture sprząta własny kontener także po błędach. Po zakończeniu docker ps -a zawiera tylko trzy usługi Roomify, bez kontenerów testowych; db healthy. Compose config --quiet PASS. API i frontend pozostają zatrzymane; frontend ma Exited 1, API Exited 255. To zastany stan usług, nie wynik odbioru UI w tym pakiecie; działanie UI/Nginx nie było wymagane do wykonanych testów.

Referencje zachowane: Domain nie ma pakietów ani referencji infrastrukturalnych; Application → Domain; Infrastructure → Application/Domain; Api → Application/Infrastructure. Baseline hashy potwierdza brak zmian całego backend/src, zastosowanych migracji, snapshotu, oryginalnych testów Domain i czterech dokumentów WP-00. Nie aktualizowano narzędzi ani pakietów. Kontrola `.gitignore` i kandydatów Git nie wykazała lokalnego hasła; .env, artefakty/TRX/HTML/coverage, bin/obj i node_modules pozostają ignorowane. `git diff --check` bez błędów; staging pusty. Zachowano wcześniejsze niezatwierdzone prace.

## Kryteria AC-001–AC-030

| AC | Status | Dowód / granica |
|---|---|---|
| AC-001 | PASS | RoomTests: tworzenie, wymagane pola, pojemność, edycja i archiwizacja; prywatne settery; pełne funkcje HTTP poza zakresem |
| AC-002 | PASS | ReservationTests: identyfikatory, tytuł, okres, czas, zmiana/anulowanie/status i właściciel |
| AC-003 | PASS | ReservationPeriodTests: granice, półotwarte zakresy, offset/UTC, Duration i niezmienność |
| AC-004 | PASS | Granice 08–20/8 h/przyszłość/minuty/lokalny dzień i DST, bez zmiany WP-00 |
| AC-005 | PASS | Domain.csproj bez EF/Identity/Npgsql, źródła niezmienione |
| AC-006 | PASS | 35 wcześniejszych EF + nowy audyt wszystkich kolumn/PK schematu |
| AC-007 | PASS | 34 wcześniejsze Identity + stores/normalizacja/hash/unikalność na PostgreSQL |
| AC-008 | PASS | MIG-001 w Testcontainers: pusta baza → InitialCreate |
| AC-009 | PASS | MIG-003/004/006/012/013/014 i audyt kolumn/PK/CHECK/exclusion |
| AC-010 | PASS | MIG-007, 010, 011 i odrzucona edycja na realnym PostgreSQL |
| AC-011 | PASS | MIG-008, obie granice styku |
| AC-012 | PASS | MIG-009 i Cancel/Blocks w domenie |
| AC-013 | PASS | Dwie serie po 12 prób; każdy przypadek jeden commit/aktywny wiersz |
| AC-014 | PASS | MIG-011: dokładnie 23P01 i nazwa constraint; 40P01 osobno rozróżnione |
| AC-015 | PASS | Domain 110/110, zero failed/skipped |
| AC-016 | PASS | PostgreSQL 42/42 w końcowej regresji |
| AC-017 | PASS | Dotychczasowe 3 HTTP i 34 Identity/security ponownie przechodzą |
| AC-018 | PASS | Testy Domain referencjonują tylko Domain, bez bazy/Dockera |
| AC-019 | PASS | Rzeczywiste dwie analizy Stryker.NET 5.0.0 domeny |
| AC-020 | PASS | 133 mutanty; rzeczywiste stany, score, pełna lista 7 Survived i zachowane raporty |
| AC-021 | PASS | Cobertura: 160/160 linii, 38/38 gałęzi; miara instrukcji IL nie jest osobno dostępna |
| AC-022 | PASS | REQUIREMENTS_TRACEABILITY.md: istniejące FR/BR, testy, poziom, wynik i braki |
| AC-023 | PASS | Niniejszy WP02_ACCEPTANCE_REPORT.md |
| AC-024 | PASS | Debug całego rozwiązania, 0 ostrzeżeń/błędów |
| AC-025 | PASS | Release całego rozwiązania, 0 ostrzeżeń/błędów |
| AC-026 | PASS | has-pending-model-changes, audyt fizycznych kolumn/PK/exclusion i niezmienne źródła migracji |
| AC-027 | PASS | Cztery pliki WP-00 identyczne z baseline |
| AC-028 | PASS | Zmiany wyłącznie w testach i dokumentacji; żadnych nowych funkcji produktu |
| AC-029 | PASS | Identyczny logiczny dump i historia developerska przed/po |
| AC-030 | PASS | Jednoznaczne READY WITH WARNINGS, granice i problemy poniżej |

## Pliki zmienione w WP-02.5

- `backend/tests/Roomify.Domain.UnitTests/DomainContractTests.cs` — 23 nowe przypadki kontraktów.
- `backend/tests/Roomify.Api.IntegrationTests/PostgreSqlMigrationTests.cs` — 12 wariantów MIG-010 i zapis rezultatów; dotychczasowe asercje integralności zachowane.
- `backend/tests/Roomify.Api.IntegrationTests/PostgreSqlSchemaAuditTests.cs` — 3 nowe testy PostgreSQL: schemat, stores Identity i edycja okresu.
- `docs/testing/REQUIREMENTS_TRACEABILITY.md` i `docs/testing/WP02_ACCEPTANCE_REPORT.md` — macierz i odbiór.
- `docs/testing/UNIT_TESTS.md`, `INTEGRATION_TESTS.md`, `MUTATION_TESTS.md`, `README.md` — bieżące wyniki i instrukcje, zachowana historia.
- `docs/architecture/DOMAIN.md`, `DATABASE.md`, `docs/ai/IMPLEMENTATION_STATUS.md` i główny `README.md` — aktualny stan i odnośniki.

Lokalne `artifacts/wp025/` zawiera logi, baseline, TRX, Cobertura, oba HTML/JSON mutacji i pomocnicze skrypty audytu; pozostaje ignorowane i nie jest przeznaczone do commitowania.

## Otwarte ograniczenia i następny etap

**READY WITH WARNINGS:** fundament WP-02 został rzeczywiście zweryfikowany, brak otwartej krytycznej usterki integralności w tym zakresie. Pozostaje siedem jawnych Survived i ograniczenia diagnostyczne opisane wyżej. 40P01 wymaga zaprojektowania bezpiecznej obsługi przy przyszłych operacjach. Nie ma przypadków Application, biznesowych endpointów ani pełnej autoryzacji; aktywność kont/sal, CSRF, unieważnianie sesji i docelowy key ring/TLS muszą zostać ujęte przed udostępnieniem funkcji. Szczegóły wyposażenia sal, edycji tytułu/sali i pełnego kalendarza z FR nie zostały wykonane w tym odbiorze.

Testów Angular/E2E/manualnych, wydajności, backup/restore i odporności całego systemu nie wykonywano w WP-02.5. Historyczne raporty ich nie zastępują. **WP-03 może otrzymać zweryfikowaną domenę i schemat oraz powyższą listę zależności; nie rozpoczęto WP-03 i nie zakładamy jego szczegółowego zakresu bez zlecenia.**

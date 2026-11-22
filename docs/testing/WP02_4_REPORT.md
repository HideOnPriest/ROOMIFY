# WP-02.4 — migracje EF Core i schemat PostgreSQL

Data wykonania: **18.10.2026**. Wynik: **30/30 AC PASS, WP-02.4 ukończone**. WP-02.5 nie rozpoczęto. Raport opisuje bieżące wykonanie, zachowując historyczne raporty WP-01 oraz WP-02.1/02.2/02.3.

## Zakres i wynik

Wprowadzono `20261018093445_InitialCreate`, jej designer i snapshot we wspólnym Infrastructure. Schemat: dziewięć tabel modelu (rooms, reservations i siedem Identity), dziesiąta techniczna `__EFMigrationsHistory`, dziewięć PK modelu, osiem FK, jedenaście jawnych indeksów, sześć CHECK, kolacja ICU i exclusion z częściowym indeksem GiST. PK mają również własne indeksy. [Kompletny ERD, lista tabel, indeksów i ograniczeń](../architecture/DATABASE.md) stanowi część raportu schematu.

Ochrona aktywnych rezerwacji to `ex_reservations_active_period`: równość room_id i nakładanie `tstzrange(start_at,end_at,'[)')`, wyłącznie status `active`. `btree_gist` zapewnia operator GiST dla uuid. Cancelled zwalnia termin, stykające się granice i różne sale są dozwolone. Status jest zapisywany jako `active`/`cancelled`, daty jako timestamptz, Period jako dwie kolumny istniejącego mapowania. Nie zmieniano Domain ani polityki czasu.

Do lokalnego manifestu dodano dotnet-ef 10.0.9, pozostawiając Stryker 5.0.0. Infrastructure otrzymało prywatny Design 10.0.9. Fabryka design-time i wspólna konfiguracja Npgsql używają konfiguracji środowiskowej/argumentów; nie czytają .env ani nie otwierają bazy podczas tworzenia kontekstu. API nie migruje automatycznie. Nie dodano logowania, endpointów, przypadków użycia, seeda, Angulara ani CI.

## Rzeczywiście wykonane kontrole

| Kontrola | Wynik |
|---|---|
| dotnet restore backend/Roomify.slnx | PASS, także po zakończeniu zmian |
| Build całego rozwiązania Debug / Release | PASS, 0 ostrzeżeń i błędów |
| Domain | **87/87 PASS**, 0 skipped |
| Application | Uruchomiono; **0 przypadków**, nie raportujemy fikcyjnego PASS testów |
| Metadane EF / DI / materializacja | **35/35 PASS** w pełnej regresji |
| Identity / cookies / security | **34/34 PASS** w pełnej regresji |
| Dotychczasowe HTTP WebApplicationFactory | **3/3 PASS** w pełnej regresji |
| PostgreSQL SELECT 1 | **1/1 PASS** w pełnej regresji |
| PostgreSQL migracje / integralność / concurrency / Down | **27/27 PASS** w końcowej pełnej regresji |
| Razem Domain + integracyjne | **187/187 PASS**, bez skipped |
| migrations has-pending-model-changes | PASS: No changes have been made to the model since the last migration |
| migrations script | PASS; SQL przejrzany, zapis tylko w ignorowanym artifacts |
| migrations list --no-connect | Lokalna lista zawiera tylko 20261018093445_InitialCreate; bez połączenia nie ustala stanu wdrożenia |
| docker compose config --quiet | PASS |
| Docker / PostgreSQL | Docker Desktop uruchomiony, Linux Engine 29.8.2; db postgres:18.6-bookworm healthy |
| Sprzątnięcie Testcontainers | PASS: docker ps -a zawiera wyłącznie trzy usługi Roomify; brak kontenerów testowych/Ryuk |
| Brak baz testowych w developerze | pg_database: postgres, roomify_dev, template0, template1 |
| Git / sekrety | Brak staged; kontrola kandydatów i .gitignore, brak lokalnego hasła w plikach repozytorium |

Polecenia wykonano z katalogu głównego repozytorium. Testy migracji osobno: `dotnet test backend/tests/Roomify.Api.IntegrationTests --filter Category=DatabaseMigrations`. Końcowa pełna regresja: `dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --logger "trx;LogFileName=regression.trx" --results-directory artifacts/wp024/trx`; wybrała 100 przypadków, w tym wszystkie 28 RequiresDocker=true. Domain i Application uruchomiono osobnymi `dotnet test` ich projektów. Restore/build dotyczyły `backend/Roomify.slnx`.

Nie powtarzano testów Angular/Playwright, testów manualnych, pokrycia ani mutacji; poprzednie liczby w ich raportach są historyczne. API i frontend po ponownym uruchomieniu Desktop pozostały zatrzymane (`Exited 255`), db uruchomiono przez compose start db. Nie deklarujemy bieżącego sprawdzenia UI/proxy/Kestrela. Wymagane testy HTTP wykonano przez TestServer.

## Historia przebiegów i współbieżność

1. Pierwsze uruchomienie samej klasy migracji: **27/27 PASS**.
2. Pierwsza pełna regresja: **99/100 PASS**, MIG-010 failed. PostgreSQL przerwał jedną z równoczesnych transakcji przez **40P01**, a druga wykonała commit. Pierwotna asercja wymagała wyłącznie 23P01. Nie był to podwójny zapis ani błąd migracji; była to zbyt wąska asercja nowego testu.
3. Uściślono nowy MIG-010: dokładnie jeden commit, druga transakcja musi zostać odrzucona przez 23P01 albo 40P01, a w bazie ma pozostać dokładnie jedna aktywna rezerwacja. Dwie niezależne transakcje rozpoczynają INSERT po wspólnej barierze; limit 20 s, komendy 10 s. Nie zserializowano próby, nie wyłączono globalnej równoległości, nie przechwycono dowolnych błędów jako sukcesu. Dotychczasowych testów nie osłabiono.
4. Końcowa pełna regresja: **100/100 PASS** (migracje 27/27), Domain **87/87 PASS**. MIG-011 nadal wymaga dokładnie **23P01** i **ex_reservations_active_period** w PostgresException opakowanym przez DbUpdateException.

Deadlock jest prawidłowym mechanizmem ochrony PostgreSQL, który abortuje jedną transakcję; jego obsługa i ewentualne ponowienie całej transakcji należą do przyszłego serwisu. Nie dodano automatycznego retry ani mapowania każdego błędu do 409. [Dokumentacja PostgreSQL](https://www.postgresql.org/docs/18/explicit-locking.html#LOCKING-DEADLOCKS).

## Pokrycie MIG-001–015

| Scenariusz | Rzeczywiste potwierdzenie — wszystkie PASS w końcowym wykonaniu |
|---|---|
| MIG-001 | Pusta baza → Migrate; jeden InitialCreate, brak pending migrations i zmian modelu |
| MIG-002 | Dokładna lista 9 tabel modelu i historii |
| MIG-003 | 9 PK modelu, 8 FK, dwa FK rezerwacji Restrict; dokładne metadane także w dotychczasowych testach |
| MIG-004 | Indeksy terminów, 6 CHECK, exclusion, rozszerzenie i nondeterministic ICU |
| MIG-005 | Ponowne Migrate bez błędu, zachowane dane sali i użytkownika |
| MIG-006 | Zerowy i odwrócony przedział → 23514, ck_reservations_period |
| MIG-007 | Częściowe, otaczające, zawarte i identyczne nakładanie → 23P01 |
| MIG-008 | Styk przed i po istniejącym terminie dozwolony |
| MIG-009 | Cancelled zwalnia termin; reaktywacja w SQL ponownie podlega exclusion |
| MIG-010 | Dwie równoczesne transakcje, dokładnie jeden commit i jedna aktywna rezerwacja |
| MIG-011 | Rzeczywisty błąd EF/Npgsql: SqlState 23P01, poprawna ConstraintName |
| MIG-012 | Nieistniejąca sala → 23503, właściwy FK |
| MIG-013 | Nieistniejący użytkownik → 23503, właściwy FK |
| MIG-014 | DELETE użytkownika i DELETE sali odrzucone; rezerwacja i encja pozostają |
| MIG-015 | SaveChanges i ponowny odczyt UTC, Period, Cancelled, dat audytu, sali i użytkownika |

Parametryzacja i dodatkowe testy dają **27 przypadków**. Dodatkowo: różne sale w tej samej godzinie, trzy CHECK sal, nieprawidłowy status, unikalność nazwy bez wielkości liter oraz rollback z ponownym Up.

## Izolacja i baza developerska

Każdy przypadek tworzy własną bazę roomify_mig_<Guid> w PostgreSQL uruchomionym przez fixture. Connection string pochodzi wyłącznie z Testcontainers, port i hasło są losowe, brak odczytu .env i developerskiego wolumenu. Guid są generowane przez fabryki; czas jest stały: 2030-01-01 UTC, okresy 2030-01-15. Techniczni użytkownicy Identity bez hasła służą tylko FK w izolowanych bazach.

Przed pełnymi testami (w tym przebiegiem 99/100) i po końcowym przebiegu pobrano z developerki logiczny pg_dump `--no-owner --no-privileges`, normalizując tylko losowe linie `\restrict`/`\unrestrict`. **SHA-256 identyczny: `9411a97aa689338fd4155acc14dd13318118c1baeb2d21eb5ae225a79e9187bf`**, identyfikator klastra `7697670479764820011`, lista tabel public pusta. Dowód obejmuje pełne testy, nie samą analizę connection stringów. Pierwszy osobny przebieg migracji poprzedzał ten pomiar; jego izolację potwierdza konstrukcja fixture i brak tabel developerskich po nim.

Dopiero po poprawnym końcowym przebiegu wykonano odrębną, autoryzowaną migrację developerską. Przed nią ponownie sprawdzono:

- Compose project roomify, service db, obraz postgres:18.6-bookworm.
- Nazwany wolumen roomify_postgres_data i publikację wyłącznie 127.0.0.1:5432.
- `.env` wskazuje roomify_dev, current_database/current_user oba roomify_dev; hasła nie wypisano.
- Ten sam klaster, zero istniejących tabel public, brak historii migracji i identyczny dump.
- Brak nadrzędnego ConnectionStrings__Roomify; parametry Database__* przekazano tylko środowisku procesu dotnet-ef.

`dotnet ef database update --project backend/src/Roomify.Infrastructure --startup-project backend/src/Roomify.Infrastructure` zakończył się **kodem 0, Done**. Na nowej bazie EF zalogował początkowe nieudane zapytanie do nieistniejącej jeszcze historii; następnie utworzył schemat i wpisał migrację. Weryfikacja po operacji: historia **20261018093445_InitialCreate / 10.0.9**, dokładnie dziesięć tabel public, **6 CHECK + 1 exclusion**, rooms/reservations/users/roles **0/0/0/0**. Nie seedowano danych ani kont. Baza developerska otrzymała wyłącznie addytywny schemat; testy go nie zmieniły.

## Down i ochrona danych

Rollback wykonano tylko na jednorazowej bazie Testcontainers po utworzeniu danych testowych. Usuwa exclusion, dziewięć tabel w kolejności zależności i własną kolację. Pozostawia pustą historię EF oraz **btree_gist**: Up używa IF NOT EXISTS, więc rozszerzenie mogło istnieć wcześniej. Nie usuwamy potencjalnie współdzielonego zasobu ani jego zależności przez CASCADE. Test potwierdza ten kontrakt i ponowne poprawne Up, po czym usuwa prywatną bazę i kontener.

**Down pierwszej migracji jest destrukcyjny dla danych jej tabel. Nie wykonywano go na roomify_dev.** Nie użyto EnsureCreated, EnsureDeleted, docker compose down -v, DROP istniejących tabel użytkownika ani usuwania developerskiego wolumenu.

## Pliki WP-02.4

Poniżej zakres tego pakietu; git status zawiera także niezatwierdzone pliki wcześniejszych WP-02.1–02.3, które pozostawiono.

| Plik względem repozytorium | Zmiana |
|---|---|
| .config/dotnet-tools.json | Lokalny dotnet-ef 10.0.9; Stryker bez zmian |
| backend/src/Roomify.Infrastructure/Roomify.Infrastructure.csproj | Prywatny EF.Design 10.0.9 |
| backend/src/Roomify.Infrastructure/InfrastructureServiceCollectionExtensions.cs | Wspólna konfiguracja Npgsql, ustawienia Identity zachowane |
| backend/src/Roomify.Infrastructure/Persistence/PostgreSqlConfiguration.cs | Składanie ustawień bez sekretów w kodzie |
| backend/src/Roomify.Infrastructure/Persistence/RoomifyDesignTimeDbContextFactory.cs | Fabryka narzędzi EF |
| backend/src/Roomify.Infrastructure/Persistence/RoomifyDbContext.cs | Deklaracja btree_gist |
| backend/src/Roomify.Infrastructure/Persistence/Configurations/RoomConfiguration.cs | Trzy CHECK sali |
| backend/src/Roomify.Infrastructure/Persistence/Configurations/ReservationConfiguration.cs | Trzy CHECK rezerwacji |
| backend/src/Roomify.Infrastructure/Persistence/Migrations/20261018093445_InitialCreate.cs | Up/Down, jawny SQL exclusion |
| backend/src/Roomify.Infrastructure/Persistence/Migrations/20261018093445_InitialCreate.Designer.cs | Wygenerowany model migracji |
| backend/src/Roomify.Infrastructure/Persistence/Migrations/RoomifyDbContextModelSnapshot.cs | Wygenerowany snapshot |
| backend/tests/Roomify.Api.IntegrationTests/MigrationDatabase.cs | Izolowane bazy i fabryki danych |
| backend/tests/Roomify.Api.IntegrationTests/PostgreSqlMigrationTests.cs | 27 przypadków PostgreSQL |
| docs/architecture/DATABASE.md | ERD, schemat, błędy, operacje migracji i granice reguł |
| docs/architecture/PERSISTENCE.md | Odnośnik do aktualnego schematu, zachowana historia |
| docs/architecture/IDENTITY.md | Odnośnik do wdrożonych migracji, zachowana historia |
| docs/testing/INTEGRATION_TESTS.md | Migracje, izolacja, komendy i wyniki |
| docs/testing/WP02_4_REPORT.md | Ten raport |
| docs/ai/IMPLEMENTATION_STATUS.md | Bieżący stan po WP-02.4 |
| README.md | Aktualny status i odnośniki do migracji |

Lokalne logi, TRX, pomocnicze skrypty kontroli, dump-hashe i wygenerowany SQL pozostają w ignorowanym `artifacts/wp024`; nie są kodem przeznaczonym do wersjonowania. Źródła migracji są przeznaczone do Git. Nie dodano `.env`, bin/obj, node_modules ani raportów generowanych.

## Kryteria akceptacji

| AC | Status | Dowód |
|---|---|---|
| AC-001 | PASS | InitialCreate + designer + snapshot w Infrastructure |
| AC-002 | PASS | MIG-002: rooms i reservations |
| AC-003 | PASS | MIG-002: komplet siedmiu tabel Identity |
| AC-004 | PASS | MIG-003 i metadane: 9 PK modelu, klucze złożone Identity |
| AC-005 | PASS | MIG-003, MIG-012/013/014: 8 FK, Restrict historii |
| AC-006 | PASS | MIG-004, metadane, unikalność ICU; pełna lista w DATABASE.md |
| AC-007 | PASS | MIG-006: CHECK start_at < end_at, 23514 |
| AC-008 | PASS | MIG-004: fizyczny ex_reservations_active_period |
| AC-009 | PASS | MIG-007: cztery kształty konfliktu |
| AC-010 | PASS | MIG-008: dwie granice styku |
| AC-011 | PASS | MIG-009: anulowanie zwalnia termin |
| AC-012 | PASS | MIG-010: dokładnie jeden commit i wiersz |
| AC-013 | PASS | MIG-011: PostgresException 23P01 i nazwa constraint przez EF |
| AC-014 | PASS | MIG-001: pusta baza → Up |
| AC-015 | PASS | MIG-005: ponowne Migrate, dane zachowane |
| AC-016 | PASS | Down → Up na jednorazowej bazie; kontrolowane zachowanie btree_gist opisane |
| AC-017 | PASS | Bazy per test, serwer Testcontainers, dynamiczny port, sprzątanie |
| AC-018 | PASS | Identyczny dump, klaster i pusta lista tabel przed/po pełnych testach |
| AC-019 | PASS | Domain 87/87; Application uruchomione, 0 przypadków |
| AC-020 | PASS | Dotychczasowe HTTP 3/3 i Identity/security 34/34 |
| AC-021 | PASS | Debug całego rozwiązania, 0 warning/error |
| AC-022 | PASS | Release całego rozwiązania, 0 warning/error |
| AC-023 | PASS | Brak nowych endpointów/serwisów/UI/seeda/CI |
| AC-024 | PASS | Domain.csproj bez pakietów i referencji EF; źródła bez zmian |
| AC-025 | PASS | Sekrety tylko w ignorowanym .env/środowisku, kontrola kandydatów Git |
| AC-026 | PASS | DATABASE.md: 17 wymaganych tematów, ERD i instrukcje |
| AC-027 | PASS | InitialCreate zastosowana i zweryfikowana w roomify_dev |
| AC-028 | PASS | Developer tylko addytywne Up na pustej bazie, Down wyłącznie testowy |
| AC-029 | PASS | IMPLEMENTATION_STATUS.md i README zaktualizowane |
| AC-030 | PASS | Wyniki rzeczywistych wykonań, także nieudany pierwszy pełny przebieg |

## Architektura, Git i ograniczenia

Referencje zachowane: Domain niezależny; Application → Domain; Infrastructure → Domain/Application; Api → Application/Infrastructure. Porównanie hashy z lokalnym baseline WP-02.3 potwierdziło niezmienność **73 chronionych plików** WP-00, Domain i jego testów, Application oraz frontendu. Żaden wcześniejszy test nie został usunięty ani osłabiony. Weryfikacja aktualnego zestawu kandydatów Git nie znalazła lokalnego hasła; `.gitignore` ignoruje `.env`, artifacts, node_modules i bin/obj. `git diff --check` bez błędów; Git zgłasza jedynie normalizację CRLF do LF zgodną z ustawieniami repozytorium. Staging pusty. **Nie wykonano commit ani push.** Wcześniejsze niezatwierdzone zmiany pozostają w repozytorium.

Exclusion jest jawnym SQL i wymaga ręcznej uwagi w kolejnych migracjach; snapshot nie wykryje usunięcia tego obiektu poza EF. ICU i btree_gist muszą być dostępne na docelowym PostgreSQL. Konto migratora wymaga odpowiednich uprawnień; konto runtime nie powinno automatycznie zarządzać schematem. Brak bazy produkcyjnej i testu backup/restore w tym zakresie. Down zachowuje współdzielone rozszerzenie. Nie deklarujemy gotowości do publikacji internetowej ani całego MVP.

**Gotowość do kolejnego zlecenia WP-02.5:** model i rzeczywisty schemat są przygotowane, testy migracji i integralności działają, developer ma pusty schemat bez kont. Nadal potrzebne są odpowiednie przypadki użycia, uprawnienia i biznesowe API; pełne bezpieczeństwo sesji/CSRF, egzekwowanie IsActive, key ring/TLS i polityka obsługi 40P01 pozostają do uwzględnienia w dalszych zadaniach. Zakres WP-02.5 nie jest tu implementowany ani domyślnie rozszerzany.

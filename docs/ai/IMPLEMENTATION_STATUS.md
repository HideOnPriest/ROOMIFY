# Roomify — bieżący stan implementacji

Aktualizacja: 18.10.2026, po WP-03.4. **WP-01: READY WITH WARNINGS. WP-02.1: 25/25 AC PASS. WP-02.2: 26/26 AC PASS. WP-02.3: 30/30 AC PASS. WP-02.4: 30/30 AC PASS. WP-02.5: 30/30 AC PASS. Odbiór WP-02: READY WITH WARNINGS. WP-03.1: 26/26 AC PASS. WP-03.2: 25/25 AC PASS. WP-03.3: 26/26 AC PASS, READY WITH WARNINGS. WP-03.4: 30/30 AC PASS, READY WITH WARNINGS.** Wyniki: [audyt środowiska](../testing/WP01_AUDIT_REPORT.md), [Domain](../testing/WP02_1_REPORT.md), [EF](../testing/WP02_2_REPORT.md), [Identity](../testing/WP02_3_REPORT.md) i [migracje PostgreSQL](../testing/WP02_4_REPORT.md). WP-00 pozostaje niezmienione; stan nie zatwierdza automatycznie pozostałych propozycji `[P]`.

| Etap / obszar | Stan | Dowód / granica |
|---|---|---|
| WP-00 | Specyfikacja dostępna | Cztery oryginalne dokumenty w docs/ai; rozróżnienie `[Z]` i `[P]`. |
| WP-01.1 | Gotowe | Monorepo, architektura i ADR, konfiguracja Git/edytora. |
| WP-01.2 | Gotowe | Cztery projekty net10.0, DI, routing, MVC i /health. |
| WP-01.3 | Gotowe | Angular 22 CSR, layout, /home i 404, HttpClient i lazy routing. |
| WP-01.4 | Zweryfikowane w Dockerze | Build i up działają; frontend, api, db running; API i PostgreSQL healthy. |
| WP-01.5 | Infrastruktura gotowa | HTTP 3/3, PostgreSQL 1/1, Vitest 5/5, Chromium 2/2; różne wykonania opisane w audycie. |
| WP-01.6 | Zamknięte z ostrzeżeniami | Blokada Docker usunięta; proxy i izolacja bazy potwierdzone. |
| WP-02.1 | Wykonane | Room, Reservation, ReservationPeriod, ReservationStatus, polityka czasu i błędy domenowe; 85/85 testów Domain. |
| WP-02.2 | Wykonane | EF Core 10.0.9, Npgsql provider 10.0.3, RoomifyDbContext, mapowania i Scoped DI; 35/35 testów EF. Bez tabel i migracji. |
| WP-02.3 | Wykonane | IdentityUser/Role<Guid>, jeden IdentityDbContext, FK użytkownika Restrict, standardowe stores/managery i konfiguracja cookies. 34/34 Identity/security. |
| WP-02.4 | Wykonane | InitialCreate, 9 tabel modelu + historia EF, 6 CHECK, 8 FK i exclusion GiST; 27/27 migracje, rollback testowy, schemat developerski wdrożony bez danych. |
| WP-02.5 | Wykonane, READY WITH WARNINGS | Domain 110/110, integracyjne 114/114, coverage 100% linii/gałęzi Domain; Stryker 93,46%, 7 Survived; macierz wymagań i raport odbioru. |
| WP-03.1 | Wykonane | CreateReservationService, zaufany ICurrentUser, Scoped repozytoria EF, EXISTS dostępności i kontrolowane wyniki zapisu. 32 Application i 24 nowe integracyjne. |
| WP-03.2 | Wykonane | Update/Cancel, właściciel/admin, wspólny wynik, native xmin, odrębne konflikty, 87 Application i 21 nowych PostgreSQL. Migracja mapowania przetestowana; developerka nadal ma tylko InitialCreate w historii. |
| WP-03.3 | Wykonane, READY WITH WARNINGS | 6 usług sal, scope odczytu, filtry/paginacja PostgreSQL, UNIQUE i xmin. [Raport](../testing/WP03_3_REPORT.md). |
| WP-03.4 | Wykonane, READY WITH WARNINGS | 6 operacji kont, Identity, transakcje, aktywność i stamp co żądanie, ostatni admin; [raport](../testing/WP03_4_REPORT.md). |
| WP-03.5 i kolejne | Nie rozpoczęto | Brak login/logout/me, bootstrapu administratora, endpointów biznesowych i UI. |
| Logika biznesowa | Model domenowy wprowadzony | Niezmienniki sal/rezerwacji i czas Europe/Warsaw; Application tworzy, edytuje i anuluje, sprawdza właściciela/admina, salę/dostępność i odrębne konflikty terminu/wersji. |
| CI | Nie rozpoczęto | .github/workflows zawiera wyłącznie .gitkeep. |

WP-02.1: restore i build Debug/Release całego rozwiązania przeszły bez ostrzeżeń/błędów. Domain **85/85**, HTTP bez Dockera **3/3**; Application uruchomione, **0 przypadków**. Coverlet Domain: **149/149 linii, 38/38 gałęzi**. Stryker.NET: **84,31%**, 86 Killed, 16 Survived, 24 Ignored i 1 CompileError z 127 utworzonych mutantów. Szczegóły diagnostycznych luk są w raporcie; score nie dotyczy całego MVP.

WP-02.2: restore, Debug i Release ponownie PASS, 0 ostrzeżeń/błędów. **Domain 87/87, EF/DI/materializacja 35/35, istniejące HTTP 3/3**; Application nadal 0 przypadków. Historycznych wyników mutacji/pokrycia nie przeliczano dla zmienionej Reservation. W Domain dodano wyłącznie konieczne pola czasu i prywatny konstruktor materializacji, zachowując publiczne API i walidację nowych operacji. Pola umożliwiają indeksy okresu w EF10; ComplexProperty odrzucono po rzeczywistej próbie. Dwa dodatkowe testy zabezpieczają UTC i niezmienność migawki Period.

WP-02.3: restore, Debug i Release PASS bez ostrzeżeń/błędów. **159/159** w końcowej regresji: Domain 87, EF 35, Identity/security 34, dotychczasowe HTTP 3; Application 0 przypadków. Dziewięć encji we wspólnym modelu, źródła Domain/Application bez zmian. Tabele Identity snake_case, centralne role User/Administrator, standardowe hashowanie i lockout. Cookies HttpOnly/Lax, Secure poza lokalnym HTTP Development, 30 minut bez odnawiania. TestServer potwierdza 401/403 bez przekierowań i publiczne /health z niedostępną bazą. Kontroler prób jest wyłącznie testowy; brak produkcyjnych endpointów auth/rejestracji.

Stan historyczny po WP-02.3 (przed migracją WP-02.4): model był gotowy, schemat niewdrożony. Brak kont i round-trip do tabel; IsActive sam nie blokuje jeszcze logowania/sesji. Pełny antiforgery, egzekwowanie aktywności, trwały key ring/TLS oraz przypadki użycia wymagają dalszych zleceń przed udostępnieniem logowania. FK i unikalność są deklaracjami modelu, bez fizycznych ograniczeń PostgreSQL. /health nadal liveness bez bazy. Logiczny dump developerski bez zmian, lista tabel użytkownika pusta. Testcontainers nie powtarzano w WP-02.1/02.2/02.3; ostatni wynik 1/1 z WP-01.6. Manualne NOT RUN, E2E obejmują frontend.

Przed dalszym etapem przeczytaj [DATABASE.md](../architecture/DATABASE.md), [raport WP-02.4](../testing/WP02_4_REPORT.md), [DOMAIN.md](../architecture/DOMAIN.md), [PERSISTENCE.md](../architecture/PERSISTENCE.md), [IDENTITY.md](../architecture/IDENTITY.md), raporty, ADR i WP-00. [WP02_HANDOFF.md](WP02_HANDOFF.md) zachowuje historię WP-01. WP-02.4 wykonano według osobnego zlecenia; WP-02.5 wykonano; przed WP-03 przeczytaj [odbiór](../testing/WP02_ACCEPTANCE_REPORT.md) i [macierz wymagań](../testing/REQUIREMENTS_TRACEABILITY.md). WP-03 nie rozpoczęto.

Wersjonowanie: bazowy lokalny commit fb429d4; WP-02.1/02.2/02.3/02.4/02.5/03.1/03.2 pozostają niezatwierdzone zgodnie ze zleceniami, bez commit i push. Przed publikacją trzeba uzgodnić zdalną historię opisaną w handoff.

## Weryfikacja WP-02.4 — 18.10.2026

Restore, Debug i Release PASS, 0 ostrzeżeń/błędów. Domain **87/87**, integracyjne **100/100**: EF 35, Identity/security 34, HTTP 3, PostgreSQL 28 (27 przypadków migracji + SELECT 1). Application uruchomione, 0 przypadków. Snapshot aktualny. Współbieżne transakcje: dokładnie jeden commit; PostgreSQL może przerwać drugą przez 23P01 albo 40P01, oba bez podwójnej rezerwacji. Osobny MIG-011 potwierdza 23P01 i nazwę ograniczenia przez DbUpdateException/Npgsql.

Docker Desktop uruchomiono ponownie, db healthy, Compose config PASS. API/frontend pozostają zatrzymane po restarcie Desktop; nie były potrzebne do migracji i nie są deklarowane jako zweryfikowane runtime w WP-02.4. Historyczne wyniki WP-01 pozostają historią. Testcontainers sprząta kontenery; każda próba migracji ma osobną bazę. Dump roomify_dev przed/po testach identyczny. Dopiero potem jawnie zastosowano **20261018093445_InitialCreate** do pustej roomify_dev na 127.0.0.1:5432 / roomify_postgres_data. Powstały wszystkie tabele i fizyczne ograniczenia; rooms/reservations/users/roles nadal 0 rekordów. Nie cofano migracji developerskiej, nie kasowano danych ani wolumenu.

Ograniczenia pozostające po WP-02.4: brak logowania HTTP, serwisów i endpointów biznesowych, seeda i CI. IsActive konta nie jest jeszcze egzekwowane przez pełny proces sesji. CSRF, trwały key ring/TLS i obsługa ponowień transakcji wymagają dalszej implementacji. /health nadal liveness. Down usuwa dane tabel aplikacji i własną kolację; celowo zachowuje współdzielone btree_gist oraz pustą historię EF. Nie stosować rollbacku pierwszej migracji do bazy z danymi. Exclusion jest jawnym SQL migracji i wymaga uwagi przy kolejnych zmianach modelu. Pokrycia, mutacji, Angular/E2E i manualnych nie powtarzano.

## Odbiór WP-02.5 — 18.10.2026

**READY WITH WARNINGS, 30/30 AC PASS dla zakresu WP-02.** Produkcyjne źródła, dotychczasowe testy Domain, zastosowana migracja i snapshot oraz cztery dokumenty WP-00 bez zmian. Dodano 23 przypadki DomainContractTests, 3 rzeczywiste audyty PostgreSQL (kolumny/PK, stores Identity i odrzucona edycja) i rozszerzono MIG-010 do 12 wariantów z zapisem wyników. Aktualna regresja: **224/224 (Domain 110, integracyjne 114)**; Application uruchomione, nadal 0. Restore, Debug i Release PASS bez ostrzeżeń/błędów. EF has-pending-model-changes: brak zmian.

Stryker wykonano dwukrotnie bez zmiany zakresu/wykluczeń: **76,64% → 93,46%**, 133 wygenerowane, 100 Killed, 7 Survived, 0 NoCoverage/Timeout/RuntimeError, dodatkowo 25 Ignored przez domyślny filtr i 1 CompileError. Ocalałe dotyczą podwójnej walidacji fabryki/konstruktora; nie wykluczono ich i nie uznano zbiorczo za równoważne. Coverlet wyłącznie Domain: **160/160 linii i 38/38 gałęzi (100%)**, bez odrębnej metryki instrukcji IL. Raporty początkowe i końcowe zachowano w ignorowanym artifacts/wp025.

W dwóch seriach MIG-010: **24 scenariusze / 48 prób zapisu, 24 commit i 24 odrzucenia (9 × 23P01, 15 × 40P01)**, zawsze 1 aktywny rekord, bez timeoutów i opóźnień Task.Delay. Test 23P01 osobno nadal wymaga konkretnego kodu i nazwy constraint. Powtarzalność testu potwierdzona w tych przebiegach; obsługa retry 40P01 należy do przyszłego serwisu.

Baza developerska nietknięta: identyczny logiczny dump przed/po, historia 20261018093445_InitialCreate / 10.0.9; nie wykonywano migracji, rollbacku ani seeda do developerki. Testy wyłącznie w bazach Testcontainers; po nich brak kontenerów testowych, db healthy. API/frontend zastano zatrzymane i nie odbierano tu UI/Nginx. Nie wykonywano E2E/manualnych, mutacji Application/Infrastructure, wydajności ani backup/restore. Brak pełnego loginu, autoryzacji właściciela, kontroli aktywności sal/kont i CSRF pozostaje jawnie PLANNED, nie jako zrealizowane FR.

Dokumenty wejściowe do kolejnego zlecenia WP-03: [WP02_ACCEPTANCE_REPORT.md](../testing/WP02_ACCEPTANCE_REPORT.md), [REQUIREMENTS_TRACEABILITY.md](../testing/REQUIREMENTS_TRACEABILITY.md), [DATABASE.md](../architecture/DATABASE.md). Nie rozpoczęto WP-03. Nie wykonano commit ani push.

## WP-03.1 — 18.10.2026

**Wykonane, 26/26 AC PASS.** [Architektura](../architecture/RESERVATION_APPLICATION.md), [raport z wynikami i historią napraw](../testing/WP03_1_REPORT.md). CreateReservationService wykorzystuje istniejącą domenę i TimeProvider, IRoomRepository/IReservationRepository oraz zaufany ICurrentUser. Request bez UserId; wyniki Success/InvalidInput/RoomNotFound/RoomInactive/Conflict/Unauthenticated/InfrastructureFailure, bez HTTP i danych providera. Domyślny adapter zawsze nieuwierzytelniony; nie dodano adaptera HttpContext ani loginu.

Infrastructure: Scoped repozytoria EF, pobranie sali po PK, kontrola IsActive w Application, AnyAsync → EXISTS dla aktywnych półotwartych przedziałów, SaveChanges. Jedynie 23P01 z ex_reservations_active_period daje Conflict. FK, inny exclusion, awaria połączenia oraz 40P01 (także opakowany przez NpgsqlExecutionStrategy) dają neutralny InfrastructureFailure. Nie dodano retry; anulowanie propagowane. Kandydat po nieudanym zapisie jest odłączony. Application nie zna EF/Npgsql/Identity/HttpContext; Domain, Program.cs, mapowania i migracje bez zmian.

Restore/Debug/Release PASS bez ostrzeżeń/błędów. Pełny backend **280/280**: Domain 110, Application **32**, integracyjne **138** (75 bez Dockera, 63 PostgreSQL). Nowe 24 integracyjne: 21 PostgreSQL INT-RES-001–009 i inna sala, 3 kompozycji/awarii. Dwie poprawne serie nowej współbieżności: 12 scenariuszy / 24 wywołania, zawsze jeden sukces i jeden aktywny rekord; 11 Conflict/23P01 i 1 InfrastructureFailure/40P01. Początkowe dwa niepowodzenia zachowane: brak obsługi dodatkowego opakowania deadlocka naprawiono w Infrastructure; dane testowe innego exclusion znormalizowano bez osłabienia asercji.

Stryker.NET 5.0.0 tylko dla CreateReservationService: **100%**, 19/19 Killed, 0 Survived/NoCoverage/Timeout/RuntimeError, 5 CompileError. Log narzędzia: 29 wygenerowanych i 5 Ignored; JSON: 28 wpisów i 4 Ignored. Rozbieżność jawna w raporcie; nie powiększa liczby wykrytych. Istniejących narzędzi nie aktualizowano; Moq 4.20.72 dodano wyłącznie do testów Application. Coverage i mutacji Domain nie powtarzano.

Developer roomify_dev bez zmian: identyczny logiczny dump i historia InitialCreate przed/po; nie wykonywano migracji ani seeda. Brak pozostałych kontenerów testowych. Bieżące usługi Compose uruchomione (api/db healthy), ale obrazów nie przebudowywano w tym pakiecie. Nie dodano endpointów, edycji/anulowania Application, CRUD sal, Angulara ani CI. Aktywność konta, sesje/CSRF i zaufany adapter Identity pozostają do przyszłego podłączenia; sprawdzenie sali nie jest nowym protokołem koordynacji równoczesnej archiwizacji. **WP-03.2 nie rozpoczęto. Bez commit i push.**

## WP-03.2 — 18.10.2026

**Wykonane, 25/25 AC PASS.** [RESERVATION_MANAGEMENT.md](../architecture/RESERVATION_MANAGEMENT.md), [WP03_2_REPORT.md](../testing/WP03_2_REPORT.md). Scoped UpdateReservationService i CancelReservationService korzystają z tego samego repozytorium, ICurrentUser (IsAdministrator domyślnie false), TimeProvider i domeny. Wspólny ReservationAccess sprawdza właściciela lub administratora po uwierzytelnieniu. Brak uprawnień nie ujawnia danych ani statusu cudzej rezerwacji. Edycja zmienia tytuł/okres w tej samej sali, bez zmiany ID/właściciela. Cancel zachowuje rekord i zwalnia slot, powtórzenie daje AlreadyCancelled bez zapisu.

Istniejący mechanizm wyniku uogólniono: ReservationOperationResult / ReservationOperationStatus / ReservationDetails (z UpdatedAt), wspólny dla Create/Update/Cancel. Nie dodano drugiego Result ani HTTP. Domain otrzymał prywatny setter Title, czystą ValidateUpdate i atomowe UpdateDetails wykorzystujące te same walidatory. Istniejące ChangePeriod/Cancel i wcześniejsze asercje zachowane.

Optimistic concurrency: shadow uint xmin/xid IsRowVersion w Infrastructure. Odczyt do modyfikacji ze śledzeniem, UPDATE z oryginalnym xmin i RETURNING nowej wersji. DbUpdateConcurrencyException → ConcurrentModification, osobno od właściwego 23P01 → Conflict; po błędzie własna encja odłączana. Bez retry. Nowa **20261018104209_ReservationOptimisticConcurrency** i snapshot spójne; Npgsql pomija fizyczne ADD/DROP systemowego xmin. Up/Down nowej migracji zachowuje istniejące dane i token w teście. InitialCreate/designer bez zmian.

**Developer roomify_dev bez zmian**, historia nadal tylko InitialCreate. Nowa migracja mapowania/historii jest przetestowana, oczekuje na jawne zastosowanie przy wdrożeniu; natywny xmin już istnieje. Nie wykonywano developer database update, rollbacku ani seeda, dump przed/po identyczny. Testy w osobnych bazach Testcontainers, kontenery sprzątnięte.

Restore/Debug/Release PASS, zero ostrzeżeń/błędów. Końcowo **362/362**: Domain **116**, Application **87** (Create 32, Update 37, Cancel 18), integracyjne **159** (75 bez Dockera, 84 PostgreSQL). Nowe 21 PostgreSQL obejmuje wszystkie INT-UPD/INT-CAN/INT-SEC oraz migrację. W nowym podzbiorze i końcowej regresji 18 scenariuszy równoczesnych / 36 wywołań: zawsze jeden Success, jeden ConcurrentModification i jeden rekord zgodny ze zwycięzcą. Zachowano pierwszy pełny wynik 361 PASS/1 FAIL: stary test po surowym UPDATE potrzebował Reload nowego xmin; poprawiono przygotowanie, pozostawiając asercję 23P01.

Stryker Application Create/Update/Cancel/Access/SaveResults: **100%**, 63 Killed/63 badane, 0 Survived/NoCoverage/Timeout/RuntimeError, 5 CompileError. Log narzędzia 82 wygenerowane i 14 Ignored, JSON 80 wpisów i 12 Ignored — różnica jawna w raporcie, bez dopisywania wyników. Coverage/mutacji nowego Domain nie powtarzano. Cztery dokumenty WP-00, API i frontend bez zmian; brak nowych pakietów. Nie dodano endpointów, logowania, CRUD sal ani UI. Brak adaptera Identity/aktywności konta, sesji/CSRF i ETag klienta pozostaje granicą przyszłych prac. **WP-03.3 nie rozpoczęto, bez commit/push.**


## WP-03.3 — zarządzanie salami, 18.10.2026

**READY WITH WARNINGS, 26/26 AC PASS w zakresie Application/Infrastructure.** Sześć usług: CreateRoom, UpdateRoom, ArchiveRoom, ActivateRoom, GetRoomById, GetRooms. Zaufany administrator dla zapisu, uwierzytelniony odczyt i aktywne sale dla zwykłego użytkownika. Dane waliduje Domain. Archiwizacja/aktywacja oraz zmiana pojemności zachowują wszystkie rezerwacje i ich statusy. Powtórzenia dają AlreadyArchived/AlreadyActive bez zapisu. DTO nie ujawniają użytkowników ani rezerwacji.

GetRooms wykonuje filtrowanie nazwy/lokalizacji/pojemności/aktywności, COUNT i paginację w PostgreSQL. PageSize 1–100; enum sortowania + Id jako drugi klucz. Istniejący IRoomRepository rozszerzono o tracking, unikalność i listę. Room otrzymał shadow xmin tak jak Reservation; odrębne DuplicateName i ConcurrentModification, bez retry. Migracja 20261018111403_RoomOptimisticConcurrency przeszła up/down na izolowanej bazie bez zmiany danych. Nie zmieniono InitialCreate i poprzedniej migracji. Na developerce nie wdrożono obu migracji xmin; systemowe kolumny już istnieją.

**469/469 testów backendu:** Domain 118, Application 164, integracyjne 187 = 76 bez Dockera + 111 PostgreSQL. Przyrost: 2 Domain, 77 Application, 1 DI i 27 PostgreSQL. Restore/Debug/Release PASS, 0 ostrzeżeń/błędów; snapshot aktualny. Regresje WP-03.1 i WP-03.2 zawarte w tych liczbach. Stryker Application **99,42%**, 170 Killed, 1 równoważny Survived, 15 CompileError; szczegóły liczników i granicy mutanta w raporcie. Nowego coverage/mutation Domain, Angular/E2E/manualnych nie uruchamiano.

Developerka bez zmian: identyczny logiczny dump SHA256 be2f8674ec8f1819e2ba85f5f90a5dcbdf1cd01f511d08b268904004f984f280 oraz historia zawierająca tylko InitialCreate. Compose config PASS, wszystkie 3 usługi działają, api/db healthy; obrazów nie przebudowywano, więc nie stanowią wdrożenia nowego kodu. Sekrety i artifacts pozostają poza Git. Bez staging, commit i push; wcześniejsze lokalne zmiany zachowane.

Ograniczenia: brak HTTP/auth/UI, osobnego wyposażenia oraz wspólnej koordynacji Archive z równoczesnym CreateReservation; xmin chroni zapisy sali, nie transakcję tworzącą rezerwację. Paginacja offsetowa jest stabilna dla niezmienionego zbioru. Nie deklarujemy całego FR-ROOM-002/005 jako ukończonego produktu. [Architektura i diagramy](../architecture/ROOM_MANAGEMENT.md), [raport i pełna tabela AC](../testing/WP03_3_REPORT.md). **WP-03.4 nie rozpoczęto.** Wcześniejsze sekcje zachowują wyniki historyczne i ich ówczesny zakres.


## WP-03.4 — konta i wspólne uprawnienia, 18.10.2026

**READY WITH WARNINGS, 30/30 AC PASS dla zleconego Application/Infrastructure i mechanizmu Identity/cookies.** CreateUser/GetUsers/GetUserById/UpdateUser/DeactivateUser/ActivateUser; wszystkie wymagają aktywnego administratora. Application nie zależy od Identity/EF/HTTP. IUserAccountStore implementuje IdentityUserAccountStore z UserManager, RoleManager i jednym Scoped DbContext. Create atomowo tworzy konto i rolę User, bez dowolnego nadawania ról i bez publicznej rejestracji. Update przez SetEmail/SetUserName utrzymuje login/normalizację, ConcurrencyStamp chroni zapisy, rollback cofa częściowe zmiany. Dezaktywacja/aktywacja zachowują hasło/role/rezerwacje, obracają SecurityStamp; advisory lock chroni ostatniego aktywnego administratora także przy dwóch równoczesnych próbach.

Wspólny ApplicationAccess sprawdza Guid, IsActive i wymaganą rolę dla całego WP-03.1–03.4. ICurrentUser.IsActive domyślnie false. Produkcyjny HttpIdentityCurrentUser korzysta z principal Identity; aktywność i stamp są walidowane co uwierzytelnione żądanie, role odświeżane z bazy. ActiveAccountSignInManager odrzuca nieaktywne konto w standardowym sprawdzeniu hasła. Stara sesja nie wraca po aktywacji. Zachowano politykę cookies; nie dodano endpointu logowania.

**557/557 testów:** Domain 118, Application 228, integracyjne 211 = 78 bez Dockera + 133 PostgreSQL. Przyrost 64 Application i 24 integracyjne. Wszystkie poprzednie regresje zawarte; 0 failed/skipped. Restore, Debug i Release PASS, po 0 warnings/errors. Stryker Application **99,17%: 238 Killed, 2 równoważne Survived granicy offsetu, 33 CompileError**, bez NoCoverage/Timeout/RuntimeError; zakres i liczniki w raporcie. Coverage/mutation Domain i Angular/E2E/manualnych nie powtarzano.

Bez nowej migracji: model/snapshot i dotychczasowe migracje bez zmian, has-pending-model-changes PASS. Logiczny dump developerki SHA256 be2f8674ec8f1819e2ba85f5f90a5dcbdf1cd01f511d08b268904004f984f280 i historia tylko InitialCreate identyczne przed/po. Brak seedowania kont, usuwania danych, staging/commit/push. Compose config PASS, 3 usługi running, api/db healthy, obrazy bez przebudowy.

Granice: brak login/logout/me, biznesowych endpointów i Angulara; HTTP cookies testowane przez kontrolery wyłącznie testowe, nie jako gotowe logowanie produktu. Pierwszy administrator wymaga osobnego kontrolowanego bootstrapu. Aktywne żądania rozpoczęte przed commit nie są cofane; każde następne sprawdza konto. Walidacja sesji czyta Identity co żądanie. CSRF/TLS/trwały key ring pozostają przyszłym wdrożeniem, a wyścig Archive/CreateReservation z WP-03.3 nie jest zmieniony. Obie wcześniejsze migracje xmin nadal niewdrożone do developerki. [Architektura](../architecture/USER_MANAGEMENT.md), [raport z AC i listą plików](../testing/WP03_4_REPORT.md). **WP-03.5 nie rozpoczęto.** Wcześniejsze wpisy zachowują wyniki i ograniczenia historyczne.

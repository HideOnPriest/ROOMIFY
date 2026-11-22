# Śledzenie wymagań WP-00 — stan po WP-03.4

Stan: **18.10.2026**. Źródła identyfikatorów: [REQUIREMENTS.md](../ai/REQUIREMENTS.md), [BUSINESS_RULES.md](../ai/BUSINESS_RULES.md), [TEST_STRATEGY.md](../ai/TEST_STRATEGY.md). Zachowano oznaczenia zatwierdzeń `[Z]` i propozycji `[P]`; ta macierz ich nie zmienia. Wcześniejsze zlecenia WP-02 wybrały zakres implementowanych reguł, nie zatwierdzając całego MVP.

Wyniki PASS poniżej odnoszą się **wyłącznie do wskazanego zakresu i poziomu testu**. PARTIAL oznacza fundament domenowy/bazodanowy z testami PASS, lecz brak kompletnej funkcji produktu. NOT IMPLEMENTED / PLANNED oznacza brak implementacji i brak dowodu wykonania przyszłego scenariusza. SELECT 1, /health, konfiguracja cookies ani sam FK nie dowodzą realizacji operacji biznesowej HTTP. Przykładowe TC-I-* i TC-E-* ze strategii są planem; nie przypisujemy im wyników testów bazodanowych.

Skróty: U — xUnit Domain, I-M — metadane/DI EF, I-PG — PostgreSQL Testcontainers, I-HTTP — WebApplicationFactory. Bieżące wykonanie WP-03.4: **Domain 118/118, Application 228/228, integracyjne 211/211**, brak failed/skipped. Pełne nazwy metod i dane są w źródłach testów i lokalnym TRX; [raport odbioru](WP02_ACCEPTANCE_REPORT.md) zawiera komendy oraz granice dowodów.

## Wymagania funkcjonalne

| FR z WP-00 | Istniejąca implementacja / brakujący zakres | Przypadki i poziom | Wynik wymagania produktu |
|---|---|---|---|
| FR-AUTH-001 | Identity i ActiveAccountSignInManager, rzeczywisty CheckPasswordSignInAsync | INT-USR-010: aktywne hasło PASS, nieaktywne NotAllowed; potok cookies z realnym kontem PASS | PARTIAL; produkcyjny endpoint logowania i UI PLANNED |
| FR-AUTH-002 | Schemat cookies; brak wylogowania | Konfiguracja i testowe cookies PASS; brak testu biznesowego logout | NOT IMPLEMENTED / PLANNED |
| FR-AUTH-003 | Brak endpointu profilu | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-AUTH-004 | CreateUserService, aktywny admin, tylko User, transakcja i UNIQUE | 64 nowe Application; INT-USR-001/002/003/004/012/013/014, rollback roli PASS | PASS Application/Identity/SQL; HTTP/UI PLANNED |
| FR-AUTH-005 | DeactivateUserService, IsActive i nowy stamp, kontrola cookies co żądanie | INT-USR-007/008/010/011, stary bilet 401 również po aktywacji; historia i role zachowane | PASS Application/Identity i mechanizmu middleware; endpoint administracyjny/UI PLANNED |
| FR-AUTH-006 | GetUsersService, admin, SQL Search/IsActive i strony z limitem 100 | 6 sortowań PostgreSQL, bez poufnych kolumn; macierz uprawnień PASS | PASS Application/SQL; endpoint/UI PLANNED |
| FR-ROOM-001 | GetRoomsService, aktywne sale dla użytkownika i zakres administratora | RoomManagementTests Unit; INT-ROOM-008/009 i TrustedIdentity I-PG PASS | PASS Application/SQL; HTTP/UI PLANNED |
| FR-ROOM-002 | GetRoomByIdService i RoomDetails, bez wyposażenia jako osobnego pola | GetById_Visibility; INT-ROOM-001/002 i TrustedIdentity PASS | PARTIAL: odczyt Application PASS, wyposażenie/HTTP/UI PLANNED |
| FR-ROOM-003 | Filtry Search/Location/MinCapacity/IsActive, sortowanie i strony w PostgreSQL | List_* Unit; INT-ROOM-008/009 SQL/8 sortowań PASS | PASS Application/SQL; HTTP/UI PLANNED |
| FR-ROOM-004 | CreateRoomService, admin, Domain, precheck i fizyczne UNIQUE | Unit Create_* i Writes_*; INT-ROOM-001/010/012 PASS | PASS Application/SQL; HTTP/UI PLANNED |
| FR-ROOM-005 | UpdateRoomService, admin, tracking/xmin, zachowanie historii i Id/CreatedAt | Unit Update_*; INT-ROOM-003/011 i konflikt nazwy PASS | PARTIAL: cztery pola Application PASS, wyposażenie/HTTP/UI PLANNED |
| FR-ROOM-006 | ArchiveRoomService i ActivateRoomService, bez DELETE i bez anulowania rezerwacji | Unit ArchiveAndActivate_*; INT-ROOM-004/005/006/007/013 PASS | PASS Application/SQL po zakończonej archiwizacji; wyścig Archive/Create i HTTP/UI poza dowodem |
| FR-RES-001 | CreateReservationService, ICurrentUser, aktywna sala, domena, precheck i końcowy exclusion; brak HTTP/UI | CreateReservationServiceTests — U Application 32/32; INT-RES-001–009 — I-PG PASS | PASS dla przypadku użycia Application; PARTIAL produktu, brak 201/HTTP/UI i adaptera rzeczywistej tożsamości |
| FR-RES-002 | Brak odczytu HTTP i kontroli właściciela | MIG-015 sprawdza tylko persystencję; brak testu uprawnień produktu | NOT IMPLEMENTED / PLANNED |
| FR-RES-003 | Brak listy własnych rezerwacji | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-RES-004 | UpdateReservationService i Domain.UpdateDetails walidują tytuł/okres, właściciela/admina i wersję; bez przenoszenia sali | UpdateReservationServiceTests 37/37; INT-UPD-001–007, INT-SEC-001/002 — PASS | PASS przypadku użycia Application; pełny HTTP/UI i zmiana sali nadal PLANNED |
| FR-RES-005 | CancelReservationService, Domain.Cancel, zachowana historia, zwolniony slot, kontrola właściciela/admina | CancelReservationServiceTests 18/18; INT-CAN-001–005 i INT-SEC-003/004 — PASS | PASS Application; endpoint i potwierdzenie UI nadal PLANNED |
| FR-RES-006 | Brak administracyjnej listy i filtrów | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-RES-007 | DomainException i CreateReservationResult rozróżniają błędy; brak kontraktu HTTP | DomainContractTests/CreateReservationServiceTests — U; INT-RES-008/009 — I-PG PASS | PASS wyniku Application; PARTIAL produktu, mapowanie HTTP PLANNED |
| FR-CAL-001 | Brak kalendarza API/UI | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-CAL-002 | Brak filtrów kalendarza | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-CAL-003 | Brak formularza i wyboru terminu | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-UI-001 | Szkielet Angular z WP-01; brak pełnych widoków biznesowych | Frontendu nie uruchamiano w WP-02.5; historyczne testy nie zastępują odbioru responsywności | NOT IMPLEMENTED / PLANNED dla pełnych widoków |
| FR-UI-002 | Brak stanów widoków danych | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-UI-003 | Brak potwierdzenia anulowania w UI | Brak testu produktu | NOT IMPLEMENTED / PLANNED |
| FR-ADMIN-001 | Brak statystyki obłożenia | Brak testu produktu | NOT IMPLEMENTED / PLANNED |

## Reguły biznesowe

| BR | Klasy i rzeczywisty zakres | Testy / poziom | Wynik wykonania i granica |
|---|---|---|---|
| BR-001 | ReservationPeriod; ck_reservations_period | Constructor_RejectsEqualOrEarlierEnd, Constructor_OrdersInstantsInsteadOfDisplayedLocalTimes — U; MIG-006 — I-PG | PASS, end > start |
| BR-002 | ReservationTimePolicy.Validate; wstrzyknięty TimeProvider w Reservation | Booking_UsesInjectedClockAndRequiresStrictlyFutureStart; ChangePeriod_RevalidatesRulesAndKeepsOldDataOnFailure — U | PASS, przeszłość i równość odrzucone; brak zegara bieżącego w CHECK |
| BR-003 | ReservationPeriod.Overlaps, Reservation.Blocks; precheck repozytorium i ex_reservations_active_period | Overlaps_UsesHalfOpenIntervalsSymmetrically, Blocks_ActiveReservationDoesNotBlockAdjacentOrSeparateTerms — U; MIG-007 i INT-RES-002/008 — I-PG | PASS geometrii, przypadku użycia i trwałej ochrony aktywnych terminów; HTTP PLANNED |
| BR-004 | Półotwarte okresy i tstzrange '[)' | Overlaps_UsesHalfOpenIntervalsSymmetrically — U; MIG-008 — I-PG | PASS dla styku z obu stron |
| BR-005 | ReservationAccess: właściciel lub zaufany administrator dla Update/Cancel | Macierz czterech kombinacji unit; INT-SEC-001–004 — PASS | PARTIAL: edycja/anulowanie Application PASS, autoryzacja odczytu/HTTP PLANNED |
| BR-006 | CreateReservationService sprawdza Room.IsActive przed dostępnością/zapisem | Create_MissingOrArchivedRoomDoesNotCheckAvailabilityOrSave — Application; INT-RES-005 — I-PG | PASS przy tworzeniu, bez protokołu współbieżności z przyszłą archiwizacją |
| BR-007 | ReservationTimePolicy, Duration | Booking_AcceptsOpeningClosingAndEightHourBoundaries / RejectsInvalidBusinessPeriod — U | PASS: dokładnie 8 h dozwolone, 8 h + 1 min / tick odrzucone |
| BR-008 | Europe/Warsaw, polityka 08–20 | Booking_RespectsWarsawOffsetsBeforeAndAfterBothDstChanges, StandaloneTimePolicy_UsesWarsawOnBothDstTransitionDates — U | PASS; zimą/latem oraz obie daty przejścia DST |
| BR-009 | Reservation.Blocks/Cancel; predicate status='active' | Cancel_ChangesStatusAndFreesTermWithoutDeletingReservation — U; MIG-009 — I-PG | PASS: cancelled nie blokuje |
| BR-010 | PostgreSQL exclusion między transakcjami | MIG010_ConcurrentTransactionsCannotDoubleBook, 12 przypadków — I-PG | PASS: 24 próby zapisu, 12 commit, 12 odrzuceń, po 1 aktywnym wierszu; nie są to dwa żądania HTTP |
| BR-011 | CreateUser wymaga aktywnego administratora; brak pola roli, zawsze User | Unit/INT-USR-012/013 i brak publicznych tras PASS | PASS Application; bootstrap pierwszego administratora i HTTP PLANNED |
| BR-012 | UpdateReservationService ponownie waliduje tytuł/okres, salę, dostępność i wyklucza własne ID | UpdateReservationServiceTests; INT-UPD-001/002/003 — PASS | PASS edycji tytułu/okresu w tej samej sali; przenoszenie sali/HTTP PLANNED |
| BR-013 | Application weryfikuje istnienie/aktywność sali i zaufany UserId, FK chroni istnienie użytkownika | INT-RES-005/006/009, Application mocks; MIG-012/013 — PASS | PARTIAL: sala PASS, aktywność konta i adapter sesji Identity nadal PLANNED |
| BR-014 | ReservationTimePolicy porównuje daty warszawskie | Policy_RejectsMultiDayPeriodEvenWhenUtcDatesDifferFromWarsaw — U | PASS, także ta sama data UTC i różne daty lokalne |
| BR-015 | Pełne minuty w Validate | Booking_RejectsInvalidBusinessPeriod — U | PASS: sekundy i tick po obu końcach odrzucone |
| BR-016 | Reguły zakresu, dodatnia długość i godziny biznesowe | Booking_AcceptsOpeningClosingAndEightHourBoundaries / RejectsInvalidBusinessPeriod — U | PASS: 08:00/20:00 jako właściwe granice |
| BR-017 | EnsureCanChange w ChangePeriod/UpdateDetails/Cancel, przyszłe Active | Unit Domain/Application, StartedReservation* i AlreadyCancelled; INT-UPD-004 — PASS | PASS w Domain/Application, także dla administratora; powtórny Cancel zgodnie z BR-027 |
| BR-018 | CancelReservationService wywołuje Domain.Cancel, nie Delete | INT-CAN-001/002/003 i unit — PASS | PASS: status, historia i zwolniony termin; transport HTTP PLANNED |
| BR-019 | Archive zachowuje obiekt; Restrict historii; kontrola IsActive w CreateReservationService | Archive_IsIdempotentAndKeepsDataEditableWithoutReactivation — U; MIG-014 i INT-RES-005 — I-PG | PASS historii i zakazu nowej rezerwacji nieaktywnej sali; operacje HTTP PLANNED |
| BR-020 | Normalizacja Identity, UNIQUE email/login, kontrolowany DuplicateEmail | INT-USR-004/005/006, fizyczny indeks i dwie równoległe próby PASS | PASS Application/Identity/SQL; HTTP PLANNED |
| BR-021 | ApplicationAccess, IsActive w CanSignIn oraz walidatorze stamp co żądanie | Unit odmowy wszystkich 9 wcześniejszych usług; INT-USR-010/011 i raw IsActive bez obrotu stamp PASS | PASS mechanizmu Identity/Application/cookies; brak produkcyjnego endpointu logowania |
| BR-022 | Request bez UserId; właściciel wyłącznie z ICurrentUser, brak roli pozwalającej tworzyć w cudzym imieniu | Create_ValidRequestSavesExactlyOnceWithTrustedOwnerAndUtcData, Request_HasNoClientControlledIdentityField, testy braku tożsamości — Application; INT-RES-001 — I-PG | PASS w Application; realny zaufany adapter Identity/HTTP PLANNED |
| BR-023 | Id/RoomId/UserId nie są zmieniane przez Update, właściciel nie jest w DTO | Unit i INT-UPD-007/INT-SEC-002 — PASS | PASS Application/persystencji, ochrona przyszłego HTTP DTO do podłączenia |
| BR-024 | DomainGuard.RequiredText, Reservation.Create; CHECK tytułu | Create_RejectsMissingTitle / AcceptsTitleLengthBoundariesAfterTrim / RejectsTitleLongerThan120Characters — U; audit kolumn I-PG | PASS dla modelu; pełna obsługa edycji tytułu PLANNED |
| BR-025 | Room/DomainGuard i ux_rooms_name z ICU | RoomTests.Create_* / UpdateDetails_* — U; RoomChecks_* / StatusAndCaseInsensitiveUniqueness_AreEnforced — I-PG | PASS: limity nazwy/lokalizacji, pojemność, normalizacja i unikalność case-insensitive |
| BR-026 | Precheck Application jest informacyjny, baza ostatecznie wymusza exclusion przy zapisie | MIG-007/010/011 i INT-RES-007/008 — I-PG | PASS precheck i ochrony przy zapisie; kalendarz PLANNED |
| BR-027 | Powtórny Cancel daje AlreadyCancelled bez zmiany metadanych; dwie stare wersje rozstrzyga xmin | INT-CAN-004/005 oraz unit — PASS | PASS Application/persystencji: jeden sukces i konflikt wersji; endpoint HTTP PLANNED |
| BR-028 | Wspólny guard i zaufany adapter Identity dla sal | Unit sal, HTTP testowego GetRooms przed/po dezaktywacji PASS | PASS Application i technicznego potoku; produkcyjne endpointy sal/kalendarza PLANNED |

## Przypadki graniczne WP-00

| EDGE z BUSINESS_RULES | Dowód rzeczywisty |
|---|---|
| EDGE-001 | MIG-008 + Overlaps_UsesHalfOpenIntervalsSymmetrically: PASS |
| EDGE-002/003/004 | MIG-007 + parametryzacja Overlaps: PASS |
| EDGE-005 | MIG-006 + Constructor_RejectsEqualOrEarlierEnd: PASS |
| EDGE-006/007/008/009 | Booking_AcceptsOpeningClosingAndEightHourBoundaries / RejectsInvalidBusinessPeriod: PASS |
| EDGE-010 | Reguła jednego dnia: Booking_RejectsInvalidBusinessPeriod i Policy_RejectsMultiDayPeriodEvenWhenUtcDatesDifferFromWarsaw: PASS; konkretna wielodobowa próba może wcześniej naruszyć także limit 8 h |
| EDGE-011 | MIG-009: PASS |
| EDGE-012 | MIG-010 × 12: PASS na poziomie transakcji PostgreSQL; odpowiedzi HTTP PLANNED |
| EDGE-013 | Model dopuszcza niezmieniony okres; bez osobnego serwisu konfliktów. Kontrola PostgreSQL UPDATE bez zmiany okresu: test dopisany w odbiorze, patrz raport |
| EDGE-014 | ChangeAndCancel_RequireOriginalReservationNotStarted: PASS |
| EDGE-015 | Booking_RejectsInvalidBusinessPeriod: PASS, wybrane odrzucenie w domenie; kontrakt API PLANNED |

## Wymagania niefunkcjonalne istotne dla odbioru

| NFR | Bieżący dowód / ograniczenie |
|---|---|
| NFR-001 | Szkielety backend/frontend istnieją; brak biznesowego przepływu HTTP/E2E — PARTIAL |
| NFR-002 | Testowe cookies/401/403 PASS, brak autoryzacji zasobów produktu — PARTIAL |
| NFR-003 | Test stores potwierdza hash hasła i weryfikację na PostgreSQL; pełne bezpieczeństwo wdrożenia/session PLANNED |
| NFR-004 | Zaimplementowane reguły czasu, integralności i aktywności sali PASS; aktywność konta i dalsze uprawnienia PLANNED |
| NFR-005 | 12 równoczesnych prób transakcyjnych — PASS dla PostgreSQL; pełne requesty HTTP PLANNED |
| NFR-006 | Brak kontraktu błędów produktu — NOT IMPLEMENTED / PLANNED |
| NFR-007 | Przegląd źródeł i kontrola lokalnego hasła w kandydatach Git; pełny audyt przyszłego logowania PLANNED |
| NFR-008 | Unit/integration/mutation rzeczywiście wykonane; E2E biznesowe PLANNED |
| NFR-009 | Trzy historyczne scenariusze manualne NOT RUN; brak wykonania w WP-02.5 |
| NFR-010 | Restore/build i testy na bieżącym środowisku PASS; nie wykonano instalacji od zera na czystym hoście |
| NFR-011 | Nowe komentarze i dokumentacja po polsku; raporty narzędzi zachowują oryginalne nazwy/treści |
| NFR-012 | Nie wykonywano odbioru dostępności UI w WP-02.5 — PLANNED |

**Wniosek:** odbiór WP-02 dotyczy fundamentu domeny, mapowań i bazy. Nie oznacza ukończenia wszystkich FR, pełnej autoryzacji ani gotowości całego MVP. Sprzeczności wymagającej zmiany wymagań lub produkcyjnego kodu/migracji nie wykryto.

## Przyrost WP-03.1 i aktualna granica dowodu

Poprzedni odbiór WP-02 opisuje fundament. WP-03.1 dodaje **działający przypadek użycia tworzenia rezerwacji**, nie endpoint. Nowe 32 jednostkowe Application i 24 integracyjne przechodzą; cały backend 280/280. Statusy tabeli powyżej należy czytać wraz z tym przyrostem, bez uznawania pełnych przepływów HTTP/UI za wykonane.

| Wymaganie / reguła WP-00 | Aktualny dowód WP-03.1 | Wynik i granica |
|---|---|---|
| FR-RES-001 / UC-005 | CreateReservationServiceTests; INT-RES-001–009, 21 przypadków PostgreSQL nowej klasy | PASS tworzenia przez Application i persystencję; brak HTTP 201, UI oraz adaptera rzeczywistego uwierzytelnienia |
| FR-RES-007 | CreateReservationResult ze zróżnicowanymi statusami/kodami, testy walidacji/repozytoriów i INT-RES-008/009 | PASS odróżnienia wyników Application; kontrakt komunikatów HTTP nadal PLANNED |
| BR-001/002/007/008/014/015/016/024 | 14 wariantów wejścia Application + niezmieniona domena i jej 110 testów | PASS; istniejąca domena jest źródłem walidacji, brak zapisu/odczytu po błędzie |
| BR-003/004/009 | SQL EXISTS po aktywnych zakresach, INT-RES-002/003/004 i inna sala | PASS rzeczywistego precheck oraz zapisu; styk/cancelled/inne sale dozwolone |
| BR-006 / część BR-013/019 | Kontrola sali po PK i IsActive przed zapisem; INT-RES-005/006 | PASS sali; koordynacja z równoczesną archiwizacją poza zakresem |
| BR-010 / NFR-005 | INT-RES-007, dwie poprawne serie po 6; dwie realne kontrole dostępności i współbieżne zapisy | PASS, zawsze 1 Success i 1 aktywny rekord; odrzucenia 23P01→Conflict albo 40P01→InfrastructureFailure |
| BR-022 | ICurrentUser, brak UserId w request, brak tożsamości nie dotyka bazy | PASS Application, realny adapter Identity i kontrola aktywności konta PLANNED |
| BR-026 | Precheck nie rezerwuje terminu; INT-RES-008 zajmuje termin między SELECT i INSERT | PASS: właściwy 23P01 daje Conflict, nie pozostaje kandydat do ponownego SaveChanges |
| NFR-006/007 | Wynik bez SQL, Npgsql i connection stringów; rozróżnienie FK/innego exclusion/deadlocka/połączenia | PASS granicy Application, pełny kontrakt HTTP/audyt logowania sesji nadal PLANNED |
| NFR-008 | Unit/Application/integration i Stryker 19/19 Killed badanego serwisu | PASS wybranego zakresu; E2E biznesowe/manualne nie były wykonywane |

Pozostałe wymagania logowania, odczytu/edycji/anulowania, CRUD, sesji/CSRF, kalendarza i UI zachowują status przyszłego zakresu. Szczegółowe wyniki i ograniczenia: [raport WP-03.1](WP03_1_REPORT.md), [architektura](../architecture/RESERVATION_APPLICATION.md). Nie zmieniono identyfikatorów ani treści dokumentów WP-00.

## Przyrost WP-03.2

**362/362** backend, z rzeczywistym PostgreSQL i dotychczasową regresją WP-03.1. Nie przypisujemy tym testom kodów odpowiedzi HTTP, którego nadal nie dodano. Poniższe stany opisują obecne Application; zapisy historyczne przyrostu WP-03.1 wyżej zachowują kontekst etapu.

| FR/BR/NFR WP-00 | Klasy i testy | Wynik / granica |
|---|---|---|
| FR-RES-004, BR-012/017/023/024 | UpdateReservationService, ValidateUpdate/UpdateDetails; 37 nowych unit Update, INT-UPD-001–007 | PASS tytułu/okresu tej samej przyszłej aktywnej rezerwacji, bez zmiany właściciela; brak HTTP i przenoszenia sali |
| FR-RES-005, BR-009/017/018/027 | CancelReservationService, Domain.Cancel; 18 unit Cancel, INT-CAN-001–005 | PASS historii/zwolnienia slotu i AlreadyCancelled bez nowego zapisu |
| BR-005 i część NFR-002 | ReservationAccess i ICurrentUser.IsAdministrator; INT-SEC-001–004 | PASS właściciela/admina dla obu operacji, brak ujawnienia stanu obcemu; odczyt i pełny adapter HTTP/Identity PLANNED |
| BR-003/004/012/026 | HasActiveConflictExcludingAsync, istniejący exclusion; INT-UPD-002/003 | PASS precheck z własnym ID i końcowego 23P01; nie myli konfliktu terminu z wersją |
| NFR-005 i integralność zmian | Shadow xmin, IsRowVersion, DbUpdateConcurrencyException; INT-UPD-005/006 i INT-CAN-005, po 3 próby | PASS: jeden Success, drugi ConcurrentModification, zachowany stan zwycięzcy, bez retry/timeoutu jako sukcesu |
| NFR-006/007 | Wspólny ReservationOperationResult/Status, bez SQL i wyjątków providera | PASS granicy Application; kontrakt HTTP PLANNED |
| NFR-008 | Domain 116, Application 87, integracyjne 159; Stryker 63/63 Killed | PASS wykonanych poziomów; Domain coverage/mutation i E2E/manualne niepowtarzane |

Nowa migracja mapowania natywnego xmin przetestowana Up/Down z zachowaniem danych; w developerce nadal tylko InitialCreate w historii, bez zmiany dumpa. Model/snapshot spójne. Szczegóły: [architektura zarządzania](../architecture/RESERVATION_MANAGEMENT.md), [raport WP-03.2](WP03_2_REPORT.md). Pozostałe FR i aktywność konta/sesje/CSRF/adapter ról/ETag klienta pozostają poza zakresem. WP-00 i jego identyfikatory bez zmian.

## Przyrost WP-03.3

**469/469** backend, w tym 77 nowych Application i 27 nowych PostgreSQL. Pełne dowody: [raport](WP03_3_REPORT.md), [sale](../architecture/ROOM_MANAGEMENT.md). Historyczne wyniki WP-03.1/03.2 pozostają powyżej jako wcześniejsze etapy.

| Reguła / obszar | Dowód | Granica |
|---|---|---|
| BR-025 | Domain, Application Create/Update, INT-ROOM-010/012, wyścig nazwy i Update DuplicateName | PASS walidacji i fizycznej unikalności także archiwalnych sal |
| BR-019 / część BR-006/013 | INT-ROOM-004/005/006/007/013, zachowane snapshoty wszystkich rezerwacji | PASS zakończonej archiwizacji/aktywacji; wyścig Archive/Create nadal poza zakresem |
| BR-028 / dostęp administracyjny | 6 usług, domyślna odmowa, Unit + TrustedIdentity na realnych repozytoriach | PASS Application; adapter uwierzytelnienia HTTP i aktywność konta PLANNED |
| NFR integralności / wersja sali | INT-ROOM-011, dwa niezależne scope i prawdziwe xmin; 1 Success + 1 ConcurrentModification | PASS dwóch edycji oraz edycja/archiwizacja; bez automatycznego retry |
| NFR testowalności | Regresja 469, Stryker 99,42%, migracja up/down i dump developerki przed/po | PASS opisanego zakresu; brak nowego E2E/manualnego i pomiaru coverage Domain |

Activate jest jawnie zleconym przywróceniem IsActive, nie reaktywuje anulowanych rezerwacji. Zmiana pojemności nie ma reguły automatycznego anulowania. Nie zmieniono czterech dokumentów WP-00 ani oznaczeń zatwierdzeń. **WP-03.4 nie rozpoczęto.**

## Przyrost WP-03.4

**557/557 backend**, 64 nowe jednostkowe Application, 22 PostgreSQL i 2 bez Dockera. [Raport z 30 AC](WP03_4_REPORT.md), [architektura kont](../architecture/USER_MANAGEMENT.md). Wcześniejsze sekcje WP-03.1–03.3 są historią; ich ówczesne ograniczenie braku adaptera i kontroli aktywności zamyka bieżąca implementacja.

| Obszar | Dowód | Granica |
|---|---|---|
| UC-002 / FR-AUTH-004/005/006 | 6 usług, UserManager/RoleManager, transakcje, DTO, SQL filtry/strony, 15 INT-USR | PASS zakresu Application/Infrastructure; nie gotowe endpointy produktu |
| BR-011/020/021 | Tylko User; unikalny e-mail; nieaktywność blokuje logowanie mechanizmu i chronione żądania | PASS w testach realnego Identity i cookies; brak publicznej rejestracji |
| BR-005/022/023 i sala admin | Regresja poprzednich usług + wspólny ApplicationAccess + brak danych aktora w DTO | PASS bez zmiany logiki terminów ani przenoszenia rezerwacji |
| NFR-002/003/007 | Standardowy hash, brak danych wrażliwych w DTO, test 200/403/401 realnego potoku | PASS opisanego mechanizmu, nie produkcyjnego login HTTP ani pełnego CSRF |
| Integralność kont | Rollback roli/częściowego e-maila, ConcurrencyStamp, wyścig ostatnich adminów | PASS, 1 Success + 1 kontrolowany konflikt/LastAdministrator |
| NFR-008 | Stryker Application 99,17%, 238/240 Killed, 2 równoważne mutanty offsetu | PASS wybranego zakresu; bez nowego Domain coverage i E2E/manualnego |

Aktywacja nie przywraca starych sesji ani nie zmienia hasła/ról/historii. Nie dodano resetu hasła, JWT ani nadawania Administrator. WP-00 i wcześniejsze raporty bez zmian. **WP-03.5 nie rozpoczęto.**

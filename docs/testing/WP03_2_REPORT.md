# WP-03.2 — edycja i anulowanie rezerwacji

**18.10.2026: wykonane, 25/25 AC PASS dla zakresu WP-03.2.** WP-03.3 nie rozpoczęto. Nie wykonano commit ani push. [RESERVATION_MANAGEMENT.md](../architecture/RESERVATION_MANAGEMENT.md) zawiera opis obu operacji, sekwencje Mermaid, uprawnienia, wyniki i instrukcje; [macierz wymagań](REQUIREMENTS_TRACEABILITY.md) rozróżnia implementację Application od przyszłego HTTP/UI.

## Zmiana zachowania

UpdateReservationService edytuje tytuł i okres istniejącej, przyszłej aktywnej rezerwacji. CancelReservationService zmienia status przez istniejącą metodę Domain.Cancel, zachowując rekord. Uprawniony jest właściciel lub administrator ze **zaufanego ICurrentUser**, nigdy z DTO. Brak uwierzytelnienia jest odrębny od Forbidden. Autoryzacja poprzedza odczyt stanu/zwrot błędów domenowych; błąd nie zawiera danych cudzej rezerwacji.

Request edycji nie zawiera właściciela, roli ani sali, request anulowania nie zawiera statusu. Id/RoomId/UserId/CreatedAt pozostają niezmienne. Powtórne Cancel zwraca **AlreadyCancelled bez zapisu i zmiany UpdatedAt/xmin**, także po pierwotnym początku, zgodnie z dotychczasową idempotencją Domain. Nie ma przywracania Active. Rozpoczęta rezerwacja nadal podlega BR-017, także dla administratora.

Minimalne rozszerzenie Domain: prywatny setter Title, czysta walidacja ValidateUpdate i atomowe UpdateDetails. Wspólne ValidateDetails używa istniejącego DomainGuard/EnsureCanChange/ReservationTimePolicy; nie skopiowano reguł czasu do Application. Błąd walidacji/sali/precheck nie zmienia trackowanej encji. Właściwa mutacja ponownie odczytuje TimeProvider po asynchronicznych kontrolach.

Nie utworzono drugiego mechanizmu Result: istniejące typy uogólniono do **ReservationOperationResult / ReservationOperationStatus / ReservationDetails** (dawniej nazwy CreateReservation*). Dodano UpdatedAt oraz ReservationNotFound, Forbidden, AlreadyCancelled i ConcurrentModification. InvalidInput/Conflict pozostają odpowiednikami ValidationError/ReservationConflict w obecnych konwencjach. Create również korzysta ze wspólnego ReservationSaveResults; wcześniejsze asercje zachowano, dostosowując nazwy typów.

## Wersja i terminy — odrębne problemy

**Konflikt terminu:** precheck EXISTS wyklucza własne ID i bada aktywne półotwarte zakresy tej samej sali. Exclusion ex_reservations_active_period ostatecznie chroni zapis; tylko właściwy 23P01 daje Conflict. Styk i cancelled pozostają dozwolone. Nie pobiera się całej listy ani nie porównuje wyłącznie dnia.

**Konflikt wersji:** systemowe PostgreSQL xmin jest shadow property uint/xid z IsRowVersion w Infrastructure, bez pola domenowego/DTO. FindByIdAsync czyta ze śledzeniem. UPDATE zawiera ID i oryginalne xmin, a RETURNING xmin odświeża token po sukcesie. DbUpdateConcurrencyException jest przechwytywany przed ogólnymi błędami persystencji, własna encja odłączana, wynik to ConcurrentModification. Nie wykonuje się automatycznego odświeżenia i powtórzenia zapisu nad nowszym stanem. [Npgsql — natywne tokeny współbieżności](https://www.npgsql.org/efcore/modeling/concurrency.html).

Application nadal nie zna EF, Npgsql, Identity i HttpContext. Scoped są wszystkie trzy serwisy i repozytoria/DbContext, Singleton TimeProvider; rozszerzono istniejący test kompozycji o nowe serwisy, zakresy i brak duplikatów. Default IsAdministrator jest false, default użytkownik nadal nieuwierzytelniony. Nie dodano frameworka CQRS, GenericRepository, endpointów, logowania, Angulara ani CRUD sal.

## Migracja i developerka

Dodano **20261018104209_ReservationOptimisticConcurrency**, designer i aktualny snapshot. InitialCreate i jego designer bez zmian. Wygenerowane operacje AddColumn/DropColumn xmin są rozpoznawane przez Npgsql jako systemowe i pomijane w DDL. Skrypt Up zawiera **tylko transakcję i wpis historii EF**, bez ALTER/CREATE/DROP kolumny biznesowej. Przejrzano rzeczywisty artifacts/wp032/xmin.sql.

Test jednorazowej bazy: InitialCreate → istniejąca rezerwacja → nowa migracja → Down do InitialCreate → ponowne Up. Za każdym razem row_to_json danych i natywne xmin identyczne, systemowa kolumna zachowana. Po Up są dwa oczekiwane wpisy historii; snapshot bez pending model changes. Pełny dotychczasowy test Down do zera nadal wykonywany jest wyłącznie w prywatnej bazie testowej.

**Nie stosowano nowej migracji do developerskiej roomify_dev**. Zlecenie dopuszcza jej ewentualne zastosowanie po testach, nie wymaga operacji na developerce. Wybrano przygotowanie i weryfikację bez zmiany jej danych/historii: historia nadal `20261018093445_InitialCreate|10.0.9`, nowa migracja oczekuje na jawne wdrożenie. Fizyczny xmin już istnieje jako natywny atrybut, więc nie jest to brak kolumny potrzebnej do runtime. Nie edytowano zastosowanej InitialCreate ani snapshotu bez odpowiadającej nowej migracji.

## Rzeczywiste wyniki

| Kontrola | Wynik |
|---|---|
| dotnet restore backend/Roomify.slnx | PASS; bez dodawania/aktualizacji bibliotek |
| Debug / Release całego rozwiązania | PASS, 0 ostrzeżeń/błędów |
| Domain | **116/116 PASS**, 0 failed/skipped; 110 zachowanych + 6 nowych; runner 86 ms |
| Application | **87/87 PASS**, 0 failed/skipped; Create 32, Update 37, Cancel 18; runner 213 ms |
| Wszystkie integracyjne | **159/159 PASS**, 0 failed/skipped; runner 20 s |
| W tym bez Dockera | **75/75 PASS**, dawne HTTP/EF/Identity/kompozycja |
| W tym PostgreSQL | **84/84 PASS**, dawne 63 + 21 nowych |
| Nowa klasa ReservationManagement | **21/21 PASS**, także uruchomiona osobno |
| Końcowy unikalny zestaw backendu | **362/362 PASS** (116 + 87 + 159) |
| Spójność modelu | has-pending-model-changes PASS, brak zmian od nowej migracji |
| Mutacje Application | **100%**, 63 Killed/63 badane, bez Survived/NoCoverage/Timeout/RuntimeError |
| Developerka | Identyczny logiczny dump i historia przed/po |
| Git, sekrety i architektura | Kontrola źródeł, referencji, kandydatów i ignorowania; staging pusty |

Sesje TRX końcowego wykonania z uruchomieniem runnera: Domain 0,715 s, Application 0,839 s, integracyjne 21,718 s. W tabeli czas samych testów jest oddzielną miarą. Nie sumujemy powtórzeń jako nowych przypadków. Pełny przebieg obejmuje wszystkie podzbiory bez Dockera i PostgreSQL; nie twierdzimy, że każdy filtr wykonywano osobno.

Nie wykonano testów frontend/E2E/manualnych, wydajności/backup-restore ani nowego pomiaru coverage/mutacji Domain. Historyczne procenty Domain po WP-02.5 nie są aktualnym pomiarem nowych metod edycji. Kod API i obrazy Compose nie były aktualizowane/wdrażane w tym pakiecie; działające usługi nie są dowodem publikacji nowej wersji.

## Testy jednostkowe i regresja

Update: 37 przypadków — poprawny tytuł/okres i sam tytuł bez zmiany okresu, zachowanie tożsamości, cztery kombinacje właściciel/admin, null/Empty tożsamości (także z admin=true), null/Empty request/ID, brak rezerwacji, autoryzacja przed AlreadyCancelled, 11 wariantów błędnych danych, granice rozpoczęcia, brak/nieaktywna sala, konflikt z inną rezerwacją, wykluczenie własnego ID, distinct Conflict/ConcurrentModification/infrastructure, cztery granice awarii, anulowany token. Po błędzie walidacji/sali/precheck nie ma mutacji ani Save.

Cancel: 18 przypadków — właściciel/admin/zabronione, brak tożsamości/rekordu/requestu/ID, powtórne anulowanie przed/po pierwotnym początku i brak ujawnienia stanu obcej rezerwacji, granice rozpoczęcia, zachowanie historii, odrębny konflikt wersji, błąd odczytu/zapisu, anulowany token i brak pól pozwalających przemycić owner/room/role/status.

Domain: 6 nowych przypadków — poprawna atomowa aktualizacja, trzy błędne tytuły bez częściowej zmiany okresu, błędny okres bez zmiany tytułu, walidacja bez mutacji. Zachowano wszystkie wcześniejsze testy. Mechaniczne zmiany typów w testach WP-03.1 nie usuwały ani nie osłabiały asercji.

## Integracja — wymagane scenariusze

| ID | Dowód i wynik |
|---|---|
| INT-UPD-001 | Poprawny tytuł i okres zapisane; UTC, UpdatedAt i zmiana natywnego tokenu — PASS |
| INT-UPD-002 | Dwa warianty: precheck i zajęcie terminu po precheck. Oba Conflict, zapisane dane pierwotnej rezerwacji niezmienione — PASS |
| INT-UPD-003 | Edycja samego tytułu w tym samym okresie; SQL EXISTS zawiera id <> własne — PASS |
| INT-UPD-004 | Cancelled odrzucone jako AlreadyCancelled, historia bez zmian — PASS |
| INT-UPD-005 | Dwie edycje tej samej wersji, 3 powtórzenia; jeden Success i jeden ConcurrentModification — PASS |
| INT-UPD-006 | Edycja/anulowanie tej samej wersji, 3 powtórzenia; cały stan zgodny ze zwycięzcą — PASS |
| INT-UPD-007 | ID, RoomId, UserId i CreatedAt zachowane — PASS |
| INT-CAN-001 | Status rzeczywiście Cancelled, metadane zapisane — PASS |
| INT-CAN-002 | Rekord pozostaje pod tym samym ID — PASS |
| INT-CAN-003 | Nowy Create w zwolnionym terminie przechodzi, stary rekord pozostaje — PASS |
| INT-CAN-004 | AlreadyCancelled, brak SQL UPDATE, UpdatedAt i xmin identyczne — PASS |
| INT-CAN-005 | Dwa anulowania tej samej wersji, 3 powtórzenia: jeden Success, drugi ConcurrentModification; następny odczyt/Cancel daje AlreadyCancelled — PASS |
| INT-SEC-001 | Zwykły użytkownik nie edytuje obcej rezerwacji, brak jej danych w wyniku — PASS |
| INT-SEC-002 | Zaufany administrator edytuje cudzą, bez zmiany właściciela — PASS |
| INT-SEC-003 | Zwykły użytkownik nie anuluje cudzej — PASS |
| INT-SEC-004 | Administrator anuluje cudzą, zachowując historię — PASS |

Scenariusze łączą pokrewne wymagania w 21 rzeczywistych przypadkach, w tym 9 równoczesnych i test nowej migracji. Każdy ma osobną bazę w PostgreSQL 18.6 Testcontainers, korzysta z rzeczywistych migracji i standardowego Npgsql. Nie używa SQLite/InMemory ani developerskich połączeń/wolumenu.

## Współbieżność — faktyczne rezultaty

Bariera występuje **po obu prawdziwych odczytach ze śledzeniem**. Obie operacje posiadają tę samą początkową wersję, a potem wykonują się na osobnych zakresach DI, DbContext i połączeniach. Limit 20 s, bez Task.Delay/lock procesu/globalnego wyłączenia równoległości. Test wymaga dokładnie jednego Success, jednego ConcurrentModification oraz zgodności wszystkich zapisanych pól z odpowiedzią zwycięzcy. Timeout lub InfrastructureFailure nie są zaliczane jako konflikt wersji. Przegrana encja jest odłączona, bez ślepego retry.

| Scenariusz, próba | Końcowa regresja A / B | Stan po zapisie |
|---|---|---|
| update-update, 1 | ConcurrentModification / Success | Active, tytuł Wygrywa B, okres B |
| update-update, 2 | ConcurrentModification / Success | Active, tytuł Wygrywa B, okres B |
| update-update, 3 | Success / ConcurrentModification | Active, tytuł Wygrywa A, okres A |
| update-cancel, 1 | ConcurrentModification / Success | Cancelled, pierwotny tytuł/okres |
| update-cancel, 2 | ConcurrentModification / Success | Cancelled, pierwotny tytuł/okres |
| update-cancel, 3 | ConcurrentModification / Success | Cancelled, pierwotny tytuł/okres |
| cancel-cancel, 1 | Success / ConcurrentModification | Cancelled, pierwotny tytuł/okres |
| cancel-cancel, 2 | Success / ConcurrentModification | Cancelled, pierwotny tytuł/okres |
| cancel-cancel, 3 | ConcurrentModification / Success | Cancelled, pierwotny tytuł/okres |

W osobnym podzbiorze i końcowej regresji **18/18 scenariuszy, 36 wywołań, 18 Success i 18 ConcurrentModification**, zawsze jeden zachowany rekord. W każdej serii trzy powtórzenia każdego wariantu. Dodatkowo te same testy przeszły w pierwszym pełnym przebiegu, który miał niezależną usterkę starego testu opisaną poniżej; nie doliczamy go do tych 18. W wariancie mieszanym w obserwowanych seriach wygrało anulowanie — nie deklarujemy pomiaru wszystkich przeplotów schedulera ani deterministycznego wyboru zwycięzcy przez produkt. Gwarancją wersji jest predykat xmin, nie szybkość wykonania testu.

SQL przechwycony testowo zawiera `AND xmin =` i `RETURNING xmin`; token po sukcesie jest inny. Powtórny Cancel nie wykonuje UPDATE ani nie zmienia wersji. Osobne testy nadal wymagają dokładnie 23P01 dla kolizji innego rekordu. Te dwie klasy konfliktów nie zostały połączone w jedną asercję.

## Pierwsze niepowodzenie pełnej regresji

Pierwszy pełny przebieg: Domain 116/116, Application 87/87, integracyjne **158/159** (łącznie 361 PASS, 1 FAIL). Stary `PeriodUpdate_DoesNotConflictWithItselfAndRejectedChangePreservesStoredPeriod` wykonał surowy UPDATE bez zmiany wartości po odczytaniu encji EF. PostgreSQL prawidłowo nadał nową wersję; późniejszy Save używał starego xmin i zgłosił DbUpdateConcurrencyException zamiast oczekiwanego exclusion.

Naprawiono **przygotowanie scenariusza**, nie asercję wyniku: po surowym UPDATE dodano Reload i potwierdzenie, że token się zmienił. Późniejsza edycja nadal musi zgłosić DbUpdateException z dokładnym PostgresException/23P01 i zachować pierwotne dane. Pozostałe testy już osobno sprawdzają konflikt wersji. Końcowo 362/362 PASS. Pierwotne logi/TRX pozostają w artifacts/wp032/regression i regression.log; poprawne w regression-final i regression-final.log.

## Stryker.NET

Ta sama wersja 5.0.0, bez nowych bibliotek/narzędzi. Rozszerzono istniejącą konfigurację Application o UpdateReservationService, CancelReservationService, ReservationAccess i ReservationSaveResults, zachowując CreateReservationService. Obejmuje kontrolę uwierzytelnienia, właściciela/admina, statusu, walidacji i rezultatów zapisu, w tym ConcurrentModification. Nie mutuje DTO/DI/migracji/snapshotu ani technicznych mapowań EF. Sam catch DbUpdateConcurrencyException w Infrastructure jest potwierdzony przez rzeczywiste PostgreSQL, nie zaliczany do score Application.

**Wynik: 100,00%, 63 Killed / 63 badane, 0 Survived, 0 NoCoverage, 0 Timeout, 0 RuntimeError; 5 CompileError. Czas 22,813 s.** Wykryte per klasa: Create 15, Update 24, Cancel 17, Access 4, SaveResults 3. Brak listy Survived, ponieważ nie wystąpiły w badanym zakresie.

Jak we wcześniejszym narzędziu istnieje różnica liczników: **log 82 wygenerowane**, 5 CompileError, 6 Ignored filtrem pokrytych bloków i 8 Ignored filtrem zakresu; **JSON 80 wpisów**, 63 Killed, 5 CompileError i 12 Ignored (6 bloków + 6 jawnych DI). Nie dopisujemy brakujących dwóch wpisów ani nie liczymy ich jako Killed. Oba źródła zgodnie podają 63 badane/wykryte. CompileError to syntetyczne usunięcia bloków skutkujące brakiem zwrotu/niezainicjowaną zmienną, nie błędy normalnego builda. [Definicje stanów Stryker](https://stryker-mutator.io/docs/mutation-testing-elements/mutant-states-and-metrics/).

HTML/JSON: `artifacts/wp032/mutation-initial/reports/mutation-report.html` i `.json`, log stryker-initial.log. Nazwa initial oznacza pierwszy, poprawny przebieg tego pakietu; nie przeprowadzano drugiej sesji, ponieważ brak Survived i nie zmieniano potem produkcyjnego badanego kodu. Nie jest to score całego rozwiązania ani nowy pomiar Domain.

## Dane, Git i granice

Przed implementacją zapisano baseline 167 kandydatów repozytorium oraz logiczny pg_dump developerki. Po wszystkich testach dump i historia nadal identyczne: **SHA-256 be2f8674ec8f1819e2ba85f5f90a5dcbdf1cd01f511d08b268904004f984f280**, `20261018093445_InitialCreate|10.0.9`. Normalizowano tylko losowe linie restrict/unrestrict dumpa. Nie wykonywano developer database update/Down, seeda, usuwania tabel/rekordów/wolumenu. Testy i ich sprzątanie dotyczą wyłącznie prywatnych baz/kontenerów fixture.

Sprawdzono brak zmian czterech dokumentów WP-00, InitialCreate/designer, kodu API, frontendu i referencji projektów. Application → Domain, Infrastructure → Application/Domain, Api → Application/Infrastructure; Domain bez EF/HTTP. Dotychczasowe testy zachowano: zmiany są mechaniczne dla wspólnych nazw typów albo dodają kontrolę xmin, nowej historii i właściwego odświeżenia wersji. Artefakty, .env, bin/obj i node_modules ignorowane, sekret z lokalnej konfiguracji nie występuje w kandydatach Git. Staging pusty, git diff --check bez błędów. Wcześniejsze niezatwierdzone zmiany pozostawiono; nie wykonano commit/push.

## Kryteria AC-001–AC-025

| AC | Status | Dowód |
|---|---|---|
| AC-001 | PASS | UpdateReservationService i INT-UPD-001 |
| AC-002 | PASS | CancelReservationService i INT-CAN-001 |
| AC-003 | PASS | Bez nowej referencji Application; brak EF/Infrastructure/HTTP |
| AC-004 | PASS | Właściciel update/cancel — unit i realny PostgreSQL |
| AC-005 | PASS | Administrator cudzej rezerwacji — INT-SEC-002/004 i unit |
| AC-006 | PASS | INT-SEC-001/003, Forbidden bez danych, brak zapisu |
| AC-007 | PASS | Domain ValidateUpdate/UpdateDetails, 11 wariantów błędnych danych unit |
| AC-008 | PASS | Precheck oraz późny exclusion w INT-UPD-002 |
| AC-009 | PASS | INT-UPD-003, EXISTS z wykluczeniem własnego ID |
| AC-010 | PASS | AlreadyCancelled po autoryzacji, INT-UPD-004 |
| AC-011 | PASS | INT-CAN-001/002, brak Delete w kontrakcie |
| AC-012 | PASS | INT-CAN-003: Create nowej rezerwacji tego slotu |
| AC-013 | PASS | Native xmin, DbUpdateConcurrencyException → ConcurrentModification |
| AC-014 | PASS | Trzy warianty wyścigu × 3, cały stan zgodny z wynikiem zwycięzcy |
| AC-015 | PASS | Osobne Conflict/23P01 i ConcurrentModification/zero wierszy |
| AC-016 | PASS | Domain 116/116, Application 87/87 |
| AC-017 | PASS | PostgreSQL 84/84, nowa klasa 21/21 |
| AC-018 | PASS | INT-UPD-005/006/INT-CAN-005, bez akceptowania timeoutu |
| AC-019 | PASS | Dotychczasowe Create 32 unit i testy WP-03.1 zachowane/przechodzą |
| AC-020 | PASS | Debug całego rozwiązania, bez warning/error |
| AC-021 | PASS | Release całego rozwiązania, bez warning/error |
| AC-022 | PASS | Cztery dokumenty WP-00 bez zmian |
| AC-023 | PASS | Identyczny dump i historia developerska przed/po |
| AC-024 | PASS | RESERVATION_MANAGEMENT.md: 13 tematów, diagramy i instrukcje; status/macierz aktualne |
| AC-025 | PASS | Rzeczywiste wyniki, pierwsze niepowodzenie i ograniczenia w tym raporcie |

## Pliki tego pakietu

- Domain: `Reservations/Reservation.cs`; nowy `backend/tests/Roomify.Domain.UnitTests/ReservationUpdateTests.cs`.
- Application: `Abstractions/ICurrentUser.cs`, `ApplicationServiceCollectionExtensions.cs`, `Reservations/CreateReservationModels.cs`, `CreateReservationService.cs`, `ReservationContracts.cs`; nowe `ReservationManagementModels.cs`, `ReservationAccess.cs`, `ReservationSaveResults.cs`, `UpdateReservationService.cs`, `CancelReservationService.cs` w tym samym katalogu Reservations.
- Infrastructure: `Persistence/Configurations/ReservationConfiguration.cs`, `Persistence/Repositories/EfReservationRepository.cs`; nowa migracja `20261018104209_ReservationOptimisticConcurrency.cs` i `.Designer.cs`, aktualny `RoomifyDbContextModelSnapshot.cs`.
- Application.UnitTests: nowe `ReservationManagementTestContext.cs`, `UpdateReservationServiceTests.cs`, `CancelReservationServiceTests.cs`; dostosowany CreateReservationServiceTests.cs i rozszerzony stryker-config.json.
- Api.IntegrationTests: nowy ReservationManagementTests.cs; dostosowane ReservationApplicationTests.cs, ReservationApplicationRegistrationTests.cs, PersistenceModelTests.cs, PersistenceMaterializationTests.cs, PostgreSqlMigrationTests.cs, PostgreSqlSchemaAuditTests.cs.
- Dokumentacja: RESERVATION_MANAGEMENT.md, WP03_2_REPORT.md, status, macierz, README, dokumentacja testów oraz odnośniki/aktualizacje Domain/Database/Reservation Application.

Pełne ścieżki klas wynikają z katalogów backend/src/Roomify.* i backend/tests/Roomify.*; logi/TRX/SQL/HTML/JSON oraz pomocnicze skrypty są wyłącznie w ignorowanym artifacts/wp032. Źródła migracji są kodem do wersjonowania, raporty generowane nie są dodawane do Git.

## Otwarte ograniczenia i następny etap

Nie ma otwartego błędu integralności w wymaganym zakresie. Nowa migracja modelu/historii jest przetestowana, ale **niewdrożona do developerki**; to jawny stan, nie deklaracja pełnej aktualizacji historii tej bazy. Xmin działa tam natywnie. Nie ma adaptera Identity/HTTP, pełnych sesji/CSRF ani tokenu wersji klienta w request/ETag. Współbieżność chroni wersję odczytaną w danym przypadku użycia, nie dowolną nieaktualną kopię formularza sprzed tego odczytu. Rola i aktywność konta muszą być zapewnione przez przyszły zaufany adapter. 40P01 pozostaje InfrastructureFailure bez retry; nie dodano koordynacji równoczesnej archiwizacji sali. Scope DbContext nie powinien zawierać cudzych oczekujących zmian.

Wynik 100% dotyczy wybranego Application; rozbieżność liczników log/JSON jest jawna. Coverage/Domain mutation, UI/E2E/manualne i wdrożenie kontenerów pozostają poza tym pakietem. **WP-03.3 może korzystać z Create/Update/Cancel i ich wspólnego wyniku; nie rozpoczęto WP-03.3 automatycznie.**

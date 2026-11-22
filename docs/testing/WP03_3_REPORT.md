# WP-03.3 — zarządzanie salami konferencyjnymi

Data: **18.10.2026**. **READY WITH WARNINGS; 26/26 AC PASS w zakresie Application/Infrastructure.** WP-03.4 nie rozpoczęto. WP-00 i wcześniejsze raporty odbioru zachowano bez zmian; niniejszy raport stanowi kolejny przyrost, a nie ponowną deklarację ukończenia całego MVP.

## Implementacja i przypadki użycia

Dodano CreateRoomService, UpdateRoomService, ArchiveRoomService, ActivateRoomService, GetRoomByIdService i GetRoomsService. Zapis wymaga zaufanego, uwierzytelnionego administratora z istniejącego ICurrentUser. Odczyt wymaga uwierzytelnienia; zwykły użytkownik widzi aktywne sale, administrator także archiwalne. Żądanie nie przyjmuje roli ani UserId. Nieaktywne szczegóły są ukryte jako NotFound dla zwykłego użytkownika.

Room.Create/UpdateDetails i czyste ValidateUpdate egzekwują istniejące reguły nazwy/lokalizacji/pojemności. Nowa sala jest aktywna. Update zachowuje Id/CreatedAt/IsActive, waliduje przed mutacją i sprawdza unikalność z pominięciem własnego ID. Archive/Activate zmieniają wyłącznie IsActive; powtórzenie daje AlreadyArchived/AlreadyActive bez dodatkowego zapisu. Wszystkie historyczne, przyszłe i anulowane rezerwacje pozostają niezmienione. Zmiana pojemności nie wywołuje anulowania.

Rozszerzono istniejący IRoomRepository. Odczyt AsNoTracking, aktualizacje przez tracking, Scoped DI bez SQL podczas rejestracji. Wyniki zawierają DTO bez danych użytkowników/rezerwacji i bez wyjątków providera. Rozróżniono DuplicateName, ConcurrentModification, InvalidInput i InfrastructureFailure. Jedynie 23505 indeksu ux_rooms_name jest duplikatem nazwy; PK i CHECK nie są tak mapowane.

GetRooms przekazuje PostgreSQL filtry Search/Location/MinCapacity/IsActive, COUNT, sortowanie i paginację. PageNumber>=1, PageSize 1–100, offset sprawdzany w long, enum sortowania Name/Location/Capacity/CreatedAt i kierunek z walidacją; drugi klucz Id rosnąco. Brak pobierania całej listy do pamięci. Wzorce escapują dosłowne `%`, `_`, `\`. Istniejąca kolacja ICU i PG18 LIKE obsługują nazwę bez rozróżnienia wielkości liter.

Room otrzymał analogiczne do Reservation mapowanie shadow xmin/xid IsRowVersion. Dwa odczyty tej samej wersji i konkurencyjne zapisy kończą się jednym Success i jednym ConcurrentModification. Nie ma automatycznego retry. Nowa migracja `20261018111403_RoomOptimisticConcurrency` aktualizuje model/historię EF, bez fizycznego ADD/DROP COLUMN; snapshot aktualny. Zastosowanej InitialCreate i wcześniejszej migracji Reservation nie zmieniono. Developerka nadal zawiera wyłącznie InitialCreate w historii, obie migracje xmin pozostają do jawnego wdrożenia.

[Pełna architektura, kontrakty i trzy diagramy](../architecture/ROOM_MANAGEMENT.md).

## Rzeczywiste wyniki

| Kontrola | Wynik końcowy |
|---|---|
| dotnet restore backend/Roomify.slnx | PASS |
| Build Debug / Release | PASS / PASS, po 0 ostrzeżeń i 0 błędów |
| Domain | **118/118 PASS**, przyrost 2 |
| Application xUnit/Moq | **164/164 PASS**, przyrost 77 |
| Integracyjne RequiresDocker=false | **76/76 PASS**, przyrost 1 DI |
| Integracyjne RequiresDocker=true | **111/111 PASS**, przyrost 27 PostgreSQL |
| Łącznie backend | **469/469 PASS**, 0 failed/skipped |
| Regresja WP-03.1 i WP-03.2 | PASS, wszystkie poprzednie klasy w powyższych wykonaniach |
| SQL filtrów/paginacji, 8 sortowań i remisy | PASS, wykonany rzeczywisty SQL z WHERE/ORDER BY/LIMIT/OFFSET, projekcja/no tracking |
| Role na realnych repozytoriach | PASS: gość, zwykły użytkownik, administrator; nieaktywna sala ukryta |
| Wyścig nazw | PASS: dwa poprawne prechecki, 1 Success + 1 DuplicateName, 1 rekord |
| Wyścigi wersji | **2/2 PASS**: update/update i update/archive, niezależne DbContext, WHERE id i xmin |
| Migracja up/down na Testcontainers | PASS, sala/rezerwacje/xmin zachowane, 2→3→2→3 migracje |
| has-pending-model-changes | PASS, brak zmian modelu |
| SQL nowej migracji | Wyłącznie transakcja i wpis historii; brak ALTER/ADD/DROP COLUMN |
| Referencje projektowe | PASS: Domain bez referencji; Application→Domain; Infrastructure→Application/Domain; Api→Application/Infrastructure |
| Compose config --quiet / ps | PASS; wszystkie 3 running, db i api healthy; obrazy bez przebudowy |
| Developerka przed/po | PASS, identyczny logiczny dump i historia migracji |
| WP-00, stare raporty, InitialCreate i migracja Reservation | Bez zmian, kontrola SHA256 względem początku WP-03.3 |
| Git, sekrety, wygenerowane pliki, linki dokumentacji | Kontrola kandydatów i chronionych plików; pełna lista poniżej, brak staging/commit/push |

Pierwsze wykonanie PostgreSQL: 109/110 PASS, nowy test usuwania oczekiwał 23503, podczas gdy `ON DELETE RESTRICT` prawidłowo zwrócił 23001. Poprawiono oczekiwanie na RestrictViolation i dodano asercję dokładnej nazwy FK; ochrony danych nie zmieniano. Jedna próba powtórzenia kompilacji kolidowała z równoległą przebudową Strykera (CS0006). Po zakończeniu Strykera wykonano kolejno restore, oba buildy i pełne testy. Końcowy wynik powyżej obejmuje także dodany później test konfliktu nazwy przy edycji. Nie przedstawiamy przerwanych/błędnych prób jako PASS. Lokalny log pierwszej próby zachowano w artifacts/wp033/postgresql-initial.log.

### Scenariusze PostgreSQL

| ID zlecenia | Dowód wykonany w RoomManagementTests |
|---|---|
| INT-ROOM-001 | Create, aktywna sala, normalizacja, data zegara |
| INT-ROOM-002 | Round-trip DTO, 1 rekord, brak tracking odczytu |
| INT-ROOM-003 | Update i niezmienne Id/CreatedAt oraz pełny snapshot rezerwacji |
| INT-ROOM-004 | Archive daje 1 nieaktywny rekord; powtórzenie bez zmiany |
| INT-ROOM-005 | Snapshot wszystkich rezerwacji identyczny po archiwizacji |
| INT-ROOM-006 | CreateReservationService zwraca RoomInactive po archiwizacji |
| INT-ROOM-007 | Activate przywraca nowe rezerwowanie, Cancelled pozostaje Cancelled |
| INT-ROOM-008 | Rzeczywiste SQL filtrów, COUNT i projekcji, także polskie litery i literalne wildcardy |
| INT-ROOM-009 | 8 konfiguracji sortowania, ponowny odczyt stron, brak pominięć/duplikatów dla stałych danych |
| INT-ROOM-010 | Równoległe Create oraz archiwalna nazwa: precheck i fizyczny UNIQUE |
| INT-ROOM-011 | Dwie równoległe edycje oraz edycja/archiwizacja: 1 zwycięzca, 1 konflikt |
| INT-ROOM-012 | 3 CHECK i 2 długości; odrzucony zapis nie zmienia wiersza |
| INT-ROOM-013 | Archiwizacja/aktywacja bez zmian historii; fizyczny DELETE blokuje FK RESTRICT |
| INT-ROOM-014 | Niezależny użytkownik, nazwa bazy i port fixture; dodatkowo dump developerki przed/po |

Nowa klasa PostgreSQL ma **27 przypadków**, ponieważ część numerów łączy w jednym przepływie, a sortowanie/CHECK/role/wyścigi są parametryzowane. Dodatkowe przypadki sprawdzają odrębny konflikt nazwy przy Update, klasyfikację PK/CHECK oraz bezpieczne cofnięcie mapowania. Wszystkie używają rzeczywistych migracji w PostgreSQL 18.6 Testcontainers; brak .env i developerskich wolumenów w fixture.

### Mutacje Application

Stryker.NET **5.0.0**, badane 164 testy jednostkowe. Konfiguracja rozszerza istniejące reguły rezerwacji o CreateRoomService, UpdateRoomService, RoomActivity, GetRoomByIdService, GetRoomsService i RoomRules. Nie mutuje DTO, DI, migracji ani snapshotu.

**99,42% = 170 Killed / 171 ocenionych.** 1 Survived, 15 CompileError; 0 NoCoverage, Timeout i RuntimeError. Log narzędzia: 216 utworzonych, 30 Ignored. Zserializowany JSON: 214 wpisów, 28 Ignored; różnica dotyczy 2 pominiętych wpisów, nie wyniku 170/171.

Survived #190, RoomRules.Valid: zmiana `offset <= int.MaxValue` na `<`. Równość wymaga `(PageNumber-1)*PageSize = 2147483647`. Liczba ta jest pierwsza; przy PageSize 1–100 jedyny dzielnik to 1, co wymaga PageNumber=2147483648 poza zakresem int. Mutant równoważny dla dostępnego kontraktu. Nie usuwano warunku ani nie zmieniano poprawnej logiki w celu osiągnięcia 100%. Nowego coverage/mutation Domain nie uruchamiano; wcześniejsze wyniki pozostają historyczne.

### Developerka i bezpieczeństwo repozytorium

Logiczny dump przed i po: SHA256 `be2f8674ec8f1819e2ba85f5f90a5dcbdf1cd01f511d08b268904004f984f280`. Historia w obu odczytach: `20261018093445_InitialCreate|10.0.9`. Nie wykonano migracji/rollbacku ani operacji na danych developerki; nie usuwano wolumenów. Surowe dumpy i hasła nie trafiły do raportu ani Git. Testy archiwizacji porównują pełny snapshot rezerwacji, a nie jedynie liczbę rekordów.

Sprawdzono .gitignore i kandydatów Git pod kątem rzeczywistego lokalnego sekretu oraz .env/node_modules/bin/obj/artifacts. Pliki wygenerowanych wyników pozostają wyłącznie w ignorowanym artifacts/wp033. Istniejące niezacommitowane zmiany wcześniejszych WP zachowano. Nie wykonano git add, commit ani push.

## AC-001–AC-026

| AC | Wynik | Dowód i granica |
|---|---|---|
| AC-001 | PASS | CreateRoomService + unit + INT-ROOM-001 |
| AC-002 | PASS | UpdateRoomService + unit + INT-ROOM-003 |
| AC-003 | PASS | ArchiveRoomService + INT-ROOM-004 |
| AC-004 | PASS | ActivateRoomService + INT-ROOM-007 |
| AC-005 | PASS | GetRoomByIdService, DTO i macierz widoczności |
| AC-006 | PASS | GetRoomsService i strona SQL |
| AC-007 | PASS | 4 operacje zapisu, zaufany admin, brak SQL po odmowie |
| AC-008 | PASS | Aktywne dla użytkownika, archiwalne dla admina, gość odrzucony |
| AC-009 | PASS | Wspólna walidacja Domain, bez równoległych reguł Application |
| AC-010 | PASS | Unit złych danych/granic + fizyczne CHECK/długości |
| AC-011 | PASS | Po Archive nadal 1 rekord rooms |
| AC-012 | PASS | Pełny snapshot rezerwacji niezmieniony |
| AC-013 | PASS | Istniejący CreateReservation odrzuca zakończoną archiwizację; wyścig opisany osobno |
| AC-014 | PASS | Nowy zapis rezerwacji po Activate, bez reaktywowania Cancelled |
| AC-015 | PASS | SQL filtrów, COUNT/projekcja, brak listy w pamięci |
| AC-016 | PASS | Id jako drugi klucz, 8 sortowań i remisy; stały zbiór danych |
| AC-017 | PASS | xmin/DbUpdateConcurrencyException, 2 rzeczywiste wyścigi |
| AC-018 | PASS | Domain 118, Application 164 |
| AC-019 | PASS | PostgreSQL 111/111, w tym 27 nowych |
| AC-020 | PASS | Poprzednie WP-03.1/03.2 w pełnej regresji |
| AC-021 | PASS | Debug 0 warnings/errors |
| AC-022 | PASS | Release 0 warnings/errors |
| AC-023 | PASS | Cztery dokumenty WP-00 identyczne SHA256 |
| AC-024 | PASS | Dump i historia developerki identyczne |
| AC-025 | PASS | ROOM_MANAGEMENT: 12 tematów, diagramy, macierz, komendy; status i traceability |
| AC-026 | PASS | Niniejsze rzeczywiste wyniki, wcześniejsze błędne próby i ograniczenia jawne |

## Problemy i ograniczenia

- Nie wdrażano HTTP, endpointów sal, cookies/logowania, Angulara ani panelu administracyjnego. Testy ról są testami Application z kontrolowaną zaufaną tożsamością i realnymi repozytoriami. Nie dowodzą gotowości sesji HTTP ani egzekwowania aktywności konta.
- Pełne wyposażenie z FR-ROOM-002/005 nie jest częścią istniejącego modelu i bieżącego kontraktu. Wymagania produktu pozostają PARTIAL; WP-00 bez zmian.
- Wcześniejszy wyścig Archive vs CreateReservation pozostaje: tworzenie czyta aktywność przed INSERT bez wspólnej blokady transakcyjnej. xmin chroni konkurujące zapisy sali, nie oddzielny zapis rezerwacji. Nie deklarujemy odporności tego przeplotu.
- Stabilne sortowanie nie gwarantuje snapshotu przy zmianach zbioru między stronami. COUNT i strona są osobnymi zapytaniami.
- Migracje mapowania xmin są przygotowane i przetestowane, ale niewdrożone do developerki. Obrazów Compose nie przebudowywano dla obecnego kodu.
- Nie uruchamiano nowego Angular/E2E/manualnego, nowego coverage Domain ani CI. Nie przypisano im PASS.
- WP-03.4 wymaga osobnego zlecenia; nie został rozpoczęty. Brak blokady zależności dla ukończonego zakresu WP-03.3, pozostają powyższe ostrzeżenia.

## Pliki zmienione w tym przyroście

Lista dotyczy WP-03.3 względem stanu zastanego, nie wszystkich zmian od pierwszego commita:

Sprawdzono **196 kandydatów Git**, **161 zastanych plików chronionych bez zmian**, 0 dopasowań lokalnego sekretu i 0 plików staged. Przyrost obejmuje **35 plików** (20 zmodyfikowanych i 15 nowych):

- `README.md`
- `backend/src/Roomify.Application/ApplicationServiceCollectionExtensions.cs`
- `backend/src/Roomify.Application/Reservations/ReservationContracts.cs`
- `backend/src/Roomify.Application/Rooms/CreateRoomService.cs`
- `backend/src/Roomify.Application/Rooms/GetRoomByIdService.cs`
- `backend/src/Roomify.Application/Rooms/GetRoomsService.cs`
- `backend/src/Roomify.Application/Rooms/RoomActivity.cs`
- `backend/src/Roomify.Application/Rooms/RoomModels.cs`
- `backend/src/Roomify.Application/Rooms/RoomRules.cs`
- `backend/src/Roomify.Application/Rooms/UpdateRoomService.cs`
- `backend/src/Roomify.Domain/Rooms/Room.cs`
- `backend/src/Roomify.Infrastructure/Persistence/Configurations/RoomConfiguration.cs`
- `backend/src/Roomify.Infrastructure/Persistence/Migrations/20261018111403_RoomOptimisticConcurrency.Designer.cs`
- `backend/src/Roomify.Infrastructure/Persistence/Migrations/20261018111403_RoomOptimisticConcurrency.cs`
- `backend/src/Roomify.Infrastructure/Persistence/Migrations/RoomifyDbContextModelSnapshot.cs`
- `backend/src/Roomify.Infrastructure/Persistence/Repositories/EfRoomRepository.cs`
- `backend/tests/Roomify.Api.IntegrationTests/PersistenceMaterializationTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/PostgreSqlMigrationTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/PostgreSqlSchemaAuditTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/ReservationManagementTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/RoomApplicationRegistrationTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/RoomManagementTests.cs`
- `backend/tests/Roomify.Application.UnitTests/RoomManagementTests.cs`
- `backend/tests/Roomify.Application.UnitTests/stryker-config.json`
- `backend/tests/Roomify.Domain.UnitTests/RoomActivationTests.cs`
- `docs/ai/IMPLEMENTATION_STATUS.md`
- `docs/architecture/DATABASE.md`
- `docs/architecture/DOMAIN.md`
- `docs/architecture/ROOM_MANAGEMENT.md`
- `docs/testing/INTEGRATION_TESTS.md`
- `docs/testing/MUTATION_TESTS.md`
- `docs/testing/README.md`
- `docs/testing/REQUIREMENTS_TRACEABILITY.md`
- `docs/testing/UNIT_TESTS.md`
- `docs/testing/WP03_3_REPORT.md`

## Odtworzenie weryfikacji

Komendy restore/build/test i Stryker: [ROOM_MANAGEMENT.md, sekcja 12](../architecture/ROOM_MANAGEMENT.md#12-uruchamianie). Końcowe TRX i logi są lokalnie w artifacts/wp033. Stryker należy uruchamiać sekwencyjnie względem innych buildów. Nową migrację można obejrzeć przez `dotnet ef migrations script ReservationOptimisticConcurrency RoomOptimisticConcurrency` z projektami Infrastructure jako project/startup; wygenerowany SQL nie zmienia kolumn danych. Testy nie wymagają migracji developerki. Dokumentacja nie upoważnia do destrukcyjnego rollbacku InitialCreate.

# WP-03.4 — konta użytkowników i kontrola uprawnień

Data: **18.10.2026**. Wynik: **READY WITH WARNINGS, 30/30 AC PASS dla zleconego zakresu Application/Infrastructure i mechanizmu Identity/cookies**. Nie jest to odbiór gotowego logowania HTTP ani całego MVP. **WP-03.5 nie rozpoczęto.** Dokumenty WP-00 i wcześniejsze raporty pozostają bez zmian.

## Implementacja

Dodano CreateUserService, GetUsersService, GetUserByIdService, UpdateUserService, DeactivateUserService i ActivateUserService. Każda wymaga aktywnego, uwierzytelnionego administratora. Application zna tylko port IUserAccountStore, DTO i wyniki, bez Identity/EF/Npgsql/HttpContext. IdentityUserAccountStore używa istniejących UserManager<ApplicationUser>, RoleManager<IdentityRole<Guid>> i jednego Scoped RoomifyDbContext. Nie powstał drugi magazyn użytkowników ani drugi schemat Identity.

Create przyjmuje Email, DisplayName i InitialPassword. Rola jest zawsze User, bez pola roli w wejściu. Standardowe Identity waliduje i hashuje hasło. Tworzenie konta, ewentualny bootstrap samej roli User i przypisanie roli są jedną transakcją. Awaria przypisania wycofuje nowy rekord i rolę, zachowując inne dane. Precheck i fizyczne indeksy znormalizowanego e-maila/loginu chronią również równoległe próby utworzenia konta.

GetUsers wykonuje w PostgreSQL Search w Email/DisplayName, filtr IsActive, COUNT, projekcję, sortowanie i paginację. Limit PageSize 1–100, domyślnie 20; SortBy/Kierunek są enumami z walidacją, drugi klucz Id rosnąco. Stabilność dotyczy niezmienionego zbioru. GetUserById zwraca UserNotFound, gdy brak konta. UserDetails zawiera tylko Id, Email, DisplayName, IsActive, CreatedAt. Nie ma PasswordHash, SecurityStamp, ConcurrencyStamp, tokenów ani historii logowania.

Update zmienia wyłącznie Email i DisplayName przez SetEmailAsync/SetUserNameAsync i zapis Identity. NormalizedEmail/NormalizedUserName pozostają spójne z loginem równym e-mailowi. Transakcja cofa częściowy zapis, jeżeli kolejny krok zawiedzie. Standardowy ConcurrencyStamp chroni dwie odczytane wersje tego samego konta. Request nie przyjmuje wewnętrznych pól Identity ani dowolnej roli.

Deactivate/Activate zmieniają IsActive i SecurityStamp, bez zmiany hasła, ról, Id/CreatedAt czy rezerwacji. Powtórzenia dają AlreadyInactive/AlreadyActive. Globalny advisory lock w transakcjach tworzenia i zmiany aktywności chroni bootstrap roli i ostatniego aktywnego administratora. Dwie równoczesne dezaktywacje dwóch ostatnich administratorów dają 1 Success i 1 LastAdministrator, pozostaje 1 aktywny administrator. Update profilu używa ConcurrencyStamp bez tego locka.

## Uprawnienia i sesje

Wprowadzono wspólny ApplicationAccess: brak Guid/Guid.Empty → Unauthenticated, nieaktywność lub brak wymaganej roli → Forbidden. Korzystają z niego także wszystkie wcześniejsze usługi rezerwacji i sal. Reguły właściciel/admin oraz logika terminów bez zmian. ICurrentUser.IsActive domyślnie false; starsze zaufane konteksty testowe jawnie potwierdzają aktywność. Nie podmieniono asercji dostępu na oczekiwanie sukcesu.

HttpIdentityCurrentUser korzysta z principal Identity, nigdy z roli/UserId przesłanych w DTO. Serwerowa claims factory ustala aktywność z rekordu, a walidator odświeża principal/role z bazy przy każdym uwierzytelnionym żądaniu. ActiveAccountSignInManager rozszerza standardowe CanSignInAsync o IsActive. ActiveAccountStampValidator sprawdza standardowy security stamp **i** aktywność bez oczekiwania na interwał. Brak/zmiana stempla, brak konta lub nieaktywność odrzucają principal i wygaszają cookies.

Aktywacja ponownie obraca stamp i nie przywraca starych sesji. Zmiany profilu również unieważniają dotychczasowe sesje. Zachowano HttpOnly, Lax, Secure poza lokalnym HTTP Development, 30 minut i brak sliding expiration. Walidacja stempla nie jest wyłączona; skonfigurowano Zero i egzekwuje się ją także przy takiej samej chwili zegara. Odrzucenie obejmuje następne żądanie po commit, bez cofania już wykonywanych operacji.

Szczegóły, 12 tematów i trzy diagramy: [USER_MANAGEMENT.md](../architecture/USER_MANAGEMENT.md).

## Rzeczywiste wyniki końcowe

| Kontrola | Wynik |
|---|---|
| dotnet restore backend/Roomify.slnx | PASS |
| Debug / Release | PASS / PASS, po 0 warnings/errors |
| Domain | **118/118 PASS**, bez zmiany Domain |
| Application xUnit/Moq | **228/228 PASS**, przyrost 64 |
| Integracyjne RequiresDocker=false | **78/78 PASS**, przyrost 2 DI/awaria |
| Integracyjne RequiresDocker=true | **133/133 PASS**, przyrost 22 konta/sesje/PostgreSQL |
| **Łącznie backend** | **557/557 PASS**, 0 failed/skipped |
| Identity i bezpieczeństwo | Standardowy hash/normalizacja/role, realne cookies, aktywność i odrzucenie starych sesji PASS |
| Regresja WP-03.1–03.3 | Wszystkie poprzednie klasy wykonane w powyższych zestawach PASS |
| Ostatni administrator | 1 Success + 1 LastAdministrator, zachowane wszystkie role |
| Współbieżny Update | 1 Success + 1 ConcurrentModification, dane zgodne ze zwycięzcą |
| Create i rola / częściowy Update e-maila | Rollback potwierdzony pełnym snapshotem wierszy, brak pozostałego zapisu |
| Filtry/paging | 6 sortowań i remisy, SQL z LIMIT/OFFSET i projekcja bez poufnych kolumn PASS |
| Stryker Application | **99,17%**, 238 Killed / 240 ocenionych, 2 równoważne Survived |
| has-pending-model-changes | PASS, brak zmiany modelu; nowej migracji nie potrzeba |
| Schemat/migracje/Domain/produkcja API/frontend | Bez zmian względem początku pakietu, kontrola hashy |
| Developerka przed/po | Identyczny logiczny dump i historia migracji |
| Compose config --quiet / ps | PASS; 3 running, api/db healthy; obrazy nieprzebudowane |
| Architektura / sekrety / dokumentacja / Git | Kontrola końcowa i lista plików poniżej; 0 plików staged, brak commit/push |

Pierwsza regresja bez Dockera miała 74/76 PASS: historyczny test oczekiwał interwału 5 minut oraz zaakceptowania biletu bez istniejącego konta/stempla. Dostosowano oczekiwania do świadomie zaostrzonej polityki: Zero i odrzucenie niepełnego biletu. Wszystkie asercje atrybutów/ważności cookies zachowano. Dowód poprawnego 200 i 403 przeniesiono na rzeczywiste konta i rozszerzono o 401 po dezaktywacji oraz zachowanie po aktywacji; nie wyłączano walidatora w fixture. Początkowe ostrzeżenie nullability w CreateReservation usunięto po wspólnym guardzie, bez zmiany domeny. Końcowe buildy i testy powyżej nie mają błędów ani pominięć.

### INT-USR-001–015

| ID | Rzeczywisty dowód |
|---|---|
| INT-USR-001 | CreateUserService + UserManager, rekord zapisany z poprawnymi danymi |
| INT-USR-002 | PasswordHash różny od jawnego hasła, CheckPasswordAsync=true, brak hasła/stempla/hash w wyniku |
| INT-USR-003 | Dokładnie rola User, standardowy RoleManager i user_roles |
| INT-USR-004 | Dwie równoległe próby różnych wielkości liter: 1 Success, 1 DuplicateEmail, 1 konto; obejście managera odrzuca indeks e-maila |
| INT-USR-005 | Update DisplayName i Email zachowuje Id/CreatedAt/hasło/role |
| INT-USR-006 | Email=UserName, spójne znormalizowane dane; rollback po odrzuconym loginie |
| INT-USR-007 | Po Deactivate rekord nadal istnieje, IsActive=false, nowy stamp |
| INT-USR-008 | Pełny snapshot rezerwacji aktywnej i historycznej/anulowanej identyczny |
| INT-USR-009 | Po Activate role, hasło, Id/CreatedAt i historia zachowane |
| INT-USR-010 | Rzeczywisty CheckPasswordSignInAsync: aktywne konto Succeeded, nieaktywne IsNotAllowed, po aktywacji Succeeded |
| INT-USR-011 | Potok testowego hosta: poprawny bilet 200, zwykły użytkownik admin 403, po dezaktywacji 401; stary bilet po aktywacji nadal 401, nowy 200 |
| INT-USR-012 | Realny adapter kont i macierz zwykłego użytkownika: wszystkie 6 operacji odrzucone |
| INT-USR-013 | Aktywny administrator tworzy/czyta; brak rekordu daje UserNotFound, nie Forbidden |
| INT-USR-014 | Odczytowe DTO i SQL projekcji bez hash/stamp/tokenów; wejście nie zawiera ról |
| INT-USR-015 | Fixture ma niezależne konto/bazę/port; dump developerki przed/po identyczny |

Nowa klasa PostgreSQL ma **22 przypadki**, ponieważ część identyfikatorów łączy w jeden przepływ, a macierz tożsamości i 6 sortowań jest parametryzowana. Dodatkowo sprawdzono odrzucenie cookie przy IsActive zmienionym bez obrotu stempla, odświeżenie roli z bazy, rollback przypisania roli oraz wyścig ostatnich administratorów i ConcurrencyStamp. Testy używają rzeczywistych migracji i PostgreSQL 18.6 Testcontainers.

**Granica HTTP:** `/__tests/accounts/*` istnieje wyłącznie w assembly testowym dołączanym przez WebApplicationFactory. Wystawia bilet do kontroli middleware, nie jest login endpointem produktu. Produkcyjny host nadal zwraca 404 dla tych tras, rejestracji i login/logout. INT-USR-010 dowodzi mechanizmu Identity, INT-USR-011 rzeczywistego potoku cookies, **nie** kompletnej funkcji logowania HTTP. Tego niezaimplementowanego przepływu nie oznaczono PASS.

### Mutacje

Stryker.NET 5.0.0, zakres Application: dotychczasowe rezerwacje/sale oraz ApplicationAccess, UserServices i UserRules. Bez DTO, DI, migracji, snapshotu i adaptera/middleware Identity. Ten ostatni zakres ma realne testy integracyjne, nie przypisujemy mu wyniku mutacyjnego.

Pierwsze wykonanie: **93,33%, 224 Killed, 16 Survived**. Uzupełniono dokładne asercje kodów błędów i granicy e-maila 256; poprawnej logiki produkcyjnej nie zmieniano pod wynik narzędzia. Końcowo **99,17% = 238 Killed / 240 ocenionych**, 2 Survived, 33 CompileError, 0 NoCoverage/Timeout/RuntimeError. Log ma 315 utworzonych i 42 Ignored; JSON 312 wpisów i 39 Ignored. Różnica 3 pominiętych wpisów serializacji nie dotyczy licznika 238/240.

Survived #221 w RoomRules i #268 w UserRules zmieniają `offset <= int.MaxValue` na `<`. Równość `(PageNumber-1)*PageSize=2147483647` jest nieosiągalna: int.MaxValue jest liczbą pierwszą, PageSize<=100, więc jedynym dzielnikiem jest 1, co wymagałoby PageNumber=2147483648 poza zakresem int. To mutanty równoważne dla dostępnych kontraktów, bez zmiany poprawnej logiki lub wykluczania ich dla 100%. Nie powtarzano coverage/mutation Domain, Angular/E2E ani manualnych.

### Baza developerska i kontrola plików

Logiczny dump przed/po: SHA256 `be2f8674ec8f1819e2ba85f5f90a5dcbdf1cd01f511d08b268904004f984f280`. Historia w obu odczytach: `20261018093445_InitialCreate|10.0.9`. Nie zapisywano nowych kont/ról/danych do developerki. Nie wykonano migracji, EnsureDeleted, usunięcia użytkownika/rezerwacji, rollbacku developerskiego ani usunięcia wolumenu. Dotychczasowe migracje i snapshot identyczne; dwie migracje mapowania xmin nadal czekają na osobne wdrożenie.

Sprawdzono .gitignore i kandydatów Git pod kątem rzeczywistego lokalnego sekretu oraz .env/node_modules/bin/obj/artifacts. Wygenerowane TRX, logi i raporty Stryker są wyłącznie w ignorowanym artifacts/wp034. Kod produkcyjny nie zawiera seeda administratora ani jego hasła. Brak nowego kontrolera/endpointu w produkcji. Zachowano istniejące niezacommitowane zmiany; bez git add/commit/push.

## AC-001–AC-030

| AC | Wynik | Dowód |
|---|---|---|
| AC-001 | PASS | CreateUserService, Unit i INT-USR-001 |
| AC-002 | PASS | GetUsersService, Unit i 6 sortowań PostgreSQL |
| AC-003 | PASS | GetUserByIdService, DTO i UserNotFound |
| AC-004 | PASS | UpdateUserService, normalizacja/rollback/concurrency |
| AC-005 | PASS | DeactivateUserService, stamp, historia i ostatni admin |
| AC-006 | PASS | ActivateUserService, zachowane dane i nowy stamp |
| AC-007 | PASS | Wszystkie 6 wymagają aktywnego administratora, Unit i realne repozytoria |
| AC-008 | PASS | Brak rejestracji w źródłach API i test 404 tras produkcyjnych |
| AC-009 | PASS | Referencje Application wyłącznie Domain i DI.Abstractions; kontrola źródeł |
| AC-010 | PASS | Rzeczywiste UserManager/RoleManager i wspólne stores |
| AC-011 | PASS | Standardowy PasswordHasher, CheckPasswordAsync, brak hasła w wyniku |
| AC-012 | PASS | Transakcja konta/roli, wymuszony błąd przypisania i rollback bez innych zmian |
| AC-013 | PASS | Dwie równoległe próby i fizyczny UNIQUE |
| AC-014 | PASS | Dokładne pola DTO, serializacja i SQL bez danych Identity |
| AC-015 | PASS | CanSignIn/CheckPasswordSignIn, cookie co żądanie, ApplicationAccess |
| AC-016 | PASS | Pełny snapshot istniejących rezerwacji zachowany |
| AC-017 | PASS | CheckPasswordSignInAsync i świeży testowy bilet po Activate |
| AC-018 | PASS | Stary cookie po aktywacji nadal 401, nowy stamp |
| AC-019 | PASS | Brak wejścia role/IsAdministrator, nowe konto wyłącznie User |
| AC-020 | PASS | Domain 118/118, Application 228/228 |
| AC-021 | PASS | PostgreSQL 133/133, w tym 22 nowe |
| AC-022 | PASS | Admin/gość/user/nieaktywny, realne 200/403/401 i odmowa 9 wcześniejszych usług |
| AC-023 | PASS | Pełna regresja WP-03.1–03.3 zawarta |
| AC-024 | PASS | Debug, 0 warnings/errors |
| AC-025 | PASS | Release, 0 warnings/errors |
| AC-026 | PASS | Brak zmiany schematu/migracji, has-pending-model-changes PASS |
| AC-027 | PASS | Dump i historia developerki identyczne |
| AC-028 | PASS | Cztery dokumenty WP-00 zachowane SHA256 |
| AC-029 | PASS | USER_MANAGEMENT 12 tematów, 3 diagramy, testy/komendy; status/macierz/README |
| AC-030 | PASS | Rzeczywiste TRX/logi, zakres HTTP i wyniki błędnych pierwszych prób jawne |

## Ograniczenia i dalszy etap

- Nie ma produkcyjnych endpointów login/logout/me, kont, sal i rezerwacji ani Angulara. Nie ma publicznej rejestracji, JWT, resetu e-mail, wysyłania hasła ani zewnętrznych providerów.
- Pierwszego administratora nie tworzy seed ze stałym hasłem. Kontrolowany bootstrap i bezpieczny kanał początkowego hasła wymagają osobnego zakresu przed użytkowaniem produktu.
- Walidacja Identity co żądanie wymaga dostępnej bazy i dodatkowych odczytów. Awaria nie dopuszcza operacji na podstawie niezweryfikowanej starej sesji. Żądania rozpoczęte przed dezaktywacją nie są cofane.
- Pełna ochrona CSRF/TLS/trwałego key ring i obsługa login endpointu pozostają do wdrożenia. Testowe wydanie biletu nie zastępuje tych funkcji.
- Aktualizacja profilu unieważnia wszystkie dotychczasowe sesje. Nie dodano klientowego stempla wersji; bieżąca ochrona obejmuje konkurencyjne odczyty/zapisy w zakresach usług. Przyszły kontrakt formularza/ETag wymaga osobnej decyzji bez ujawniania wewnętrznego ConcurrencyStamp.
- Advisory lock serializuje Create i zmiany aktywności. Przyszłe operacje nadawania/odbierania ról muszą respektować ochronę ostatniego administratora. Adapter wymaga kontekstu bez innych oczekujących zmian, ponieważ standardowy UserStore wywołuje SaveChanges.
- Wcześniejszy wyścig Archive/CreateReservation i brak osobnego wyposażenia sal pozostają bez zmian; nie ingerowano w terminy/rezerwacje. Paginacja offsetowa nie daje wspólnego snapshotu zmieniającej się listy.
- Developerka nadal bez nowych kont i obu wcześniejszych wpisów migracji xmin. Obrazy Compose nie były przebudowywane dla tego kodu. Nie deklarujemy wdrożenia produkcyjnego.
- **WP-03.5 nie rozpoczęto.** Brak blokady do odbioru opisanego zakresu WP-03.4; kolejne funkcje wymagają osobnego zlecenia.

## Lista plików zmienionych w WP-03.4

Względem stanu zastanego na początku tego pakietu, nie względem pierwszego commita:

Kontrola: **208 kandydatów Git**, **168 chronionych zastanych plików identycznych**, 0 dopasowań lokalnego sekretu i 0 staged. Przyrost: **40 plików** (28 zmodyfikowanych i 12 nowych):

- `README.md`
- `backend/src/Roomify.Application/Abstractions/ApplicationAccess.cs`
- `backend/src/Roomify.Application/Abstractions/ICurrentUser.cs`
- `backend/src/Roomify.Application/ApplicationServiceCollectionExtensions.cs`
- `backend/src/Roomify.Application/Reservations/CancelReservationService.cs`
- `backend/src/Roomify.Application/Reservations/CreateReservationService.cs`
- `backend/src/Roomify.Application/Reservations/ReservationAccess.cs`
- `backend/src/Roomify.Application/Reservations/UpdateReservationService.cs`
- `backend/src/Roomify.Application/Rooms/RoomRules.cs`
- `backend/src/Roomify.Application/Users/UserModels.cs`
- `backend/src/Roomify.Application/Users/UserRules.cs`
- `backend/src/Roomify.Application/Users/UserServices.cs`
- `backend/src/Roomify.Infrastructure/Identity/AccountSessionSecurity.cs`
- `backend/src/Roomify.Infrastructure/Identity/IdentityServiceCollectionExtensions.cs`
- `backend/src/Roomify.Infrastructure/Identity/IdentityUserAccountStore.cs`
- `backend/tests/Roomify.Api.IntegrationTests/IdentityConfigurationTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/ReservationApplicationRegistrationTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/ReservationApplicationTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/ReservationManagementTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/RoomManagementTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/UserApplicationRegistrationTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/UserManagementTests.cs`
- `backend/tests/Roomify.Api.IntegrationTests/UserSessionTestFactory.cs`
- `backend/tests/Roomify.Application.UnitTests/CreateReservationServiceTests.cs`
- `backend/tests/Roomify.Application.UnitTests/ReservationManagementTestContext.cs`
- `backend/tests/Roomify.Application.UnitTests/RoomManagementTests.cs`
- `backend/tests/Roomify.Application.UnitTests/UserManagementTests.cs`
- `backend/tests/Roomify.Application.UnitTests/stryker-config.json`
- `docs/ai/IMPLEMENTATION_STATUS.md`
- `docs/architecture/IDENTITY.md`
- `docs/architecture/RESERVATION_APPLICATION.md`
- `docs/architecture/RESERVATION_MANAGEMENT.md`
- `docs/architecture/ROOM_MANAGEMENT.md`
- `docs/architecture/USER_MANAGEMENT.md`
- `docs/testing/INTEGRATION_TESTS.md`
- `docs/testing/MUTATION_TESTS.md`
- `docs/testing/README.md`
- `docs/testing/REQUIREMENTS_TRACEABILITY.md`
- `docs/testing/UNIT_TESTS.md`
- `docs/testing/WP03_4_REPORT.md`

## Komendy i artefakty

Komendy restore/build/test i Stryker: [USER_MANAGEMENT, sekcja 12](../architecture/USER_MANAGEMENT.md#12-uruchomienie). Uruchomienia Docker obejmują `--filter "RequiresDocker=true"`, pozostałe integracje `--filter "RequiresDocker=false"`. Końcowe TRX: artifacts/wp034/domain.trx, application.trx, without-docker.trx, postgresql.trx. Końcowe mutacje: artifacts/wp034/stryker-final/reports/mutation-report.json/html. Początkowe logi pozostają lokalne; wyniki wygenerowane są ignorowane przez Git. Nie uruchamiać innych buildów równocześnie ze Strykerem.

# WP-02.3 — integracja Identity: raport odbioru

17.10.2026. **30/30 AC PASS — zakres infrastruktury WP-02.3 zakończony.** Identity współdzieli działający model EF z encjami Roomify. Nie ma jeszcze rzeczywistych kont/tabel ani systemu logowania HTTP. Nie wykonano commit ani push; zastane zmiany WP-02.1/02.2 zachowano, HEAD pozostaje fb429d4.

## Pliki i zależności

Dodano w Infrastructure/Identity: ApplicationUser.cs, IdentityRoles.cs, IdentityServiceCollectionExtensions.cs i IdentityModelConfiguration.cs. Dodano w Api.IntegrationTests: IdentityModelAssertions.cs, IdentityModelTests.cs, IdentityConfigurationTests.cs i IdentityTestFactory.cs. Nowa dokumentacja: [IDENTITY.md](../architecture/IDENTITY.md) oraz niniejszy raport.

Zmodyfikowano Infrastructure.csproj, InfrastructureServiceCollectionExtensions.cs, Persistence/RoomifyDbContext.cs i Api/Program.cs. Dostosowano dwa wcześniejsze pliki testów metadanych/DI (PersistenceModelTests.cs, PersistenceRegistrationTests.cs): oczekiwanie dwóch encji zastąpiono dokładną listą dziewięciu, a wyłączny FK sali rozszerzono o wymagany FK użytkownika. Nie usunięto asercji biznesowych typów/kolumn/indeksów/usuwania ani testów jednostkowych domeny. Nie zmieniono żadnego źródła Domain/Application ani WP-00.

Zaktualizowano README.md, docs/ai/IMPLEMENTATION_STATUS.md, WP02_HANDOFF.md (uwaga historyczna), docs/architecture/BACKEND.md, OVERVIEW.md, DOMAIN.md, PERSISTENCE.md oraz docs/testing/README.md i INTEGRATION_TESTS.md. Historyczne raporty WP-01.6/WP-02.1/WP-02.2 pozostają zapisem ich wykonań.

| Zależność Infrastructure | Wersja / zmiana |
|---|---|
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | **10.0.9**, nowy stabilny pakiet |
| Microsoft.EntityFrameworkCore | **10.0.9**, bez zmiany |
| Npgsql.EntityFrameworkCore.PostgreSQL | **10.0.3**, bez zmiany |
| EF Core Relational (przechodnio) | Obecnie **10.0.9**, minimum nowego pakietu Identity |
| Microsoft.AspNetCore.App | FrameworkReference w Infrastructure dla usług Identity/cookies |
| DI.Abstractions jako bezpośredni pakiet Infrastructure | Usunięty jako redundantny po dodaniu framework reference; początkowe NU1510 naprawiono bez tłumienia ostrzeżeń |

SDK 10.0.301; restore i finalne buildy bez ostrzeżeń. Nie dodano Design, JWT, OAuth, IdentityServer ani własnego hashowania. Pakiety testowe i referencje pomiędzy projektami są niezmienione.

## Model, DI i bezpieczeństwo

ApplicationUser : IdentityUser<Guid> ma opcjonalny DisplayName, IsActive=true i CreatedAt UTC z kontrolowalnym TimeProvider. Nie dodano nowych obowiązkowych FirstName/LastName ani własnego pola hasła. Role to IdentityRole<Guid>, centralne nazwy IdentityRoles.User/Administrator. RoleManager i UserManager będą obsługiwać ich późniejsze przypisanie — nie ma seeda ani samodzielnego nadawania uprawnień.

RoomifyDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid> wywołuje base.OnModelCreating przed dotychczasowymi konfiguracjami. Jawny model Identity nadaje siedmiu tabelom/kolumnom konwencję snake_case, zachowuje klucze, concurrency, znaczniki bezpieczeństwa i lockout. Wspólny model zawiera **dokładnie dziewięć encji**, bez passkeys w zakresie Stores.SchemaVersion=Version2. Reservation.UserId ma wymagany FK do users.id z Restrict, bez nawigacji w Domain. Metadane Room/Reservation, ich indeksy, UTC i pola Period przechodzą dotychczasową regresję.

AddIdentity i AddEntityFrameworkStores rejestrują standardowe UserManager, RoleManager, SignInManager, stores i PasswordHasher. Test potwierdza ten sam Scoped DbContext w obu stores i brak otwartego połączenia. UseAuthentication i UseAuthorization pracują po routingu. /health pozostaje publiczny, bez kontroli bazy i bez fallback policy.

Polityka: hasła ≥12 znaków z małą/wielką literą i cyfrą, znaki specjalne dopuszczone, niewymagane; blokada po 5 próbach na 15 minut; unikalny email. Cookie Roomify.Auth: HttpOnly, SameSite=Lax, host-only, Path=/, bilet 30 minut, SlidingExpiration=false. Secure=Always poza Development; w Development SameAsRequest. Standardowy security stamp validator co 5 minut zachowano. API ma jawne 401/403 zamiast przekierowań.

Testowy kontroler jest w assembly testów i tylko IdentityTestFactory dołącza go do MVC. Standardowy host zwraca 404 dla ścieżek prób. Bilet testowy nie tworzy użytkownika ani nie jest implementacją login. Testy weryfikują rzeczywiste Set-Cookie, zawartość czasu biletu, anonimowe 401, akceptację cookie i 403 dla roli User wobec Administrator. Data Protection jest efemeryczne wyłącznie w tej fixture. Testy nie wyłączają zabezpieczeń aplikacji.

## Rzeczywiste wykonanie

Przed zmianami wykonano 35/35 testów EF, potwierdzając zależność WP-02.2; zapisano sumy plików. Dowody końcowe: ignorowany artifacts/wp023 — commands.json, logi, TRX i static.json. Czasy oznaczają całe polecenia.

| Kontrola | Wynik |
|---|---|
| dotnet restore backend/Roomify.slnx | PASS, kod 0, 1,078 s |
| dotnet build backend/Roomify.slnx --configuration Debug --no-restore | PASS, 0 błędów/ostrzeżeń, 2,500 s |
| Build Release z --no-restore | PASS, 0 błędów/ostrzeżeń, 2,859 s |
| Domain.UnitTests --no-build --no-restore | **87/87 PASS**, 0 failed/skipped, 1,813 s |
| Application.UnitTests --no-build --no-restore | Kod 0, **0 przypadków**, 1,484 s |
| IntegrationTests --filter Category=PersistenceMetadata | **35/35 PASS**, 0 failed/skipped, 2,656 s |
| IntegrationTests --filter Category=IdentityConfiguration | **34/34 PASS**, 0 failed/skipped, 2,375 s |
| IntegrationTests --filter FullyQualifiedName~HealthCheckTests | **3/3 PASS**, 0 failed/skipped, 1,797 s |
| Lista pakietów z --include-transitive --no-restore | PASS, zgodne główne wersje 10, 0,860 s |
| SELECT schemaname, tablename FROM pg_tables z wyłączeniem systemowych | Kod 0, pusta lista tabel użytkownika, 0,421 s |

Testy mają --logger trx i osobne nazwy wyników w artifacts/wp023/trx. Łącznie w końcowym zestawie: **159/159 zaliczonych przypadków** (87 + 35 + 34 + 3), bez doliczania powtórzeń wcześniejszych prób. Application nie ma jeszcze przypadków — kod 0 nie jest zaliczeniem przypadków użycia. Testcontainers, frontend, coverage i Stryker nie były ponownie wykonywane; historyczne wyniki pozostają w poprzednich raportach.

Konfiguracja bazy testowego hosta wskazuje port 1; model, DI i /health działają przy zamkniętym połączeniu. Nie tworzono użytkowników/rol w realnej bazie ani nie wywoływano migracji/EnsureCreated/SaveChanges. Logiczny dump developerskiej bazy przed/po jest identyczny: SHA-256 `eecd1a3cab6c14d496cdc5600f591e3a8ea96f0ac7d40ba6042195d90d7d2ab5`, po pominięciu wyłącznie losowych znaczników restrict/unrestrict pg_dump. Nie zatrzymywano kontenerów ani nie usuwano wolumenów.

Końcowa kontrola static.json: wszystkie źródła Domain/Application i testy Domain identyczne ze stanem początku WP-02.3; cztery dokumenty WP-00 bajtowo identyczne z oryginalnym ZIP. Referencje siedmiu projektów zachowano. Brak wywołań inicjalizacji schematu/zapisu, migracji i produkcyjnego mapowania publicznego Identity/JWT/Razor UI. Skan lokalnego hasła i typowych tokenów nie wykazał trafień w plikach widocznych dla Git. Sprawdzono lokalne odnośniki i git diff --check. .env, bin, obj, zależności i artefakty nie znajdują się w indeksie; indeks pozostaje pusty, HEAD niezmieniony. To kontrola zakresu tego pakietu, nie deklaracja pełnego audytu bezpieczeństwa produktu.

## AC-001–AC-030

| ID | Wynik | Dowód |
|---|---|---|
| AC-001 | PASS | ApplicationUser : IdentityUser<Guid>. |
| AC-002 | PASS | IdentityRole<Guid>, metadane klucza uuid. |
| AC-003 | PASS | Centralne role User/Administrator, wykorzystane w rzeczywistym teście autoryzacji. |
| AC-004 | PASS | Kontekst dziedziczy po IdentityDbContext. |
| AC-005 | PASS | Jeden RoomifyDbContext w DI i w user/role stores. |
| AC-006 | PASS | Regresja 35/35 i brak zmiany konfiguracji biznesowych. |
| AC-007 | PASS | Reservation.UserId → ApplicationUser.Id, FK wymagany. |
| AC-008 | PASS | DeleteBehavior.Restrict dla historii rezerwacji. |
| AC-009 | PASS | AddIdentity oraz EF stores. |
| AC-010 | PASS | Wszystkie trzy managery rozwiązują się z DI. |
| AC-011 | PASS | Standardowy PasswordHasher i walidatory, bez własnej kryptografii. |
| AC-012 | PASS | IdentityOptions i sześć przypadków wybranej polityki haseł. |
| AC-013 | PASS | Identity.Application z CookieAuthenticationHandler. |
| AC-014 | PASS | HttpOnly, Lax, Secure, brak Domain, skończony bilet; sprawdzone opcje i nagłówek. |
| AC-015 | PASS | GET/POST rejestracji i Identity UI zwracają 404; brak mapowania w produkcji. |
| AC-016 | PASS | Brak JWT/OAuth/Bearer w pakietach i schematach. |
| AC-017 | PASS | /health publiczny; brak globalnej polityki autoryzacji. |
| AC-018 | PASS | /health 200 Healthy z niedostępną bazą. |
| AC-019 | PASS | Dokładna lista dziewięciu encji, klucze i właściwości Identity. |
| AC-020 | PASS | Testy managerów, standardowych stores i wspólnego scope. |
| AC-021 | PASS | 34/34 Identity/security oraz dotychczasowe HTTP 3/3. |
| AC-022 | PASS | Domain 87/87 bez zmiany źródeł/testów. |
| AC-023 | PASS | Domain bez Identity/ASP.NET/EF i bez zmian referencji. |
| AC-024 | PASS | Application nadal nie zależy od Infrastructure. |
| AC-025 | PASS | Debug, zero ostrzeżeń/błędów. |
| AC-026 | PASS | Release, zero ostrzeżeń/błędów. |
| AC-027 | PASS | Brak migracji i tabel; wyłącznie odczyt bazy. |
| AC-028 | PASS | Brak prawdziwych haseł/tokenów w zmianach, .env i artefakty ignorowane. |
| AC-029 | PASS | IDENTITY.md opisuje model, managerów, cookies, CSRF, migracje i testy. |
| AC-030 | PASS | Wyniki z poleceń/TRX, bez deklarowania niezrealizowanych funkcji. |

## Ograniczenia i następny etap

Metadane nie stanowią wdrożonych ograniczeń PostgreSQL. Nie przetestowano zapisu kont/ról, weryfikacji hasła realnego użytkownika, faktycznego naliczania lockout ani sesji po dezaktywacji. IsActive nie blokuje samodzielnie SignInManager i cookie — należy to dopracować przed udostępnieniem login. Przyszłe żądania zmieniające stan wymagają kompletnego antiforgery; SameSite nie zastępuje CSRF. Potrzebne będą też TLS, trwały chroniony key ring i zasady zaufania do Nginx przed użyciem produkcyjnym. Obecna konfiguracja jest infrastrukturą projektu, bez deklaracji odbioru całego systemu bezpieczeństwa.

WP-02.4 powinien przygotować migracje wspólnego modelu i dalsze zlecone zabezpieczenia bazy na odrębnym PostgreSQL testowym. Nie rozpoczęto go automatycznie. Zmiany pozostają niezacommitowane zgodnie z poleceniem.

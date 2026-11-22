# Testowanie Roomify — WP-01 i WP-02

Cel: pięć poziomów testowania opartych na rzeczywistym zachowaniu. [TEST_STRATEGY.md](../ai/TEST_STRATEGY.md) zachowuje pełną strategię WP-00, identyfikatory scenariuszy, macierz ryzyk i robocze cele jakościowe. Plan docelowy uzupełniają historyczne wyniki WP-01 oraz [odbiór WP-02](WP02_ACCEPTANCE_REPORT.md) i [macierz FR/BR](REQUIREMENTS_TRACEABILITY.md). Macierz implementowanych BR i testów domenowych jest w [DOMAIN.md](../architecture/DOMAIN.md#przykłady-graniczne-i-powiązania-z-wp-00).

## Unit Tests — testy jednostkowe

Izolowane testy C# będą wykonywane przez xUnit, a zależności Application zastępowane mockami Moq. Obejmą reguły Domain, granice przedziałów czasu, walidację i koordynację przypadków użycia. Miejsce: `backend/tests/Roomify.Domain.UnitTests/` i `backend/tests/Roomify.Application.UnitTests/`.

Vitest i Angular TestBed posłużą do izolowanego sprawdzania komponentów, formularzy, walidatorów i serwisów Angulara. Pliki testowe powstaną przy kodzie frontendu po wygenerowaniu projektu. Żaden test jednostkowy nie będzie wymagać prawdziwej bazy, sieci ani uruchomionego API; żądania HTTP będą zastępowane kontrolowanymi odpowiednikami.

Ten poziom zapewnia szybkie wykrywanie błędów reguł i precyzyjne wskazanie ich źródła.

## Integration Tests — testy integracyjne

W `backend/tests/Roomify.Api.IntegrationTests/` WebApplicationFactory udostępni aplikację do testowania żądań HTTP, a Testcontainers zapewni rzeczywisty PostgreSQL. Testy sprawdzą status odpowiedzi, stan bazy, EF Core, Identity, uprawnienia i zachowanie równoczesnych zapisów. EF Core InMemory nie zastępuje PostgreSQL.

Testy będą izolować dane i nie zależeć od kolejności wykonania. Próba dwóch kolidujących rezerwacji musi potwierdzić, że obie nie zostaną zapisane. Integracja jest potrzebna, ponieważ mocki nie odtwarzają ograniczeń SQL, transakcji i całej obsługi żądania.

## Mutation Tests — testy mutacyjne

Stryker.NET będzie wprowadzać kontrolowane zmiany w kodzie backendu i sprawdzać, czy testy je wykrywają. Główny obszar to krytyczne reguły rezerwacji, np. zmiana operatora porównania dla sąsiadujących terminów. Analizie podlegać będą także mutanty przeżyłe i nieobjęte testami, z opisem przyczyn lub naprawionych luk.

Ten poziom ocenia jakość asercji: wykonanie danej linii kodu nie oznacza jeszcze wykrywania jej błędnego działania. Progi WP-00 są celami roboczymi; nie stanowią osiągniętych wyników ani nowych wymagań zaliczenia.

## E2E Tests — testy End-to-End

Playwright w `frontend/e2e/` sprawdzi ścieżki użytkownika w przeglądarce z działającym frontendem, API i odrębną bazą testową: logowanie, wyszukanie sali, rezerwacja, kalendarz i anulowanie oraz operacje administratora. Dane i konta będą przewidywalne, a scenariusze niezależne. Po błędzie przewiduje się zapis śladu i zrzutu ekranu.

Testy te potwierdzą, że poprawne oddzielnie komponenty współpracują w rzeczywistym przepływie użytkownika.

## Manual Tests — testy manualne

Scenariusze ręczne obejmą czytelność komunikatów, responsywność, obsługę klawiaturą i użyteczność formularzy. Protokół będzie zawierać identyfikator, powiązane wymaganie/regułę, środowisko, warunki wstępne, kroki, wynik oczekiwany, wynik rzeczywisty, status PASS/FAIL/BLOCKED, datę i dowód wykonania.

Ocena przez człowieka uzupełnia automatyzację tam, gdzie znaczenie ma zrozumiałość i wygoda interfejsu. W WP-01.1 nie powstają jeszcze protokoły wykonania.

## Powiązanie z wymaganiami i raportowanie

Przyszłe testy będą odwoływać się do `FR-*`, `NFR-*`, `BR-*` oraz scenariuszy `TC-*` ze strategii WP-00, z zachowaniem statusu propozycji wymagających dalszego doprecyzowania. Szczególną uwagę otrzymają granice czasu, uprawnienia i współbieżność.

## Aktualny stan, uruchamianie i raporty

| Poziom | Narzędzia i obecny zakres | Szczegóły |
|---|---|---|
| Unit backend | xUnit, Test SDK, coverlet; Domain 116, Application 87 przypadków | [UNIT_TESTS.md](UNIT_TESTS.md) |
| Unit frontend | Vitest, TestBed, jsdom, coverage-v8; zachowane 5 testów | [UNIT_TESTS.md](UNIT_TESTS.md) |
| Integration | 159 przypadków: 75 bez Dockera i 84 PostgreSQL, w tym 21 nowych WP-03.2 | [INTEGRATION_TESTS.md](INTEGRATION_TESTS.md) |
| Mutation | Stryker.NET 5.0.0, Application Create/Update/Cancel/Access/SaveResults: 100%, 63 Killed; Domain historyczne WP-02.5 | [MUTATION_TESTS.md](MUTATION_TESTS.md) |
| E2E | Playwright/Chromium, dwa scenariusze przez Angular Dev Server | [E2E_TESTS.md](E2E_TESTS.md) |
| Manual | Przeglądarka, szablon i trzy scenariusze NOT RUN | [MANUAL_TESTS.md](MANUAL_TESTS.md) |

Przygotowanie: `dotnet restore backend/Roomify.slnx`, `dotnet tool restore`, a w `frontend/` — `npm ci` i `npx playwright install chromium`. Wymagane są SDK .NET 10, zgodny Node oraz npm. Pierwsze pobranie pakietów/przeglądarki wymaga internetu; same szybkie testy nie korzystają z sieci.

Z katalogu głównego:

```powershell
dotnet test backend/tests/Roomify.Domain.UnitTests --no-restore
dotnet test backend/tests/Roomify.Application.UnitTests --no-restore
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=false"
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=true"
```

W `frontend/`:

```powershell
npm run test:ci
npm run test:coverage
npm run e2e
npm run e2e:report
```

Nie trzeba uruchamiać całości naraz: `dotnet test backend/Roomify.slnx` obejmuje również Dockera. Vitest jawnie wyklucza `e2e/**`, a Playwright wybiera tylko `e2e/`.

Wyniki xUnit zapisuje `--logger "trx;LogFileName=api.trx" --results-directory artifacts/test-results/api`. Używaj oddzielnych nazw dla jednostek, HTTP i PostgreSQL. Pokrycie .NET zapewnia dołączony coverlet przez `--collect:"XPlat Code Coverage"`, zapisując `coverage.cobertura.xml` w katalogu wyników. Aktualny pomiar domeny WP-02.5: 160/160 linii i 38/38 gałęzi; nie obejmuje całego rozwiązania.

Vitest wypisuje wynik w konsoli i tworzy coverage pod `frontend/coverage/`. Playwright zapisuje HTML w `frontend/playwright-report/` i artefakty błędów w `frontend/test-results/`. Stryker będzie tworzyć lokalne HTML/JSON w `artifacts/mutation/`. Wszystkie te katalogi są ignorowane przez Git; nie dodano agregatora ani CI. Nie umieszczaj w raportach sekretów.

PASS oznacza rzeczywiste powodzenie asercji, FAIL niepowodzenie, BLOCKED przeszkodę środowiskową w raporcie pracy, a NOT RUN brak wykonania. Runner zapisuje niedostępny Docker jako failed, bez automatycznego maskowania. Zero przypadków jednostkowych nie oznacza zaliczenia domeny. Dowody bieżącej weryfikacji WP-03.2 zapisano pod ignorowanym `artifacts/wp032/`; WP-03.1 zachowuje `artifacts/wp031/`, a odbioru WP-02.5 pod `artifacts/wp025/`; artifacts/wp015 zachowuje historię.

## Obecne ograniczenia

Domain, mapowania/Identity i rzeczywisty schemat InitialCreate są zweryfikowane. Odbiór WP-02.5: [historyczne READY WITH WARNINGS](WP02_ACCEPTANCE_REPORT.md), 224/224. Bieżący [WP-03.1](WP03_1_REPORT.md): 280/280 (Domain 110, Application 32, integracyjne 138). Testcontainers wykonuje migracje, round-trip, ograniczenia i powtarzane transakcje; developerka pozostaje niezmieniona. Application tworzy, edytuje i anuluje, sprawdzając właściciela/admina i wersję; brak kont developerskich, endpointów logowania/danych i pełnej autoryzacji. Stryker 93,46% z 7 Survived; coverage Domain 100% linii/gałęzi. 40P01 wymaga przyszłej obsługi retry. E2E biznesowe i manualne pozostają poza odbiorem, Application 32 przypadki. CSRF i egzekwowanie IsActive w sesjach wymagane przed udostępnieniem logowania. WP-03.1 wykonano, WP-03.2 nie rozpoczęto.

## Wyniki WP-02.2

Debug/Release i restore PASS bez ostrzeżeń/błędów. Domain **87/87**, metadane/DI/materializacja EF **35/35**, HTTP **3/3**; łącznie **125/125** w końcowej regresji. EF wybiera Category=PersistenceMetadata, a wszystkie testy bez Dockera wybiera RequiresDocker=false. Nie wykonano migracji/EnsureCreated ani zmian bazy. Polityka UTC, mapowanie pól i granice dowodów są w [PERSISTENCE.md](../architecture/PERSISTENCE.md).

## Wyniki WP-02.3

Restore i Debug/Release bez ostrzeżeń/błędów; **159/159**: Domain 87, EF 35, Identity/security 34, HTTP 3. Testy Identity wybiera Category=IdentityConfiguration; RequiresDocker=false obejmuje teraz 72 przypadki integracyjne. Potwierdzono rzeczywiste Set-Cookie, 401/403 bez redirectów, publiczny /health i brak endpointów rejestracji w zwykłym hoście. Baza nie została zmieniona. [IDENTITY.md](../architecture/IDENTITY.md) zawiera model, polityki i granice dowodów.

## Wyniki weryfikacji WP-01.5

- Restore i build siedmiu projektów .NET: sukces, bez ostrzeżeń i błędów.
- Projekty jednostkowe backendu: runner uruchomiony, zero przypadków; brak deklaracji PASS domeny.
- WebApplicationFactory: 3/3 przypadki przeszły, bez PostgreSQL.
- PostgreSQL: 1 przypadek zakończony failed przez DockerUnavailableException; raport pracy: BLOCKED.
- Vitest: 5/5 testów przeszło; wygenerowano raporty pokrycia oraz JSON wyników.
- Playwright: 2/2 scenariusze przeszły w Chromium, bez ponowień; powstał raport HTML.
- Stryker: narzędzie lokalne dostępne, konfiguracja zaakceptowana; uruchomienie zatrzymane przy braku testów domenowych, score N/A.
- Manualne: 3 przygotowane scenariusze, wszystkie NOT RUN.

Raport HTML E2E jest w `frontend/playwright-report/index.html`. Obecny coverage HTML Vitest znajduje się w `frontend/coverage/roomify/`; JSON wykonania jest w `frontend/coverage/vitest-results.json`. Nie należy utożsamiać pokrycia prostego szkieletu z jakością nieistniejącej jeszcze logiki biznesowej.

## Ponowny odbiór WP-01.6 — 17.10.2026

Historyczne wyniki WP-01.5 powyżej zachowano. Po uruchomieniu Docker Desktop: RequiresDocker=true **1/1 PASS**, RequiresDocker=false **3/3 PASS**. Potwierdzono oddzielny kontener PostgreSQL, losowy port i wolumen testowy, usunięcie kontenera oraz niezmienność logicznego dumpa developerskiej bazy przed i po teście. Compose build/config/up, zdrowie API i bazy oraz HTTP przez Nginx przeszły. Vitest 5/5 i Playwright 2/2 zachowują wyniki pierwotnego audytu; nie powtarzano ich przy zmianach wyłącznie dokumentacyjnych. Status środowiska: **READY WITH WARNINGS**, ograniczenia manualne i mutacyjne pozostają jawne. Dowody: ignorowany `artifacts/wp016-recheck/`; [przekazanie WP-02](../ai/WP02_HANDOFF.md).

## WP-03.1 — bieżące komendy i granice

`dotnet test backend/tests/Roomify.Application.UnitTests --no-restore` uruchamia 32 przypadki bez bazy, `dotnet test backend/Roomify.slnx --no-restore` cały backend, także 63 PostgreSQL. Nowe filtry integracyjne: Category=ReservationApplication (21) i Category=ApplicationComposition (3); cały RequiresDocker=false (75). Raport z 26 AC i pierwszymi niepowodzeniami: [WP03_1_REPORT.md](WP03_1_REPORT.md). [RESERVATION_APPLICATION.md](../architecture/RESERVATION_APPLICATION.md) opisuje sekwencję, kontrakty i brak adaptera HTTP. 40P01 ma odrębny InfrastructureFailure, bez retry. Nie wykonano nowych E2E/manualnych/pokrycia/Domain mutation. Baza developerska zachowana bez zmian.

## WP-03.2 — bieżący stan

**362/362 PASS**, 25/25 AC. Domain 116, Application 87, integracyjne 159. `dotnet test backend/Roomify.slnx --no-restore` obejmuje wszystkie poziomy backendu, także Dockera. Filtr Category=ReservationManagement wybiera 21 nowych testów PostgreSQL. Dokumentacja i wspólny wynik: [RESERVATION_MANAGEMENT.md](../architecture/RESERVATION_MANAGEMENT.md), [raport](WP03_2_REPORT.md). Wersja shadow xmin rozróżnia ConcurrentModification od terminu Conflict; bez retry i bez zmiany właściciela/sali. Migracja mapowania przetestowana, developerka nie migrowana, dump/historia zachowane. Pierwszy nieudany test pełnej regresji i jego naprawa pozostają jawne. WP-03.3 nie rozpoczęto. Wcześniejsze liczby w sekcjach historycznych opisują tamte etapy; nowe Domain coverage/mutation, UI/E2E/manualne nie wykonywane.

## Aktualizacja WP-03.3

[Raport i 26 AC](WP03_3_REPORT.md), [zarządzanie salami](../architecture/ROOM_MANAGEMENT.md). Backend **469/469**: Domain 118, Application 164, integracyjne 187 = 76 bez Dockera + 111 PostgreSQL. Stryker Application 99,42%; 1 równoważny Survived. Developerka bez zmian. READY WITH WARNINGS w zakresie WP-03.3; bez HTTP/UI i bez automatycznego rozpoczęcia WP-03.4. Wcześniejsze sekcje i raporty stanowią historię.


## Przyrost WP-03.4 — 18.10.2026

**557/557 backend**: Domain 118, Application 228, integracyjne 211 (78 bez Dockera + 133 PostgreSQL). Restore/Debug/Release PASS, Stryker Application 99,17%. READY WITH WARNINGS, 30/30 AC PASS. Bez nowych migracji i bez zmian developerki. Brak produkcyjnych endpointów login/logout/me i UI; WP-03.5 nie rozpoczęto.

[Pełny raport](WP03_4_REPORT.md), [architektura kont i sesji](../architecture/USER_MANAGEMENT.md). Poprzednie wyniki zachowano jako historyczne.

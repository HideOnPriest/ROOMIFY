# Testowanie Roomify — WP-01.5

Cel: przygotować pięć poziomów testowania bez fikcyjnej logiki biznesowej. [TEST_STRATEGY.md](../ai/TEST_STRATEGY.md) zachowuje pełną strategię WP-00, identyfikatory scenariuszy, macierz ryzyk i robocze cele jakościowe. Poniższy plan docelowy uzupełnia aktualny stan WP-01.5 opisany na końcu dokumentu.

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
| Unit backend | xUnit, Test SDK, coverlet; dwa projekty bez przypadków do czasu WP-03 | [UNIT_TESTS.md](UNIT_TESTS.md) |
| Unit frontend | Vitest, TestBed, jsdom, coverage-v8; zachowane 5 testów | [UNIT_TESTS.md](UNIT_TESTS.md) |
| Integration | WebApplicationFactory: 3 przypadki HTTP; Testcontainers i Npgsql: osobny SELECT 1 | [INTEGRATION_TESTS.md](INTEGRATION_TESTS.md) |
| Mutation | Lokalny Stryker.NET 5.0.0, konfiguracja Domain; score N/A | [MUTATION_TESTS.md](MUTATION_TESTS.md) |
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

Wyniki xUnit zapisuje `--logger "trx;LogFileName=api.trx" --results-directory artifacts/test-results/api`. Używaj oddzielnych nazw dla jednostek, HTTP i PostgreSQL. Pokrycie .NET zapewnia dołączony coverlet przez `--collect:"XPlat Code Coverage"`, zapisując `coverage.cobertura.xml` w katalogu wyników. Miarodajne pokrycie domeny powstanie wraz z jej logiką.

Vitest wypisuje wynik w konsoli i tworzy coverage pod `frontend/coverage/`. Playwright zapisuje HTML w `frontend/playwright-report/` i artefakty błędów w `frontend/test-results/`. Stryker będzie tworzyć lokalne HTML/JSON w `artifacts/mutation/`. Wszystkie te katalogi są ignorowane przez Git; nie dodano agregatora ani CI. Nie umieszczaj w raportach sekretów.

PASS oznacza rzeczywiste powodzenie asercji, FAIL niepowodzenie, BLOCKED przeszkodę środowiskową w raporcie pracy, a NOT RUN brak wykonania. Runner zapisuje niedostępny Docker jako failed, bez automatycznego maskowania. Zero przypadków jednostkowych nie oznacza zaliczenia domeny. Dowody bieżącej weryfikacji .NET zapisano pod ignorowanym `artifacts/wp015/`.

## Obecne ograniczenia

Brak kodu domenowego, EF Core, Identity i integracji API z bazą. Test SELECT 1 wymaga działającego Dockera; po uruchomieniu Desktop potwierdzono jego sukces w ponownym odbiorze WP-01.6. E2E obejmują aktualny frontend, bez API i bazy. Scenariusze ręczne pozostają NOT RUN. Moq zostanie dodany dopiero przy rzeczywistej potrzebie mockowania. Wysokie pokrycie szkieletu nie dowodzi jakości przyszłych funkcji. Aktualny odbiór środowiska przedstawia [audyt WP-01.6](WP01_AUDIT_REPORT.md).

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

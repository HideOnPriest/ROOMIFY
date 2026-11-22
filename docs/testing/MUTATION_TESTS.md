# Testy mutacyjne

## Cel i interpretacja

Stryker.NET ocenia skuteczność testów przez kontrolowane zmienianie kodu. **Mutant** to taka zmiana, np. `<` na `<=`. **Wykryty (killed)** powoduje błąd testu. **Niewykryty (survived)** pozostaje mimo przechodzących testów; trzeba zbadać brak przypadku, słabą asercję lub zmianę równoważną.

**Mutation score** jest miarą wykrytych zmian według definicji danej wersji narzędzia. Interpretuj ją wraz z kategoriami, wykluczeniami, mutantami nieobjętymi testami, timeoutami i błędami kompilacji. Brak mutantów oznacza N/A, a nie 100%. Pokrycie linii nie gwarantuje jakości: test może wykonać kod bez sprawdzenia właściwego rezultatu.

## Narzędzie i zakres

Stryker.NET 5.0.0 jest przypięty lokalnie w `.config/dotnet-tools.json`. Plik `backend/tests/Roomify.Domain.UnitTests/stryker-config.json` wybiera wyłącznie Domain i jego projekt testowy. Wzorce obejmują `.cs`, pomijając bin, obj, migracje i typowe pliki wygenerowane. Konfiguracje JSON nie są mutowane.

Lokalne raporty HTML/JSON trafiają do ignorowanego katalogu podanego przez --output; pierwsza pełna analiza WP-02.1 znajduje się w `artifacts/wp021/mutation/reports/`. Nie skonfigurowano publikacji do dashboardu ani nowego progu zaliczenia.

## Uruchomienie

Z katalogu głównego:

```powershell
dotnet tool restore
dotnet tool list --local
dotnet stryker --help
```

Po powstaniu kodu i testów Domain (wykonane w WP-02.1):

```powershell
Set-Location backend/tests/Roomify.Domain.UnitTests
dotnet stryker --config-file stryker-config.json --output ../../../artifacts/mutation
Set-Location ../../..
```

Ścieżki są względne do katalogu projektu testowego. Stryker 5 przyjmuje `--output` wyłącznie jako opcję CLI, nie klucz JSON; potwierdzają to [dokumentacja narzędzia](https://stryker-mutator.io/docs/stryker-net/configuration/#output-string) i kontrola zainstalowanej wersji. Bez tej flagi domyślny `StrykerOutput/` również jest ignorowany przez Git. Opcja `--version` nie służy do wypisania wersji narzędzia; użyj manifestu i `dotnet tool list --local`.

## Przykład i ograniczenia

Zamiana `Start < other.End` na `<=` w ReservationPeriod.Overlaps błędnie wykrywa konflikt sąsiadujących rezerwacji. Theory z oboma kierunkami stykania wykrywa tę mutację. Jest to teraz istniejący model i test, bez mechanizmu konfliktów w bazie.

W WP-02.1 wykonano pełną analizę istniejącego modelu i 85 testów: **84,31%**, 86 Killed, 16 Survived z 102 badanych mutantów, 0 Timeout i 0 NoCoverage. Dodatkowo 24 Ignored przez domyślny filtr pokrytych bloków oraz 1 CompileError, łącznie 127 utworzonych. Wynik nie obejmuje przyszłych przypadków Application, persystencji ani autoryzacji.

Pozostałe 16 mutantów to 10 zmian komunikatów i 6 usunięć kontroli null w API programistycznym. Testy nie weryfikują wszystkich tych kontraktów; nie klasyfikujemy zmian jako równoważnych ani nie wykluczamy ich z wyniku. Szczegółowa analiza i polecenie wykonania są w [raporcie WP-02.1](WP02_1_REPORT.md).

Historycznie kontrolne uruchomienie WP-01.5 wczytało poprawioną konfigurację i rozpoznało projekt Domain, po czym zakończyło się kodem 1 z komunikatem o zerowej liczbie testów. Nie była to zaliczona sesja mutacyjna. Log zachowano w ignorowanym `artifacts/wp015/stryker-check.log`; nie zastępujemy go nowym wynikiem.

## Aktualna analiza WP-02.5 — 18.10.2026

Ten sam Stryker.NET 5.0.0 i niezmieniona konfiguracja Domain, bez rozszerzania wykluczeń. Pierwszy wynik aktualnego kodu: **76,64%**, 82 Killed i 25 Survived. Po dodaniu 23 testów kontraktów/null/komunikatów i samodzielnej polityki DST: **93,46%**, 100 Killed i 7 Survived. W obu przebiegach: **133 wygenerowane, 107 badane, 25 Ignored** z rzeczywistym powodem `Removed by block already covered filter`, **1 CompileError**, **0 NoCoverage, Timeout i RuntimeError**. Nie liczono Ignored jako Killed. CompileError to mutant 54 (End + Start dla DateTimeOffset), nie błąd produkcyjnego builda.

Końcowe Survived to ID **17, 18, 19, 20, 21, 24, 26**, wszystkie w Reservation.cs: usunięcia powtórnych walidacji identyfikatorów i zmiany kodów błędu wewnątrz konstruktora albo usunięcie wcześniejszej kontroli z fabryki przy zachowaniu kontroli konstruktora. Nie uznajemy ich zbiorczo za równoważne: część wpływałaby na odtwarzanie błędnych danych poza publiczną fabryką lub kolejność błędów wielokrotnie niepoprawnego wejścia. Pozostają w mianowniku score; nie zmieniono domeny ani nie dodano testów prywatnego konstruktora przez refleksję tylko dla wyniku. Pełna tabela wszystkich 7 mutantów i 18 naprawionych luk: [raport odbioru](WP02_ACCEPTANCE_REPORT.md#wszystkie-końcowe-niewykryte-mutanty).

```powershell
Set-Location backend/tests/Roomify.Domain.UnitTests
dotnet stryker --config-file stryker-config.json --output ../../../artifacts/wp025/mutation-final --skip-version-check --concurrency 4
Set-Location ../../..
```

HTML końcowy: `artifacts/wp025/mutation-final/reports/mutation-report.html`, JSON obok. Pierwsze raporty i logi pozostały w `artifacts/wp025/mutation-initial/` i `artifacts/wp025/stryker-initial.log`. Końcowy log: stryker-final.log. Czas narzędzia 28,063 s / 27,251 s. Cel 80% został osiągnięty; score dotyczy wyłącznie Domain, nie ochrony PostgreSQL ani autoryzacji HTTP. Poprzednie wyniki 84,31% pozostają historią WP-02.1.

## Nowa logika Application — WP-03.1

Dodano osobną konfigurację `backend/tests/Roomify.Application.UnitTests/stryker-config.json`, która wybiera tylko `Reservations/CreateReservationService.cs`, bez DTO, DI, EF i migracji. Stryker.NET 5.0.0 bez aktualizacji: **100% — 19/19 Killed**, 0 Survived/NoCoverage/Timeout/RuntimeError. Pięć CompileError to syntetyczne usunięcia bloków; zwykłe buildy PASS. Jawna rozbieżność narzędzia: log 29 wygenerowanych/5 Ignored, JSON 28 wpisów/4 Ignored; oba zgodne co do 19 badanych i wykrytych. Nie traktujemy brakującego wpisu jako wykrytego ani nie poprawiamy ręcznie raportów. Pełne liczniki i ograniczenia w [raporcie WP-03.1](WP03_1_REPORT.md#mutacje-nowej-logiki-application).

Z backend/tests/Roomify.Application.UnitTests: `dotnet stryker --config-file stryker-config.json --output ../../../artifacts/wp031/mutation-application --skip-version-check --concurrency 4`. HTML/JSON pod artifacts/wp031/mutation-application/reports, log stryker-application.log. Wynik dotyczy wyłącznie badanego serwisu; Domain nie mutowano ponownie w WP-03.1. Zachowano wcześniejsze wyniki i konfigurację Domain.

## Rozszerzony zakres Application — WP-03.2

Stryker.NET 5.0.0 w istniejącym projekcie Application.UnitTests obejmuje Create/Update/Cancel oraz ReservationAccess/ReservationSaveResults, bez DTO/DI/mapowań/migracji. Rzeczywisty wynik **100% — 63/63 Killed**, 0 Survived/NoCoverage/Timeout/RuntimeError, 5 CompileError; czas 22,813 s. JSON: Create 15 Killed, Update 24, Cancel 17, Access 4, SaveResults 3. Nie zmieniano poprawnej logiki ani asercji dla procentu. Rozpoznanie DbUpdateConcurrencyException w Infrastructure jest sprawdzane prawdziwymi testami, nie zaliczane do score Application.

Jawne liczniki: log **82 wygenerowane / 14 Ignored**, JSON **80 wpisów / 12 Ignored**; 5 CompileError i 63 Killed zgodne w obu. Brakujące wpisy filtra nie są dopisywane ani liczone jako wykryte. Błędy kompilacji dotyczą syntetycznego usunięcia bloków, zwykłe buildy PASS. [Pełne wyjaśnienie](WP03_2_REPORT.md#strykernet).

Z backend/tests/Roomify.Application.UnitTests: `dotnet stryker --config-file stryker-config.json --output ../../../artifacts/wp032/mutation-initial --skip-version-check --concurrency 4`. HTML/JSON w artifacts/wp032/mutation-initial/reports, log stryker-initial.log. To jedyna, poprawna sesja tego pakietu (nazwa initial zachowana); nie mierzono ponownie mutacji/coverage zmienionego Domain. Historycznych wyników wcześniejszych etapów nie zastąpiono.

## Application po WP-03.3 — 18.10.2026

Stryker.NET 5.0.0 uruchomiono z rozszerzoną konfiguracją Application: dotychczasowe rezerwacje oraz CreateRoom, UpdateRoom, RoomActivity, GetRoomById, GetRooms i RoomRules. Pominięto DTO/DI/migracje/snapshot. **99,42%**, 170 Killed / 171 ocenionych, 1 Survived, 15 CompileError; 0 NoCoverage/Timeout/RuntimeError. Ocalały mutant zmienia <=int.MaxValue na <int.MaxValue dla offsetu: równość jest nieosiągalna przy PageNumber<=int.MaxValue i PageSize<=100, bo int.MaxValue jest liczbą pierwszą. To mutant równoważny, bez zmiany poprawnej logiki dla wyniku. Log: 216 utworzonych/30 Ignored; JSON: 214/28 Ignored — różnica serializacji 2 pominiętych, nie ocenionych. Raporty wygenerowane tylko w ignorowanym artifacts/wp033. Stryker przebudowuje projekty; nie uruchamiać równocześnie innych buildów. Wyniki wcześniejszych etapów zachowano. [Pełny raport](WP03_3_REPORT.md).


## Przyrost WP-03.4 — 18.10.2026

Stryker.NET 5.0.0, Application rozszerzone o ApplicationAccess, UserServices i UserRules, obok wcześniejszych sal/rezerwacji. Końcowo **99,17% = 238 Killed / 240 ocenionych**, 2 równoważne Survived offsetu, 33 CompileError, 0 NoCoverage/Timeout/RuntimeError. Log 315 utworzonych i 42 Ignored; JSON 312 wpisów i 39 Ignored (3 pominięte wpisy serializacji). Pierwsze wykonanie 93,33%/16 Survived wskazało brak dokładnych asercji kodów i granicy e-maila; dodano testy bez zmiany poprawnej logiki. Dwa pozostałe mutanty <=int.MaxValue → < dotyczą nieosiągalnej równości przy PageSize<=100. Raporty tylko w ignorowanym artifacts/wp034/stryker-final. Stryker nie obejmuje adaptera Identity ani middleware; te zabezpieczono realnymi testami integracyjnymi.

[Pełny raport](WP03_4_REPORT.md), [architektura kont i sesji](../architecture/USER_MANAGEMENT.md). Poprzednie wyniki zachowano jako historyczne.

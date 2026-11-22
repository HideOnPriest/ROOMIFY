# Testy jednostkowe

## Cel, zakres i narzędzia

Izolowane testy sprawdzają konkretne zachowanie bez Dockera, bazy i prawdziwych żądań HTTP. Projekty .NET 10: `Roomify.Domain.UnitTests` zależy wyłącznie od Domain, a `Roomify.Application.UnitTests` od Application i Domain. Używają xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, runnera Visual Studio 3.1.4 i coverlet.collector 6.0.4 z szablonu SDK.

`Fact` oznacza pojedynczy przypadek, a `Theory` wykonuje ten sam test dla różnych danych, np. `InlineData`. ARRANGE przygotowuje dane, ACT wykonuje operację, ASSERT sprawdza wynik. DI dostarcza zależności zamiast ich samodzielnego tworzenia; Moq będzie potrzebny dopiero przy rzeczywistych interfejsach, dlatego nie został jeszcze dodany.

Frontend używa Vitest 5.0.5, Angular TestBed, jsdom i coverage-v8 5.0.5. TestBed składa izolowane środowisko Angulara, a `whenStable()` pozwala czekać na renderowanie. Zachowano pięć testów WP-01.3 sprawdzających layout, przekierowanie, powrót z 404, treści strony głównej oraz komunikat i link 404. Testy sprawdzają DOM i nawigację, bez prywatnych metod i prawdziwej sieci.

## Uruchomienie i raportowanie

Po restore, z katalogu głównego:

```powershell
dotnet test backend/tests/Roomify.Domain.UnitTests --no-restore --logger "trx;LogFileName=domain.trx" --results-directory artifacts/test-results/unit
dotnet test backend/tests/Roomify.Application.UnitTests --no-restore --logger "trx;LogFileName=application.trx" --results-directory artifacts/test-results/unit
```

W `frontend/`, po `npm ci`:

```powershell
npm run test:ci
npm run test:coverage
npm run test:ci -- --reporters=json --output-file=coverage/vitest-results.json
```

Ostatnie polecenie zapisuje JSON po utworzeniu katalogu coverage poprzednim poleceniem. `npm test` służy pracy interaktywnej. HTML pokrycia powstaje w `frontend/coverage/`, ewentualnie w podkatalogu projektu. W przyszłych testach .NET dodaj `--collect:"XPlat Code Coverage"` do uzyskania Cobertura.

## Interpretacja i ograniczenia

Domain zawiera Room, Reservation, ReservationPeriod i politykę czasu. Po WP-02.2 **87/87 przypadków** sprawdza walidację, aktualizacje, statusy, nakładanie, UTC, granice godzin, DST Europe/Warsaw i niezmienność migawek Period. Zachowano 85 wcześniejszych przypadków i dodano dwa dla mapowanych pól. Testy używają ControlledTimeProvider, bez bieżącego zegara i bazy. Historyczny pomiar WP-02.1 wyniósł 149/149 linii i 38/38 gałęzi; nie powtarzano go po zmianie Reservation. [DOMAIN.md](../architecture/DOMAIN.md) mapuje BR na testy; [raport WP-02.2](WP02_2_REPORT.md) opisuje bieżącą regresję i odrębne testy materializacji.

Application nadal zawiera tylko rejestrację DI i zero przypadków jednostkowych. Komunikat runnera o braku testów dotyczy obecnie tego projektu; nie oznacza PASS przypadków użycia. Nie dodano pustych Fact ani `Assert.True(true)`.

Vitest nie uruchamia Chromium ani E2E, a `e2e/**` jest jawnie wykluczone. Pokrycie obecnego szkieletu nie potwierdza przyszłych reguł rezerwacji. Sama wykonana linia kodu nie gwarantuje dobrych asercji.

## Ponowny odbiór domeny — WP-02.5, 18.10.2026

Aktualnie **110/110 PASS, 0 failed/skipped**. Zachowano wszystkie 87 wcześniejszych przypadków, dodając 23 w DomainContractTests: 11 wariantów null zegara/okresu z kontrolą ParamName i zachowania stanu, 10 błędów z kodem/kategorią/niepustym komunikatem oraz 2 osobno tworzone polityki na dniach zmiany DST. Nie dodano zależności, mocków infrastruktury ani zmian produkcyjnej domeny.

```powershell
dotnet test backend/tests/Roomify.Domain.UnitTests --no-restore --collect:"XPlat Code Coverage" --logger "trx;LogFileName=domain.trx" --results-directory artifacts/wp025/coverage
```

Coverlet: **160/160 wykonywalnych linii, 38/38 gałęzi — 100%**, wyłącznie assembly Domain. To miary linii/gałęzi, nie niezależna metryka instrukcji IL ani pokrycie MVP. Sześć klas ma wykonywalny kod, żadna istotna linia/gałąź nie pozostała niepokryta w tym pomiarze. Testy używają własnego ControlledTimeProvider i jawnych dat, bez Dockera/bazy. Czas runnera 76 ms, sesja TRX z kolektorem 1,745 s. Application ponownie uruchomione: 0 przypadków. Historyczne liczby powyżej zachowano jako wyniki wcześniejszych pakietów. [Odbiór WP-02](WP02_ACCEPTANCE_REPORT.md), [macierz wymagań](REQUIREMENTS_TRACEABILITY.md).

## Application — WP-03.1, 18.10.2026

Application ma obecnie **32/32 PASS** (wcześniejsze 0 pozostaje historią). Dodano Moq 4.20.72 tylko do projektu testów; mocks IRoomRepository/IReservationRepository/ICurrentUser współpracują z rzeczywistą domeną i kontrolowanym TimeProvider. Dane/testy są niezależne od bazy i Dockera. Obejmują poprawny zapis/UTC/właściciela, 14 wariantów wejścia, brak tożsamości/sali, nieaktywność, 7 układów dostępności, konflikt przy zapisie, 3 granice awarii, anulowanie i brak UserId w kontrakcie. Ważne przypadki mają komentarze Arrange/Act/Assert po polsku. Nie dodano pustych testów ani własnej geometrii czasu do serwisu.

`dotnet test backend/tests/Roomify.Application.UnitTests --no-restore`. Domain ponownie 110/110, jego kod/testy bez zmian. Końcowy backend: 280/280. Coverage Domain i mutacji Domain nie mierzono ponownie. [Architektura](../architecture/RESERVATION_APPLICATION.md), [wyniki i 26 AC](WP03_1_REPORT.md).

## Edycja i anulowanie — WP-03.2

18.10.2026: Domain **116/116** (6 nowych ReservationUpdateTests, 110 zachowanych), Application **87/87** (Create 32, Update 37, Cancel 18), bez failed/skipped. Nowe testy sprawdzają atomowość tytułu/okresu, walidację bez mutacji, właściciela/admina, null/Empty tożsamości i requestu, stan/termimy, salę, własne ID, brak Save po błędzie, rozróżnienie konfliktów i błędów oraz powtórne Cancel. Zegar kontrolowany, Moq/xUnit, bez bazy/Dockera. Ważne przypadki mają polskie komentarze Arrange/Act/Assert.

Wspólny wynik WP-03.1 ma teraz nazwę ReservationOperationResult/Status/Details; dawne asercje zachowano, typy uogólniono bez drugiego Result. Historycznego coverage Domain nie przeliczano dla nowej metody edycji. Komendy: `dotnet test backend/tests/Roomify.Domain.UnitTests --no-restore`, analogicznie Application.UnitTests. [Pełny raport](WP03_2_REPORT.md), [architektura](../architecture/RESERVATION_MANAGEMENT.md).

## Przyrost WP-03.3 — 18.10.2026

Bieżące wykonanie: Domain **118/118**, Application **164/164**, bez failed/skipped. Dodano 2 RoomActivationTests i 77 RoomManagementTests (xUnit/Moq): 6 usług, admin/odczyt, walidacja Domain bez mutacji, precheck i wyniki zapisu, filtrowanie zakresu, paging i błędy. Wcześniejsze wyniki są historią. Nowego coverage/mutation Domain nie wykonywano. [Raport](WP03_3_REPORT.md), [architektura](../architecture/ROOM_MANAGEMENT.md).


## Przyrost WP-03.4 — 18.10.2026

Domain **118/118**, Application **228/228**; przyrost 64 xUnit/Moq dotyczących sześciu usług kont, admin/gość/nieaktywność, walidacja i bezpieczne DTO, wyniki, paging, granica e-maila 256 i blokada nieaktywnego we wszystkich 9 wcześniejszych usługach. Starsze zaufane helpery jawnie deklarują IsActive=true; asercje biznesowe zachowane. Bez nowego coverage/mutation Domain.

[Pełny raport](WP03_4_REPORT.md), [architektura kont i sesji](../architecture/USER_MANAGEMENT.md). Poprzednie wyniki zachowano jako historyczne.

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

Domain nie ma jeszcze logiki, a rejestracje Application są puste. Projekty jednostkowe nie zawierają więc przypadków. Komunikat runnera o braku testów jest oczekiwany i nie oznacza PASS zachowania domeny ani 100% jakości. Testy reguł powstaną w WP-03. Nie dodano pustych Fact ani `Assert.True(true)`.

Vitest nie uruchamia Chromium ani E2E, a `e2e/**` jest jawnie wykluczone. Pokrycie obecnego szkieletu nie potwierdza przyszłych reguł rezerwacji. Sama wykonana linia kodu nie gwarantuje dobrych asercji.

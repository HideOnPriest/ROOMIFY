# Testy mutacyjne

## Cel i interpretacja

Stryker.NET ocenia skuteczność testów przez kontrolowane zmienianie kodu. **Mutant** to taka zmiana, np. `<` na `<=`. **Wykryty (killed)** powoduje błąd testu. **Niewykryty (survived)** pozostaje mimo przechodzących testów; trzeba zbadać brak przypadku, słabą asercję lub zmianę równoważną.

**Mutation score** jest miarą wykrytych zmian według definicji danej wersji narzędzia. Interpretuj ją wraz z kategoriami, wykluczeniami, mutantami nieobjętymi testami, timeoutami i błędami kompilacji. Brak mutantów oznacza N/A, a nie 100%. Pokrycie linii nie gwarantuje jakości: test może wykonać kod bez sprawdzenia właściwego rezultatu.

## Narzędzie i zakres

Stryker.NET 5.0.0 jest przypięty lokalnie w `.config/dotnet-tools.json`. Plik `backend/tests/Roomify.Domain.UnitTests/stryker-config.json` wybiera wyłącznie Domain i jego projekt testowy. Wzorce obejmują `.cs`, pomijając bin, obj, migracje i typowe pliki wygenerowane. Konfiguracje JSON nie są mutowane.

Lokalne raporty HTML/JSON będą w ignorowanym `artifacts/mutation/`. Nie skonfigurowano publikacji do dashboardu ani progu zaliczenia dla pustej domeny.

## Uruchomienie

Z katalogu głównego:

```powershell
dotnet tool restore
dotnet tool list --local
dotnet stryker --help
```

Po powstaniu kodu i testów Domain:

```powershell
Set-Location backend/tests/Roomify.Domain.UnitTests
dotnet stryker --config-file stryker-config.json --output ../../../artifacts/mutation
Set-Location ../../..
```

Ścieżki są względne do katalogu projektu testowego. Stryker 5 przyjmuje `--output` wyłącznie jako opcję CLI, nie klucz JSON; potwierdzają to [dokumentacja narzędzia](https://stryker-mutator.io/docs/stryker-net/configuration/#output-string) i kontrola zainstalowanej wersji. Bez tej flagi domyślny `StrykerOutput/` również jest ignorowany przez Git. Opcja `--version` nie służy do wypisania wersji narzędzia; użyj manifestu i `dotnet tool list --local`.

## Przyszły przykład i ograniczenia

Zamiana `A.StartAtUtc < B.EndAtUtc` na `<=` w przyszłej logice konfliktu może błędnie odrzucić sąsiadujące rezerwacje. Test stykających się przedziałów powinien wykryć tę zmianę. Jest to opis planu, bez implementacji reguły.

Domain nie ma jeszcze kodu biznesowego, a projekt jednostkowy nie ma przypadków. Instalacja i konfiguracja nie są miarodajnym wykonaniem mutacyjnym. Nie raportujemy score i nie tworzymy sztucznej logiki do uzyskania mutantów. Właściwe użycie nastąpi w WP-03.

Kontrolne uruchomienie WP-01.5 wczytało poprawioną konfigurację i rozpoznało projekt Domain, po czym zakończyło się kodem 1 z komunikatem o zerowej liczbie testów. Jest to oczekiwane ograniczenie obecnego zakresu, nie zaliczona sesja mutacyjna. Log zapisano w ignorowanym `artifacts/wp015/stryker-check.log`.

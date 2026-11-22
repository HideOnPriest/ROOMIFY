# WP-02.1 — raport implementacji i odbioru

Data: 17.10.2026. Zakres: model domenowy sal i rezerwacji oraz testy bez infrastruktury. Punkt wyjścia: czysty Git, HEAD `fb429d45161c371db77837308b9855ce47a5f588`. Nie wykonywano commitów ani push w WP-02.1. Nie zmieniono WP-00, istniejących testów integracyjnych, referencji projektów ani zależności pakietowych.

## Implementacja i śledzenie wymagań

Dodano osiem plików Domain: Room, Reservation, ReservationPeriod, ReservationTimePolicy, ReservationStatus, DomainGuard, DomainException i DomainErrorKind. Dodano pięć plików testowych: cztery zestawy oraz DomainTestData z kontrolowanym TimeProvider. Diagram zależności, szczegółowe powiązanie BR z nazwami testów, przykłady i decyzje opisuje [DOMAIN.md](../architecture/DOMAIN.md). Dokument ten jest bieżącą macierzą realizacji reguł; historycznej macierzy planu WP-00 nie modyfikowano.

| Reguły | Zrealizowana część | Przypadki |
|---|---|---|
| BR-001/003/004 | Poprawny przedział i geometria nakładania | Równy/wcześniejszy koniec, offsety, częściowe/całkowite/identyczne nakładanie, stykanie, rozłączność, północ. Bez zapytań do bazy. |
| BR-002/007/008/014/015/016 | Polityka czasu | Przeszłość/równość/1 tick przed startem, 08:00, 20:00, 8 h i przekroczenie, lokalna doba, minuty, DST marzec/październik. |
| BR-009/017/018/027 | Cykl Active → Cancelled | Zwolnienie terminu, zachowanie danych, idempotencja, zakaz edycji Cancelled i rozpoczętej rezerwacji. |
| BR-012/023/024 | Zmiana terminu, właściciel i tytuł | Ponowna walidacja oraz brak częściowej zmiany po błędzie, stały UserId, tytuł 1–120 po trimowaniu. |
| BR-019/025; FR-ROOM-004/005/006 | Sala | Nazwa/lokalizacja/pojemność, granice długości, aktualizacja, archiwizacja bez usuwania danych. Bez globalnej unikalności i bez blokady zapisu rezerwacji nieaktywnej sali. |

BR-002 precyzuje start ściśle późniejszy niż now; sformułowanie WP-02.1 „nie w przeszłości” nie osłabia tej granicy. BR-014/015 realizują doprecyzowania WP-00 w zakresie modelu zleconego WP-02.1; dokumenty nadal zachowują oznaczenie `[P]`. Ponowne anulowanie wybrano jako idempotentne (BR-027 nie narzuca konkretnej odpowiedzi HTTP). Nie dodano reaktywacji sal, bo nie została opisana. Nie ma sprzeczności wymagającej zmiany zatwierdzonych pięciu zasad.

## Weryfikacja wykonana

Dowody lokalne: ignorowany `artifacts/wp021/`, w tym commands.json, logi, TRX, Cobertura i raporty mutacyjne HTML/JSON. Wyniki odnoszą się do rzeczywiście wykonanych poleceń; zera przypadków nie traktujemy jako zaliczonych asercji.

| Polecenie / kontrola | Rzeczywisty wynik |
|---|---|
| `dotnet restore backend/Roomify.slnx` | PASS, kod 0, 1,328 s. |
| `dotnet build backend/Roomify.slnx --configuration Debug --no-restore` | PASS, 0 błędów i ostrzeżeń, 3,672 s całego polecenia. |
| Analogiczny build Release | PASS, 0 błędów i ostrzeżeń, 2,328 s. |
| `dotnet test backend/tests/Roomify.Domain.UnitTests --no-build --no-restore` z TRX i XPlat Code Coverage | **85/85 PASS**, 0 failed, 0 skipped, 4,125 s z pomiarem pokrycia. |
| `dotnet test backend/tests/Roomify.Application.UnitTests --no-build --no-restore` z TRX | Kod 0, **0 przypadków**, 2,437 s; brak przypadków Application jest jawnym ograniczeniem. |
| `dotnet test backend/tests/Roomify.Api.IntegrationTests --no-build --no-restore --filter "RequiresDocker=false"` z TRX | **3/3 PASS**, 0 failed, 0 skipped, 1,594 s. |
| Referencje projektowe i pakiety | Domain nadal nie ma referencji ani pakietów; wszystkie siedem .csproj niezmienione względem HEAD. |
| Dokumenty WP-00 | Cztery oryginalne pliki porównane ze stanem HEAD i archiwum źródłowym; bez zmian. |
| Dokumentacja / Git | Kontrola odnośników, różnic i ignorowania artefaktów; bez dodawania sekretów, bin, obj i raportów. |

Łącznie bieżąca kontrola regresji wykonała **88 zaliczonych przypadków xUnit** (85 domenowych + 3 HTTP). Osobno początkowy przebieg domeny również zakończył się 85/85; nie sumujemy powtórzeń jako nowych testów. Nie uruchamiano frontendu ani RequiresDocker=true w tym pakiecie: zlecona regresja obejmuje API bez Dockera, a ich kod nie został zmieniony. Wynik Testcontainers 1/1 pozostaje historycznym dowodem odbioru WP-01.6, nie nowym wynikiem WP-02.1.

| Zestaw | Wykonane / zaliczone | Failed / skipped |
|---|---:|---:|
| RoomTests | 22 / 22 | 0 / 0 |
| ReservationPeriodTests | 17 / 17 | 0 / 0 |
| ReservationTimePolicyTests | 24 / 24 | 0 / 0 |
| ReservationTests | 22 / 22 | 0 / 0 |
| HealthCheckTests (istniejące) | 3 / 3 | 0 / 0 |

Nazwy i indywidualny wynik każdego przypadku Theory/Fact są zapisane w `artifacts/wp021/trx/domain.trx` i `http.trx`. Raporty wykonania pozostają ignorowane, bez wersjonowania wygenerowanych plików.

Coverlet dla Roomify.Domain: **100% linii (149/149), 100% gałęzi (38/38)**. To metryka tego małego modelu, bez twierdzenia o pokryciu przyszłej persystencji, uwierzytelniania czy całej aplikacji.

## Pierwsza analiza mutacyjna

Wykonano Stryker.NET 5.0.0 z istniejącym stryker-config.json, `--output ../../../artifacts/wp021/mutation --skip-version-check --concurrency 4`, w katalogu Domain.UnitTests. Konfiguracji i zakresu mutowania nie zmieniano.

- Kod wyjścia 0; czas narzędzia **26,577 s**, początkowe 85 testów przechodzi.
- Utworzono **127** mutantów. **24 Ignored** przez domyślny filtr „block already covered” i **1 CompileError** nie weszły do 102 testowanych mutantów.
- **86 Killed, 16 Survived, 0 Timeout, 0 NoCoverage**. Błędy wykonania: 0; pojedynczy CompileError to błąd kompilacji mutanta, nie kodu aplikacji.
- Score narzędzia: **84,31% = 86/102**. Wykluczeń nie traktujemy jako wykrytych mutantów.

Przejrzano wszystkie 16 Survived w JSON: **10 zmian tekstów komunikatów** na puste i **6 usunięć ArgumentNullException.ThrowIfNull** (Room.Create; Reservation.Create/ChangePeriod/Cancel; ReservationPeriod.Overlaps; ReservationTimePolicy.Validate). Testy sprawdzają kody/kategorie naruszeń oraz zachowanie modelu, ale nie gwarantują pełnego brzmienia komunikatów i wszystkich programistycznych kontraktów null. To jawne ograniczenie. Nie uznajemy tych mutantów za równoważne: pogarszają diagnostykę. Badane mutanty porównań nakładania, granic godzin, długości, teraźniejszości oraz statusów zostały wykryte. Nie zmieniano zasad ani nie wykluczano trudnych mutantów dla podniesienia wyniku.

## Kryteria AC-001–AC-025

| ID | Wynik | Dowód |
|---|---|---|
| AC-001 | PASS | Room: tworzenie, aktualizacja, archiwizacja. |
| AC-002 | PASS | Reservation: tworzenie, zmiana terminu, anulowanie. |
| AC-003 | PASS | Niemutowalny ReservationPeriod. |
| AC-004 | PASS | ReservationStatus: Active, Cancelled. |
| AC-005 | PASS | Domain bez pakietów i referencji do infrastruktury. |
| AC-006 | PASS | Prywatne konstruktory, brak publicznych setterów, walidacja przed zmianą danych. |
| AC-007 | PASS | Theory sprawdza oba kierunki stykania. |
| AC-008 | PASS | Częściowe, pełne i identyczne nakładanie. |
| AC-009 | PASS | Koniec równy/wcześniejszy od początku odrzucany. |
| AC-010 | PASS | Granice 08:00–20:00 w Europe/Warsaw. |
| AC-011 | PASS | 8 h dopuszczone, 8 h + minuta/tick odrzucone. |
| AC-012 | PASS | Start w przeszłości i równy now odrzucany. |
| AC-013 | PASS | Jawne testy dat przed i po obu przejściach DST. |
| AC-014 | PASS | Anulowanie ustawia Cancelled i zwalnia Blocks. |
| AC-015 | PASS | Brak ponownego otwarcia; zmiana Cancelled/rozpoczętej niedozwolona; Cancel idempotentne. |
| AC-016 | PASS | 85 rzeczywistych przypadków domenowych, bez pozornych Fact. |
| AC-017 | PASS | Granice długości pól, godzin, czasu trwania, now i statusów. |
| AC-018 | PASS | Domain.UnitTests bez bazy, Dockera i zależności infrastruktury. |
| AC-019 | PASS | Debug i Release całego rozwiązania, zero ostrzeżeń/błędów. |
| AC-020 | PASS | Istniejące HTTP 3/3; Application uruchomione, 0 przypadków. Testcontainers bez ponownego wykonania. |
| AC-021 | PASS | DOMAIN.md: model, Mermaid, decyzje, BR, przykłady i polecenia. |
| AC-022 | PASS | Komentarze i objaśnienia po polsku, identyfikatory po angielsku. |
| AC-023 | PASS | Wyłącznie model, błędy i polityka Domain, bez persystencji. |
| AC-024 | PASS | Cztery dokumenty WP-00 bez zmian. |
| AC-025 | PASS | Bez EF/Identity/API/frontendu/repozytoriów/współbieżności bazodanowej. |

Wynik odbioru WP-02.1: **25 PASS, 0 FAIL, 0 BLOCKED**. Ograniczenia pozostają opisane; wynik nie jest odbiorem całego MVP.

## Pozostałe prace i WP-02.2

Brak walidacji globalnej unikalności, aktywności/istnienia innych encji, uprawnień zalogowanego użytkownika, kontroli kolizji w bazie i ochrony współbieżności. Model nie ma mechanizmu persystencji, metod materializacji ORM, wyposażenia sal ani edycji innych pól rezerwacji. Wymagają zakresu kolejnych zleceń i nie są oznaczone jako ukończone FR. Application nadal ma zero testów; manualne pozostają NOT RUN. Pierwszy mutation score nie usuwa opisanych luk diagnostycznych.

WP-02.1 zakończono. **WP-02.2 nie rozpoczęto**; można przekazać mu model i ten raport. Szczegółowe zlecenie WP-02.2 nie zostało dostarczone, więc nie przesądzamy jego implementacji ani zależności. Przy późniejszym mapowaniu ORM zachowaj niezmienniki i granice warstw; żadna zmiana stanu testów nie może dotykać developerskiego wolumenu.

# Testy integracyjne

## Cel, narzędzia i odpowiedzialność

Projekt `Roomify.Api.IntegrationTests` na .NET 10 sprawdza współpracę rzeczywistych elementów. Używa xUnit, Microsoft.AspNetCore.Mvc.Testing 10.0.9, Testcontainers.PostgreSql 4.16.0 i Npgsql 10.0.3.

WebApplicationFactory składa host API, DI, routing i Health Checks, ale żądania HttpClient trafiają do TestServer w pamięci. Nie startuje osobny Kestrel, nie rezerwujemy portu TCP i nie potrzebujemy bazy. Publiczna częściowa klasa Program udostępnia punkt wejścia bez zmiany zachowania endpointów. Testowe ustawienia wyłączają zależność od portu HTTPS hosta.

`HealthCheckTests` ma `RequiresDocker=false`. Weryfikuje 200, dokładny tekst Healthy i `text/plain`, a parametryzowany test sprawdza dwa adresy 404. To integracja, ponieważ współdziałają host, konfiguracja, middleware i endpoint, a nie izolowana metoda.

## Fixture PostgreSQL

Fixture to obiekt środowiska testu zarządzany przez xUnit. `PostgreSqlFixture` przez `IAsyncLifetime` uruchamia odrębny `postgres:18.6-bookworm` z własną bazą, użytkownikiem i losowym hasłem. Port hosta jest dynamiczny, a connection string pochodzi z Testcontainers. Nie odczytuje `.env` i nie montuje developerskich wolumenów.

Test z `RequiresDocker=true` otwiera połączenie Npgsql, wykonuje SELECT 1 i wymaga wyniku int równego 1. `await using` zamyka komendę i połączenie, a fixture usuwa kontener przez `DisposeAsync()`, również przy błędzie częściowego startu. Nie wyłączono domyślnego sprzątania Testcontainers. Nie powstają tabele ani migracje.

## Osobne uruchamianie i raporty

Po restore, z katalogu głównego:

```powershell
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=false" --logger "trx;LogFileName=api.trx" --results-directory artifacts/test-results/api
dotnet test backend/tests/Roomify.Api.IntegrationTests --no-restore --filter "RequiresDocker=true" --logger "trx;LogFileName=postgresql.trx" --results-directory artifacts/test-results/postgresql
```

Filtr działa przed tworzeniem fixture, dlatego testy HTTP nie startują kontenera. PostgreSQL wymaga działającego Dockera Linux, ale nie `docker compose up`. Pokrycie HTTP można uzyskać przez `--collect:"XPlat Code Coverage" --results-directory artifacts/coverage/api`.

## Interpretacja i ograniczenia

TRX zapisuje rzeczywiste wyniki. Brak Dockera daje failed w runnerze i środowiskowe BLOCKED w raporcie pracy; nie ma automatycznego skip ani PASS. W WP-01.5 i pierwotnym audycie WP-01.6 test PostgreSQL napotkał niedostępny `docker_engine`. Po uruchomieniu Docker Desktop 17.10.2026 ponowiono RequiresDocker=true: **1/1 PASS**, rzeczywiste SELECT 1, osobny port 52627, odrębny anonimowy wolumen i usunięcie kontenera potwierdzone. Porównanie logicznego dumpa developerskiej bazy przed i po teście nie wykazało zmiany. Ponowione testy HTTP: **3/3 PASS**. Szczegóły i granice dowodu opisuje [audyt, sekcja 9](WP01_AUDIT_REPORT.md#9-ponowny-odbiór-po-uruchomieniu-docker-desktop--17102026).

API nie jest jeszcze połączone z bazą. EF Core, DbContext, Identity i migracje pozostają poza zakresem; integracja danych nastąpi w WP-02. SELECT 1 jest kontrolą infrastruktury, nie reguł biznesowych.

# Rejestr decyzji architektonicznych

Decyzje wynikają z zatwierdzonego zakresu WP-01.1. Status „zaakceptowana” oznacza wybór architektury, nie wykonanie implementacji. Statusy i zapisy o przyszłych pracach przy poszczególnych decyzjach zachowują perspektywę WP-01.1. Od tego czasu powstały szkielety .NET i Angulara, konfiguracja kontenerów oraz infrastruktura testowa; ich aktualną weryfikację przedstawia [raport WP-01.6](../testing/WP01_AUDIT_REPORT.md). Dokumenty [WP-00](../ai/PROJECT_CONTEXT.md) pozostają niezmienionym zapisem wcześniejszego etapu; WP-01.1 rozstrzyga wybór wersji technologii i uwierzytelniania. Pozostałe propozycje analityczne `[P]` nie otrzymują przez to automatycznie statusu zatwierdzonych reguł biznesowych.

## ADR-001: Modularny monolit i Clean Architecture

- **Status:** zaakceptowana w WP-01.1; do implementacji.
- **Kontekst:** aplikacja obejmie konta, sale, rezerwacje i dostępność; projekt ma pokazać projektowanie i testowanie bez zbędnych kosztów operacyjnych.
- **Uzasadnienie:** jeden backend z warstwami Domain, Application, Infrastructure i Api oddzieli reguły biznesowe od mechanizmów technicznych.
- **Konsekwencje:** Domain nie zależy od innych projektów Roomify, Application zależy od Domain, Infrastructure od Application i Domain, Api od Application i Infrastructure. Domena będzie testowalna bez serwera i bazy. Trzeba utrzymywać granice obszarów funkcjonalnych wewnątrz monolitu.

## ADR-002: .NET 10 i Angular 22

- **Status:** zaakceptowana w WP-01.1; do implementacji.
- **Kontekst:** WP-00 proponował .NET i Angular bez zamrożenia wersji; bieżący zakres ustala .NET 10 LTS i Angular 22.
- **Uzasadnienie:** wspólny wybór wersji daje spójną podstawę backendu C# i frontendu TypeScript oraz późniejszej konfiguracji narzędzi testowych.
- **Konsekwencje:** projekty i zależności zostaną wygenerowane później. Przy ich inicjalizacji trzeba sprawdzić wymagania SDK, Node i zgodność narzędzi oraz zapisać konkretne wersje; teraz nie instaluje się pakietów.

## ADR-003: PostgreSQL i EF Core 10

- **Status:** zaakceptowana w WP-01.1; do implementacji.
- **Kontekst:** dane rezerwacji mają relacje i wymagają integralności oraz transakcji.
- **Uzasadnienie:** PostgreSQL zapewni relacyjny zapis i ochronę danych, a EF Core 10 mapowanie obiektów oraz wersjonowanie schematu migracjami.
- **Konsekwencje:** dostęp do danych i migracje należą do Infrastructure. Testy integracyjne będą korzystać z rzeczywistego PostgreSQL. Baza, schemat i migracje nie powstają w WP-01.1.

## ADR-004: ASP.NET Core Identity + cookies

- **Status:** zaakceptowana w WP-01.1; do implementacji.
- **Kontekst:** potrzebne są konta zarządzane przez administratora i uwierzytelnianie przeglądarki. WP-00 pozostawiał wybór cookies/JWT otwarty.
- **Uzasadnienie:** Identity zapewni mechanizmy zarządzania tożsamością, a cookies obsługę uwierzytelniania sesji w aplikacji webowej.
- **Konsekwencje:** integracja Identity należy do Infrastructure, konfiguracja uwierzytelniania i autoryzacji do Api. Należy zaprojektować wylogowanie, dezaktywację kont, ochronę CSRF i właściwości cookies. Nie przewiduje się publicznej rejestracji; szczegóły resetu haseł pozostają do ustalenia.

## ADR-005: Angular CSR i Standalone Components

- **Status:** zaakceptowana w WP-01.1; do implementacji.
- **Kontekst:** interfejs aplikacji wymaga formularzy i kalendarza; zakres nie wymaga renderowania serwerowego.
- **Uzasadnienie:** CSR i Standalone Components pozwolą budować interfejs bez osobnego serwera SSR; Reactive Forms, Signals i RxJS obsłużą formularze i stan.
- **Konsekwencje:** przeglądarka będzie pobierać dane przez API; trzeba zapewnić stany ładowania, braku danych i błędu. Frontend nie zastąpi walidacji backendu. Projekt Angular powstanie w kolejnym etapie.

## ADR-006: Monorepozytorium

- **Status:** zaakceptowana w WP-01.1; struktura przygotowana.
- **Kontekst:** backend, frontend, infrastruktura i dokumentacja dotyczą jednego produktu akademickiego.
- **Uzasadnienie:** wspólne repozytorium ułatwi przegląd zmian i spójne powiązanie wymagań z kodem i testami.
- **Konsekwencje:** katalogi `backend/`, `frontend/`, `infra/`, `docs/` i `.github/` mają odrębne odpowiedzialności. Wspólna konfiguracja edytora i Git obowiązuje całość; nie jest to wspólny projekt ani wspólny zestaw zależności.

## ADR-007: Docker Compose i Testcontainers

- **Status:** zaakceptowana w WP-01.1; do implementacji.
- **Kontekst:** środowisko lokalne i testy integracyjne wymagają przewidywalnego PostgreSQL.
- **Uzasadnienie:** Compose opisze usługi developerskie, a Testcontainers pozwoli testom zarządzać odrębnymi instancjami bazy.
- **Konsekwencje:** testy integracyjne będą wymagać dostępnego środowiska kontenerowego i izolowanych danych. Compose powstanie w WP-01.4; teraz nie tworzy się definicji ani kontenerów.

## ADR-008: Ochrona przed konfliktami rezerwacji na poziomie PostgreSQL

- **Status:** zaakceptowana w WP-01.1; mechanizm do zaprojektowania i implementacji.
- **Kontekst:** dwa równoczesne żądania mogą zobaczyć ten sam wolny termin przed zapisem; samo sprawdzenie w serwisie nie zapewnia integralności.
- **Uzasadnienie:** gwarancja na poziomie PostgreSQL zabezpieczy zapis także przy współbieżności i obejściu wcześniejszego odczytu dostępności.
- **Konsekwencje:** należy dobrać mechanizm bazodanowy i obsługę konfliktu HTTP 409, a następnie zweryfikować je testami z prawdziwym PostgreSQL. Szczegóły ograniczeń, transakcji i migracji zostaną ustalone później. Nie zmieniamy treści ani oznaczeń BR-003, BR-010 i BR-026 w WP-00.

## ADR-009: GitHub Actions

- **Status:** zaakceptowana w WP-01.1; do implementacji.
- **Kontekst:** projekt wymaga powtarzalnej weryfikacji kodu i testów.
- **Uzasadnienie:** GitHub Actions pozwoli opisać wspólny proces budowania i sprawdzania zmian w repozytorium.
- **Konsekwencje:** `.github/workflows/` jest miejscem na przyszłe konfiguracje CI. Etapy, sekrety, wymagania kontenerowe i publikacja raportów zostaną skonfigurowane po utworzeniu projektów; teraz pipeline nie istnieje.

## ADR-010: Dokumentacja i komentarze po polsku

- **Status:** zaakceptowana; stosowana w dokumentacji WP-01.1.
- **Kontekst:** materiały mają być zrozumiałe dla odbiorcy akademickiego, także bez znajomości .NET i Angulara.
- **Uzasadnienie:** polskie objaśnienia celu, decyzji i mniej oczywistych mechanizmów ułatwią naukę i przegląd projektu.
- **Konsekwencje:** dokumentacja i przyszłe komentarze będą po polsku, a nazwy klas, metod, katalogów i identyfikatorów technicznych po angielsku. Należy objaśniać przyczyny i zależności, bez komentowania oczywistych instrukcji.

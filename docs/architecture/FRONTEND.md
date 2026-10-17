# Frontend Roomify — WP-01.3

## Stan i technologie

Frontend jest szkieletem Angular 22 renderowanym w przeglądarce (CSR), bez SSR. Zawiera stronę startową, stronę 404 i responsywny layout. Nie realizuje logowania, rezerwacji ani innych funkcji biznesowych. Backend i dokumenty WP-00 nie są zmieniane w tym etapie.

Wersje zweryfikowane: Angular i CLI 22.2.3, TypeScript 6.0.3, Node.js 24.19.0, npm 12.2.0, Vitest 5.0.5 i RxJS 7.8.2. Konkretne zależności zapisuje `package-lock.json`; `npm ci` odtwarza instalację. Node musi spełniać wymagania opublikowanych pakietów Angulara: `^22.22.3 || ^24.15.0 || >=26.0.0`. Zalecany do tego projektu jest Node 24 LTS. [Oficjalna tabela zgodności](https://angular.dev/reference/versions) dokumentuje wymagania platformy; metadane zainstalowanych pakietów są podstawą kontroli konkretnej wersji.

## Standalone Components i struktura

Komponent standalone deklaruje w `imports` używane elementy, np. `RouterLink` lub `RouterOutlet`, bez pośrednictwa NgModule. W Angular 22 standalone jest domyślny, dlatego szablon CLI nie wymaga dopisywania `standalone: true`. `bootstrapApplication` w `main.ts` uruchamia komponent `App` z konfiguracją `app.config.ts`.

Zachowano nazewnictwo CLI 2025: `app.ts`, `home.ts`, `not-found.ts` i oddzielne szablony HTML oraz style SCSS. TypeScript ma jawne `strict: true`, a kompilator szablonów `strictTemplates: true` oraz kontrolę parametrów DI.

| Katalog | Przeznaczenie |
|---|---|
| `core/auth`, `core/http`, `core/services` | Przyszłe usługi ogólnoaplikacyjne i mechanizmy techniczne; obecnie tylko `.gitkeep` |
| `shared/components`, `shared/models`, `shared/utils` | Przyszłe elementy współdzielone, bez odpowiedzialności konkretnej funkcjonalności |
| `features/home`, `features/not-found` | Działające, samodzielne strony z lokalnymi szablonami, stylami i testami |
| `features/auth`, `rooms`, `reservations`, `calendar`, `admin` | Miejsca na kolejne funkcjonalności, bez implementacji i tras |
| `layout` | Miejsce na przyszłe wydzielenie elementów układu; obecny prosty layout mieści się w App |
| `src/environments` | Rezerwa struktury; bez zbędnych plików konfiguracyjnych środowisk |
| `e2e` | Od WP-01.5 dwa scenariusze Playwright/Chromium; [opis E2E](../testing/E2E_TESTS.md) |

## Routing i lazy loading

`provideRouter(routes)` rejestruje Router w kontenerze Angulara. Pusta ścieżka z `pathMatch: 'full'` przekierowuje na `/home`. Strona startowa i ostatnia trasa `**` używają `loadComponent` oraz dynamicznego `import()`. Ich kod trafia do osobnych fragmentów JavaScript i jest pobierany po dopasowaniu trasy. Wildcard musi pozostać ostatni, aby nie przechwytywał poprawnych adresów.

`RouterLink` obsługuje nawigację bez przeładowania całej strony, a `router-outlet` umieszcza dopasowany komponent wewnątrz `main`. Każda strona ma własny tytuł dokumentu. Nie istnieją jeszcze trasy `/login`, `/rooms`, `/reservations`, `/calendar` ani `/admin`; kierują obecnie na stronę 404.

Strona 404 jest widokiem CSR. Serwer developerski zwraca dokument startowy z HTTP 200 także dla nieznanych ścieżek; dopiero Router wyświetla komunikat 404. Docelowy hosting musi zapewnić obsługę powrotu do `index.html` dla tras frontendu, oddzielnie od `/api` i plików statycznych.

## Layout i dostępność

App zawiera semantyczne `header`, `nav`, `main` i `footer`. Link do strony głównej używa `aria-current`, a link „Przejdź do treści” jest dostępny z klawiatury i przenosi fokus do `main`. Interaktywne elementy mają widoczny fokus. Ilustracja sali to dekoracja CSS ukryta przed czytnikami ekranu; nie przedstawia rzeczywistych danych sal.

Układ przechodzi z dwóch kolumn na jedną na mniejszych ekranach. Zastosowano lokalne fonty systemowe, bez zewnętrznych zdjęć, fontów ani bibliotek UI. Dokument HTML ma język polski. Weryfikacja responsywności nie zastępuje pełnego audytu dostępności.

## Dependency Injection i HttpClient

DI w Angularze udostępnia zależności przez hierarchię kontenerów. Providery z `app.config.ts` zapewniają Router, globalne nasłuchiwanie błędów przeglądarki i `HttpClient`. Przyszłe serwisy będą pobierać potrzebne zależności np. przez `inject(HttpClient)`; nie tworzymy ich teraz wyłącznie dla demonstracji.

`provideHttpClient()` przygotowuje komunikację HTTP. Komponenty obecnych stron nie wykonują żadnych żądań API. Przyszłe adresy mają być względne, np. `/api/rooms`, aby przeglądarka komunikowała się z jednym originem. Nie dodano interceptorów, uwierzytelniania cookies ani serwisów biznesowych.

## Proxy i konfiguracja środowiska

`angular.json` wskazuje `proxy.conf.json` dla serwera developerskiego. Reguły `/api` i `/api/**` przekazują żądania do `http://localhost:5080` bez przepisywania ścieżki. Obejmuje to zarówno dokładne `/api`, jak i zagnieżdżone adresy. Prefiks jest zachowywany — nie ma `pathRewrite`. Port backendu znajduje się wyłącznie w konfiguracji proxy, nie w komponentach.

Backend WP-01.2 ma tylko techniczny `/health`, poza `/api`. Nie dodajemy dla niego dodatkowego przekierowania ani endpointów biznesowych; żądanie do przyszłego `/api/rooms` może obecnie zakończyć się 404. Frontend działa również przy wyłączonym backendzie, o ile użytkownik nie wykonuje jeszcze nieistniejących operacji API.

Proxy dotyczy `ng serve`, nie plików produkcyjnego builda. Docelowa infrastruktura jednego originu będzie kierować `/api` do backendu. Nie skonfigurowano CORS w .NET. `secure: false` dotyczy lokalnego celu HTTP i nie określa zasad bezpieczeństwa wdrożenia produkcyjnego.

Nie ma potrzeby tworzenia `environment.ts`: obecne wymagania rozwiązuje proxy. Żaden plik frontendu nie może przechowywać hasła, tokenu ani sekretu — jego zawartość trafia do przeglądarki. Przyszłe uwierzytelnianie cookies będzie wymagać ochrony operacji zmieniających stan przed CSRF, spójnej z backendem. Ten mechanizm pozostaje do implementacji w odpowiednim etapie.

## Signals, RxJS i Reactive Forms

Signals będą służyć do lokalnego stanu reaktywnego i wartości wyliczanych. RxJS będzie obsługiwać asynchroniczne strumienie, m.in. wyniki HttpClient i zmieniające się kryteria wyszukiwania. Reactive Forms pozwolą opisać formularze i walidację w kodzie. Zależności Angular Forms i RxJS są dostępne, ale informacyjne strony nie wymagają sztucznego stanu ani formularza. Nie dodano globalnego magazynu stanu ani NgRx.

## Testy komponentów

Zachowano builder `@angular/build:unit-test` z domyślnym Vitest i środowiskiem jsdom. Angular CLI przygotowuje kompilację szablonów oraz TestBed; nie dodano osobnej konfiguracji runnera. Pięć testów w trzech plikach sprawdza layout, przekierowanie `/`, powrót z nieznanej trasy, treści strony startowej oraz komunikat i link strony 404.

TestBed tworzy izolowane środowisko komponentów, a `provideRouter` udostępnia lokalny Router bez serwera. `whenStable()` czeka na zakończenie pracy Angulara przed sprawdzeniem DOM. Testy nie wykonują prawdziwych żądań sieciowych i nie wymagają backendu ani bazy. Komentarze Arrange / Act / Assert objaśniają przygotowanie, wykonanie i weryfikację.

## Uruchamianie

W katalogu `frontend/`, z Node i npm dostępnymi w PATH:

```powershell
npm ci
npm start
npm run build
npm test
npm run test:ci
```

`npm start` uruchamia `http://localhost:4200`. Build domyślnie jest produkcyjny i trafia do `dist/roomify/browser/`. `npm test` umożliwia pracę interaktywną, a `test:ci` wykonuje `ng test --watch=false`; dostępność tej flagi sprawdzono przez CLI 22.2.3. Serwer zatrzymuje się przez `Ctrl+C`.

W środowisku wykonania WP-01.3 dostępny był Node 24.19.0 bez npm w PATH. Do kontroli użyto lokalnej kopii npm 12.2.0 pobranej z oficjalnego rejestru do ignorowanego `artifacts/wp013/npm/`. Nie jest to zależność aplikacji ani element wymagany w czystym checkout; standardowe środowisko powinno udostępniać Node i npm. Przykład uruchomienia z dostępnym tutaj runtime podano w [README](../../README.md#uruchamianie-frontendu).

# Testy manualne

## Cel, narzędzia i uruchomienie

Człowiek ocenia czytelność, wygodę, obsługę klawiatury i układ ekranu. Narzędzia to przeglądarka, responsywny tryb DevTools i protokół poniżej. W `frontend/` uruchom `npm ci` oraz `npm start`, następnie otwórz `http://localhost:4200`. Backend i baza nie są wymagane. Serwer zatrzymuje Ctrl+C.

Zapisz datę, system, wersję przeglądarki i rozmiar ekranu. Wykonaj kroki i uzupełnij rzeczywisty wynik z dowodem. PASS oznacza zgodność, FAIL rozbieżność, BLOCKED niemożność wykonania, a NOT RUN brak wykonania. Automatyczny E2E nie jest ręcznym protokołem.

## Jednolity szablon

| Pole | Do uzupełnienia |
|---|---|
| ID testu | Stały identyfikator |
| Nazwa | Krótki opis |
| Powiązane wymaganie | ID z WP-00 lub kryterium etapu |
| Cel testu | Co oceniamy |
| Środowisko i data | System, przeglądarka/wersja, wymiary, data |
| Warunki wstępne | Stan aplikacji |
| Dane testowe | Adresy, parametry |
| Kroki wykonania | Numerowane czynności |
| Wynik oczekiwany | Obserwowalne zachowanie |
| Wynik rzeczywisty | Obserwacja po wykonaniu |
| Status | PASS / FAIL / BLOCKED / NOT RUN |
| Uwagi | Dowód, błąd, ograniczenia |

## TC-M-SMOKE-001 — Strona startowa

- **Powiązanie:** WP-01.5 AC-021; NFR-010 — aktualna instrukcja uruchomienia szkieletu.
- **Cel:** ocena czytelności informacji o projekcie.
- **Środowisko i data:** do uzupełnienia.
- **Warunki:** działa Angular Dev Server.
- **Dane:** `http://localhost:4200/`.
- **Kroki:** 1. Otwórz adres. 2. Sprawdź przekierowanie na /home. 3. Przeczytaj nazwę, opis, status i cel testowania. 4. Sprawdź header, main i footer.
- **Oczekiwany wynik:** Roomify, opis systemu, status realizacji i cel akademicki są czytelne; treści nie nachodzą na siebie.
- **Wynik rzeczywisty:** nie wykonano ręcznie w WP-01.5.
- **Status:** NOT RUN.
- **Uwagi:** dołączyć obserwację lub screenshot po wykonaniu.

## TC-M-SMOKE-002 — Nieznana trasa i powrót

- **Powiązanie:** WP-01.5 AC-021; NFR-012 — podstawowa obsługa linku klawiaturą.
- **Cel:** ocena komunikatu i nawigacji powrotnej.
- **Środowisko i data:** do uzupełnienia.
- **Warunki:** działa Angular Dev Server.
- **Dane:** `http://localhost:4200/nieistniejaca-strona`.
- **Kroki:** 1. Otwórz adres. 2. Przeczytaj 404. 3. Klawiszem Tab znajdź link powrotu. 4. Sprawdź fokus i aktywuj Enterem.
- **Oczekiwany wynik:** zrozumiały komunikat, dostępny link, powrót na /home i widoczna nazwa Roomify.
- **Wynik rzeczywisty:** nie wykonano ręcznie w WP-01.5.
- **Status:** NOT RUN.
- **Uwagi:** dotyczy widoku CSR, nie statusu HTTP serwera.

## TC-M-SMOKE-003 — Layout mobilny

- **Powiązanie:** FR-UI-001 — wyłącznie obecny szkielet.
- **Cel:** ocena odstępów i użyteczności na małym ekranie.
- **Środowisko i data:** do uzupełnienia.
- **Warunki:** otwarta /home, dostępny tryb responsywny.
- **Dane:** szerokości 390 i 320 px, orientacja pionowa.
- **Kroki:** 1. Ustaw 390 px. 2. Przewiń od headera do footera. 3. Sprawdź teksty i nawigację. 4. Powtórz przy 320 px. 5. Sprawdź przewijanie poziome.
- **Oczekiwany wynik:** jedna kolumna, brak obcięć i nachodzenia elementów, działające linki i brak poziomego przewijania.
- **Wynik rzeczywisty:** nie wykonano ręcznie w WP-01.5.
- **Status:** NOT RUN.
- **Uwagi:** DevTools nie zastępuje kontroli na urządzeniach.

## Ograniczenia

Wszystkie scenariusze pozostają NOT RUN. Wcześniejsze kontrole wizualne i automatyczne nie są nowym ręcznym wykonaniem tego protokołu. Nie ma jeszcze formularzy, logowania ani kalendarza i nie raportujemy ich sprawdzenia.

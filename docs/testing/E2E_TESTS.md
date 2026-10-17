# Testy End-to-End

## Cel, narzędzia i zakres

Oficjalny pakiet developerski `@playwright/test` uruchamia Chromium i sprawdza zachowanie widoczne w przeglądarce. Dwa scenariusze w `e2e/smoke.spec.ts` obejmują stronę główną oraz nieznaną trasę z powrotem do /home. Stabilne `getByRole` i `getByText` odwołują się do semantyki i tekstu UI; asercje automatycznie oczekują na warunek bez `waitForTimeout`.

Wybrano wariant A: webServer uruchamia Angulara na `127.0.0.1:4300` i zatrzymuje go po testach. Wywołuje lokalny Angular CLI przez Node, bez globalnego CLI i zagnieżdżonego npm. `reuseExistingServer: false` zapobiega użyciu przypadkowej aplikacji na tym porcie. API i PostgreSQL nie są potrzebne.

## Uruchomienie

W `frontend/`:

```powershell
npm ci
npx playwright install chromium
npm run e2e
npm run e2e:report
```

Pierwsza instalacja Chromium wymaga internetu. Dostępny musi być Node w PATH i wolny port 4300. Domyślny tryb to headless. `npm run e2e -- --list` tylko wypisuje scenariusze.

## Raport i interpretacja

HTML powstaje w `frontend/playwright-report/index.html` bez automatycznego otwarcia. `e2e:report` uruchamia lokalny serwer raportu, zatrzymywany Ctrl+C. Artefakty błędów są w `frontend/test-results/`: screenshot po błędzie i trace przy pierwszym ponowieniu (`retries: 1`). Trace otworzysz przez `npx playwright show-trace <ścieżka-do-trace.zip>`.

Timeout testu wynosi 30 s, asercji 5 s, a startu Angulara 120 s. Wynik flaky po ponowieniu wymaga analizy; nie jest stabilnym powodzeniem za pierwszym razem. Katalogi wyników są ignorowane przez Git.

## Ograniczenia

Obecny zakres dotyczy wyłącznie frontendu, bez pełnego przyszłego przepływu przeglądarka → API → baza. Nie tworzymy kont ani testów logowania i rezerwacji. Brak przeglądarki jest blokadą środowiskową, a nie powodem pomijania asercji. Vitest i Playwright mają odrębne zakresy oraz polecenia.

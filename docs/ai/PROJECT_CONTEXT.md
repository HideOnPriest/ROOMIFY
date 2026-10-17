# Roomify — kontekst projektu (WP-00)

**Status:** specyfikacja bazowa v1.0, 17.10.2026. **Etap:** analiza, bez implementacji.

## Cel
Roomify to webowy system rezerwacji sal konferencyjnych i spotkań. Projekt na zaliczenie przedmiotu „Programowanie i testowanie aplikacji webowych”. Najważniejszym wynikiem projektu jest udokumentowane testowanie: jednostkowe, integracyjne, mutacyjne, E2E i manualne.

## Zatwierdzone decyzje
- Konta użytkowników tworzy wyłącznie administrator; brak publicznej rejestracji.
- Rezerwowane zasoby: sale konferencyjne i spotkań.
- Godziny dopuszczalnych rezerwacji: 08:00–20:00.
- Administrator może zarządzać rezerwacjami innych użytkowników.
- Jedna rezerwacja może trwać maksymalnie 8 godzin.

## Proponowany stos technologiczny (do weryfikacji w WP-01)
- Angular + TypeScript (CSR), ASP.NET Core Web API + C#, Entity Framework Core, PostgreSQL.
- Backend: xUnit, mocki zależności, WebApplicationFactory, Testcontainers, Stryker.NET.
- Frontend: narzędzie do testów komponentów zgodne z wybraną wersją Angulara (preferowany Vitest), Playwright do E2E.
- Docker Compose do lokalnego środowiska; GitHub Actions do CI.
- Nie zakładamy wymogu SSR. Wersje narzędzi i sposób uwierzytelniania zostaną zamrożone w WP-01.

## Zakres MVP
1. Logowanie, wylogowanie, autoryzacja i konta zakładane przez administratora.
2. Lista i szczegóły sal, wyszukiwanie i filtrowanie.
3. Administracyjne dodawanie, edytowanie i archiwizowanie sal.
4. Tworzenie, przeglądanie, edycja i anulowanie rezerwacji z walidacją konfliktów.
5. Kalendarz dostępności, widok własnych rezerwacji i administracyjny widok wszystkich.
6. Zarządzanie kontami przez administratora (minimum utworzenie i dezaktywacja).

## Poza MVP
Samodzielna rejestracja, płatności, SMS/e-mail, integracje z zewnętrznymi kalendarzami, cykliczne rezerwacje, wielooddziałowość, powiadomienia push, zaawansowane raporty, SSR i aplikacja mobilna.

## Użytkownicy i role
- **Gość:** ekran logowania; bez dostępu do danych sal i rezerwacji (decyzja specyfikacyjna upraszczająca ochronę danych).
- **Użytkownik:** widzi sale i dostępność, tworzy rezerwacje na swoje konto, czyta i zmienia własne rezerwacje.
- **Administrator:** posiada prawa użytkownika, zarządza salami i kontami oraz wszystkimi rezerwacjami.

## Zasady czasowe
- Zakładamy jedną lokalizację i strefę **Europe/Warsaw**; 08:00–20:00 to godziny lokalne.
- W bazie przechowujemy chwile w UTC; wejście i prezentację czasu przeliczamy względem Europe/Warsaw.
- Przedziały są półotwarte: `[początek, koniec)`; zetknięcie końca jednej i początku drugiej rezerwacji jest dozwolone.
- Dopuszczone są rezerwacje wyłącznie w obrębie jednego lokalnego dnia kalendarzowego.
- Dla MVP rezerwacje mają dokładność jednej minuty; sekundy i milisekundy nie są akceptowane w danych wejściowych.
- Godzina 08:00 jest dopuszczalnym początkiem, 20:00 dopuszczalnym końcem; zakończenie o 20:00 nie umożliwia rozpoczęcia nowej rezerwacji o 20:00.

## Kluczowe decyzje jakościowe
- API jest źródłem prawdy; walidacje po stronie Angulara są pomocnicze.
- Zapobieganie podwójnej rezerwacji musi działać również przy współbieżnych żądaniach na poziomie transakcji/bazy.
- Testy mają odwoływać się do identyfikatorów wymagań i reguł biznesowych.
- Komentarze i dokumentacja po polsku, nazwy identyfikatorów w kodzie po angielsku.
- Nie uznajemy testów za wykonane bez rzeczywistego uruchomienia i wyników.

## Dokumenty WP-00
- `PROJECT_CONTEXT.md`: zakres i ustalenia.
- `REQUIREMENTS.md`: wymagania funkcjonalne i niefunkcjonalne.
- `BUSINESS_RULES.md`: jednoznaczne reguły biznesowe.
- `TEST_STRATEGY.md`: plan testowania i mapowanie ryzyk.

## Status decyzji
Zatwierdzone są wyłącznie pięć decyzji użytkownika. Pozostałe doprecyzowania w dokumentach WP-00 to **proponowane założenia analityczne v1.0**, które należy zweryfikować przed implementacją WP-01/WP-02. Zmiany wpisujemy do rejestru decyzji i aktualizujemy powiązane wymagania oraz testy.

# Roomify — strategia testowania (WP-00)

**Wersja:** 1.0 (17.10.2026). Dokument opisuje plan, nie raport wykonania. Na etapie WP-00 **nie uruchamiano testów**.

## Cele testowania
1. Potwierdzić zgodność z `FR-*`, `NFR-*` i `BR-*`.
2. Wykazać poprawność brzegowych przedziałów czasu i autoryzacji.
3. Zabezpieczyć integralność danych przy równoczesnych żądaniach.
4. Sprawdzić jakość samych testów narzędziem mutacyjnym.
5. Zweryfikować pełne przepływy użytkownika i czytelność UI.

## Piramida testów
| Rodzaj | Narzędzia (propozycja) | Obszar | Planowana skala |
|---|---|---|---|
| Jednostkowe backend | xUnit + mocki | Walidatory, konflikty, uprawnienia, serwisy | 25–40 przypadków |
| Jednostkowe frontend | Angular TestBed + Vitest, jeśli obsługiwane w ustalonej wersji | Formularze, walidatory, komponenty, serwisy HTTP | 15–25 przypadków |
| Integracyjne | WebApplicationFactory + Testcontainers + prawdziwy PostgreSQL | Endpointy, EF Core, tożsamość, transakcje | 15–25 przypadków |
| Mutacyjne | Stryker.NET; StrykerJS opcjonalnie | Reguły domenowe, operatory porównania i warunki | Raport mutation score |
| End-to-end | Playwright | Pełne procesy przez przeglądarkę, API i bazę | 8–12 scenariuszy |
| Manualne | Scenariusze z tabelą kroków | UX, dostępność, responsywność, czytelność | 10–15 scenariuszy |

Wszystkie liczby są **celami orientacyjnymi**, a nie wymaganiami prowadzącego. Liczymy istotne przypadki, nie jedynie liczbę metod testowych.

## Techniki projektowania testów
- Klasy równoważności (poprawne i błędne przedziały).
- Analiza wartości granicznych (08:00, 20:00, 8 h, dokładne stykanie się rezerwacji).
- Tablice decyzyjne (rola × właściciel × status × czas).
- Testowanie przejść stanów (`Active -> Cancelled`; zakaz niepoprawnych przejść).
- Testy parametryzowane i negatywne.
- Współbieżne żądania do rzeczywistej bazy.
- Testy mutacyjne operatorów `<` / `<=`, `>` / `>=`, `&&` / `||` i warunków statusu.

## Macierz krytyczna
| Wymaganie/reguła | Unit | Integration | Mutation | E2E | Manual |
|---|:---:|:---:|:---:|:---:|:---:|
| BR-001, BR-002 | ✓ | ✓ | ✓ | ✓ | ✓ |
| BR-003, BR-004 | ✓ | ✓ | ✓ | ✓ | ✓ |
| BR-005, BR-011, BR-021 | ✓ | ✓ | Wybrane | ✓ | ✓ |
| BR-006, BR-013 | ✓ | ✓ | Wybrane | ✓ | ✓ |
| BR-007, BR-008, BR-014, BR-016 | ✓ | ✓ | ✓ | ✓ | ✓ |
| BR-009, BR-017, BR-018 | ✓ | ✓ | ✓ | ✓ | ✓ |
| BR-010, BR-026 |  | ✓ |  |  |  |
| BR-012 | ✓ | ✓ | ✓ | ✓ |  |
| FR-CAL-001/002 | ✓ frontend | ✓ |  | ✓ | ✓ |
| FR-ROOM-003/004/005/006 | ✓ | ✓ | Wybrane | ✓ | ✓ |

## Przykładowe identyfikatory testów
| ID | Poziom | Scenariusz | Odniesienie | Oczekiwany wynik |
|---|---|---|---|---|
| TC-U-001 | Jednostkowy | Koniec równy początkowi | BR-001 | Odrzucenie |
| TC-U-002 | Jednostkowy | Sąsiednie rezerwacje | BR-004 | Brak konfliktu |
| TC-U-003 | Jednostkowy | Limit dokładnie 8 godzin | BR-007 | Akceptacja |
| TC-U-004 | Jednostkowy | Limit 8 h 1 min | BR-007 | Odrzucenie |
| TC-U-005 | Jednostkowy | Anulowana rezerwacja | BR-009 | Nie blokuje |
| TC-I-001 | Integracyjny | Utworzenie poprawnej rezerwacji | FR-RES-001 | 201 i rekord w DB |
| TC-I-002 | Integracyjny | Konflikt istniejącego terminu | BR-003 | 409 i brak nowego rekordu |
| TC-I-003 | Integracyjny | Dwa równoczesne żądania | BR-010 | Jeden sukces, drugi konflikt |
| TC-I-004 | Integracyjny | Użytkownik edytuje cudzą rezerwację | BR-005 | 403 (lub ujednolicone 404, jeśli ukrywamy istnienie zasobu) |
| TC-I-005 | Integracyjny | Wygaszony/dezaktywowany użytkownik | BR-021 | Brak dostępu |
| TC-F-001 | Frontend | Nieprawidłowy zakres czasu | FR-RES-001 | Komunikat walidacji i brak żądania |
| TC-F-002 | Frontend | Konflikt 409 API | FR-RES-007 | Czytelny komunikat |
| TC-E-001 | E2E | Login → lista sal → rezerwacja → kalendarz | UC-001/003/005/008 | Zapis i aktualizacja widoku |
| TC-E-002 | E2E | Dwóch użytkowników próbuje rezerwować ten sam termin | BR-003 | Drugi widzi konflikt |
| TC-E-003 | E2E | Administrator anuluje cudzą rezerwację | BR-005 | Termin zwolniony |
| TC-M-001 | Manualny | Obsługa formularza klawiaturą | NFR-012 | Wszystkie elementy osiągalne |
| TC-M-002 | Manualny | Widok mobilny | FR-UI-001 | Brak blokujących problemów |

## Testy mutacyjne
- Podstawowy zakres: funkcje walidujące rezerwacje, wykrywanie konfliktów i autoryzacja domenowa.
- Wynik raportu: mutanty zabite, przeżyłe, nieobjęte, błędy wykonania; opis wybranych naprawionych luk.
- **Mutation score** liczymy według definicji narzędzia i zaznaczamy wykluczenia.
- Cel roboczy: co najmniej 80% dla wydzielonej krytycznej logiki biznesowej, nie jako bezwzględna granica zaliczenia.
- Demonstracja: zamiana `<` na `<=` w sprawdzaniu nakładających się przedziałów, wykrywana testem stykających się terminów.

## Testy integracyjne: zasady
- Używamy prawdziwego silnika PostgreSQL, a nie EF Core InMemory jako zamiennika.
- Testy są izolowane: każdy test otrzymuje przewidywalne dane i nie zależy od kolejności innych.
- Weryfikujemy zarówno status HTTP, jak i rzeczywisty stan bazy.
- Test współbieżności musi wymusić jednoczesne żądania i sprawdzić niezmiennik: najwyżej jedna aktywna rezerwacja na kolidujący przedział jednej sali.
- Pomiędzy testami odtwarzamy stan deterministycznie (transakcje, czyszczenie danych lub osobna baza).

## E2E: zasady
- Korzystamy z odrębnej bazy testowej i kont testowych.
- Przypadki są niezależne, mają przewidywalny seed i nie polegają na oczekiwaniu stałą liczbę sekund.
- Po błędzie zapisujemy screen, trace i czytelny wynik uruchomienia.

## Manualne: wzór protokołu
**ID, wymóg, środowisko (przeglądarka/wersja/rozmiar ekranu), warunki wstępne, kroki, oczekiwany wynik, rzeczywisty wynik, status PASS/FAIL/BLOCKED, data, uwagi/dowód.**

## Cele jakościowe (robocze)
| Metryka | Cel |
|---|---|
| Odsetek przechodzących testów wymaganych do wydania | 100% |
| Coverage instrukcji krytycznej logiki biznesowej | >= 85% |
| Coverage gałęzi krytycznej logiki biznesowej | >= 80% |
| Mutation score krytycznej logiki biznesowej | >= 80% |
| Scenariusze E2E | 8–12 |
| Scenariusze manualne | 10–15 |

Te wskaźniki mierzymy dopiero po implementacji. Sam procent pokrycia nie jest dowodem jakości bez weryfikacji poprawności asercji.

## Warunki zamknięcia testowania
1. Nie ma otwartych błędów krytycznych dotyczących danych, uprawnień ani kolizji terminów.
2. Wszystkie wymagane testy automatyczne przechodzą, co potwierdza raport z datą i środowiskiem.
3. Przeanalizowano mutanty, które przeżyły, i udokumentowano istotne luki.
4. Wszystkie krytyczne scenariusze manualne posiadają wynik wykonania.
5. README zawiera instrukcję uruchomienia testów i lokalizację raportów.

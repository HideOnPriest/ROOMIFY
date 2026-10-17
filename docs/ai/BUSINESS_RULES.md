# Roomify — reguły biznesowe (WP-00)

**Wersja:** 1.0. Reguły zatwierdzone przez użytkownika oznaczono **[Z]**, a doprecyzowania analityczne do weryfikacji **[P]**.

| ID | Reguła | Status | Priorytet |
|---|---|---|---|
| BR-001 | `EndAtUtc > StartAtUtc` | [P] | Krytyczny |
| BR-002 | Początek nowej rezerwacji musi być późniejszy niż aktualna chwila; czas odczytywany z serwera | [P] | Wysoki |
| BR-003 | Aktywne rezerwacje tej samej sali nie mogą mieć części wspólnej czasowej | [P] | Krytyczny |
| BR-004 | Sąsiadujące przedziały `[start, end)` nie kolidują, np. 10–11 i 11–12 | [P] | Wysoki |
| BR-005 | Tylko właściciel lub administrator może odczytać, zmienić albo anulować daną rezerwację | [Z: uprawnienia administratora; P: właściciel] | Krytyczny |
| BR-006 | Nie można tworzyć nowych rezerwacji sali nieaktywnej | [P] | Wysoki |
| BR-007 | `EndAt - StartAt <= 8 godzin` | [Z] | Wysoki |
| BR-008 | Rezerwacja musi mieścić się w przedziale 08:00–20:00 czasu Europe/Warsaw | [Z: godziny; P: strefa] | Wysoki |
| BR-009 | Rezerwacja anulowana nie blokuje terminu | [P] | Wysoki |
| BR-010 | Dwa współbieżne żądania kolidujących rezerwacji nie mogą obydwa zakończyć się powodzeniem | [P] | Krytyczny |
| BR-011 | Tylko administrator tworzy konta; klient nie może samodzielnie nadawać sobie roli | [Z: tworzenie kont; P: model ról] | Krytyczny |
| BR-012 | Edycja sali lub terminu aktywnej rezerwacji ponownie wymusza odpowiednie walidacje; własnego ID nie liczymy jako konfliktu | [P] | Wysoki |
| BR-013 | Nowe rezerwacje tworzy się tylko dla istniejących aktywnych sal i aktywnych kont | [P] | Wysoki |
| BR-014 | Dopuszcza się rezerwacje wyłącznie w obrębie jednego lokalnego dnia kalendarzowego | [P] | Średni |
| BR-015 | Początek/koniec rezerwacji mają dokładność jednej minuty; sekundy i milisekundy są zerowe | [P] | Średni |
| BR-016 | Dozwolony początek: od 08:00 włącznie, przed 20:00; dozwolony koniec: po 08:00, do 20:00 włącznie | [P] | Wysoki |
| BR-017 | Użytkownik może modyfikować/anulować wyłącznie rezerwacje aktywne, które jeszcze się nie rozpoczęły | [P] | Wysoki |
| BR-018 | Anulowanie jest zmianą statusu, nie fizycznym usunięciem rekordu | [P] | Średni |
| BR-019 | Archiwizacja sali nie usuwa historii; nieaktywnej sali nie wolno ponownie zarezerwować | [P] | Wysoki |
| BR-020 | E-maile kont użytkowników są unikalne bez rozróżnienia wielkości liter i normalizowane przy zapisie | [P] | Średni |
| BR-021 | Konto nieaktywne nie może logować się ani wykonywać chronionych operacji | [P] | Krytyczny |
| BR-022 | Użytkownik tworzy rezerwacje tylko na swoje konto; administrator może zarządzać rezerwacjami innych, ale w MVP nie tworzy w ich imieniu | [P] | Wysoki |
| BR-023 | Właściciel rezerwacji nie ulega zmianie podczas edycji | [P] | Średni |
| BR-024 | Tytuł rezerwacji jest wymagany (1–120 znaków po trimowaniu) | [P] | Średni |
| BR-025 | Sala ma nazwę (1–100 znaków), lokalizację (1–150 znaków), dodatnią pojemność; nazwa jest unikalna bez rozróżnienia wielkości liter | [P] | Średni |
| BR-026 | Wynik wyświetlenia dostępności jest informacyjny; przed samym zapisem trzeba ponownie zweryfikować konflikt | [P] | Krytyczny |
| BR-027 | Dwa identyczne anulowania nie mogą stworzyć dwóch sprzecznych zmian stanu; drugi request daje przewidywalną odpowiedź | [P] | Średni |
| BR-028 | Dane sali i kalendarza nie są udostępniane niezalogowanym użytkownikom | [P] | Średni |

## Algorytm wykrywania konfliktu (specyfikacja, nie kod)
Dla dwóch **aktywnych** rezerwacji A i B tej samej sali konflikt istnieje wtedy i tylko wtedy, gdy:

`A.StartAtUtc < B.EndAtUtc` **oraz** `A.EndAtUtc > B.StartAtUtc`.

Podczas edycji nie porównujemy modyfikowanego rekordu z samym sobą. Anulowane rezerwacje pomijamy. Gwarancja bezpieczeństwa współbieżności musi być wymuszona także w bazie danych lub transakcji, nie tylko przez wcześniejsze sprawdzenie serwisowe.

## Przypadki graniczne do obowiązkowego sprawdzenia
| ID | Dane | Oczekiwany rezultat | Powiązanie |
|---|---|---|---|
| EDGE-001 | 10:00–11:00 oraz 11:00–12:00 | Dopuszczone | BR-004 |
| EDGE-002 | 10:00–12:00 oraz 11:59–13:00 | Konflikt | BR-003 |
| EDGE-003 | 10:00–12:00 oraz 09:00–13:00 | Konflikt | BR-003 |
| EDGE-004 | 10:00–12:00 oraz 10:00–12:00 | Konflikt | BR-003 |
| EDGE-005 | 10:00–10:00 | Odrzucenie | BR-001 |
| EDGE-006 | 08:00–16:00 | Dopuszczone przy braku konfliktu | BR-007/008 |
| EDGE-007 | 08:00–16:01 | Odrzucenie (limit 8 h) | BR-007 |
| EDGE-008 | 19:30–20:00 | Dopuszczone przy braku konfliktu | BR-016 |
| EDGE-009 | 20:00–20:30 | Odrzucenie | BR-008/016 |
| EDGE-010 | 10:00–11:00 następnego dnia | Odrzucenie (różne dni lokalne) | BR-014 |
| EDGE-011 | Anulowana 10:00–11:00, nowa 10:00–11:00 | Dopuszczone | BR-009 |
| EDGE-012 | Jednoczesne utworzenie dwóch identycznych rezerwacji | Dokładnie jedna przyjęta | BR-010 |
| EDGE-013 | Edycja bez zmiany godzin tej samej rezerwacji | Nie koliduje sama ze sobą | BR-012 |
| EDGE-014 | Próba edycji rezerwacji rozpoczętej | Odrzucenie | BR-017 |
| EDGE-015 | Rezerwacja o sekundach innych niż 00 | Odrzucenie lub jawna normalizacja, do decyzji w API; preferowane odrzucenie | BR-015 |

## Uwagi o czasie i DST
Godziny 08:00–20:00 czasu Europe/Warsaw nie przecinają nocy zmiany czasu, ale implementacja powinna przeliczać konkretną **datę i godzinę lokalną** na UTC przy użyciu właściwych zasad strefy czasowej, a nie stałego przesunięcia +1/+2. Zakaz rezerwacji między dniami eliminuje niejednoznaczność nocnych przedziałów.

## Konflikt zmian i odpowiedzi API
- Błędy składni i wymaganych pól: HTTP 400.
- Nieautoryzowany: 401; brak uprawnień: 403; nieistniejący identyfikator: 404.
- Nakładające się terminy i konflikty stanu: 409.
- Pozostałe naruszenia reguł domenowych: rekomendowane 400 z kodem błędu domenowego; jeden kontrakt stosowany konsekwentnie.

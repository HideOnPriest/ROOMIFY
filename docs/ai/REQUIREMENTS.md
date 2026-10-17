# Roomify — wymagania systemowe (WP-00)

**Wersja:** 1.0 (17.10.2026). Identyfikatory są stałe; zmiany wersjonujemy.

## Aktorzy
**G** – gość; **U** – zalogowany użytkownik; **A** – administrator.

## Funkcjonalne
| ID | Moduł | Wymaganie | Aktor | Priorytet | Kryterium akceptacji |
|---|---|---|---|---|---|
| FR-AUTH-001 | Konta | Logowanie e-mailem i hasłem | G | MUST | Poprawne dane umożliwiają dostęp, błędne nie ujawniają istnienia konta |
| FR-AUTH-002 | Konta | Wylogowanie | U/A | MUST | Sesja/token przestaje pozwalać na operacje chronione zgodnie z wybraną architekturą sesji |
| FR-AUTH-003 | Konta | Odczyt profilu bieżącego użytkownika | U/A | MUST | Zwracane są tylko dane bieżącego użytkownika |
| FR-AUTH-004 | Konta | Utworzenie konta przez administratora | A | MUST | Konto ma unikalny e-mail; nie ma publicznej rejestracji |
| FR-AUTH-005 | Konta | Dezaktywacja konta | A | MUST | Dezaktywowany użytkownik traci dostęp, dane historyczne pozostają |
| FR-AUTH-006 | Konta | Lista użytkowników | A | MUST | Użytkownik bez roli administratora nie uzyska danych |
| FR-ROOM-001 | Sale | Lista sal z podstawowymi danymi | U/A | MUST | Lista uwzględnia wyłącznie aktywne sale dla U |
| FR-ROOM-002 | Sale | Szczegóły sali | U/A | MUST | Dane obejmują nazwę, opis, pojemność, lokalizację i wyposażenie |
| FR-ROOM-003 | Sale | Filtrowanie i wyszukiwanie sal | U/A | MUST | Filtr według nazwy i minimalnej pojemności daje oczekiwane wyniki |
| FR-ROOM-004 | Sale | Tworzenie sali | A | MUST | Dane zwalidowane; nowa sala jest aktywna |
| FR-ROOM-005 | Sale | Edycja sali | A | MUST | Zmiana nazwy/opisu/lokalizacji/pojemności/wyposażenia nie usuwa rezerwacji |
| FR-ROOM-006 | Sale | Archiwizacja sali | A | MUST | Sala nie przyjmuje nowych rezerwacji; historyczne dane pozostają |
| FR-RES-001 | Rezerwacje | Tworzenie rezerwacji na własne konto | U/A | MUST | Sukces dla wolnego dozwolonego terminu; weryfikacja po stronie API |
| FR-RES-002 | Rezerwacje | Odczyt szczegółów rezerwacji | U/A | MUST | U widzi tylko własne; A wszystkie |
| FR-RES-003 | Rezerwacje | Lista własnych rezerwacji | U/A | MUST | Właściciel może przeglądać bieżące, przyszłe i anulowane |
| FR-RES-004 | Rezerwacje | Edycja przyszłej aktywnej rezerwacji | U-właściciel/A | MUST | Zmiana terminu/sali/tytułu ponownie waliduje wszystkie reguły |
| FR-RES-005 | Rezerwacje | Anulowanie przyszłej aktywnej rezerwacji | U-właściciel/A | MUST | Rezerwacja otrzymuje status Cancelled, termin się zwalnia |
| FR-RES-006 | Rezerwacje | Lista wszystkich rezerwacji | A | MUST | A może filtrować po sali, użytkowniku, statusie i przedziale dat |
| FR-RES-007 | Rezerwacje | Czytelne komunikaty o błędach | U/A | MUST | Walidacja, konflikt, brak uprawnień i brak rekordu są rozróżnialne |
| FR-CAL-001 | Dostępność | Kalendarz rezerwacji sali (dzień/tydzień) | U/A | MUST | Zajęte przedziały odpowiadają aktywnym rezerwacjom |
| FR-CAL-002 | Dostępność | Filtrowanie kalendarza po sali i dacie | U/A | MUST | Zmiana parametrów odświeża stan z API |
| FR-CAL-003 | Dostępność | Formularz rezerwacji z wybranego terminu | U/A | SHOULD | Wybranie wolnego slotu otwiera formularz z wstępnymi danymi |
| FR-UI-001 | Interfejs | Responsywność podstawowych widoków | U/A | MUST | Widoki są używalne na desktopie i ekranie mobilnym |
| FR-UI-002 | Interfejs | Obsługa loading/empty/error | U/A | MUST | Przeładowanie, brak danych i błąd API są wyraźnie pokazane |
| FR-UI-003 | Interfejs | Potwierdzenie anulowania | U/A | SHOULD | Potwierdzenie poprzedza wykonanie operacji |
| FR-ADMIN-001 | Administracja | Statystyka obłożenia sal | A | COULD | Dane liczone z rezerwacji aktywnych we wskazanym przedziale |

**MUST** – niezbędne do MVP; **SHOULD** – wskazane; **COULD** – opcjonalne.

## Niefunkcjonalne
| ID | Wymaganie | Sposób weryfikacji |
|---|---|---|
| NFR-001 | Aplikacja zawiera frontend i backend komunikujące się przez HTTP API | Test E2E i dokumentacja architektury |
| NFR-002 | Brak możliwości wykonania operacji bez wymaganych uprawnień | Testy integracyjne 401/403 oraz E2E |
| NFR-003 | Dane i hasła są przechowywane bezpiecznie (hasła wyłącznie w postaci haszowanej) | Przegląd implementacji i testy |
| NFR-004 | Każda reguła krytyczna walidowana w backendzie | Unit + integration |
| NFR-005 | Odporność na równoczesne próby rezerwacji tego samego przedziału | Testy integracyjne z prawdziwym PostgreSQL |
| NFR-006 | Jednolity format błędów API, z rozróżnieniem typów błędów | Testy kontraktowe/integracyjne |
| NFR-007 | Logi nie zawierają haseł ani danych uwierzytelniających | Przegląd logowania |
| NFR-008 | Automatyczne testy jednostkowe, integracyjne, mutacyjne i E2E | Raporty z uruchomień |
| NFR-009 | Udokumentowane scenariusze manualne | Protokół wykonania z rzeczywistymi wynikami |
| NFR-010 | Odtwarzalne lokalne uruchomienie z README | Przejście instrukcji na czystym środowisku |
| NFR-011 | Kod i testy opisane czytelnymi komentarzami po polsku | Przegląd dokumentacji i komentarzy |
| NFR-012 | Dostępność podstawowych kontrolek z klawiatury, czytelne etykiety i komunikaty | Testy manualne i wybrane E2E |

## Przypadki użycia (skrót)
- **UC-001 Logowanie:** G podaje dane; system uwierzytelnia lub zwraca neutralny błąd.
- **UC-002 Administracja kontami:** A tworzy/dezaktywuje konto; system egzekwuje unikalność e-mail.
- **UC-003 Przegląd sal:** U/A wyszukuje salę i sprawdza jej parametry.
- **UC-004 Administracja salami:** A tworzy/edytuje/archiwizuje salę.
- **UC-005 Utworzenie rezerwacji:** U/A wybiera aktywną salę, tytuł i przedział; API weryfikuje warunki i atomowo zapisuje rezerwację.
- **UC-006 Edycja rezerwacji:** właściciel/A zmienia przyszłą aktywną rezerwację; konflikt sprawdzany z wyłączeniem jej własnego identyfikatora.
- **UC-007 Anulowanie rezerwacji:** właściciel/A potwierdza anulowanie; termin staje się wolny.
- **UC-008 Sprawdzenie dostępności:** U/A filtruje kalendarz; UI wyświetla aktywne zajętości.
- **UC-009 Administracyjny przegląd:** A filtruje wszystkie rezerwacje.

## Przepływ UC-005: utworzenie rezerwacji
**Warunki wstępne:** zalogowany aktywny użytkownik, aktywna sala.
1. Użytkownik otwiera formularz, wybiera salę, datę, początek i koniec, wprowadza tytuł.
2. Frontend wstępnie waliduje pola i wysyła żądanie do API.
3. Backend sprawdza tożsamość, dane wejściowe, czas, salę i konflikt z aktywnymi rezerwacjami.
4. Baza gwarantuje brak konfliktu również przy współbieżnych żądaniach.
5. System zapisuje rezerwację i zwraca `201 Created` z jej identyfikatorem.
6. Interfejs odświeża kalendarz i potwierdza utworzenie.
**Alternatywy:** niepoprawny format (`400`), brak sesji (`401`), brak uprawnień (`403`), brak sali (`404`), konflikt (`409`), czas niedozwolony (`422` lub `400`, do jednoznacznego ustalenia w kontrakcie API).

## Model pojęciowy
- `User`: Id, Email, PasswordHash, DisplayName, Role, IsActive, CreatedAt.
- `Room`: Id, Name, Description, Location, Capacity, Equipment, IsActive, CreatedAt.
- `Reservation`: Id, RoomId, UserId, Title, StartAtUtc, EndAtUtc, Status, CreatedAtUtc, UpdatedAtUtc, CancelledAtUtc.
- Opcjonalnie `ReservationAuditLog` do śledzenia zmian. Usuwanie fizyczne historycznych rezerwacji nie jest częścią MVP.

## Uprawnienia — macierz
| Operacja | G | U | A |
|---|:---:|:---:|:---:|
| Logowanie | ✓ | ✓ | ✓ |
| Lista/szczegóły aktywnych sal |  | ✓ | ✓ |
| Zarządzanie salami |  |  | ✓ |
| Tworzenie własnej rezerwacji |  | ✓ | ✓ |
| Odczyt/edycja/anulowanie własnej rezerwacji |  | ✓ | ✓ |
| Odczyt/edycja/anulowanie cudzej rezerwacji |  |  | ✓ |
| Zarządzanie użytkownikami |  |  | ✓ |
| Lista wszystkich rezerwacji |  |  | ✓ |

## Decyzje otwarte
1. Autoryzacja: cookies czy JWT; zarządzanie hasłami (reset w MVP czy tylko konto z hasłem początkowym).
2. Dokładny kontrakt API i spójność kodów `400`/`422`.
3. Edycja kont (poza utworzeniem/dezaktywacją), zarządzanie rolami (na MVP bez samodzielnej zmiany roli).
4. Statystyki w MVP lub jako dodatek.
5. Sprzężenie kalendarza ze zmianami na żywo: w MVP odświeżenie po operacji, bez WebSocket.

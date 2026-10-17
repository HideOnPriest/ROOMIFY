import { TestBed } from '@angular/core/testing';
import { Home } from './home';

describe('Strona startowa', () => {
  it('przedstawia projekt, status realizacji i planowane rodzaje testów', async () => {
    // Arrange — komponent nie potrzebuje serwisów HTTP ani zewnętrznych danych.
    await TestBed.configureTestingModule({ imports: [Home] }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    // Act — oczekujemy na zakończenie renderowania komponentu.
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    // Assert — weryfikujemy treści informacyjne i wszystkie pięć rodzajów testów.
    expect(element.querySelector('h1')?.textContent).toContain('Roomify');
    expect(element.textContent).toContain('W trakcie realizacji');
    expect(element.textContent).toContain('system rezerwacji sal');
    expect(element.textContent).toContain('kompleksowo testować');
    expect(Array.from(element.querySelectorAll('li'), (item) => item.textContent)).toEqual([
      'Jednostkowe',
      'Integracyjne',
      'Mutacyjne',
      'E2E',
      'Manualne',
    ]);
  });
});

import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotFound } from './not-found';

describe('Strona 404', () => {
  it('wyświetla komunikat i dostępny link powrotu na /home', async () => {
    // Arrange — lokalny Router obsługuje RouterLink bez prawdziwej sieci.
    await TestBed.configureTestingModule({
      imports: [NotFound],
      providers: [provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(NotFound);
    // Act — renderujemy komponent tak jak w przeglądarce.
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    // Assert — komunikat i cel odnośnika muszą być zrozumiałe dla użytkownika.
    expect(element.querySelector('h1')?.textContent).toContain('Tej strony nie ma');
    expect(element.querySelector('a')?.getAttribute('href')).toBe('/home');
    expect(element.querySelector('a')?.textContent).toContain('Wróć do strony głównej');
  });
});

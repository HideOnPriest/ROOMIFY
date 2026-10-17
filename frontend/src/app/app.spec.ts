import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('Główny komponent i routing', () => {
  beforeEach(async () => {
    // Arrange — TestBed tworzy izolowane środowisko Angulara, bez backendu.
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes)],
    }).compileComponents();
  });

  it('wyświetla semantyczny layout i link nawigacji głównej', async () => {
    const fixture = TestBed.createComponent(App);
    // Act — Angular renderuje szablon i wiązania RouterLink.
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    // Assert — sprawdzamy zachowanie widoczne dla użytkownika.
    expect(element.querySelector('header nav a')?.getAttribute('href')).toBe('/home');
    expect(element.querySelector('main router-outlet')).not.toBeNull();
    expect(element.querySelector('footer')?.textContent).toContain('Projekt akademicki');
  });

  it('przekierowuje pustą ścieżkę na stronę główną', async () => {
    const fixture = TestBed.createComponent(App);
    const router = TestBed.inject(Router);
    await fixture.whenStable();
    await router.navigateByUrl('/');
    await fixture.whenStable();
    expect(router.url).toBe('/home');
    expect((fixture.nativeElement as HTMLElement).querySelector('h1')?.textContent).toContain(
      'Roomify',
    );
  });

  it('obsługuje nieznany adres i powrót ze strony 404', async () => {
    const fixture = TestBed.createComponent(App);
    const router = TestBed.inject(Router);
    await fixture.whenStable();
    await router.navigateByUrl('/nieistniejaca-strona');
    await fixture.whenStable();
    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>(
      'app-not-found a',
    );
    expect(link?.textContent).toContain('Wróć do strony głównej');
    link?.click();
    await fixture.whenStable();
    expect(router.url).toBe('/home');
  });
});

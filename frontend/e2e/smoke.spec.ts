import { expect, test } from '@playwright/test';

test('strona główna przedstawia Roomify i status projektu', async ({ page }) => {
  // ARRANGE — przeglądarka korzysta z lokalnego Angulara uruchomionego przez webServer.
  // ACT — Router powinien przekierować pustą ścieżkę do /home.
  await page.goto('/');

  // ASSERT — lokatory roli sprawdzają treść widoczną także dla użytkownika.
  // Playwright ponawia asercje do timeoutu; nie potrzebujemy sztywnych opóźnień.
  await expect(page).toHaveURL(/\/home$/);
  await expect(page.getByRole('heading', { name: /Roomify/ })).toBeVisible();
  await expect(page.getByText('W trakcie realizacji', { exact: true })).toBeVisible();
});

test('nieznana trasa pokazuje 404 i pozwala wrócić do strony głównej', async ({ page }) => {
  // ARRANGE — adres nie odpowiada żadnej z istniejących funkcjonalności.
  // ACT — otwieramy bezpośredni link, tak jak po wklejeniu go do przeglądarki.
  await page.goto('/nieistniejaca-strona');

  // ASSERT — sprawdzamy komunikat, dostępny link i rezultat jego użycia.
  await expect(page.getByRole('heading', { name: /Tej strony nie ma/ })).toBeVisible();
  const homeLink = page.getByRole('link', { name: /Wróć do strony głównej/ });
  await expect(homeLink).toHaveAttribute('href', '/home');
  await homeLink.click();
  await expect(page).toHaveURL(/\/home$/);
  await expect(page.getByRole('heading', { name: /Roomify/ })).toBeVisible();
});

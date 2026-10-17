import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  {
    path: 'home',
    title: 'Roomify — strona główna',
    // Import dynamiczny pobiera kod strony dopiero po dopasowaniu trasy.
    loadComponent: () => import('./features/home/home').then((module) => module.Home),
  },
  {
    path: '**',
    title: 'Nie znaleziono strony — Roomify',
    loadComponent: () => import('./features/not-found/not-found').then((module) => module.NotFound),
  },
];

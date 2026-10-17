import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  // Providery udostępniają wspólne zależności przez kontener DI Angulara.
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Przyszłe serwisy użyją względnych adresów /api przez proxy tego samego originu.
    provideHttpClient(),
  ],
};

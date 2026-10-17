# Roomify — frontend

Szkielet Angular 22 w trybie CSR, z komponentami standalone i testami Vitest.

Wymagania i pełne instrukcje: [README repozytorium](../README.md#uruchamianie-frontendu).
Architektura: [FRONTEND.md](../docs/architecture/FRONTEND.md).

```powershell
npm ci
npm start
npm run build
npm run test:ci
```

Polecenia wykonuj w tym katalogu. Serwer developerski: http://localhost:4200.
Widoki /home i 404 działają bez backendu. Proxy przekazuje przyszłe żądania /api
do backendu na porcie 5080 bez usuwania prefiksu.

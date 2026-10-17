FROM node:24.19.0-bookworm-slim AS build
WORKDIR /app
ENV NG_CLI_ANALYTICS=false
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# Końcowy obraz zawiera wyłącznie Nginx i gotowy build CSR, bez Node.js.
FROM nginx:1.30.6-alpine AS runtime
COPY --from=build /app/dist/roomify/browser/ /usr/share/nginx/html/
EXPOSE 80
# Konfigurację proxy Compose montuje tylko do odczytu z infra/nginx/.

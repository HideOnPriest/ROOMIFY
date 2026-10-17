#!/bin/bash
set -euo pipefail

# Bash w obrazie Noble otwiera lokalne TCP bez instalowania curl/wget.
# Timeout całej kontroli ustala Compose, także dla oczekiwania na odpowiedź.
exec 3<>/dev/tcp/127.0.0.1/8080
printf 'GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n' >&3
IFS= read -r status <&3
[[ "$status" == HTTP/*" 200 "* ]] || exit 1

# Dokładna linia Healthy jest poprawna również dla odpowiedzi chunked HTTP/1.1.
while IFS= read -r line <&3 || [[ -n "$line" ]]; do
    if [[ "${line%$'\r'}" == "Healthy" ]]; then
        exit 0
    fi
done
exit 1

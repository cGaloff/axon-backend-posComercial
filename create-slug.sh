#!/usr/bin/env bash
# Alta manual de una empresa. Desde que el endpoint dejó de ser anónimo hay que
# mandar el secreto, el mismo PROVISIONING_SECRET del .env de la API:
#
#   PROVISIONING_SECRET=xxxx ./create-slug.sh
set -euo pipefail

API="${API:-https://api.axonpospymes.com/api}"

if [ -z "${PROVISIONING_SECRET:-}" ]; then
  echo "Falta PROVISIONING_SECRET (está en el .env de la API)." >&2
  exit 1
fi

curl -s -X POST "$API/tenants/register" \
  -H "Content-Type: application/json" \
  -H "X-Provisioning-Secret: $PROVISIONING_SECRET" \
  -d '{
    "businessName": "BananasClub",
    "slug": "bananas-club",
    "ownerEmail": "qa_test001@test.com",
    "ownerPassword": "CAMBIA-esta-clave",
    "plan": "basic"
  }'

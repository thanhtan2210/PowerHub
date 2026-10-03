#!/usr/bin/env sh
# Smoke and negative-access checks against a deployed environment, through its public
# origin only. It needs two existing accounts: an administrator (created with the
# create-admin command) and an ordinary confirmed user (registered through the UI).
#   STAGING_URL=https://host ADMIN_EMAIL=... ADMIN_PASSWORD=... \
#   USER_EMAIL=... USER_PASSWORD=... sh tests/integration/staging-smoke.sh
# Requires: curl, python. Exit code 0 means every expectation held.
set -eu

: "${STAGING_URL:?}" "${ADMIN_EMAIL:?}" "${ADMIN_PASSWORD:?}" "${USER_EMAIL:?}" "${USER_PASSWORD:?}"
BASE="${STAGING_URL%/}"
failures=0

json() { python -c "import sys,json; print(json.load(sys.stdin)$1)"; }

check() { # check <description> <expected> <actual>
  if [ "$2" = "$3" ]; then echo "ok    $1"; else echo "FAIL  $1 (expected $2, got $3)"; failures=$((failures + 1)); fi
}

status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

sign_in() { # sign_in <email> <password> -> access token
  curl -fsS -X POST "$BASE/api/v1/auth/sign-in" -H 'Content-Type: application/json' \
    -d "{\"email\":\"$1\",\"password\":\"$2\"}" | json "['accessToken']"
}

# A path that is not routed to a backend falls through to the frontend, which answers
# with its HTML shell (or 404). Anything else means a backend answered.
reaches_backend() { # reaches_backend <path> -> no | yes
  type=$(curl -s -o /dev/null -w '%{content_type}' "$BASE$1")
  code=$(status "$BASE$1")
  case "$code:$type" in 404:*|*:text/html*) echo no ;; *) echo yes ;; esac
}

echo "--- public surface"
check "frontend is served"                      200 "$(status "$BASE/")"
check "origin is HTTPS"                         https "${BASE%%:*}"
check "plain HTTP is not served as-is"          no "$([ "$(status "http://${BASE#https://}/")" = 200 ] && echo yes || echo no)"
check "JWKS is public"                          200 "$(status "$BASE/.well-known/jwks.json")"
check "JWKS publishes an ES256 key"             ES256 "$(curl -fsS "$BASE/.well-known/jwks.json" | json "['keys'][0]['alg']")"

echo "--- internal surface stays internal"
for path in /health/ready /health/live /health/startup /openapi/v1.json /internal/v1/mqtt/auth; do
  check "$path does not reach a backend"         no "$(reaches_backend "$path")"
done

echo "--- unauthenticated requests"
check "profile without a token"                 401 "$(status "$BASE/api/v1/users/me")"
check "devices without a token"                 401 "$(status "$BASE/api/v1/devices")"
check "devices with a forged token"             401 "$(status -H 'Authorization: Bearer abc.def.ghi' "$BASE/api/v1/devices")"
check "refresh without the CSRF header"         400 "$(status -X POST "$BASE/api/v1/auth/refresh")"

echo "--- authenticated requests"
admin=$(sign_in "$ADMIN_EMAIL" "$ADMIN_PASSWORD")
user=$(sign_in "$USER_EMAIL" "$USER_PASSWORD")
check "administrator reads own profile"         200 "$(status -H "Authorization: Bearer $admin" "$BASE/api/v1/users/me")"
check "administrator lists users"               200 "$(status -H "Authorization: Bearer $admin" "$BASE/api/v1/users")"
check "user reads own profile"                  200 "$(status -H "Authorization: Bearer $user" "$BASE/api/v1/users/me")"
check "user cannot list users"                  403 "$(status -H "Authorization: Bearer $user" "$BASE/api/v1/users")"

echo "--- cross-user isolation"
device=$(curl -fsS -X POST "$BASE/api/v1/devices" -H "Authorization: Bearer $admin" -H 'Content-Type: application/json' \
  -d '{"name":"Smoke check","kind":"meter"}' | json "['device']['id']")
check "owner reads the device"                  200 "$(status -H "Authorization: Bearer $admin" "$BASE/api/v1/devices/$device")"
check "another user gets not found"             404 "$(status -H "Authorization: Bearer $user" "$BASE/api/v1/devices/$device")"
check "another user cannot remove it"           404 "$(status -X DELETE -H "Authorization: Bearer $user" "$BASE/api/v1/devices/$device")"
check "owner removes the device"                204 "$(status -X DELETE -H "Authorization: Bearer $admin" "$BASE/api/v1/devices/$device")"

echo
if [ "$failures" -eq 0 ]; then echo "All staging checks passed."; else echo "$failures check(s) failed."; exit 1; fi

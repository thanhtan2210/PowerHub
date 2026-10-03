#!/usr/bin/env sh
# End-to-end check of the MQTT device boundary against the running Compose stack:
# real accounts, real device credentials, the real broker, and its real authorization
# callbacks into Device Service. Run from the repository root after
#   cd deploy/compose && docker compose up -d --build
# Requires: docker, curl, python3. Exit code 0 means every expectation held.
set -eu

GATEWAY="${GATEWAY:-http://localhost:8080}"
MAILPIT="${MAILPIT:-http://localhost:8025}"
BROKER_CONTAINER="${BROKER_CONTAINER:-powerhub-mqtt-1}"
CACHE_SECONDS="${CACHE_SECONDS:-30}"
PASSWORD="correct horse battery staple"
failures=0

json() { python -c "import sys,json; print(json.load(sys.stdin)$1)"; }

check() { # check <description> <expected> <actual>
  if [ "$2" = "$3" ]; then echo "ok    $1"; else echo "FAIL  $1 (expected $2, got $3)"; failures=$((failures + 1)); fi
}

# Runs a Mosquitto client inside the broker container, so no client is needed on the host.
pub() { # pub <user> <password> <topic> -> ok | denied
  if docker exec "$BROKER_CONTAINER" mosquitto_pub -h localhost -u "$1" -P "$2" -i "pub-$$-$1" -q 1 -t "$3" -m '{}' >/dev/null 2>&1; then echo ok; else echo denied; fi
}
# mosquitto_sub exits 0 even when the broker refuses the subscription, so the decision is
# read from the SUBACK code in the debug output: 0-2 is a granted QoS, 128 is a refusal.
sub() { # sub <user> <password> <filter> -> ok | denied
  out=$(docker exec "$BROKER_CONTAINER" mosquitto_sub -h localhost -u "$1" -P "$2" -i "sub-$$-$1" -q 1 -t "$3" -W 2 -d 2>&1 || true)
  case "$out" in
    *"Subscribed (mid: 1): 128"*) echo denied ;;
    *"Subscribed (mid: 1): "[012]*) echo ok ;;
    *) echo "error: $(echo "$out" | tail -1)" ;;
  esac
}

email="mqtt-$(date +%s)-$$@example.test"
curl -fsS -o /dev/null -X POST "$GATEWAY/api/v1/auth/register" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email\",\"password\":\"$PASSWORD\",\"displayName\":\"MQTT check\"}"
token=""
for _ in 1 2 3 4 5 6 7 8 9 10; do
  token=$(MAILPIT="$MAILPIT" EMAIL="$email" python - <<'PY'
import json, os, re, urllib.request
base = os.environ["MAILPIT"]
for message in json.load(urllib.request.urlopen(base + "/api/v1/messages?limit=50"))["messages"]:
    if message["To"][0]["Address"] == os.environ["EMAIL"]:
        text = json.load(urllib.request.urlopen(base + "/api/v1/message/" + message["ID"]))["Text"]
        print(re.search(r"token=([\w-]+)", text).group(1))
        break
PY
)
  [ -n "$token" ] && break
  sleep 1
done
curl -fsS -o /dev/null -X POST "$GATEWAY/api/v1/auth/email/confirm" -H 'Content-Type: application/json' -d "{\"token\":\"$token\"}"
access=$(curl -fsS -X POST "$GATEWAY/api/v1/auth/sign-in" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$email\",\"password\":\"$PASSWORD\"}" | json "['accessToken']")

register() {
  curl -fsS -X POST "$GATEWAY/api/v1/devices" -H "Authorization: Bearer $access" -H 'Content-Type: application/json' \
    -d "{\"name\":\"$1\",\"kind\":\"meter\"}"
}
a=$(register "Device A"); a_id=$(echo "$a" | json "['device']['id']"); a_secret=$(echo "$a" | json "['credential']")
b=$(register "Device B"); b_id=$(echo "$b" | json "['device']['id']"); b_secret=$(echo "$b" | json "['credential']")
topic() { echo "powerhub/v1/devices/$1/$2"; }

echo "--- own topics"
check "A publishes its telemetry"              ok     "$(pub "$a_id" "$a_secret" "$(topic "$a_id" telemetry)")"
check "A publishes its reported state"         ok     "$(pub "$a_id" "$a_secret" "$(topic "$a_id" state/reported)")"
check "A subscribes to its commands"           ok     "$(sub "$a_id" "$a_secret" "$(topic "$a_id" 'commands/+')")"
check "A subscribes to its desired state"      ok     "$(sub "$a_id" "$a_secret" "$(topic "$a_id" state/desired)")"

echo "--- cross-device isolation"
check "A subscribes to B's commands"           denied "$(sub "$a_id" "$a_secret" "$(topic "$b_id" 'commands/+')")"
check "A subscribes to every device"           denied "$(sub "$a_id" "$a_secret" 'powerhub/v1/devices/+/telemetry')"
check "A subscribes to everything"             denied "$(sub "$a_id" "$a_secret" '#')"
check "A subscribes to its own telemetry"      denied "$(sub "$a_id" "$a_secret" "$(topic "$a_id" telemetry)")"

echo "--- authentication"
check "wrong password"                         denied "$(pub "$a_id" "wrong-password" "$(topic "$a_id" telemetry)")"
check "A's id with B's credential"             denied "$(pub "$a_id" "$b_secret" "$(topic "$a_id" telemetry)")"
check "unknown device id"                      denied "$(pub "00000000-0000-7000-8000-000000000000" "$a_secret" "$(topic "$a_id" telemetry)")"
anonymous=$(docker exec "$BROKER_CONTAINER" mosquitto_pub -h localhost -t "$(topic "$a_id" telemetry)" -m '{}' >/dev/null 2>&1 && echo ok || echo denied)
check "anonymous connection"                   denied "$anonymous"

# MQTT gives a QoS 1 publisher no error for an unauthorised topic: the broker acknowledges
# and drops the message. Prove the drop by listening as B while A publishes to B's topic.
echo "--- unauthorised publish is dropped"
received=$(docker exec "$BROKER_CONTAINER" sh -c "
  mosquitto_sub -h localhost -u '$b_id' -P '$b_secret' -i 'listen-$$' -t '$(topic "$b_id" 'commands/+')' -W 4 -C 1 2>/dev/null &
  sleep 1
  mosquitto_pub -h localhost -u '$a_id' -P '$a_secret' -i 'intruder-$$' -q 1 -t '$(topic "$b_id" commands/forged)' -m 'forged-by-a' 2>/dev/null
  wait" || true)
check "A's message to B's command topic is not delivered" "" "$received"

echo "--- rotation"
rotated=$(curl -fsS -X POST "$GATEWAY/api/v1/devices/$a_id/credentials" -H "Authorization: Bearer $access" | json "['credential']")
check "new credential works immediately"       ok     "$(pub "$a_id" "$rotated" "$(topic "$a_id" telemetry)")"
echo "      waiting ${CACHE_SECONDS}s for the broker's decision cache to expire"
sleep "$((CACHE_SECONDS + 3))"
check "old credential is refused after rotation" denied "$(pub "$a_id" "$a_secret" "$(topic "$a_id" telemetry)")"

echo "--- internal endpoints stay internal"
status=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$GATEWAY/internal/v1/mqtt/auth" -H 'Content-Type: application/json' \
  -d "{\"username\":\"$a_id\",\"password\":\"$rotated\"}")
check "gateway does not route /internal to Device Service" 405 "$status"

echo
if [ "$failures" -eq 0 ]; then echo "All MQTT boundary checks passed."; else echo "$failures check(s) failed."; exit 1; fi

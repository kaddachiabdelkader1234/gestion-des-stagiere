#!/usr/bin/env bash
# Smoke test: trigger a real Gmail SMTP email by accepting a candidature.
# Creates a throwaway LEARNER account with the real email, submits a candidature,
# then accepts it as ADMIN to fire the CandidatureAccepted event.

set -euo pipefail
GW="http://localhost:18080"
TS=$(date +%Y%m%d%H%M%S)
PW="Passw0rd!"
LEARNER_EMAIL="gadourkaddachi000@gmail.com"

echo "=== Real email smoke test — $TS ==="

# 1. Register LEARNER with the real Gmail address
echo "--- Registering LEARNER ($LEARNER_EMAIL) ---"
REG=$(curl -s -w "\n%{http_code}" -X POST "$GW/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"firstName\":\"Test\",\"lastName\":\"Email\",\"email\":\"$LEARNER_EMAIL\",\"password\":\"$PW\",\"phone\":\"+21600000000\",\"role\":\"LEARNER\",\"imageBase64\":null,\"experience\":0}")
REG_CODE=$(echo "$REG" | tail -1)
REG_BODY=$(echo "$REG" | sed '$d')
echo "  Register: HTTP $REG_CODE"
echo "  Body: $REG_BODY"

# 2. Register ADMIN
echo "--- Registering ADMIN ---"
ADMIN_REG=$(curl -s -w "\n%{http_code}" -X POST "$GW/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"firstName\":\"Admin\",\"lastName\":\"Test\",\"email\":\"admin.smoke.$TS@stb.tn\",\"password\":\"$PW\",\"phone\":\"+21600000001\",\"role\":\"ADMIN\",\"imageBase64\":null,\"experience\":0}")
ADMIN_REG_CODE=$(echo "$ADMIN_REG" | tail -1)
echo "  Register admin: HTTP $ADMIN_REG_CODE"

# 3. Login as LEARNER
echo "--- Logging in as LEARNER ---"
LEARNER_LOGIN=$(curl -s -X POST "$GW/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$LEARNER_EMAIL\",\"password\":\"$PW\"}")
LEARNER_TOKEN=$(echo "$LEARNER_LOGIN" | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])" 2>/dev/null || echo "$LEARNER_LOGIN" | python -c "import sys,json; print(json.load(sys.stdin)['token'])" 2>/dev/null)
if [ -z "$LEARNER_TOKEN" ]; then
  echo "  ERROR: Could not extract token. Response: $LEARNER_LOGIN"
  # Try alternative parsing
  LEARNER_TOKEN=$(echo "$LEARNER_LOGIN" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
fi
echo "  Token obtained: ${LEARNER_TOKEN:0:20}..."

# 4. Login as ADMIN
echo "--- Logging in as ADMIN ---"
ADMIN_LOGIN=$(curl -s -X POST "$GW/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin.smoke.$TS@stb.tn\",\"password\":\"$PW\"}")
ADMIN_TOKEN=$(echo "$ADMIN_LOGIN" | python3 -c "import sys,json; print(json.load(sys.stdin)['token'])" 2>/dev/null || echo "$ADMIN_LOGIN" | python -c "import sys,json; print(json.load(sys.stdin)['token'])" 2>/dev/null)
if [ -z "$ADMIN_TOKEN" ]; then
  echo "  ERROR: Could not extract token. Response: $ADMIN_LOGIN"
  ADMIN_TOKEN=$(echo "$ADMIN_LOGIN" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
fi
echo "  Token obtained: ${ADMIN_TOKEN:0:20}..."

# 5. Submit candidature as LEARNER
echo "--- Submitting candidature ---"
SUBMIT=$(curl -s -w "\n%{http_code}" -X POST "$GW/api/v1/candidatures" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LEARNER_TOKEN" \
  -d "{\"nom\":\"Kaddachi\",\"prenom\":\"Gadour\",\"email\":\"$LEARNER_EMAIL\",\"departement\":\"IT\",\"typeStage\":\"PFE\",\"ecole\":\"ENIT\",\"dateDebut\":\"2026-09-01\",\"dateFin\":\"2027-02-28\",\"motivation\":\"Real email test\"}")
SUBMIT_CODE=$(echo "$SUBMIT" | tail -1)
SUBMIT_BODY=$(echo "$SUBMIT" | sed '$d')
CAND_ID=$(echo "$SUBMIT_BODY" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])" 2>/dev/null || echo "$SUBMIT_BODY" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "  Submit: HTTP $SUBMIT_CODE, candidature id=$CAND_ID"

# 6. Accept candidature as ADMIN (triggers CandidatureAccepted → email)
echo "--- Accepting candidature (triggers email) ---"
ACCEPT=$(curl -s -w "\n%{http_code}" -X POST "$GW/api/v1/candidatures/$CAND_ID/accepter" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d "{\"departement\":\"IT\",\"encadrantId\":1,\"encadrantNom\":\"Encadrant Test\",\"dateDebut\":\"2026-09-01\",\"dateFin\":\"2027-02-28\"}")
ACCEPT_CODE=$(echo "$ACCEPT" | tail -1)
ACCEPT_BODY=$(echo "$ACCEPT" | sed '$d')
echo "  Accept: HTTP $ACCEPT_CODE"
echo "  Body: $ACCEPT_BODY"

# 7. Check notification service logs for the email attempt
echo ""
echo "--- Checking notification service logs for email ---"
sleep 3
docker compose logs notification-service --tail 20 2>&1 | grep -i "email\|smtp\|gmail\|failed\|sent" || echo "  (no email-related log lines found in last 20 lines)"

echo ""
echo "=== Done ==="
echo "Check your Gmail inbox (gadourkaddachi000@gmail.com) for an email with subject:"
echo "  'Votre candidature a été acceptée — STB'"
echo "Also check spam/junk folder."

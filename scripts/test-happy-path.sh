#!/bin/bash
# Full happy-path test: register → candidature → accept → journal → evaluation → validate → convention → sign → download
# Then verify notifications and audit entries exist.

set -e
GATEWAY="http://localhost:18080"
PASS=0
FAIL=0

check() {
  local desc="$1" expected="$2" actual="$3"
  if echo "$actual" | grep -q "$expected"; then
    echo "  ✅ $desc"
    PASS=$((PASS + 1))
  else
    echo "  ❌ $desc — expected '$expected' in response"
    FAIL=$((FAIL + 1))
  fi
}

echo "============================================"
echo "  FULL HAPPY PATH TEST"
echo "============================================"

# ---- 1. Register learner ----
echo ""
echo "1. Register learner"
REG=$(curl -s -X POST "$GATEWAY/api/v1/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"happy.test@demo.tn","password":"Demo1234!","firstName":"Happy","role":"LEARNER"}')
LEARNER_TOKEN=$(echo "$REG" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
LEARNER_ID=$(echo "$REG" | grep -o '"userId":[0-9]*' | head -1 | cut -d: -f2)
check "Learner registered" "token" "$REG"
echo "  → userId=$LEARNER_ID"

# ---- 2. Login as admin ----
echo ""
echo "2. Login as admin"
ADMIN_RESP=$(curl -s -X POST "$GATEWAY/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@stb.tn","password":"Admin123!"}')
ADMIN_TOKEN=$(echo "$ADMIN_RESP" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "Admin logged in" "token" "$ADMIN_RESP"

# ---- 3. Create encadrant ----
echo ""
echo "3. Create encadrant (trainer)"
TRAINER_RESP=$(curl -s -X POST "$GATEWAY/api/v1/auth/encadrants" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"firstName":"Mme Karim","email":"karim.enc@demo.tn","departement":"IT"}')
TRAINER_ID=$(echo "$TRAINER_RESP" | grep -o '"userId":[0-9]*' | head -1 | cut -d: -f2)
TRAINER_TEMP_PASS=$(echo "$TRAINER_RESP" | grep -o 'Mot de passe temporaire: [^"]*' | sed 's/Mot de passe temporaire: //')
check "Encadrant created" "Encadrant" "$TRAINER_RESP"
echo "  → trainerId=$TRAINER_ID, tempPassword=$TRAINER_TEMP_PASS"

# ---- 4. Learner submits candidature ----
echo ""
echo "4. Learner submits candidature"
CAND=$(curl -s -X POST "$GATEWAY/api/v1/candidatures" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LEARNER_TOKEN" \
  -d '{"nom":"Dupont","prenom":"Happy","email":"happy.test@demo.tn","departement":"IT","typeStage":"PFE","ecole":"ESPRIT","dateDebut":"2026-09-15","dateFin":"2026-12-15","motivation":"Stage PFE"}')
CAND_ID=$(echo "$CAND" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "Candidature submitted" "EnAttente\|statut" "$CAND"
echo "  → candidatureId=$CAND_ID"

# ---- 5. Admin accepts candidature ----
echo ""
echo "5. Admin accepts candidature"
ACCEPT=$(curl -s -X POST "$GATEWAY/api/v1/candidatures/$CAND_ID/accepter" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d "{\"encadrantId\":$TRAINER_ID,\"encadrantNom\":\"Mme Karim\",\"departement\":\"IT\"}")
check "Candidature accepted" "Acceptee" "$ACCEPT"

# Give RabbitMQ a moment to process events
sleep 3

# ---- 6. Learner creates journal entry ----
echo ""
echo "6. Learner creates journal entry"
JOURNAL=$(curl -s -X POST "$GATEWAY/api/v1/stagiaires/$CAND_ID/journal" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $LEARNER_TOKEN" \
  -d '{"dateEntree":"2026-09-22","texte":"Premiere semaine de stage."}')
check "Journal entry created" "dateEntree\|id\|stagi" "$JOURNAL"

# ---- 7. Login as trainer ----
echo ""
echo "7. Login as trainer"
TRAINER_LOGIN=$(curl -s -X POST "$GATEWAY/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"karim.enc@demo.tn\",\"password\":\"$TRAINER_TEMP_PASS\"}")
TRAINER_TOKEN=$(echo "$TRAINER_LOGIN" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "Trainer logged in" "token" "$TRAINER_LOGIN"

# ---- 8. Trainer creates evaluation ----
echo ""
echo "8. Trainer creates evaluation"
EVAL=$(curl -s -X POST "$GATEWAY/api/v1/evaluations" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TRAINER_TOKEN" \
  -d "{\"stagiaireId\":\"$CAND_ID\",\"typeEvaluation\":\"MiParcours\",\"dateEvaluation\":\"2026-10-15\",\"note\":15.5,\"commentaire\":\"Bon progress, motivated\"}")
EVAL_ID=$(echo "$EVAL" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "Evaluation created" "Soumise\|id" "$EVAL"
echo "  → evaluationId=$EVAL_ID"

sleep 1

# ---- 9. Admin validates evaluation ----
echo ""
echo "9. Admin validates evaluation"
VALIDATE=$(curl -s -X POST "$GATEWAY/api/v1/evaluations/$EVAL_ID/valider" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
check "Evaluation validated" "Validee" "$VALIDATE"

# ---- 10. Check conventions exist ----
echo ""
echo "10. Check conventions exist (auto-created by CandidatureAccepted event)"
sleep 2
CONV_LIST=$(curl -s "$GATEWAY/api/v1/conventions?page=1&pageSize=10" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
CONV_ID=$(echo "$CONV_LIST" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "Convention exists" "id" "$CONV_LIST"
echo "  → conventionId=$CONV_ID"

# ---- 11. Admin generates convention PDF ----
echo ""
echo "11. Admin generates convention PDF"
GEN=$(curl -s -X POST "$GATEWAY/api/v1/conventions/$CONV_ID/generer" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
check "Convention PDF generated" "pdfDisponible\|pdf\|true" "$GEN"

sleep 1

# ---- 12. Admin signs convention ----
echo ""
echo "12. Admin signs convention"
SIGN=$(curl -s -X POST "$GATEWAY/api/v1/conventions/$CONV_ID/signer" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
check "Convention signed" "Signee" "$SIGN"

# ---- 13. Download convention PDF ----
echo ""
echo "13. Download convention PDF"
PDF_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "$GATEWAY/api/v1/conventions/$CONV_ID/pdf" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
check "Convention PDF downloadable (HTTP 200)" "200" "$PDF_STATUS"

# ---- 14. Check audit log (Stagiaire.Service) ----
echo ""
echo "14. Check audit log (Stagiaire.Service)"
STAG_AUDIT=$(curl -s "$GATEWAY/api/v1/audit?page=1&pageSize=50" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
echo "  Stagiaire audit entries: $(echo "$STAG_AUDIT" | grep -o '"action"' | wc -l)"
check "CANDIDATURE_SUBMITTED in audit" "CANDIDATURE_SUBMITTED" "$STAG_AUDIT"
check "CANDIDATURE_ACCEPTED in audit" "CANDIDATURE_ACCEPTED" "$STAG_AUDIT"

# ---- 15. Check audit log (Convention.Service) ----
echo ""
echo "15. Check audit log (Convention.Service)"
CONV_AUDIT=$(curl -s "$GATEWAY/api/v1/conventions/audit?page=1&pageSize=50" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
echo "  Convention audit entries: $(echo "$CONV_AUDIT" | grep -o '"action"' | wc -l)"
check "CONVENTION_GENERATED in convention audit" "CONVENTION_GENERATED" "$CONV_AUDIT"
check "CONVENTION_SIGNED in convention audit" "CONVENTION_SIGNED" "$CONV_AUDIT"

# ---- 16. Check audit log (Evaluation.Service) ----
echo ""
echo "16. Check audit log (Evaluation.Service)"
EVAL_AUDIT=$(curl -s "$GATEWAY/api/v1/evaluations/audit?page=1&pageSize=50" \
  -H "Authorization: Bearer $ADMIN_TOKEN")
echo "  Evaluation audit entries: $(echo "$EVAL_AUDIT" | grep -o '"action"' | wc -l)"
check "EVALUATION_CREATED in evaluation audit" "EVALUATION_CREATED" "$EVAL_AUDIT"
check "EVALUATION_VALIDATED in evaluation audit" "EVALUATION_VALIDATED" "$EVAL_AUDIT"

# ---- 17. Check notifications ----
echo ""
echo "17. Check notifications for learner"
NOTIF=$(curl -s "$GATEWAY/api/v1/notifications?page=1&pageSize=50" \
  -H "Authorization: Bearer $LEARNER_TOKEN")
echo "  Notification count: $(echo "$NOTIF" | grep -o '"type"' | wc -l)"
check "CandidatureAcceptee notification exists" "CandidatureAcceptee" "$NOTIF"
check "ConventionGeneree notification exists" "ConventionGeneree" "$NOTIF"
check "EvaluationSoumise notification exists" "EvaluationSoumise" "$NOTIF"

# ---- 18. Show actual notification content ----
echo ""
echo "18. Notification details for learner"
echo "$NOTIF" | python3 -c "
import sys, json
data = json.load(sys.stdin)
for n in data.get('items', []):
    print(f\"  [{n['type']}] {n['message']} (lu={n['lu']})\")
" 2>/dev/null || echo "  (could not parse notification details)"

# ---- 19. Show convention audit entry content ----
echo ""
echo "19. Convention audit entry details"
echo "$CONV_AUDIT" | python3 -c "
import sys, json
data = json.load(sys.stdin)
for e in data.get('items', []):
    print(f\"  [{e['action']}] {e['details']} (user={e.get('userEmail','N/A')}, ts={e['timestamp']})\")
" 2>/dev/null || echo "  (could not parse audit details)"

# ---- 20. Show evaluation audit entry content ----
echo ""
echo "20. Evaluation audit entry details"
echo "$EVAL_AUDIT" | python3 -c "
import sys, json
data = json.load(sys.stdin)
for e in data.get('items', []):
    print(f\"  [{e['action']}] {e['details']} (user={e.get('userEmail','N/A')}, ts={e['timestamp']})\")
" 2>/dev/null || echo "  (could not parse audit details)"

echo ""
echo "============================================"
echo "  RESULTS: $PASS passed, $FAIL failed"
echo "============================================"
if [ $FAIL -gt 0 ]; then
  exit 1
fi

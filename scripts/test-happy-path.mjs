const G = 'http://localhost:18080';

async function req(method, path, body, token) {
  const headers = { 'Content-Type': 'application/json' };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const opts = { method, headers };
  if (body) opts.body = JSON.stringify(body);
  const res = await fetch(`${G}${path}`, opts);
  const text = await res.text();
  let json;
  try { json = JSON.parse(text); } catch { json = text; }
  return { status: res.status, json };
}

let PASS = 0, FAIL = 0;
function check(desc, ok) {
  if (ok) { console.log(`  ✅ ${desc}`); PASS++; }
  else { console.log(`  ❌ ${desc}`); FAIL++; }
}

console.log('============================================');
console.log('  FULL HAPPY PATH TEST');
console.log('============================================\n');

// 1. Register learner
console.log('1. Register learner');
const reg = await req('POST', '/api/v1/auth/register', { email:'ev3@demo.tn', password:'Demo1234!', firstName:'Ev', role:'LEARNER' });
check('Learner registered (201)', reg.status === 201);
const LID = reg.json.userId;
const LT = reg.json.token;
console.log(`  userId=${LID}`);

// 2. Login admin
console.log('\n2. Login admin');
const adminLogin = await req('POST', '/api/v1/auth/login', { email:'admin@stb.tn', password:'Admin123!' });
check('Admin logged in', !!adminLogin.json.token);
const AT = adminLogin.json.token;

// 3. Create encadrant
console.log('\n3. Create encadrant');
const tr = await req('POST', '/api/v1/auth/encadrants', { firstName:'Mme Karim', email:'karim3@demo.tn', departement:'IT' }, AT);
check('Encadrant created', tr.status === 200 || tr.json.userId);
const TID = tr.json.userId;
const TPASS = tr.json.message?.split('temporaire: ')[1];
console.log(`  trainerId=${TID}, tempPass=${TPASS}`);

// 4. Submit candidature
console.log('\n4. Submit candidature');
const cand = await req('POST', '/api/v1/candidatures', { nom:'Test',prenom:'Ev',email:'ev3@demo.tn',departement:'IT',typeStage:'PFE',ecole:'ESPRIT',dateDebut:'2026-09-15',dateFin:'2026-12-15',motivation:'PFE' }, LT);
check('Candidature submitted (201)', cand.status === 201);
const CID = cand.json.id;
console.log(`  candidatureId=${CID}, statut=${cand.json.statut}`);

// 5. Accept candidature
console.log('\n5. Accept candidature');
const acc = await req('POST', `/api/v1/candidatures/${CID}/accepter`, { encadrantId:TID, encadrantNom:'Mme Karim', departement:'IT' }, AT);
check('Candidature accepted', acc.json?.statut === 'Acceptee');

// Wait for RabbitMQ events
console.log('  (waiting 3s for RabbitMQ events...)');
await new Promise(r => setTimeout(r, 3000));

// 6. Journal entry
console.log('\n6. Journal entry');
const today = new Date().toISOString().split('T')[0];
const jnl = await req('POST', `/api/v1/stagiaires/${CID}/journal`, { dateEntree: today, texte:'Premiere semaine de stage' }, LT);
check('Journal entry created', jnl.status === 201 || jnl.json?.id);

// 7. Login trainer
console.log('\n7. Login trainer');
const tl = await req('POST', '/api/v1/auth/login', { email:'karim3@demo.tn', password: TPASS });
check('Trainer logged in', !!tl.json?.token);
const TT = tl.json?.token;

// 8. Create evaluation
console.log('\n8. Create evaluation');
const ev = await req('POST', '/api/v1/evaluations', { stagiaireId:CID, typeEvaluation:'MiParcours', dateEvaluation:'2026-10-15', note:15.5, commentaire:'Bon travail' }, TT);
check('Evaluation created', ev.json?.id || ev.status === 201);
const EVID = ev.json?.id;
console.log(`  evaluationId=${EVID}, statut=${ev.json?.statut}`);

await new Promise(r => setTimeout(r, 1000));

// 9. Validate evaluation
console.log('\n9. Validate evaluation');
const val = await req('POST', `/api/v1/evaluations/${EVID}/valider`, null, AT);
check('Evaluation validated', val.json?.statut === 'Validee');

// 10. Check convention exists
console.log('\n10. Check convention');
await new Promise(r => setTimeout(r, 2000));
const cl = await req('GET', '/api/v1/conventions?page=1&pageSize=10', null, AT);
const convId = cl.json?.items?.[0]?.id;
check('Convention exists', !!convId);
console.log(`  conventionId=${convId}`);

// 11. Generate convention PDF
console.log('\n11. Generate convention PDF');
const gen = await req('POST', `/api/v1/conventions/${convId}/generer`, null, AT);
check('Convention PDF generated', gen.json?.pdfDisponible === true || gen.json?.cheminPdf);

// 12. Sign convention
console.log('\n12. Sign convention');
const sign = await req('POST', `/api/v1/conventions/${convId}/signer`, null, AT);
check('Convention signed', sign.json?.statutSignature === 'Signee');

// 13. Download PDF
console.log('\n13. Download convention PDF');
const pdf = await req('GET', `/api/v1/conventions/${convId}/pdf`, null, AT);
check('Convention PDF downloadable (200)', pdf.status === 200);

// ========== AUDIT EVIDENCE ==========
console.log('\n============================================');
console.log('  AUDIT EVIDENCE');
console.log('============================================');

// 14. Stagiaire.Service audit
console.log('\n14. Stagiaire.Service audit entries:');
const stagAudit = await req('GET', '/api/v1/audit?page=1&pageSize=50', null, AT);
console.log(JSON.stringify(stagAudit.json, null, 2));
check('CANDIDATURE_SUBMITTED in audit', JSON.stringify(stagAudit.json).includes('CANDIDATURE_SUBMITTED'));
check('CANDIDATURE_ACCEPTED in audit', JSON.stringify(stagAudit.json).includes('CANDIDATURE_ACCEPTED'));

// 15. Convention.Service audit
console.log('\n15. Convention.Service audit entries:');
const convAudit = await req('GET', '/api/v1/conventions/audit?page=1&pageSize=50', null, AT);
console.log(JSON.stringify(convAudit.json, null, 2));
check('CONVENTION_GENERATED in audit', JSON.stringify(convAudit.json).includes('CONVENTION_GENERATED'));
check('CONVENTION_SIGNED in audit', JSON.stringify(convAudit.json).includes('CONVENTION_SIGNED'));

// 16. Evaluation.Service audit
console.log('\n16. Evaluation.Service audit entries:');
const evalAudit = await req('GET', '/api/v1/evaluations/audit?page=1&pageSize=50', null, AT);
console.log(JSON.stringify(evalAudit.json, null, 2));
check('EVALUATION_CREATED in audit', JSON.stringify(evalAudit.json).includes('EVALUATION_CREATED'));
check('EVALUATION_VALIDATED in audit', JSON.stringify(evalAudit.json).includes('EVALUATION_VALIDATED'));

// ========== NOTIFICATION EVIDENCE ==========
console.log('\n============================================');
console.log('  NOTIFICATION EVIDENCE');
console.log('============================================');

// 17. Notifications for learner
console.log('\n17. Notifications for learner:');
const notif = await req('GET', '/api/v1/notifications?page=1&pageSize=50', null, LT);
console.log(JSON.stringify(notif.json, null, 2));
check('CandidatureAcceptee notification exists', JSON.stringify(notif.json).includes('CandidatureAcceptee'));
check('ConventionGeneree notification exists', JSON.stringify(notif.json).includes('ConventionGeneree'));
check('EvaluationSoumise notification exists', JSON.stringify(notif.json).includes('EvaluationSoumise'));

console.log('\n============================================');
console.log(`  RESULTS: ${PASS} passed, ${FAIL} failed`);
console.log('============================================');

process.exit(FAIL > 0 ? 1 : 0);

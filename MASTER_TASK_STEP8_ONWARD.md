# Master Task — Real Email, Steps 9–13, and Modernization

Work through this in order. **Whenever you're unsure about something, or a decision needs my
input (cost, a real account/credential, a design choice with real trade-offs), stop and ask me
instead of guessing.** Don't silently pick an option and move on if it's something I'd reasonably
want to weigh in on. Update `HANDOFF.md` and `PROJECT_WORK_LOG.md` after every part below, same as
every phase so far.

---

## Part A — Switch from Mailhog to real Gmail SMTP (do this first)

Gmail app password (already generated, 2-Step Verification is on): `avdagqegvpufpewl`

**Put this only in `.env` — never in code, never in `.env.example`, never committed to git.**
Confirm `.env` is in `.gitignore` before you write it there.

1. Add to `.env` (not `.env.example`):
   ```
   SMTP_HOST=smtp.gmail.com
   SMTP_PORT=587
   SMTP_USERNAME=<my gmail address — ask me for this if it's not already somewhere in the config>
   SMTP_PASSWORD=avdagqegvpufpewl
   SMTP_USE_TLS=true
   ```
   Ask me for the Gmail address if you don't already have it — don't guess or leave it blank.

2. Update `Notification.Service`'s SMTP client config to read these from env vars instead of
   Mailhog's hardcoded settings. Gmail requires auth + STARTTLS on 587; Mailhog needs neither —
   make this configurable, not two separate code paths.

3. Keep Mailhog available for local dev — switching back to it should just mean changing the env
   vars, not changing code. Document which var controls this in `HANDOFF.md`.

4. **Verify for real**: trigger one real event (e.g. accept a candidature using my own email
   address as the test stagiaire) and confirm the email actually lands in my real inbox — check
   subject, body, sender, and whether it landed in spam. Tell me exactly what to check and where.

5. If Gmail rejects the connection or flags it as suspicious (it sometimes does for a new app
   password from an unfamiliar server), tell me the exact error — don't guess at a workaround,
   ask me since it may need a Google-account-side change only I can make.

---

## Part B — Finish the original brief: Steps 9–13

Refer to `IMPLEMENTATION_BRIEF.md` for full detail on each. Summary, in order:

**Step 9 — Admin stats dashboard + attestation PDF**
- Real, non-mocked stats (total stagiaires, breakdown by département, pending candidatures,
  average evaluation score) on the admin dashboard.
- Attestation de fin de stage PDF generation once a stagiaire's evaluation is validated — reuse
  the convention PDF approach, but **watch for the exact blank-PDF bug found in step 5** (missing
  font in the container image causing a 200 OK response with no visible text). Verify by opening
  the actual generated PDF, not just checking the response code.

**Step 10 — Refresh token flow**
- Extend auth-service with a refresh token so users aren't forced to re-log-in on every access
  token expiry. Keep the existing access-token JWT setup as-is.

**Step 11 — Observability (`/metrics`)**
- Wire real health checks and Prometheus metrics into the gateway and all 4 .NET services —
  `monitoring/prometheus.yml` already points at the right paths (fixed in step 3), it just needs
  something to actually scrape. Confirm targets show UP in Prometheus, not just that the endpoint
  returns 200 when curled directly.

**Step 12 — Integration tests + CI wiring**
- Integration tests covering the core happy path end-to-end (candidature → acceptance →
  convention → journal entry → evaluation → notification fired), wired into the existing GitHub
  Actions jobs so CI actually exercises them, not just builds/lints.

**Step 13 — Doc/version sweep**
- Clean up any remaining stale docs (version mismatches, outdated setup instructions) — check
  `HANDOFF.md` for anything flagged as "documented but not yet fixed" across earlier phases.

After each step: same Docker Desktop verification discipline as every phase so far — exact
commands, what to check, real evidence not just a success message.

---

## Part C — Modernization pass

Once steps 9–13 are done and verified, do the full modernization pass described in
`MODERNIZATION_PROMPT.md` (already covers: real-time updates via WebSockets, audit logging,
lightweight trace IDs, frontend UX polish, and the scalability tuning — connection pooling,
indexes, gateway rate limiting, k8s autoscaling config). Follow that file's scope exactly — it
deliberately excludes Redis and heavy infrastructure that this app's scale doesn't need yet; don't
add anything beyond what it specifies without asking me first.

---

## When you're completely done with all of the above

Create a new file, `NEXT_STEPS.md`, summarizing:

1. What was built across Parts A, B, and C, and how each was verified (not just what was
   attempted).
2. Any open items, deferred decisions, or things you flagged for me but I haven't answered yet.
3. The current, real state of the application — what a fresh person could do with it today if they
   pulled the repo and ran `docker compose up`.
4. Honest suggestions for what would be worth doing next if this were to go toward a real
   production deployment (e.g. swapping Gmail for a proper transactional email provider, real
   cloud hosting instead of local Docker, secrets management instead of `.env` files) — framed as
   options for me to decide on, not things you've already done.

This file should be written so that I — or a different agent entirely — could pick up the project
from a cold start and understand exactly where things stand, the same way `HANDOFF.md` has been
serving that purpose throughout this build.

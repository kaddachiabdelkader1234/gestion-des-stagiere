# Implementation Brief — Gestion des Stagiaires STB

## Instructions to Claude Code

You are implementing a complete, production-grade internship-management application end-to-end —
frontend (Angular 18), API Gateway, all 4 .NET microservices, the Java auth service, and RabbitMQ
messaging — all working together as one real system, not isolated pieces.

Work through this brief **in the order given in "Suggested order of work"**. Before each phase:
briefly restate what you're about to do and which files you expect to touch. After each phase: run
whatever build/test commands are available and confirm it actually works before moving on — don't
batch multiple phases into one untested pass. If something in the current codebase contradicts this
brief (a field name, a route, a port), trust the actual code and flag the discrepancy rather than
silently guessing.

Ask me before making a choice that's expensive to reverse (e.g. changing the DB schema in a
non-additive way, renaming a public API route already in use). For small implementation details not
covered below, use your judgment and keep moving.

## How to guide me through testing (I use Docker Desktop)

I'm not going to run raw `dotnet run` / `mvn spring-boot:run` / `ng serve` commands by hand across
6+ services — I test everything through Docker Desktop using the existing `docker-compose.yml`.
After **every phase** in the order below, do all of the following before moving to the next phase:

1. Tell me exactly which services changed and whether they need a rebuild
   (`docker compose build <service>`) or just a restart.
2. Give me the exact command(s) to run, e.g.:
   ```
   docker compose up -d --build stagiaire-service gateway
   ```
3. Tell me what to check to confirm it worked — which container logs to look at
   (`docker compose logs -f <service>`), which URL to hit (gateway port, Swagger UI, RabbitMQ
   management UI at `:15672`, etc.), and what a healthy result looks like vs. a broken one.
4. If the phase touches the frontend, tell me whether Angular is served via Docker Compose too or
   run separately with `ng serve` — be explicit, don't assume I remember which.
5. If a new environment variable, secret, or `docker-compose.yml` change is needed (e.g. the
   shared JWT secret, Mailhog config, DB connection strings), show me the exact diff to
   `docker-compose.yml` / `.env` and explain what it does.
6. Keep a short "how to test this locally" note at the end of each phase in plain steps — assume I
   want to click through Docker Desktop's UI to check container status, not just read CLI output.

If something fails when I test it, ask me for the container logs before guessing at a fix.

## Context (read this first)

This is a polyglot microservices app for STB (Société Tunisienne de Banque):

- **Angular 18** frontend (port 4200) — Soft UI Dashboard template, currently mostly empty (home page,
  auth pages, guarded dashboard shell with only a "Dashboard" menu item)
- **API Gateway** (Spring Cloud Gateway, port 8080) — single entry point, routes on
  `/api/v1/stagiaires/**`, `/api/v1/conventions/**`, `/api/v1/evaluations/**`,
  `/api/v1/notifications/**`, `/api/v1/auth/**`
- **Auth Service** (Java/Spring, port 8081) → MySQL — issues HS256 JWTs with a `role` claim
  (`ADMIN`, `TRAINER`, `LEARNER`, +3 unused roles)
- **Eureka** (8761) and **Config Server** (8888) — service discovery / central config
- **4x .NET 8 services → PostgreSQL**:
  - `Stagiaire.Service` (5070) — CRUD interns
  - `Convention.Service` (5071) — internship agreements (PDF)
  - `Evaluation.Service` (5072) — grades
  - `Notification.Service` (5073) — consumes RabbitMQ events, currently only logs a
    "would send email" placeholder
- **RabbitMQ** (MassTransit) for async events, e.g. `CandidatureAccepted`
- Docker Compose, k8s manifests, Prometheus config, GitHub Actions CI already scaffolded

**Known bugs to fix as part of this work (don't treat as separate tickets — fix inline as you touch
each area):**
1. `stagiaire.service.ts` calls `http://localhost:8080/api/Stagiaires` (wrong port/path) — gateway
   actually routes `/api/v1/stagiaires/**` on port `18080` per `environment.ts`. Standardize all
   frontend HTTP calls on the gateway's real base URL and versioned paths.
2. Gateway security rules still reference deleted sponsor/Keycloak endpoints and would reject the
   new `/api/v1/stagiaires/**` etc. traffic (`.anyExchange().authenticated()` with no matching
   permit rules). Rewrite the security config for the actual routes and roles below.
3. Angular version mismatch between `package.json` (18) and README (17) — fix the README.
4. `environment.ts` gateway port vs actual gateway bind port mismatch — reconcile to one port.
5. JWT secret is a `change-me` placeholder in Java config, .NET config, and docker-compose —
   replace with a single shared secret sourced from an env var / secrets file, consistent across
   all services.
6. `StagiaireService` TS model uses `id?: number` and `encadrant?`, but the .NET API returns a
   `Guid id` and `statut` (no `encadrant` field directly — see data model below). Fix the model.
7. Auth endpoints aren't secured by the gateway and public routes for the new services aren't
   declared — declare explicit permit-all vs authenticated route rules.
8. Config Server is only consumed by the gateway; auth-service explicitly disables it
   (`spring.cloud.config.enabled: false`) and the .NET services use local config. Leave this as-is
   for now (out of scope) unless it blocks something above.

---

## Business logic to implement (full scope)

Three roles, mapped from existing JWT role claims:

| JWT role  | App role                  | Summary |
|-----------|----------------------------|---------|
| `ADMIN`   | Admin (RH)                 | Runs the whole pipeline |
| `TRAINER` | Encadrant / Maître de stage | Supervises & evaluates assigned stagiaires |
| `LEARNER` | Stagiaire                  | Applies, tracks progress, receives evaluation |

### Lifecycle & status model

Add a `Statut` enum to the Stagiaire/Convention data model and drive the UI off it:

```
Candidature:  EN_ATTENTE → ACCEPTEE → REJETEE
Convention:   BROUILLON → SIGNEE
Stage:        EN_COURS → TERMINE
Evaluation:   NON_EVALUE → EVALUE
```

### Step-by-step flow

1. **Candidature** — Stagiaire registers via auth-service, then submits a "demande de stage" form:
   type de stage (`PFE` / `STAGE_ETE` / `STAGE_OUVRIER`), dates souhaitées, département souhaité,
   école, CV (file upload, store as blob or path — keep simple, local disk/volume is fine for now).
   Persisted via `Stagiaire.Service`, status `EN_ATTENTE`.

2. **Validation (Admin)** — Admin dashboard lists pending candidatures. Admin accepts (assigns
   département + encadrant from a dropdown of `TRAINER` users) or rejects (with a reason field).
   On accept: update status to `ACCEPTEE`, publish `CandidatureAccepted` to RabbitMQ (already
   wired), and auto-create a draft `Convention` via `Convention.Service`.

3. **Convention de stage** — `Convention.Service` generates a PDF (stagiaire info, dates, encadrant,
   département). Status `BROUILLON` until Admin marks it `SIGNEE` (simple confirm action — no need
   for real e-signature). Stagiaire can download the PDF from their dashboard once signed.

4. **Suivi — journal de bord** — Stagiaire submits short weekly log entries (date, texte). Encadrant
   can view all entries for their assigned stagiaires and optionally leave a short comment. Simple
   CRUD, no new service needed — add to `Stagiaire.Service` as a sub-resource
   (`/api/v1/stagiaires/{id}/journal`).

5. **Évaluation** — Near end of stage (or manually triggered by Admin/Encadrant), Encadrant fills a
   grading form: a competency grid (define 4–6 fixed criteria, e.g. ponctualité, autonomie,
   qualité du travail, esprit d'équipe — each scored 0–20) + free-text comment + computed final
   note. Submitting publishes an evaluation-submitted event → Notification.Service. Status →
   `EVALUE`.

6. **Notifications (real, not placeholder)** — Wire `Notification.Service` to actually send email
   (use a simple SMTP provider or a dev-friendly one like Mailhog/Mailtrap for local testing) for:
   candidature accepted/rejected, convention ready, evaluation submitted. Keep templates simple
   (plain text or minimal HTML) — this is not the focus, just make it real instead of a log line.

7. **Attestation** — Once `EVALUE`, Admin can generate a final "attestation de fin de stage" PDF
   (reuse the PDF generation approach from `Convention.Service`, or add an endpoint there).

### Dashboards per role

- **Admin**: stats cards (total stagiaires, breakdown by département, pending candidatures count,
  average evaluation score) + full CRUD tables for stagiaires, conventions, encadrants,
  evaluations.
- **Encadrant**: list of assigned stagiaires only, their journal entries, pending evaluations with
  one-click "Évaluer" action.
- **Stagiaire**: personal status card (candidature status, convention status, stage countdown),
  journal de bord entry form, convention/attestation download, evaluation result once published.

---

## Frontend work (Angular 18)

- Fix bugs #1, #3, #4, #6 above as you build.
- Extend `role-permission.config.ts` usage so the sidebar menu actually reflects the 3 active
  roles (currently only "Dashboard" shows) — Admin/Encadrant/Stagiaire each get their own set of
  routes and guarded components.
- Build out `StagiaireService` (and new `ConventionService`, `EvaluationService`,
  `JournalService`) as proper Angular services hitting the gateway's versioned routes.
- Reuse the existing Soft UI Dashboard components/styling — don't introduce a new design system.

## Backend work

- **Gateway**: rewrite security config per bug #2/#7 — public: `/api/v1/auth/**`; authenticated +
  role-checked for everything else, matching the 3 roles.
- **.NET services**: add the `Statut` fields/enums, journal sub-resource, evaluation competency
  grid fields, and PDF generation endpoints described above. Keep the existing JWT claims
  transformation (`TRAINER` → `Encadrant`) intact.
- **Notification.Service**: replace the log-only placeholder with real SMTP sending, gated behind
  config so local dev can still no-op or use Mailhog.
- Fix the JWT secret placeholder (#5) — one shared secret via env var across Java + .NET +
  docker-compose.

---

## Production-grade requirements (build these in from the start, not as an afterthought)

- **Validation & error handling**: every .NET endpoint validates input (model validation /
  FluentValidation is fine) and returns consistent error shapes (e.g.
  `{ "error": "...", "code": "..." }`) with correct HTTP status codes. The gateway and Angular
  services should handle and surface these errors properly — no silent failures, no raw stack
  traces reaching the browser.
- **Pagination, filtering, search**: Admin tables (stagiaires, conventions, evaluations) must
  support server-side pagination and basic filtering (by statut, département, type de stage) —
  don't just dump all rows into the frontend.
- **API documentation**: add Swagger/OpenAPI to each .NET service (Stagiaire, Convention,
  Evaluation, Notification) and expose it at `/swagger` in each. Keep it accurate as you build.
- **Observability**: wire real health checks and basic metrics (request count, latency, error
  rate) into the existing Prometheus config (`monitoring/prometheus.yml`) for the gateway and the
  .NET services — this scaffold already exists, make it actually collect data.
- **Refresh tokens**: extend auth-service beyond a single access JWT — add a refresh token flow so
  the frontend doesn't force re-login on every expiry. Keep the existing HS256/shared-secret setup
  for the access token.
- **Tests**: add integration tests covering the core happy path (candidature → acceptance →
  convention → journal entry → evaluation → notification fired) — at minimum one test per .NET
  service plus one Angular e2e/integration test for the same flow through the gateway. Wire these
  into the existing GitHub Actions jobs (Java / .NET / Angular) so CI actually exercises them.

---

## Suggested order of work

1. Fix gateway routing/security + shared JWT secret (unblocks everything else)
2. Align frontend base URLs/models with actual .NET DTOs
3. Add validation/error handling conventions + Swagger to each .NET service as you touch it
4. Build candidature submission (Stagiaire) + validation screen (Admin) end-to-end, with
   pagination/filtering on the Admin list
5. Convention auto-generation + PDF download
6. Journal de bord (Stagiaire write, Encadrant read)
7. Evaluation form (Encadrant) + result view (Stagiaire)
8. Real email notifications
9. Admin stats dashboard + attestation PDF
10. Refresh token flow
11. Observability wiring (health checks + Prometheus metrics)
12. Integration tests across all services + CI wiring
13. Sweep for remaining doc/version mismatches (#3, #9)

## Acceptance criteria

- A stagiaire can register, submit a candidature, and see its status update after Admin action —
  all through the real gateway, no hardcoded/wrong ports.
- An Admin can accept a candidature, assign an encadrant, and see a convention PDF generated.
- An Encadrant only ever sees their own assigned stagiaires, can add journal comments, and submit
  an evaluation.
- A stagiaire sees their evaluation result and can download their convention/attestation once
  available.
- Emails actually fire (or hit Mailhog locally) at each of the three trigger points.
- Admin dashboard shows real, non-mocked stats pulled from the services.
- Admin tables are paginated/filterable, not full-dump.
- Every .NET service returns proper validation errors and exposes Swagger docs.
- Health/metrics endpoints exist and Prometheus can actually scrape them.
- A user can stay logged in past access-token expiry via the refresh flow.
- CI runs the new integration tests, not just builds/lints.

## Decisions already made (don't re-ask about these)

- File storage for CVs/PDFs: local disk/volume for now, not S3.
- Email provider for local dev: Mailhog (or equivalent no-op-friendly SMTP).
- E-signature for conventions: not needed — a simple Admin "mark as signed" action is sufficient.
- Design system: reuse the existing Soft UI Dashboard components — no new UI library.

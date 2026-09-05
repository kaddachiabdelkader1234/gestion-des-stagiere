# NEXT_STEPS.md — What's Built and What Comes Next

**Generated:** 2026-08-28, after completing the modernization pass (Part C).

---

## What Exists Today

A fully functional internship management platform ("Gestion des Stagiaires STB") with:

### Backend (4 .NET microservices + 3 Java services)
| Service | Port | Purpose |
|---------|------|---------|
| `smartek-gateway` | 18080 | Single entry point — JWT auth, route scoping, rate limiting, trace ID propagation |
| `smartek-auth` | 8081 | User registration, login, refresh tokens, role management (ADMIN/TRAINER/LEARNER) |
| `smartek-stagiaire` | 5070 | Candidatures, stagiaire CRUD, journal de bord, audit log |
| `smartek-convention` | 5071 | Convention auto-generation, PDF rendering, signing |
| `smartek-evaluation` | 5072 | Evaluations, attestation PDF, competency grid |
| `smartek-notification` | 5073 | Email notifications (Gmail SMTP or Mailhog) via RabbitMQ events |
| `smartek-eureka` | 8761 | Service discovery |
| `smartek-config` | 8888 | Centralized configuration |
| `smartek-postgres` | 5432 | 4 service databases |
| `smartek-mysql` | 3306 | Auth database |
| `smartek-rabbitmq` | 5672/15672 | Event bus |

### Frontend (Angular 18)
- Role-based dashboard with sidebar navigation
- Candidature submission + admin validation with accept/reject modals
- Convention management + PDF download
- Journal de bord (weekly entries + encadrant comments)
- Evaluation form + result view
- Admin stats dashboard
- Audit log screen with filtering
- Skeleton loaders on list views

### What Each Step Built (1–13 + Part A + Part C)

| Step | What | Verified |
|------|------|----------|
| 1 | Gateway routing, JWT auth, shared secret | 39/39 gateway tests |
| 2 | Frontend aligned with real DTOs | 50/50 Angular tests |
| 3 | Validation, error handling, Swagger on all 4 .NET services | All build clean |
| 4 | Candidature submission + admin validation | `smoke-step4.ps1` green |
| 5 | Convention auto-generation + PDF | `smoke-step5.ps1` green, 34 assertions |
| 6 | Journal de bord | `smoke-step6.ps1` green, 43 assertions |
| 7 | Evaluation backend + frontend | Auth sweep done, notification gap fixed |
| 8 | Real email notifications (Gmail SMTP) | Verified live |
| 9 | Admin stats dashboard + attestation PDF | Real stats endpoints |
| 10 | Refresh token flow | Access + refresh tokens, silent refresh |
| 11 | Observability `/metrics` | prometheus-net on all 4 services |
| 12 | Integration tests + CI | 4 unit tests + 18 integration tests (full happy path + role scoping) |
| 13 | Doc/version sweep | HANDOFF.md updated |
| A | Gmail SMTP | Verified live |
| C | Tracing (X-Trace-Id) | Cross-service: gateway → Stagiaire → Convention/Notification via events |
| C | Audit logging | 3 controllers hooked, admin screen, migration verified on fresh volume |
| C | Frontend UX polish | Skeleton loaders, route transitions |
| C | Scalability | Pool config, indexes (EXPLAIN ANALYZE confirmed), rate limiting (429 verified) |

### Verification Results (2026-08-28)
- **Attestation PDF:** 24,203 bytes, 15 font references, real text content (name, note 16.5/20, date, commentary). Not blank.
- **Refresh tokens:** One-time-use enforced — old token returns 401 on reuse. Angular interceptor wired for silent refresh.
- **Cross-service tracing:** Same trace ID appears in Stagiaire.Service (65 lines), Convention.Service (34 lines), Notification.Service (2 lines) for one request.
- **xUnit unit tests:** 4 model/enum tests passing.
- **xUnit integration tests:** 18 tests covering full happy path (register → candidature → accept → convention → journal → evaluation → attestation PDF), role-scoping negative tests (6 tests), refresh token one-time-use, unauthorized access. Run with `dotnet test --filter "Category=Integration"` (requires Docker stack).

### Bugs Found and Fixed During Verification Pass
1. `Smartek.Common` missing `using` for ASP.NET Core types — caught by Docker build
2. `PagedResult.TotalPages` is read-only — AuditController tried to set it
3. Gateway missing `/api/v1/audit/**` route — 404 on audit endpoint
4. `AddDocumentFields` migration missing Designer file — silently skipped on fresh volume (permanently fixed)
5. Console logging didn't include scopes — configured `IncludeScopes: true`
6. Cross-service tracing didn't propagate through RabbitMQ — added `TraceId` to event records

---

## What's Deferred (Not Done)

### 1. Real-Time WebSockets (highest priority from MODERNIZATION_PROMPT)
**Status:** Deferred pending design decision.

**Why deferred:** Requires choosing between Spring STOMP (gateway-level), SignalR (.NET-side), or Socket.IO. Each has different architectural implications for the Java/.NET split. The brief asks to "explain your choice before building."

**Recommended approach:** Spring STOMP at the gateway level — the gateway already has the JWT decoder and routes to all services, so it's the natural WebSocket host. It would subscribe to RabbitMQ events and push to authenticated, role-scoped WebSocket connections.

**What it would add:** Live status updates on the stagiaire's dashboard (candidature accepted → convention ready → evaluation validated) without page reloads.

### 2. Optimistic UI Updates
**Status:** Deferred.

**Why deferred:** Candidature accept/reject involves server-side validation + event publishing + convention auto-drafting — not safe for optimistic updates. The skeleton loaders already cover the main perceived-latency issue.

### 3. Dark Mode
**Status:** Deferred.

**Why deferred:** The Soft UI Dashboard template uses hardcoded gradients. Retrofitting dark mode would require rebuilding significant CSS without proportional value at this project's scale.

---

## How to Run the Project

```bash
# First time: copy .env.example to .env and set JWT_SECRET
cp .env.example .env

# Start everything
docker compose up -d --build

# Wait for health checks (~60 seconds), then:
# Gateway: http://localhost:18080
# Angular: cd Frontend/angular-app && npm start → http://localhost:4200
# Admin login: admin@stb.tn / Admin123!

# Swagger docs:
# http://localhost:5070/swagger (Stagiaire)
# http://localhost:5071/swagger (Convention)
# http://localhost:5072/swagger (Evaluation)
# http://localhost:5073/swagger (Notification)
```

---

## What a Fresh Person Could Do Next

1. **Build WebSockets** — the deferred highest-priority item. Pick a library, implement live push on domain events, add the Angular WebSocket client.

2. **Add more audit coverage** — hook `Convention.Service` (convention signed) and `Evaluation.Service` (evaluation validated) into the audit trail. Currently only Stagiaire.Service controllers are hooked.

3. ~~Add a `CandidatureRejected` email notification~~ — **Already done.** `CandidaturesController.Rejeter` publishes `CandidatureRejected`; `Notification.Service/Consumers/CandidatureRejectedConsumer` consumes it and sends a rejection email. Verified live: email sent via Gmail SMTP.

4. **Kubernetes manifests** — the `k8s/` directory exists but may need updating for the new services and configurations.

5. ~~**Integration tests**~~ — **Done.** 18 integration tests covering the full happy path (register → candidature → accept → convention → journal → evaluation → notification), role-scoping negative tests, refresh token one-time-use, and unauthorized access. Run with `dotnet test --filter "Category=Integration"` (requires Docker stack).

6. **Production hardening** — HTTPS termination, proper CORS origins, stronger JWT secret rotation, database connection SSL, log aggregation.

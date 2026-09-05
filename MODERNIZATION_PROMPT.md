# Modernization Prompt

Context: Steps 1–8 gave this application a genuinely solid, production-grade backend architecture: event-driven microservices, gateway-enforced RBAC with real ownership-scoping, Swagger docs, pagination, structured errors, and real email notifications. That part is done well — don't touch or "improve" it beyond what's asked below.

What's missing to make this feel like a modern application rather than just a correct one: real-time updates, audit logging (relevant for a bank application specifically), better observability, and frontend polish. This task adds those — scoped to what actually matters at this application's size, not everything a large-scale system might have.

Do not add Redis caching, distributed tracing infrastructure beyond what's specified below, or any other heavy infrastructure not explicitly listed — this app's scale doesn't need it yet, and adding it now is complexity without payoff. If you think something below is a genuinely bad fit for this app's scale, tell me before building it rather than skipping or reinterpreting silently.

## 1. Real-time updates (highest priority of this batch)

Right now every status change (candidature accepted, convention ready, evaluation validated) requires a manual refresh to see. Add real-time push so the frontend updates live.

- Use WebSockets (Spring's STOMP-over-WebSocket via the gateway, or a lightweight Socket.IO/SignalR bridge — pick whichever fits the existing Java/.NET split best and explain your choice before building it).
- Trigger a push on the same domain events already firing over RabbitMQ (CandidatureAccepted, convention generated/signed, evaluation submitted/validated, notification created) — don't duplicate business logic, just have the existing event consumers also emit a websocket message to the relevant user(s).
- Frontend: the stagiaire's status card, the encadrant's assigned-stagiaires list, and the admin's candidature/evaluation lists should update without a page reload when a relevant event fires.
- Security: the websocket connection must be authenticated (reuse the JWT) and scoped the same way REST endpoints are — a stagiaire must not receive push events about another stagiaire's data. Test this explicitly, the same way REST scoping has been tested throughout this project.

## 2. Audit logging

For a bank application, "who did what, when" matters more than in most apps. Add a lightweight audit trail — not a full compliance system, just a real one.

- New table/service (or a table within an existing service, your call — explain the trade-off): record who (user id + role), what (action, e.g. CANDIDATURE_ACCEPTED, EVALUATION_VALIDATED, ENCADRANT_CREATED), when (timestamp), and target (which entity/id was affected).
- Log every state-changing admin/encadrant action from steps 2–8 (candidature accept/reject, convention sign, evaluation validate, encadrant creation, journal comment).
- Add a simple Admin-only "Journal d'audit" screen: paginated, filterable by action type and date range. No need for anything fancier than that.

## 3. Refresh tokens (if step 10 hasn't happened yet)

Check HANDOFF.md — if step 10 (refresh token flow) from the original brief hasn't been done yet, do it now as part of this pass, since it's a prerequisite for a real-time connection that should survive token expiry without dropping.

## 4. Basic tracing (lightweight, not full OpenTelemetry infrastructure)

Add a correlation/trace ID that's generated at the gateway, passed through every downstream service call (HTTP header, e.g. X-Trace-Id), and included in every log line and every structured error response (traceId field already exists per step 3 — confirm it's populated from this header, not generated fresh per-service).

This alone makes it possible to trace one request across all 4 .NET services + the gateway by grepping logs for one ID — which is most of the practical value of tracing at this scale, without standing up a full observability platform.

## 5. Frontend UX polish

- Replace spinners with skeleton loaders on the main list/dashboard views (stagiaire list, candidature list, evaluation list) — this is the single highest-impact "feels modern" change.
- Add optimistic UI updates where safe: e.g. when an admin accepts a candidature, update the row immediately in the UI while the request is in flight, and roll back with an error toast if it fails — don't make the user wait on a spinner for something that almost always succeeds.
- Add dark mode support if the Soft UI Dashboard template supports it cleanly — check first, don't rebuild the design system to add this. If it's a fight, skip it and tell me.
- Smooth transitions between dashboard views (route transitions, list item enter/exit) — keep it subtle, not flashy. This should feel like attention to detail, not a demo reel.

## 6. Scalability (tuning, not a rewrite)

The architecture is already shaped correctly for scale — stateless services, event-driven via RabbitMQ, containerized. This section is tuning and config, not new infrastructure. Do not add Redis, custom load-balancing logic, or anything beyond what's listed here — same rule as the rest of this document: this app's realistic scale (hundreds to a few thousand stagiaires) doesn't need heavy infrastructure, just correctly-configured basics.

- DB connection pooling: check and correctly configure connection pool sizing in each .NET service's appsettings (Npgsql pool settings) and the Java services (HikariCP settings) — sized to a realistic replica count, not left on framework defaults.
- Indexes: add indexes on columns the admin filters/queries actually use — Stagiaire.StatutCandidature, Stagiaire.EncadrantId, Stagiaire.Departement, and equivalent filter columns on Convention/Evaluation.
- Gateway rate limiting: add Spring Cloud Gateway's built-in rate limiter (in-memory is fine, no Redis needed at this scale) on the public/high-traffic routes.
- Kubernetes autoscaling config: in the existing k8s/ manifests, set replicas: 2-3 for the gateway and the busiest .NET services, and add a HorizontalPodAutoscaler keyed on CPU/memory for each.

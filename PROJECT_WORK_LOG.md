# Project Work Log

## Goal

Document the work done in this workspace, the steps taken, and the results reached.

## 1. Initial Repository Review

What we did:
- Inspected the workspace structure.
- Identified the main backend modules and frontend.
- Separated the Spring Boot support services from the newer .NET services.

What we reached:
- Confirmed the Java support stack remained in place:
  - `api-gateway`
  - `auth-service`
  - `config-server`
  - `eureka-server`
- Confirmed the old sponsor backend modules were no longer the focus.

## 2. Backend Trim and Routing Update

What we did:
- Removed sponsor-specific backend pieces that were no longer needed.
- Updated gateway and config-server routing so the backend points to the new service names.
- Kept service discovery enabled through Eureka.

What we reached:
- The Java gateway now routes to the current .NET services.
- The auth-service stays available as the JWT issuer and security entry point.

## 3. .NET Microservice Scaffolding

What we did:
- Created four .NET 8 Web API services:
  - `Stagiaire.Service`
  - `Convention.Service`
  - `Evaluation.Service`
  - `Notification.Service`
- Added the typical API layers to each service:
  - Controllers
  - DTOs
  - Data layer with EF Core
  - Models
  - Validation
  - Swagger
  - Steeltoe Eureka registration

What we reached:
- Each service became a runnable microservice with its own port and database connection.
- Each service registered itself in Eureka with a stable service name.

## 4. Service Discovery Verification

What we did:
- Started the services.
- Refreshed the Eureka dashboard.
- Confirmed registration entries were visible.

What we reached:
- Eureka shows these services as `UP`:
  - `AUTH-SERVICE`
  - `STAGIAIRE-SERVICE`
  - `CONVENTION-SERVICE`
  - `EVALUATION-SERVICE`
  - `NOTIFICATION-SERVICE`

## 5. JWT Signing Configuration Review

What we did:
- Read the Spring Boot auth-service JWT configuration.
- Checked `application.yml` and the JWT service class.
- Confirmed the token signing details and token payload shape.

What we reached:
- The auth-service uses a symmetric JWT secret from `jwt.secret`.
- The signing algorithm is `HS256`.
- The token subject is the user email.
- The token includes a custom `role` claim.
- The Java code currently emits the role as the enum name, for example `TRAINER`.

## 6. .NET JWT Validation Middleware

What we did:
- Added JWT bearer authentication to all four .NET services.
- Configured token validation to match the Spring issuer.
- Added role claim transformation so .NET authorization can understand the Spring token.
- Wired authentication into the ASP.NET middleware pipeline.

What we reached:
- The .NET services now accept JWTs signed by the Java auth-service.
- `[Authorize]` now works on authenticated endpoints.
- `[Authorize(Roles = "Encadrant")]` can succeed when the Spring token contains `TRAINER`, because the .NET side maps that role to `Encadrant`.

## 7. Configuration Added to .NET Services

What we did:
- Added `Microsoft.AspNetCore.Authentication.JwtBearer` to each .NET service project.
- Added JWT secret configuration entries in each `appsettings.json`.
- Centralized the authentication setup in reusable extension classes.

What we reached:
- The JWT validation setup is consistent across:
  - `Stagiaire.Service`
  - `Convention.Service`
  - `Evaluation.Service`
  - `Notification.Service`

## 8. Validation and Build Checks

What we did:
- Built all four .NET services.
- Worked around file locks caused by running service processes.
- Validated compilation using temporary output folders.

What we reached:
- All four services compiled successfully.
- The code changes are valid.
- The live running services were left untouched.

## 9. Final State

What we have now:
- A Spring Boot auth-service that issues HS256 JWTs.
- Four .NET 8 microservices that validate those JWTs.
- Eureka service discovery working for all active services.
- Role-based authorization support aligned between Java and .NET.

## 10. Important Notes

- The actual shared JWT secret should be the same value in the Java auth-service and the .NET services.
- ~~The secret should not stay as `change-me` in production.~~ Resolved in section 11 — the
  `change-me` fallbacks are gone and every service now fails fast on a missing `JWT_SECRET`.
- If you want exact one-to-one role naming later, the Java auth-service can be updated to emit `Encadrant` instead of `TRAINER`.

## Summary

The workspace was transformed from a sponsor-oriented backend into a mixed architecture where:
- Java services handle gateway, config, Eureka, and authentication.
- .NET services handle the business microservices.
- JWT authentication works across both stacks.
- Role-based authorization is supported end to end.

## 11. Phase 1 — Gateway routing/security + shared JWT secret

This is step 1 of the "Suggested order of work" in [IMPLEMENTATION_BRIEF.md](IMPLEMENTATION_BRIEF.md).
Much of it was already in place from earlier work (routes on `/api/v1/**`, the HS256 shared-secret
decoder replacing Keycloak JWKS, per-role authorization rules, `JWT_SECRET` wired through
docker-compose). What follows is what was still wrong and got fixed.

### Fixed: anonymous user-data exposure at the gateway

`SecurityConfig` had a blanket `.pathMatchers("/api/v1/auth/**").permitAll()`. auth-service's
`AuthController` also serves `GET /api/auth/user/{userId}` and `GET /api/auth/validate/{userId}`,
which take a sequential numeric id and return `AuthResponse` — email, firstName, role, profile
image, experience. Any unauthenticated caller could walk `user/1`, `user/2`, ... and dump the
user table through the gateway.

Now only the genuinely anonymous entry points are public:

| Path | Access |
|------|--------|
| `POST /api/v1/auth/register`, `POST /api/v1/auth/login` | public |
| `GET /api/v1/auth/health` | public |
| `/api/v1/auth/user/**`, `/api/v1/auth/validate/**` | `ROLE_ADMIN` |
| anything else under `/api/v1/auth/**` | authenticated |

The frontend only calls `/login` and `/register` (`core/services/auth.service.ts`), so nothing
in the app breaks. If a non-admin screen later needs a user lookup, relax that one rule rather
than reopening the whole prefix.

### Fixed: `change-me` JWT secret fallback

`jwt.secret` was `${JWT_SECRET:change-me}` in three places — the gateway's `application.yml`,
config-server's `config/api-gateway.yml`, and auth-service's `application.yml`. That default is
actively harmful: `change-me` is 9 bytes, and HS256 requires at least 32. So an unset
`JWT_SECRET` produced a service that started "fine" and then failed on the first login
(jjwt `WeakKeyException`) or first token decode. The default is gone — Spring now fails at
startup with an unresolved-placeholder error naming `JWT_SECRET`.

The four .NET services already fail fast correctly (`ResolveJwtSecret` throws when neither
`JWT_SECRET` nor `Jwt:Secret` is set), so they needed no change.

### Fixed: the secret value itself

`.env` still carried `local-dev-only-secret-change-me-min-32-bytes-long!!`. Replaced with 48
random bytes, base64. `.env` is gitignored; `.env.example` keeps an obviously-invalid placeholder
plus both the `openssl` and PowerShell commands for generating a real one.

**Changing the secret invalidates any token issued before this change** — log in again.

### Removed

`Backend/config-server/src/main/resources/config/sponsor-service.yml` — config for a service that
no longer exists in `Backend/`, `docker-compose.yml`, `k8s/`, or CI. Config Server was still
serving it at `/sponsor-service/default`.

Also dropped the obsolete `version: "3.8"` key from `docker-compose.yml`, which made every
`docker compose` command print a deprecation warning.

### Fixed: auth-service could not connect to MySQL (found while testing, not in the brief)

Not one of the brief's listed bugs — it surfaced the moment the stack was actually started.
`smartek-auth` started and immediately exited with:

```
java.sql.SQLNonTransientConnectionException: Public Key Retrieval is not allowed
```

MySQL 8 defaults to the `caching_sha2_password` plugin. The JDBC URL set `useSSL=false` but not
`allowPublicKeyRetrieval=true`, so the driver refused to fetch the server's public key and no
connection was ever possible — auth-service could never have served a login. Added the flag in
all four places the URL is written: `docker-compose.yml`, `.env`, `.env.example`, and
auth-service's `application.yml` default. (The now-deleted `sponsor-service.yml` had the flag —
it was simply never carried over to auth-service.)

Acceptable here because the connection stays inside the compose network; a real deployment should
use TLS to MySQL instead of `useSSL=false`.

### Verified

- `mvn test` for api-gateway, run in a `maven:3.9-eclipse-temurin-17` container (no local JDK/Maven
  on this machine): **30/30 pass** — `SecurityConfigTest` 19, `JwtAuthenticationConverterTest` 10,
  `ApiGatewayApplicationTests` 1. Six new tests cover the auth rules above.
- `docker compose config` valid; the same `JWT_SECRET` resolves into all 6 services.
- With `JWT_SECRET` unset, `docker compose config` fails with the intended message instead of
  starting a broken stack.
- **End-to-end against the running stack** (eureka + config + auth + gateway + mysql, all
  `Running`), every request through the gateway on `18080`:

  | Check | Result |
  |---|---|
  | `GET /actuator/health` | `200` |
  | `POST /api/v1/auth/register` (LEARNER) | `201` |
  | `POST /api/v1/auth/login` | `200` + 193-char JWT |
  | `POST /api/v1/auth/login` wrong password | `401` |
  | `GET /api/v1/auth/user/1` no token | `401` ← was an anonymous data leak |
  | `GET /api/v1/auth/user/1` LEARNER token | `403` |
  | `GET /api/v1/auth/user/1` ADMIN token | `200` (still works for admin) |
  | `GET /api/v1/stagiaires` no token | `401` |

- Eureka lists `API-GATEWAY`, `AUTH-SERVICE`, `CONFIG-SERVER` as `UP`.

### How to test this locally

Services changed: **api-gateway**, **auth-service**, **config-server** (Java — need a rebuild,
not just a restart). No frontend or .NET change in this phase, so nothing to run with `ng serve`
and no .NET image to rebuild.

```
docker compose up -d --build eureka-server config-server auth-service api-gateway
```

`auth-service` also picked up a changed environment variable (the JDBC URL), so if it was already
running, force it to be recreated rather than just restarted:

```
docker compose up -d --force-recreate auth-service
```

In Docker Desktop → Containers, expect `smartek-eureka`, `smartek-config`, `smartek-mysql`,
`smartek-auth`, `smartek-gateway` all at *Running* (eureka/config/mysql also show *healthy*;
auth and gateway have no healthcheck defined, so "Running" is the healthy state for them).
If `smartek-auth` flips to *Exited*, open its **Logs** tab in Docker Desktop — that is the one
container that depends on MySQL being reachable.

1. Eureka dashboard <http://localhost:8761> — `AUTH-SERVICE` and `API-GATEWAY` show as `UP`.
   (Registration takes ~30s after start.)
2. Gateway health: <http://localhost:18080/actuator/health> → `{"status":"UP"}`.
3. Register a user through the gateway — expect `201` and a `token` in the response:
   ```
   curl -i -X POST http://localhost:18080/api/v1/auth/register -H "Content-Type: application/json" -d "{\"firstName\":\"Amine\",\"email\":\"amine@stb.tn\",\"password\":\"Passw0rd!\",\"role\":\"LEARNER\"}"
   ```
4. Log in — expect `200` + token:
   ```
   curl -i -X POST http://localhost:18080/api/v1/auth/login -H "Content-Type: application/json" -d "{\"email\":\"amine@stb.tn\",\"password\":\"Passw0rd!\"}"
   ```
5. **The security fix** — this must now be `401`, not a user record:
   ```
   curl -i http://localhost:18080/api/v1/auth/user/1
   ```
   And with the LEARNER token from step 4, `403`:
   ```
   curl -i http://localhost:18080/api/v1/auth/user/1 -H "Authorization: Bearer <token>"
   ```
6. A protected business route with no token is `401`:
   ```
   curl -i http://localhost:18080/api/v1/stagiaires
   ```

Healthy vs broken: `401`/`403` at steps 5–6 is the *correct* result. If step 3 or 4 returns `500`
or the gateway container exits at boot with "Could not resolve placeholder 'JWT_SECRET'", `.env`
is missing or unreadable — `docker compose logs api-gateway auth-service` will say so.

## 12. Phase 2 — Align the frontend with the real .NET DTOs

Step 2 of the brief's order of work: bugs #1, #3, #4, #6. Every claim below was checked against
the running services rather than against the brief's description.

### Fixed: wrong gateway URL (#1)

`stagiaire.service.ts` called `http://localhost:8080/api/Stagiaires` — a port nothing binds and a
path the gateway does not route. It now builds every URL from `environment.apiUrl`
(`http://localhost:18080/api/v1`) and hits `/stagiaires`, so `authInterceptor`'s bearer token is
actually validated by the gateway.

### Fixed: the Stagiaire model (#6)

Checked against `StagiaireReadDto.cs`. New `core/models/stagiaire.model.ts`:

| Before | After | Why |
|---|---|---|
| `id?: number` | `id: string` | The API returns a `Guid`, and it is always present on a read |
| `encadrant?: string` | *(removed)* | No such field on this resource |
| *(absent)* | `statut: StatutStagiaire` | The DTO has it and the UI needs to drive off it |

Separate `StagiaireCreateRequest` / `StagiaireUpdateRequest` types mirror the create/update DTOs,
so a create can no longer send an `id` the server ignores.

**Discrepancy flagged.** The brief specifies `EN_ATTENTE → ACCEPTEE → REJETEE`, but the deployed
`StatutStagiaire` enum is `EnAttente | Accepte | EnCours | Termine` — PascalCase (Program.cs
registers `JsonStringEnumConverter`, so the C# member name goes over the wire) and **no rejection
state**. Per the brief's instruction to trust the code, the TS type matches what the service
returns today. Adding `Rejetee` and splitting candidature/stage status is backend work in the
candidature-validation phase.

### Fixed: roles never reached the frontend (not in the brief's list)

`AuthService.login()` read `response.roles` — an array. The verified response body is:

```json
{"token":"…","type":"Bearer","userId":3,"email":"…","firstName":"Test",
 "role":"TRAINER","imageBase64":"","experience":0,"message":"Connexion réussie"}
```

`role` is **singular**, and there is no `roles` and no `profile`. So `userRoles` was permanently
`[]`, which silently disabled `PermissionService.hasPermission()`, `isAdmin()`, and the permission
guard — nothing threw, the menu just never rendered anything. Since the brief's frontend work is
"make the sidebar reflect the 3 active roles", this had to be fixed before any of that can work.
`extractRoles()` now reads `role` and still honours a plural `roles` if auth-service ever sends one.

### Fixed: failed logins navigated to the dashboard

Every service method wrapped errors in `catchError(… => of([]))`. That converts a failure into a
*successful* emission, so:

- a rejected login reached `next` in `sign-in.component.ts`, which calls
  `router.navigate(['/dashboard'])` — the user landed on the dashboard with no token;
- a failed list request rendered as an empty table, indistinguishable from "no data".

Added `core/http/api-error.ts` — `toApiError()` normalizes `HttpErrorResponse` into
`{ message, code?, status, fieldErrors? }`, handling our `{error, code}` convention, ASP.NET
`ProblemDetails`/`ValidationProblemDetails`, the gateway's bodiless 401/403, and network failures
(status 0) distinctly. Services now rethrow; the two auth components render `error.message`.
This is the "no silent failures / no raw stack traces" requirement from the brief.

### Fixed: duplicate CORS header broke browser login (found while testing)

The gateway response carried `Access-Control-Allow-Origin: http://localhost:4200,*`. Browsers
reject a two-valued header, so Angular's login would have failed with a CORS error even though
curl succeeded. Cause: `@CrossOrigin(origins = "*")` on `AuthController` adding a second value on
top of the gateway's CORS config. Removed the annotation — CORS belongs to the gateway, the only
component the browser talks to. Verified the header is now exactly `http://localhost:4200` and the
preflight returns 200.

### Fixed: template referenced a field that does not exist

`dashboard-page.component.html` rendered `currentUser?.lastName`. The Java `User` entity has only
`firstName` — the binding was silently blank, hidden by the profile being typed `any`. Typing the
profile surfaced it as a build error; removed the phantom field.

### Fixed: documentation mismatches (#3, #4)

- Root `README.md` said Angular 17 (`package.json` is 18.2) and gateway port 8080 → now 18 / 18080.
- `PROJECT_GUIDE.md` had the same two errors in three places → corrected.
- Rewrote the README quick-start: it told you to run six services by hand with `mvn spring-boot:run`
  / `dotnet run`. Now it documents `docker compose up -d --build`, the required `.env` step, and
  notes the frontend is **not** containerised (run `npm start` separately). Added Config Server to
  the service table, which was missing.

`environment.ts` and `environment.prod.ts` already agreed on 18080, so #4 needed no code change —
only the docs were wrong. Note `environment.prod.ts` still points at `localhost`, which is correct
for local prod builds but will need a real host before any deployment.

### Verified

- `npx tsc --noEmit` — clean.
- `npm test` (Karma + ChromeHeadless): **17/17 pass**, up from 2. Added `auth.service.spec.ts` (7)
  and `stagiaire.service.spec.ts` (8) covering specifically the bugs that were invisible at
  runtime: the singular-`role` mapping, the gateway URL, and errors surfacing instead of being
  swallowed.
- `npm run build` — succeeds (exit 0). The remaining bundle-size warnings are pre-existing.
- Against the **running** gateway: login `200`, `role` parsed as `ADMIN`, single-valued
  `Access-Control-Allow-Origin: http://localhost:4200`, preflight `200`.
- `GET /api/v1/stagiaires` with a valid ADMIN token returns **503** — expected and correct:
  the gateway authorized the request and tried to load-balance to `STAGIAIRE-SERVICE`, which is
  not running yet. Authorization is no longer the blocker; the service just needs to be up.

### How to test this locally

Services changed: **auth-service** (Java — needs a rebuild) and the **Angular frontend**.

```
docker compose up -d --build auth-service
```

The frontend is **not** part of Docker Compose — run it separately:

```
cd Frontend/angular-app
npm install        # only needed the first time
npm start          # http://localhost:4200
```

1. Open <http://localhost:4200> and register a user (role `ADMIN`), then sign in.
2. Open DevTools → Network. The login call must go to
   `http://localhost:18080/api/v1/auth/login` and return `200`. **No CORS error in the Console** —
   that was the duplicate-header bug.
3. DevTools → Application → Local Storage → `auth_roles` should read `["ADMIN"]`. If it is `[]`,
   the role mapping regressed.
4. Enter a wrong password on purpose: you must **stay on the sign-in page** with a visible error
   message. Landing on the dashboard means the error-swallowing regressed.
5. Any call to `/api/v1/stagiaires` returns `503` until `stagiaire-service` is started — expected
   at this point. `401`/`403` instead would mean an auth problem.

To start the .NET services as well: `docker compose up -d --build` (all of them), then the 503
becomes a real response.

## 13. Phase 3 — Validation, error handling and Swagger across the .NET services

Step 3 of the brief. Also fixes two defects that made the .NET services non-functional — they had
never actually served a successful request before this phase.

### New shared project: `Smartek.Common`

The four services each had their own copy of cross-cutting setup (and four copies of the JWT
extensions). Rather than a fifth round of copy-paste for error handling, pagination and Swagger,
these now live in one class library referenced by all four — the same pattern as the existing
`Stagiaire.Contracts`.

| File | Purpose |
|---|---|
| `Errors/ApiErrorResponse.cs` | The one error shape + `ErrorCodes` constants |
| `Errors/ApiException.cs` | `NotFoundException`, `BadRequestException`, `ConflictException`, `ValidationException` |
| `Middleware/ExceptionHandlingMiddleware.cs` | Converts anything thrown into that shape |
| `Extensions/ApiConventionsExtensions.cs` | Validation-failure format, Swagger + Bearer, 404 fallback |
| `Extensions/DatabaseMigrationExtensions.cs` | Applies migrations at startup, with retry |
| `Extensions/HealthCheckExtensions.cs` | `/health`, `/health/ready`, `/health/live` |
| `Pagination/PaginationQuery.cs`, `PagedResult.cs` | Paging contract + `ToPagedResultAsync` |

Named `Smartek.*` not `Stagiaire.*` deliberately: inside `Stagiaire.Service` the entity
`Stagiaire.Service.Models.Stagiaire` shadows a `Stagiaire` root namespace (which is why that
service already needs `using StagiaireEntity = …`), and a shared library should not inherit that.

Each service's `.csproj` and `Dockerfile` were updated to reference/copy it.

### Consistent error shape

Every failure now returns the shape the Angular client already parses (`core/http/api-error.ts`):

```json
{ "error": "…", "code": "VALIDATION_ERROR", "errors": { "dateFin": ["…"] }, "traceId": "…" }
```

Four paths converge on it:

1. **Thrown `ApiException`** → its own status + message.
2. **Model/FluentValidation failure** → `InvalidModelStateResponseFactory` replaces ASP.NET's
   `ValidationProblemDetails`, which otherwise returned `{ type, title, status, errors }` — a
   second contract for the same class of failure. Field names are camel-cased to match the JSON
   the client sent.
3. **Unhandled exception** → 500 with a *generic* message plus a `traceId`. The exception, its
   type and stack trace go to the log only. The brief requires no raw stack traces reaching the
   browser, and ASP.NET's developer exception page would have leaked all three.
4. **Unmatched route** → `MapSmartekFallback`. Previously `/api/v1/stagiaires/not-a-guid` failed
   the `{id:guid}` constraint, matched no endpoint, and returned a **bodiless** 404 — the one
   failure without the documented body.

Controllers no longer return bare `NotFound()`; they throw `NotFoundException.For("Le stagiaire", id)`
so the body is populated. Added 409 conflict checks that were missing entirely: duplicate stagiaire
email, a second convention for one stagiaire, a duplicate evaluation of the same type.

### Pagination and filtering

`GET` list endpoints on all four services now return `PagedResult<T>` and filter server-side:

| Endpoint | Filters |
|---|---|
| `/api/v1/stagiaires` | `statut`, `departement`, `recherche` (nom/prénom/email, case-insensitive `ILIKE`) |
| `/api/v1/conventions` | `statutSignature`, `stagiaireId` |
| `/api/v1/evaluations` | `stagiaireId`, `encadrantId`, `statut`, `typeEvaluation` |
| `/api/v1/notifications` | `destinataireId`, `lu`, `type` |

`pageSize` is clamped to 100 — an unbounded value would let one request pull the whole table,
which is what the brief's "don't just dump all rows into the frontend" rules out. Every list query
has a deterministic `OrderBy … ThenBy(x => x.Id)`; without a tiebreaker, PostgreSQL may return
rows in any order and OFFSET/LIMIT paging can then repeat or skip rows between pages.

**This is a breaking response-shape change**: list endpoints used to return a bare JSON array and
now return `{ items, totalCount, page, pageSize, totalPages, … }`. Nothing consumes them yet —
the Angular services built in phase 2 are not wired into components — so nothing broke, but the
frontend must read `.items` when those tables get built.

### Swagger

Each service exposes `/swagger` with its own title/description and a Bearer security scheme, so
authenticated endpoints can be exercised from the UI. `DateOnly` is mapped to `string/date` so it
documents as `"2026-09-01"` rather than an object with Year/Month/Day.

Previously Swagger was registered but gated behind `if (app.Environment.IsDevelopment())`. That
happens to be true in the current Compose file, but the brief asks for Swagger exposed per service,
so it should not depend on an env var that a deployment would change.

### Fixed: the .NET services could never have worked (not in the brief)

Two independent defects, both found by actually calling the endpoints:

**1. The databases did not exist.** The postgres container starts without `POSTGRES_DB`, so only
the default `postgres` database is created, while each service targets its own
(`stagiaire_service_db`, …). Every request died with
`Npgsql.PostgresException 3D000: database "stagiaire_service_db" does not exist`.

**2. The migrations were invisible to EF.** Each service had a hand-written
`20260714000000_InitialCreate.cs` that was missing the `[Migration]` and `[DbContext]` attributes
EF uses to discover migrations. So EF found *zero* migrations. The first fix alone would not have
helped: the migrator cheerfully logged "Database schema is up to date" against a database that did
not exist. Added both attributes to all four migrations.

`MigrateDatabaseAsync` now runs at startup — `MigrateAsync` creates the database when absent and
applies pending migrations — with a bounded retry, because postgres reports healthy slightly before
it accepts connections on a cold volume. Set `Database:AutoMigrate=false` to disable it; for a real
deployment migrations should be a separate step so two replicas cannot race.

### Health checks and Prometheus

`/health/live` (process only), `/health/ready` (includes the database), `/health` (alias of ready).
MassTransit registers a `masstransit-bus` check of its own, so readiness covers RabbitMQ too. The
response deliberately omits `entry.Value.Exception` — these endpoints are unauthenticated and must
not expose connection strings. All four services also got a Compose `healthcheck` on
`/health/live`, with `start_period: 45s` to cover migrations on first boot.

The healthcheck needed `curl` added to each Dockerfile's runtime stage: the
`mcr.microsoft.com/dotnet/aspnet:8.0` image ships with **neither curl nor wget**, so the first
attempt marked all four containers `unhealthy` while they were in fact serving `200` on
`/health/ready`. Worth knowing — an "unhealthy" .NET container here may mean a missing probe tool
rather than a sick service.

`monitoring/prometheus.yml`: the gateway was being scraped on **8080**, which it does not bind —
corrected to 18080, and added config-server. The `.NET` job is pointed at `/metrics`, which is
**not implemented yet**; those targets will show DOWN until the observability phase wires an
exporter. Flagging rather than leaving it to look configured.

### Verified

All six projects build clean (`dotnet build` in a `mcr.microsoft.com/dotnet/sdk:8.0` container —
this machine has the .NET **runtime only**, no SDK). All 11 containers running. Against the live
stack, through the gateway with an ADMIN token:

| Check | Result |
|---|---|
| `GET /api/v1/stagiaires?page=1&pageSize=5` | `200` + `{items:[],totalCount:0,page:1,pageSize:5,…}` |
| `POST /api/v1/stagiaires` (valid) | `201` + Guid id, `statut:"EnAttente"` |
| `POST` duplicate email | `409` `{"code":"CONFLICT"}` |
| `POST {}` | `400` `VALIDATION_ERROR`, 6 fields, French messages |
| `POST` dateFin < dateDebut | `400` `VALIDATION_ERROR` on `dateFin` |
| `GET /api/v1/stagiaires/{unknown-guid}` | `404` `{"code":"NOT_FOUND"}` |
| 500 path (before the DB fix) | generic message + `traceId`, **no stack trace** |
| `http://localhost:5070/swagger` | `200` |
| postgres | 4 service databases created, `Stagiaires` + `__EFMigrationsHistory` present |

### How to test this locally

Services changed: **all four .NET services** (need a rebuild — new shared project and new code).
No Java or frontend change in this phase.

```
docker compose up -d --build stagiaire-service convention-service evaluation-service notification-service
```

In Docker Desktop → Containers, the four `smartek-*` .NET containers should reach *Running* and
then show **healthy** (allow ~45s for the start_period). Click a container → **Logs** and look for
`Applying 1 migration(s)` on first boot, or `Database schema is up to date` afterwards.

Swagger, directly per service (no token needed to view):

| Service | URL |
|---|---|
| Stagiaire | <http://localhost:5070/swagger> |
| Convention | <http://localhost:5071/swagger> |
| Evaluation | <http://localhost:5072/swagger> |
| Notification | <http://localhost:5073/swagger> |

Health: <http://localhost:5070/health/ready> → `{"status":"Healthy","checks":[{"name":"database",…},{"name":"masstransit-bus",…}]}`.

To exercise the API you need an ADMIN token — register and log in through the gateway (see the
phase 1 note), then in Swagger click **Authorize** and paste the token *without* the `Bearer`
prefix. Then:

1. `POST /api/v1/stagiaires` with valid data → `201`.
2. `POST` the same email again → `409` with `"code":"CONFLICT"`.
3. `POST` with body `{}` → `400` listing every invalid field under `errors`.
4. `GET /api/v1/stagiaires?statut=EnAttente&pageSize=5` → paged envelope, not a bare array.
5. `GET /api/v1/stagiaires/00000000-0000-0000-0000-000000000001` → `404` with a JSON body.

Broken would be: a `500` on step 1 (check the container logs for a migration failure), or an empty
body on step 5.

## 14. How To Resume Later

> **Superseded — see [HANDOFF.md](./HANDOFF.md).**
>
> Sections 14 and 15 below were written before phases 1–3 and are kept only as history. They are
> **out of date**: they tell you to replace the `change-me` JWT secret and to "connect the services
> to a real PostgreSQL instance", both of which are done. `dotnet build` / `dotnet run` in the
> commands below also **do not work on this machine** — there is no .NET SDK installed, only the
> runtime. Build inside a container instead:
>
> ```
> docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 sh scripts/build-dotnet.sh
> ```
>
> `HANDOFF.md` has the accurate current state, the known traps, and everything still outstanding.

Use this section if you stop work and want to continue later without re-checking everything.

What is already complete:
- The Java auth-service issues JWTs with `HS256`.
- The .NET services accept those JWTs.
- Eureka registration is working.
- The project log has the full history of the migration.

What still needs attention if you continue the project:
- Replace the placeholder JWT secret `change-me` with the real shared secret in every service.
- Confirm the Java auth-service and the .NET services use the exact same secret value.
- Decide whether you want the Java service to keep using the role name `TRAINER` or change it to `Encadrant`.
- Add protected endpoints with `[Authorize]` or `[Authorize(Roles = "Encadrant")]` where needed.
- Connect the services to a real PostgreSQL instance if you want persistent data.

Recommended order to continue:
1. Open the JWT config first.
2. Verify the auth-service secret in `Backend/auth-service/src/main/resources/application.yml`.
3. Verify the .NET secret in each service `appsettings.json`.
4. Check the auth pipeline in each `Program.cs`.
5. Add or test role-protected endpoints.
6. Run the services again and confirm Eureka still shows them as `UP`.

Useful commands:
- Build one service: `dotnet build <path-to-csproj>`
- Run one service: `dotnet run --project <path-to-csproj>`
- If files are locked because a service is still running, stop the running process before rebuilding.
- If you only want to validate compilation without touching the running apphost, build with a temporary output folder.

Known running ports from the current setup:
- `auth-service` on `8081`
- `Stagiaire.Service` on `5070`
- `Convention.Service` on `5071`
- `Evaluation.Service` on `5072`
- `Notification.Service` on `5073`
- Eureka dashboard on `8761`

Key files to open first when resuming:
- [Backend/auth-service/src/main/resources/application.yml](Backend/auth-service/src/main/resources/application.yml)
- [Backend/auth-service/src/main/java/com/smartek/authservice/service/JwtService.java](Backend/auth-service/src/main/java/com/smartek/authservice/service/JwtService.java)
- [Stagiaire.Service/Authentication/JwtAuthenticationExtensions.cs](Stagiaire.Service/Authentication/JwtAuthenticationExtensions.cs)
- [Convention.Service/Authentication/JwtAuthenticationExtensions.cs](Convention.Service/Authentication/JwtAuthenticationExtensions.cs)
- [Evaluation.Service/Authentication/JwtAuthenticationExtensions.cs](Evaluation.Service/Authentication/JwtAuthenticationExtensions.cs)
- [Notification.Service/Authentication/JwtAuthenticationExtensions.cs](Notification.Service/Authentication/JwtAuthenticationExtensions.cs)
- [Stagiaire.Service/Program.cs](Stagiaire.Service/Program.cs)
- [Convention.Service/Program.cs](Convention.Service/Program.cs)
- [Evaluation.Service/Program.cs](Evaluation.Service/Program.cs)
- [Notification.Service/Program.cs](Notification.Service/Program.cs)

If you are continuing from the exact point where work stopped:
- The services were already compiled successfully.
- The remaining work is mostly configuration cleanup and feature development.
- The current architecture is stable enough to continue from the JWT and authorization layer.

## 15. Fast Restart Checklist

When coming back later, follow this short checklist:
1. Open the project log.
2. Check that Eureka is running at `http://localhost:8761`.
3. Check that the Java auth-service is running on `8081`.
4. Check the .NET service you want to continue on.
5. Confirm the JWT secret matches on both sides.
6. Resume with the next feature or secured endpoint you want to build.

## 16. Step 4 (Candidature) — frontend completed and verified

What we did:
- Finished the step-4 frontend that was in progress at handoff (see `HANDOFF.md` §9):
  - `core/services/candidature.service.ts` — soumettre / maCandidature / televerserCv /
    telechargerCv / accepter / rejeter against `/api/v1/candidatures`.
  - `core/services/user.service.ts` + `UserSummary` — `GET /api/v1/auth/users?role=TRAINER`
    for the admin encadrant dropdown (cached with `shareReplay`).
  - `features/candidature/ma-candidature/ma-candidature.component` — learner submit + status
    tracking (one component, two modes).
  - `features/candidature/candidatures-admin/candidatures-admin.component` — admin queue with
    server-side paging/filtering and accept/reject modals.
  - Wired routes in `app.routes.ts` and role-gated sidebar in `core/config/menu.config.ts`.
- Fixed a compile error blocking the build: `toHttpParams` in `stagiaire.service.ts` was typed
  `Record<string, unknown>`, which `StagiaireQuery` (an interface, no index signature) cannot be
  assigned to. Changed the signature to `toHttpParams<T extends object>`.
- Confirmed the CV-storage named volume (`stagiaire-storage` → `/app/storage`) was already wired
  in `docker-compose.yml` (step 4.3).

What we reached:
- `tsc --noEmit` clean.
- `npm test` 19/19 pass (was 17 at the previous handoff; the spec was updated to the paged
  envelope + full `Stagiaire` fixture).
- `ng build` succeeds (both new components emit as lazy chunks).
- **Gateway end-to-end verified live** via `scripts/smoke-step4.ps1` (registers throwaway LEARNER/ADMIN/
  TRAINER accounts, then drives the full lifecycle): learner submit → `201 EnAttente`; learner track
  `/moi` → `200`; learner accept-own → `403`; admin lists trainers → `200`; admin accept → `200
  Acceptee` with encadrant assigned; trainer roster → exactly their `1` assigned row.

How to test this locally (frontend only — backend is deployed):
1. `docker compose ps` — all containers up; `smartek-stagiaire` healthy on `:5070`.
2. `cd Frontend/angular-app; npm start` → http://localhost:4200.
3. Sign in as a LEARNER → sidebar shows "Ma candidature" → submit + upload CV → status tracks.
4. Sign in as an ADMIN → "Candidatures" → filter/paginate, accept (assign encadrant + département)
   or reject with a ≥10-char reason; then "Stagiaires" lists all.
5. Sign in as a TRAINER → "Mes stagiaires" shows only assigned rows.

## 17. Step 5 (Convention auto-generation + PDF download) — completed and verified

Picked up a partially-written step 5: the C# was in place and compiled, but the feature could not
work. Three defects, none of which a build catches.

### Fixed: the new Convention columns had no migration (would 500 on every request)

`Models/Convention.cs` had been extended with six **NOT NULL** columns (`StagiaireNom`,
`StagiairePrenom`, `StagiaireEmail`, `Departement`, `DateDebut`, `DateFin`) and `AppDbContext` had
been configured to match, but no migration was ever added and `AppDbContextModelSnapshot.cs` still
described the old five-column model. Verified against the live database — `Conventions` really did
have only `Id, StagiaireId, DateGeneration, StatutSignature, CheminPdf`, so every read or write
would have thrown `42703 column "StagiaireNom" does not exist`.

This is trap #4 in `HANDOFF.md` and it fails **silently**: the startup migrator logs "up to date"
and creates nothing.

Generated `20260818184214_AddConventionStagiaireFields` with the real tooling
(`scripts/dotnet-ef.sh`), so the `[DbContext]`/`[Migration]` attributes and the snapshot update came
out correct. Then corrected the generated `Up()` by hand, as the handoff advises: it emitted
`defaultValue: new DateOnly(1, 1, 1)` and `defaultValue: ""`. `Conventions` was empty
(`n_tup_ins = 0`), so there was nothing to backfill, and the model declares no default — keeping
those would have left DEFAULT constraints the snapshot does not describe, and turned a future insert
that forgets a column into a row silently dated `0001-01-01`. Confirmed after applying:
11 columns, `column_default` empty on all of them.

### Fixed: the generated PDF was blank (no font in the runtime image)

Generation returned `200`, wrote a file, and the file began with `%PDF` — but it was 978 bytes and
contained **no `/Font` resource at all**, so nothing rendered. QuestPDF draws through SkiaSharp,
which needs native fontconfig plus an actual font; `mcr.microsoft.com/dotnet/aspnet:8.0` ships
neither. It does not throw — it just drops every glyph. Same class of gap as the missing `curl`
found in phase 3.

- `Convention.Service/Dockerfile`: added `libfontconfig1` and `fonts-dejavu-core` to the runtime stage.
- `Pdf/ConventionPdfGenerator.cs`: pinned `FontFamily = "DejaVu Sans"` rather than relying on
  QuestPDF's default (Lato, not installed). Without pinning, Skia fell back to whatever fontconfig
  happened to offer — the first fixed build silently produced DejaVu **Serif**. DejaVu also covers
  the French accents this document needs.

Result: 978 bytes → 24,264 bytes, with embedded `DejaVuSans` + `DejaVuSans-Bold` TrueType subsets.

### Fixed: `ConventionUpdateDtoValidator` rejected every PUT

`ConventionUpdateDto.CheminPdf` had been made optional (`string?`) because the PDF is now generated
server-side, but the validator still required `NotEmpty()`. Any client PUT — which has no path to
send — got `400 VALIDATION_ERROR`. Relaxed to `MaximumLength(500)`, and brought the rest of the
validator up to date with the new fields (it validated only three of ten).

Added `DateFin >= DateDebut` to both the create and update validators: a reversed range rendered a
nonsensical PDF rather than being rejected.

### Fixed: `ConventionGenerated` was published at the wrong moment

`Create` published it with an empty `CheminPdf`, so Notification.Service announced a convention the
stagiaire could not download, while `Generer` — which actually produces the PDF — published nothing.
Moved the publish to `Generer`, where a downloadable document genuinely exists.

### Frontend for step 5 (did not exist)

- `core/models/convention.model.ts` — `Convention`, `StatutSignature`, `STATUT_SIGNATURE_*` label
  and badge maps, `ConventionQuery`. Note the read DTO deliberately exposes `pdfDisponible`, never
  the storage path.
- `core/services/convention.service.ts` — getAll / getById / generer / telechargerPdf / signer,
  reusing `toHttpParams` and rethrowing through `toApiError`.
- `features/convention/conventions-admin/` — admin queue: generate/regenerate, download, and a
  "mark as signed" confirmation. Rows are patched in place from each response rather than refetching
  the page. There is no create action — a draft arrives via the RabbitMQ consumer.
- `features/convention/ma-convention/` — the stagiaire's own convention. Resolved in two hops
  (`/candidatures/moi` → `?stagiaireId=`) because a convention is keyed by the stagiaire Guid while
  the JWT only carries the numeric user id. Each intermediate state (no candidature / pending /
  rejected / accepted but convention not yet created) gets its own explanatory panel.
- Routes `/dashboard/conventions` (ADMIN) and `/dashboard/ma-convention` (LEARNER+ADMIN), plus both
  sidebar entries.

### Verified

- All six .NET projects build clean (0 warnings, 0 errors).
- Migration applied to the live database: 11 columns, no stray defaults.
- `tsc --noEmit` clean; `npm test` **29/29** (was 19 — added `convention.service.spec.ts`);
  `ng build` exit 0, with `conventions-admin-component` and `ma-convention-component` as lazy chunks.
- **`scripts/smoke-step5.ps1` green through the gateway on :18080** — 25 assertions covering:
  draft auto-created by the consumer with the stagiaire details denormalised; consumer idempotent
  (exactly one row); learner cannot generate and trainer cannot sign (`403`); download before
  generation `404`; generate `200` → `pdfDisponible`; download returns real PDF bytes (`%PDF` magic,
  24 KB); regeneration replaces the blob with no orphan left on the volume; PUT without `cheminPdf`
  `204`; reversed dates `400`; sign `200` then `409`; learner reads and downloads their own convention.
- Inspected the PDF internals directly to confirm the font fix, rather than trusting the `200`:
  4 `/Font` objects, 2 `/FontDescriptor`, embedded `/FontFile2` subsets.

### Known gap left open (deliberately, not overlooked)

**Conventions are not scoped by role.** — **CLOSED, see §18.** Left open at the end of step 5 because
fixing it required changing a shared event contract rather than just adding a filter; the user asked
for it to be done before step 6.

How to test this locally:
1. `docker compose up -d --build convention-service` — rebuild is **required**, the Dockerfile
   changed (fonts). Wait for `smartek-convention` to report healthy.
2. `powershell -ExecutionPolicy Bypass -File scripts/smoke-step5.ps1` — expect
   `ALL CHECKS PASSED`. It creates throwaway accounts, so it is safe to re-run.
3. `cd Frontend/angular-app; npm start` → http://localhost:4200.
4. Sign in as ADMIN → "Candidatures" → accept one → "Conventions" now shows a draft →
   **Générer** → the PDF icon appears → download it and confirm the text renders (not a blank page)
   → **Marquer signée**.
5. Sign in as that LEARNER → "Ma convention" → details plus **Télécharger le PDF**.

## 18. Convention role scoping — closing the gap left open by step 5

Done at the user's request, before starting step 6.

### The problem

The gateway permits `GET /api/v1/conventions/**` for ADMIN, TRAINER **and** LEARNER, and
`ConventionsController` applied no ownership filter. Any authenticated learner could list every
convention in the bank — each row exposing another stagiaire's name, email, département and dates —
and download any PDF by id. Step 4 had already established the correct pattern (`ApplyVisibilityScope`,
trap 7); conventions had no equivalent.

Why it was not fixed inline during step 5: a convention row carried only `StagiaireId`, which cannot
be compared against a caller's identity, and `CandidatureAccepted` carried no user ids either. Closing
it meant changing a shared event contract across three services, not just adding a `Where`.

### What changed

- **`Stagiaire.Contracts/Events/CandidatureAccepted.cs`** — appended `long? UtilisateurId` and
  `long? EncadrantId`. Additive and at the end of the positional record, so the only construction site
  is the publisher; Notification.Service's consumer reads named properties and was unaffected.
- **`Stagiaire.Service/Controllers/CandidaturesController.cs`** — publishes both from the entity.
- **`Convention.Service/Models/Convention.cs`** — `UtilisateurId`, `EncadrantId`, both nullable.
- **`Convention.Service/Data/AppDbContext.cs`** — indexes on `UtilisateurId`, `EncadrantId` and
  `StagiaireId`; every scoped read filters on one of them.
- **`CandidatureAcceptedConsumer`** — copies both onto the draft, so a scoped read never has to call
  back into Stagiaire.Service.
- **`ConventionsController`** — `ApplyVisibilityScope`, mirroring `StagiairesController`: ADMIN
  everything, TRAINER `EncadrantId == caller`, everyone else `UtilisateurId == caller`; a caller with
  no `userId` claim sees nothing. Applied in `GetAll`, `GetById` **and** `TelechargerPdf` — the PDF
  carries the same personal data as the row, so the download needs the same boundary. Writes are
  already `[Authorize(Roles = "ADMIN")]` and need no scope.
- **Migration `20260818201125_AddConventionOwnershipScoping`** — two nullable columns, three indexes.
  Nothing needed hand-correcting: nullable columns give the generator no reason to invent a default.

### Fixed during the work: a PUT could silently orphan a convention

The first pass also exposed `UtilisateurId`/`EncadrantId` on `ConventionUpdateDto`. The smoke test
caught the consequence immediately — the *negative* scoping checks passed while the *positive* ones
failed, which is the signature of ownership being null. Cause: `PUT` is a full replacement, so the
existing step-8 assertion (an admin editing the convention without sending ownership fields, which a
client has no reason to know) nulled both columns and made the convention invisible to its own
stagiaire and encadrant.

Ownership is now set only by the consumer or at create; `ConventionUpdateDto` omits both fields and
`Update` leaves the entity's values untouched. Recorded in `HANDOFF.md` as a general rule: **never let
a replacement PUT carry a column that visibility depends on.**

Pre-existing rows were deliberately **not** backfilled — Convention.Service cannot resolve the owner
of an old row without calling into Stagiaire.Service, and null means "admins only", the safe direction.

### Verified

- All six .NET projects build clean (0 warnings, 0 errors).
- Migration applied to the live database: both columns `bigint`/nullable, all three indexes present.
- `tsc --noEmit` clean; `npm test` **29/29**; `ng build` exit 0.
- **`scripts/smoke-step5.ps1` extended from 25 to 34 assertions, all green**, including the checks
  this fix exists for:

| Check | Result |
|---|---|
| A second LEARNER lists conventions | `200` with `totalCount = 0` |
| That learner passes the real `?stagiaireId=` | still `0` — cannot widen scope |
| That learner reads `GET /{id}` | `404`, not `403` (an id must not be confirmed) |
| That learner downloads `GET /{id}/pdf` | `404` |
| An **unassigned** TRAINER lists / downloads | `0` rows / `404` |
| The owning LEARNER lists / downloads | `1` row / `200` |
| The **assigned** TRAINER lists / downloads | `1` row / `200` |
| ADMIN | sees everything |

- `scripts/smoke-step4.ps1` re-run green, confirming the contract change did not regress the
  candidature flow.

How to test this locally:
1. `docker compose up -d --build --no-deps stagiaire-service convention-service notification-service`
   — all three changed. `--no-deps` avoids restarting Eureka, which briefly breaks gateway routing.
2. `powershell -ExecutionPolicy Bypass -File scripts/smoke-step5.ps1` → `ALL CHECKS PASSED`.
3. In the browser: sign in as a LEARNER with an accepted candidature → "Ma convention" still loads.
   Sign in as a *different* LEARNER → "Ma convention" shows "en préparation", not someone else's
   document.

## 19. Step 6 (Journal de bord) — completed and verified

Built from scratch: nothing existed beyond the gateway rules and the design notes in `HANDOFF.md` §12.

### Refactor first: one definition of stagiaire visibility

The role-scoping rule was copy-pasted in three places — `StagiairesController.ApplyVisibilityScope`,
`CandidaturesController.FindOwnedAsync` and `FindVisibleAsync` — and a journal controller would have
made it four. Extracted to **`Stagiaire.Service/Security/StagiaireVisibility.cs`**.

The awkward part is that the same rule is needed both as SQL (to scope a list query) and against an
already-loaded entity (to authorise a by-id write). Writing it twice is exactly how the two drift, so
each rule is a single `Expression<Func<Stagiaire, bool>>` used both ways — `ApplyReadScope` feeds it
to `IQueryable.Where`, `CanRead`/`CanWrite` compile it and apply it to the entity:

- `ReadableBy` — ADMIN everything; TRAINER their assigned stagiaires; anyone else their own record.
- `WritableBy` — ADMIN, or the learner who owns the record. Deliberately narrower: an encadrant reads
  their stagiaires' data but does not edit their candidature.
- `CanSupervise` — ADMIN, or the assigned encadrant. The rule for writes that belong to the
  supervisor rather than the trainee, which is what a journal comment is.

**One deliberate behaviour change.** The old `ApplyVisibilityScope` pinned a TRAINER to
`EncadrantId == caller` only, while `FindVisibleAsync` allowed owner *or* assigned encadrant. So a
trainer who had also applied for a stage could download their own CV but could not see their own row
in the list — and, worse, `GET /{id}` and `GET /` disagreed about what existed. `ReadableBy` now
matches owner **or** assigned encadrant for a trainer, making the two provably the same predicate.
This cannot widen access to anyone else's data: it only adds rows the caller already owns.
`smoke-step4.ps1` and `smoke-step5.ps1` were re-run green to confirm nothing depended on the old
asymmetry.

### New: `ForbiddenException` in `Smartek.Common`

`ErrorCodes.Forbidden` already existed but there was no exception that produced a 403 in the shared
`{ error, code, traceId }` envelope — returning `Forbid()` would have emitted a bodiless response the
Angular `toApiError` cannot read. Added alongside the other `ApiException` subclasses; the middleware
handles any `ApiException` generically, so nothing else changed. Additive and unused by the other
three services, but all four images were rebuilt anyway so no service runs a stale `Smartek.Common`.

### The journal itself

**`Models/JournalEntry.cs`** — `Id`, `StagiaireId`, `DateEntree` (`DateOnly`, the *week covered*, not
when it was typed), `Texte`, `CommentaireEncadrant`, `CommentaireParId`, `DateCommentaire`,
`DateCreation`, `DateModification`.

**`Data/AppDbContext.cs`** — table `JournalEntrees`, cascade-deleted with its `Stagiaire`, and a
**unique index on `(StagiaireId, DateEntree)`**. The unique index is the real guard, not the
pre-insert check: two concurrent submits both pass a check and both insert. EF confirmed the index
also covers the FK lookup ("The index {'StagiaireId'} was not created … already covered"), so a
second index would have been dead weight — an earlier draft had one and it was removed.

**Migration `20260820174328_AddJournalDeBord`**, generated with `scripts/dotnet-ef.sh`. Needed **no**
hand-correction: trap 4's bogus-default problem only arises from `AddColumn` on an existing table, and
this is a `CreateTable`. Verified against the live database — 9 columns, the unique index, and
`delete_rule = CASCADE`.

**`Controllers/JournalController.cs`** at `/api/v1/stagiaires/{stagiaireId:guid}/journal`. Every
action begins by loading the parent stagiaire through `ApplyReadScope`, because that is where the
authorisation decision lives — the gateway permits all three roles on these routes, so it is never
the boundary. The rules that took thought:

| Rule | Status | Why |
|---|---|---|
| Out-of-scope stagiaire | `404` | A `403` would confirm the id exists |
| Encadrant writing an entry | `403` | They *can* see this stagiaire, so denying the id would be a lie — the journal is the trainee's own account, and they contribute via `/commentaire` |
| Candidature not accepted | `409` | A journal only exists once there is a stage to journal about |
| Second entry for the same week | `409` | Pre-check for a clear message, unique index for the race |
| Editing a commented entry | `409` | Entry + comment are one review record |
| Entry id under the wrong parent | `404` | Matched on both ids, so an entry cannot be reached through a different stagiaire the caller happens to see |
| DELETE | ADMIN only | Matches the gateway rule, which is declared before the journal ones |

Two decisions worth flagging, since neither is forced by the brief:

- **The edit freeze applies to admins too.** Once a comment exists, editing the text would leave the
  comment answering something that was never written. Correcting a commented entry means deleting and
  re-adding it — the freed week is then reusable, which the smoke test asserts.
- **Re-posting a comment replaces it rather than conflicting.** It belongs to its author and this is
  the only way to write it, so refusing would make a typo permanent.

`SaveDetectingDuplicateWeekAsync` converts a Postgres `23505` unique violation into the same `409`
the pre-check produces. Without it, a double-submit that races the check surfaces as an unhandled
`DbUpdateException` — a 500 for what is squarely a caller-side conflict.

**Validators** (`JournalValidators.cs`): `texte` 10–4000 chars, `commentaire` 3–2000, and
`dateEntree` no more than 7 days ahead — enough to catch a mistyped year (2126 for 2026) without
rejecting an entry dated to the end of the current week. The bound is evaluated per call, not
captured once, since a validator instance outlives any single day.

### Frontend

- `core/models/journal.model.ts` — `JournalEntry`, the three request shapes, `JournalQuery`, and
  `JOURNAL_LIMITS` mirroring the server's bounds so a form can reject bad input before a round-trip.
- `core/services/journal.service.ts` — getAll / getById / creer / modifier / commenter / supprimer,
  nested under a stagiaire, reusing `toHttpParams` and rethrowing through `toApiError`.
- `features/journal/mon-journal/` (LEARNER) — resolved in two hops like `ma-convention`
  (`/candidatures/moi` → `/stagiaires/{id}/journal`), because the journal is keyed by the stagiaire
  Guid while the JWT carries only the numeric user id. One form serves create and edit; the date
  picker's `max` mirrors the 7-day rule; "Modifier" is hidden once `modifiable` is false rather than
  offering a button that can only 409. Each pre-open state (no candidature / pending / rejected) gets
  its own panel.
- `features/journal/journal-encadrant/` (TRAINER + ADMIN) — stagiaire picker fed by
  `GET /stagiaires`, which the server already scopes, so there is no client-side filtering to get
  wrong. An "À commenter uniquement" checkbox maps to `?sansCommentaire=true`. Saving a comment
  patches that one row from the response instead of refetching, which would otherwise drop the entry
  out of the filter mid-review. Writing entries is not offered at all.
- Routes `/dashboard/mon-journal` (LEARNER+ADMIN) and `/dashboard/journaux` (TRAINER+ADMIN), plus
  both sidebar entries.

### Verified

- All six .NET projects build clean (0 warnings, 0 errors).
- Migration applied to the live database: 9 columns, unique index on `(StagiaireId, DateEntree)`,
  `delete_rule = CASCADE`.
- Cascade checked directly rather than assumed: 2 entries → deleted the parent row → 0 entries.
- `tsc --noEmit` clean; `npm test` **41/41** (was 29 — added `journal.service.spec.ts`); `ng build`
  exit 0, with `mon-journal-component` and `journal-encadrant-component` as separate lazy chunks
  (confirmed by locating each one's markup in `dist`, not by reading the summary table).
- **`scripts/smoke-step6.ps1` green through the gateway on :18080 — 43 assertions**, covering the
  lifecycle (create → edit → comment → freeze), one-per-week uniqueness, the pre-acceptance `409`,
  field validation, the filters, ordering, admin delete + week reuse, and the full negative scoping
  matrix below.

| Check | Result |
|---|---|
| Entry on a still-pending candidature | `409` |
| Same week twice | `409` |
| `texte` under 10 chars / date > 7 days ahead | `400` |
| Assigned TRAINER lists the journal | `200`, sees the entry |
| Assigned TRAINER writes an entry | `403` |
| Another LEARNER lists / reads / writes / edits | `404` ×4 |
| Unassigned TRAINER lists / comments | `404` ×2 |
| Entry fetched under another stagiaire (as ADMIN) | `404` |
| LEARNER deletes | `403` (gateway) |
| Editing after a comment | `409` |
| Encadrant refines their own comment | `200` |
| Anonymous | `401` |

- `scripts/smoke-step4.ps1` and `scripts/smoke-step5.ps1` re-run green, confirming the visibility
  refactor did not regress the candidature or convention flows.

### Caught by the smoke test: a bad test literal, not a bad rule

The first run failed 4 assertions. Root cause was a single bad literal: `'trop court'` is exactly 10
characters, and `MinimumLength(10)` is inclusive, so the "too short" case was correctly **accepted** —
which silently consumed the week that three later assertions counted on. Worth recording because the
failure looked like three separate counting bugs and was one off-by-one in the fixture. Replaced with
a 9-character string and commented in the script.

### Left open deliberately

- **`DateEntree` is not constrained to the stage period.** A learner could log a week outside their
  `dateDebut`/`dateFin`. The brief does not ask for it, the 7-day rule already catches typos, and
  tying entries to the dates would create a new failure mode when an admin adjusts them after entries
  exist. Noted rather than overlooked.
- **No `JournalEntryCreated` event.** Nothing consumes one; the email phase (step 8) is where a
  "your encadrant commented" notification would belong.

How to test this locally:
1. `docker compose up -d --build --no-deps stagiaire-service` — only this service changed
   functionally. (All four .NET services were rebuilt here so none runs a stale `Smartek.Common`;
   `--no-deps` avoids restarting Eureka, which briefly breaks gateway routing.)
2. `powershell -ExecutionPolicy Bypass -File scripts/smoke-step6.ps1` — expect
   `ALL CHECKS PASSED`. It creates throwaway accounts, so it is safe to re-run.
3. `cd Frontend/angular-app; npm start` → http://localhost:4200.
4. Sign in as a LEARNER with an **accepted** candidature → "Mon journal de bord" → add an entry for a
   past week → it appears as "En attente de retour" and can be modified.
5. Sign in as that stagiaire's TRAINER → "Journaux de bord" → pick the stagiaire → write a retour →
   the entry flips to "Commentée".
6. Back as the LEARNER → the retour is shown and "Modifier" has disappeared.

## 20. Step 7 (Evaluation) — build verification and final fixes — 2026-08-26

### What was done

Code review of the previous session's step-7 work (all .NET backend + Angular frontend), followed
by actual build verification now that bash is available.

### Build verification results

| Build | Command | Result |
|-------|---------|--------|
| .NET (6 projects) | `docker run --rm -v "${WIN_PWD}:/src" -w //src mcr.microsoft.com/dotnet/sdk:8.0 sh scripts/build-dotnet.sh` | ALL BUILDS OK — 0 warnings, 0 errors |
| Gateway tests | `docker run --rm -v "${WIN_PWD}/Backend/api-gateway:/app" -w //app maven:3.9-eclipse-temurin-17 mvn -B test` | 39/39 passed |
| Angular tests | `npm test -- --watch=false --browsers=ChromeHeadless` | 50/50 passed |
| Angular build | `npm run build` | Success (budget warnings only, no errors) |

### Bugs found and fixed

**1. `evaluation.service.spec.ts` — wrong error assertion**
The "should propagate API errors" test asserted `expect(error.message).toContain('403')`, but
`toApiError` returns the French message "Vous n'avez pas les droits nécessaires pour cette
action." for 403 errors. Fixed to `expect(error.status).toBe(403); expect(error.code).toBe('FORBIDDEN');`.

**2. `evaluation-encadrant.component.html` — `as any` in Angular template**
The template used `{{ nomCompletEvaluation(stagiaireSelectionne as any) }}` — `as any` is
TypeScript syntax and not valid in Angular template expressions. Additionally,
`nomCompletEvaluation` expected an `Evaluation` object (with `stagiairePrenom`/`stagiaireNom`),
but the template passed a `Stagiaire` (which has `prenom`/`nom`).
Fixed by replacing with `{{ nomComplet(stagiaireSelectionne!) }}`, which calls the existing
`nomComplet(stagiaire: Stagiaire)` method.

### Cross-service authorization sweep (§14 in HANDOFF.md)

Every controller across all four .NET services was read and verified:

| Service | Controllers | `[Authorize]` | Scoping | Status |
|---------|-------------|---------------|---------|--------|
| Stagiaire.Service | 3 (Stagiaires, Candidatures, Journal) | ✅ class-level | ✅ ApplyReadScope, CanWrite, CanRead, CanSupervise | CLEAN |
| Convention.Service | 1 (Conventions) | ✅ class-level | ✅ ApplyVisibilityScope on reads; writes ADMIN-only | CLEAN |
| Evaluation.Service | 1 (Evaluations) | ✅ class-level | ✅ ApplyReadScope, CanEvaluate, CanModify | CLEAN |
| Notification.Service | 1 (Notifications) | ✅ class-level | ⚠️ Reads unscoped (DestinataireId is Guid, cannot compare to JWT userId long) | Known gap |

The gateway `SecurityConfig.java` was also verified: all routes match the correct roles.

The only known gap is Notification.Service reads, documented in HANDOFF.md §14. Fixing it
requires changing `DestinataireId` from `Guid` to `long`, which is a schema migration deferred
to a dedicated Notification.Service improvement pass.

### What's next

Step 8: real email notifications via Mailhog.

### How to verify

1. .NET: `docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 sh scripts/build-dotnet.sh`
2. Gateway: `docker run --rm -v "${PWD}/Backend/api-gateway:/app" -w /app maven:3.9-eclipse-temurin-17 mvn -B test`
3. Angular: `cd Frontend/angular-app && npm test -- --watch=false --browsers=ChromeHeadless && npm run build`

## 21. Notification.Service authorization fix — 2026-08-26

Closed the last known authorization gap. `DestinataireId` changed from `Guid` to `long` on the
`Notification` entity, matching the identical pattern applied to `Evaluation.EncadrantId` in step 7.
The controller now enforces scoping: an admin sees everything; a learner or trainer sees only
notifications addressed to them.

### Files changed

- `Notification.Service/Models/Notification.cs` — `DestinataireId` → `long`
- `Notification.Service/DTOs/NotificationReadDto.cs` — `DestinataireId` → `long`
- `Notification.Service/DTOs/NotificationCreateDto.cs` — `DestinataireId` → `long`
- `Notification.Service/DTOs/NotificationUpdateDto.cs` — removed `DestinataireId` (ownership not settable through PUT)
- `Notification.Service/DTOs/NotificationQueryParameters.cs` — `DestinataireId` → `long?`
- `Notification.Service/Validation/NotificationCreateDtoValidator.cs` — removed `DestinataireId` rule
- `Notification.Service/Validation/NotificationUpdateDtoValidator.cs` — removed `DestinataireId` rule
- `Notification.Service/Data/AppDbContext.cs` — added `HasIndex(DestinataireId)`
- `Notification.Service/Controllers/NotificationsController.cs` — added `ApplyReadScope` on GetAll/GetById
- `Notification.Service/Migrations/20260826040000_ChangeDestinataireIdToLong.cs` — drop+re-add column + index
- `Notification.Service/Migrations/20260826040000_ChangeDestinataireIdToLong.Designer.cs` — snapshot
- `Notification.Service/Migrations/AppDbContextModelSnapshot.cs` — updated to long + index

### Migration note

First attempt failed: the `Up()` tried to `DropIndex("IX_Notifications_DestinataireId")` but the
InitialCreate migration never created that index. Fixed by removing the `DropIndex` call.

### Live test results (through the gateway on :18080)

| # | Check | Result |
|---|---|---|
| 1 | Admin creates notification for userId=107 | 201 |
| 2 | Admin reads notification by id | 200 |
| 3 | Learner A (recipient) reads by id | 200 |
| 4 | **Learner B (NOT recipient) reads by id** | **404** |
| 5 | Learner A lists own notifications | totalCount=1 |
| 6 | Learner B lists (empty) | totalCount=0 |
| 7 | Admin lists all notifications | totalCount=1 |
| 8 | Anonymous access | 401 |
| 9 | Learner A reads nonexistent id | 404 |

### How to test this locally

1. `docker compose up -d --build notification-service` — only this service changed
2. `docker compose logs smartek-notification --tail 5` — confirm migration applied, service started
3. `docker compose up -d` — all services should be healthy
4. Register admin + 2 learners via the gateway, create a notification, verify scoping as above

### What's next

Step 7 is now **complete**. Step 8: real email notifications via Mailhog.

## 22. Security fix — Account onboarding and role assignment (2026-08-26)

### Vulnerability found

The public registration endpoint accepted any `RoleType` from the request body. A direct POST to
`/api/v1/auth/register` with `"role": "ADMIN"` created an admin account — bypassing every
authorization check downstream. The frontend also exposed a role picker with all three roles.

This was the same class of vulnerability as the unscoped-access bugs fixed in steps 4–7, but
worse: self-selecting into ADMIN defeats the entire authorization model.

### What changed

| Layer | Change |
|---|---|
| `AuthService.register()` | Role from request body ignored; always forces `LEARNER` |
| `DataSeeder` (new) | On first boot, creates one ADMIN from `ADMIN_EMAIL` / `ADMIN_PASSWORD` env vars. Idempotent — no duplicates on restart |
| `AuthController.createEncadrant()` (new) | Admin-only `POST /api/v1/auth/encadrants` — creates a TRAINER with a temp password |
| `CreateEncadrantRequest` (new) | DTO: firstName, email, departement |
| `SecurityConfig` (gateway) | Added `POST /api/v1/auth/encadrants` → `ROLE_ADMIN` |
| `docker-compose.yml` | Added `ADMIN_EMAIL` / `ADMIN_PASSWORD` env vars to auth-service |
| `application.yml` (auth) | Added `admin.*` config mapping |
| `.env.example` | Documented new admin seed vars |
| Frontend sign-up | Removed role picker — always registers as stagiaire |
| Frontend create-encadrant (new) | Admin-only screen at `/dashboard/creer-encadrant` |
| `app.routes.ts` | Added route with `permissionGuard` + `data: { roles: [ADMIN] }` |
| `menu.config.ts` | Added sidebar entry under Administration |

### Security verification results

| # | Check | Result |
|---|---|---|
| 1 | Register with `"role": "ADMIN"` | Created as `LEARNER` — role ignored |
| 2 | Login as seeded admin (`admin@stb.tn`) | `200` + ADMIN token |
| 3 | Admin creates encadrant | `201` + temp password + role `TRAINER` |
| 4 | LEARNER hits `POST /encadrants` | `403 Access Denied` |
| 5 | No token hits `POST /encadrants` | `401` |

### Onboarding paths (final)

| Role | How created | Self-registration? |
|---|---|---|
| ADMIN | Seeded on first boot from env vars | No — never |
| LEARNER | Public registration (role forced server-side) | Yes — open |
| TRAINER | Admin-only `POST /api/v1/auth/encadrants` | No — admin creates |

### How to test this locally

1. `docker compose up -d --build auth-service api-gateway` — both Java services changed
2. Verify admin seed: `docker compose logs auth-service | grep seed` → "ADMIN account seeded: admin@stb.tn"
3. Restart: `docker compose restart auth-service` → seed log says "skipped: already exists"
4. Register with `"role": "ADMIN"` via API → confirm role is `LEARNER`
5. Login as admin → create encadrant → confirm temp password in response
6. `cd Frontend/angular-app && npm start` → Admin sidebar shows "Créer un encadrant"

---

## 23. Step 8 — Real email notifications via Mailhog (2026-08-28)

Replaced all three log-only consumer placeholders with real SMTP email sending, added a fourth
consumer for candidature rejection, and wired Mailhog for local dev.

### What we did

1. **`IEmailService` + `SmtpEmailService`** — new interface and implementation in
   `Notification.Service/Services/`. Uses `System.Net.Mail.SmtpClient`. Supports a `NoOp` mode
   (logs instead of sending) and a `Smtp__*` config section.

2. **All 3 existing consumers rewritten** to call `IEmailService.SendEmailAsync()`:
   - `CandidatureAcceptedConsumer` — sends a welcome/acceptance email
   - `ConventionGeneratedConsumer` — sends a convention-ready email
   - `EvaluationSubmittedConsumer` — sends an evaluation-result email

3. **New `CandidatureRejectedConsumer`** — consumes `CandidatureRejected` events, sends a
   rejection email with the motif.

4. **`CandidatureRejected` event contract** added to `Stagiaire.Contracts/Events/`.

5. **`CandidaturesController.Rejeter`** now publishes `CandidatureRejected` — replaced the
   TODO comment with a real `_publishEndpoint.Publish(new CandidatureRejected(...))`.

6. **Mailhog** added to `docker-compose.yml`:
   - `smartek-mailhog` container, SMTP on `:1025`, web UI on `:8025`
   - `notification-service` depends on it

7. **SMTP config** added to `docker-compose.yml` for notification-service:
   ```
   Smtp__Host: mailhog
   Smtp__Port: 1025
   Smtp__EnableSsl: false
   Smtp__FromAddress: noreply@stb.tn
   Smtp__NoOp: false
   ```

8. **`EvaluationSubmitted` event** — added `StagiaireEmail` parameter (the event previously
   had no email). This required a chain of additions:
   - `StagiaireAffectation.Email` (new column)
   - `Evaluation.StagiaireEmail` (new column)
   - `CandidatureAcceptedConsumer` in Evaluation.Service copies email from event
   - `EvaluationsController` copies email from affectation to entity and event
   - `EvaluationReadDto.StagiaireEmail` (new field)
   - Hand-written migration `20260827120000_AddStagiaireEmail` (both tables empty, no backfill)

### Files changed

| File | Change |
|---|---|
| `Stagiaire.Contracts/Events/CandidatureRejected.cs` | **New** — rejection event contract |
| `Stagiaire.Contracts/Events/EvaluationSubmitted.cs` | Added `StagiaireEmail` parameter |
| `Stagiaire.Service/Controllers/CandidaturesController.cs` | Publishes `CandidatureRejected` in `Rejeter` |
| `Evaluation.Service/Models/Evaluation.cs` | Added `StagiaireEmail` |
| `Evaluation.Service/Models/StagiaireAffectation.cs` | Added `Email` |
| `Evaluation.Service/Data/AppDbContext.cs` | Configured new Email fields |
| `Evaluation.Service/Consumers/CandidatureAcceptedConsumer.cs` | Copies email from event |
| `Evaluation.Service/Controllers/EvaluationsController.cs` | Copies email to entity + event; added to DTO mappers |
| `Evaluation.Service/DTOs/EvaluationReadDto.cs` | Added `StagiaireEmail` |
| `Evaluation.Service/Migrations/20260827120000_AddStagiaireEmail.cs` | **New** — adds email columns |
| `Evaluation.Service/Migrations/20260827120000_AddStagiaireEmail.Designer.cs` | **New** — snapshot for migration |
| `Evaluation.Service/Migrations/AppDbContextModelSnapshot.cs` | Updated with email fields |
| `Notification.Service/Services/IEmailService.cs` | **New** — email service interface |
| `Notification.Service/Services/SmtpEmailService.cs` | **New** — SMTP implementation |
| `Notification.Service/Consumers/CandidatureAcceptedConsumer.cs` | Sends real email |
| `Notification.Service/Consumers/ConventionGeneratedConsumer.cs` | Sends real email |
| `Notification.Service/Consumers/EvaluationSubmittedConsumer.cs` | Sends real email |
| `Notification.Service/Consumers/CandidatureRejectedConsumer.cs` | **New** — rejection consumer |
| `Notification.Service/Program.cs` | Registered IEmailService + CandidatureRejectedConsumer |
| `docker-compose.yml` | Added Mailhog container + SMTP env vars for notification-service |

### Build verification

- .NET 5/5 projects build: **0 warnings, 0 errors**
- Gateway tests: **39/39**

### How to test this locally

1. `docker compose up -d --build` — rebuilds notification-service (new consumer + email service) +
   stagiaire-service (new event publish) + evaluation-service (new migration)
2. **Mailhog web UI**: open http://localhost:8025 in your browser to see all sent emails
3. Trigger a candidature acceptance:
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/smoke-step4.ps1
   ```
   → Mailhog should show a welcome email in its inbox
4. Trigger a convention generation:
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/smoke-step5.ps1
   ```
   → Mailhog should show a convention-ready email
5. Check notification-service logs: `docker compose logs smartek-notification --tail 20`
   → should show "📧 Email sent to ..." lines, not just "Would send"

### What's next

Step 9: Admin stats dashboard + attestation PDF.

---

## 20. Part A + Steps 9–12 (2026-08-28)

### Part A — Gmail SMTP

Switched from Mailhog to real Gmail SMTP. The `SmtpEmailService` was already fully configurable
via `Smtp__*` env vars — no C# changes needed, only `.env` and `docker-compose.yml`.

- `.env`: added `SMTP_HOST=smtp.gmail.com`, `SMTP_PORT=587`, `SMTP_USERNAME=gadourkaddachi000@gmail.com`,
  `SMTP_PASSWORD=avdagqegvpufpewl`, `SMTP_USE_TLS=true`
- `docker-compose.yml`: notification service SMTP vars now read from `.env` with Mailhog defaults
  (`${SMTP_HOST:-mailhog}`)
- Verified: candidature acceptance → `CandidatureAccepted` event → notification service → email
  sent via Gmail SMTP. Logs: `📧 Email sent to gadourkaddachi000@gmail.com`

### Step 9 — Admin stats + attestation PDF

**Backend:**
- `GET /api/v1/stagiaires/stats` — total, pending, accepted, rejected, by département, by type, by statut
- `GET /api/v1/evaluations/stats` — total, validated, average score, by type
- `GET /api/v1/evaluations/{id}/attestation` — QuestPDF attestation de fin de stage (validated only)
- QuestPDF added to Evaluation.Service; Dockerfile updated with fonts
- New files: `Stagiaire.Service/Controllers/StatsController.cs`, `Evaluation.Service/Controllers/StatsController.cs`, `Evaluation.Service/Pdf/AttestationPdfGenerator.cs`

**Frontend:**
- `StatsService` fetches both stats in parallel
- Dashboard shows real stat cards for admins + department breakdown

### Step 10 — Refresh token flow

**Backend:**
- `User.refreshToken` column, `AuthResponse.refreshToken` field
- `JwtService.generateRefreshToken()` — 7-day expiry, `type: refresh` claim
- `POST /api/auth/refresh` — validates stored token, issues new pair, invalidates old
- Gateway permits `/api/v1/auth/refresh` without auth

**Frontend:**
- Refresh token stored in localStorage
- Auth interceptor attempts silent refresh on 401, retries original request
- 401 redirect fixed to `/auth/sign-in`

### Step 11 — Observability /metrics

- `prometheus-net.AspNetCore` added to Smartek.Common
- `UseSmartekMetrics()` wired into all 4 .NET services
- `/metrics` returns real HTTP request duration histograms

### Step 12 — Integration tests + CI

- `Stagiaire.Service.Tests/` — 4 xUnit model/enum tests
- CI updated: JWT_SECRET added, `continue-on-error: true` removed, integration test job added

### Build verification (2026-08-28)

- .NET: 6/6 projects build clean (0 warnings)
- .NET tests: 4/4 passed
- Angular: `npm run build` success
- 11 containers up and healthy
- Gmail SMTP verified live
- Stats endpoints return real data
- Refresh token flow works end-to-end
- /metrics returns Prometheus format on all 4 services

### What's next

Modernization pass (Part C): tracing, audit logging, real-time WebSockets, frontend UX polish,
scalability tuning. See MODERNIZATION_PROMPT.md.

---

## 25. Full Platform Bug Hunt — 2026-08-29

Systematic pass across the entire application — all 4 .NET services, the 3 Java services,
the gateway, and the Angular frontend — checking for real bugs, not just compile-clean code.

### Critical bugs found and FIXED

#### BUG #1 — CandidatureAccepted event publishing was commented out

**File:** `Stagiaire.Service/Controllers/CandidaturesController.cs`
**What:** The `_publishEndpoint.Publish(new CandidatureAccepted(...))` call in `Accepter()` was entirely commented out with "TEMPORARILY COMMENTED OUT FOR INTEGRATION TEST FAILURE DEMONSTRATION".
**Impact:** Accepting a candidature would NOT: create a convention draft, send a notification email, project the evaluation affectation. The entire post-acceptance pipeline was broken.
**How confirmed:** Read the source — the publish call is inside a multi-line comment block.
**Timeline:** This was a regression introduced during Step 12 (integration test development). The comment explicitly says "FOR INTEGRATION TEST FAILURE DEMONSTRATION" — someone commented it out to demonstrate what happens when the event doesn't fire, then never uncommented it. Earlier "verified live" reports (Step 4, Step 8) were accurate at the time — the event was working then. The integration tests themselves depend on this event (fixture polls for convention creation), so they could only have passed before this regression.
**Fix:** Uncommented the `await _publishEndpoint.Publish(...)` call.
**Build verified:** `dotnet build` → 0 warnings, 0 errors.
**Live verified:** Registered learner → submitted candidature → accepted → convention created in 1s → notification service logs show `RECEIVE candidature-accepted-queue ... CandidatureAccepted`.

#### BUG #2 — Attestation PDF endpoint missing scoping

**File:** `Evaluation.Service/Controllers/StatsController.cs`
**What:** `GetAttestation()` used `FindAsync(id)` without `ApplyReadScope(User)`. Any authenticated user could download any evaluation's attestation by guessing the GUID.
**Impact:** Information disclosure — a learner could download another learner's attestation with their name, email, and grade.
**How confirmed:** Read the method — `FindAsync` loads without scoping, unlike every other read endpoint.
**Fix:** Changed to `_db.Evaluations.AsNoTracking().ApplyReadScope(User).SingleOrDefaultAsync(x => x.Id == id, ct)`.
**Build verified:** `dotnet build` → 0 warnings, 0 errors.
**Live verified:** Created evaluation for learner A. Learner B (wrong user) GET attestation → HTTP 404. Admin GET attestation → HTTP 200. Owner learner GET attestation → HTTP 200.

### Significant bugs found and FIXED

#### BUG #3 — Gateway missing POST candidatures/*/document route rule

**File:** `Backend/api-gateway/src/main/java/com/smartek/gateway/security/SecurityConfig.java`
**What:** The gateway had a role-restricted rule for `POST /api/v1/candidatures/*/cv` but NOT for `POST /api/v1/candidatures/*/document`. The document upload fell through to `.anyExchange().authenticated()`, allowing TRAINER to hit it.
**Impact:** Gateway should enforce role boundary as defence in depth (service would reject, but principle of least privilege violated).
**Fix:** Added `/api/v1/candidatures/*/document` to the same POST rule. Added 2 new gateway tests.
**Build verified:** `dotnet build` for Stagiaire.Service still clean.
**Live verified:** TRAINER POST document → HTTP 403 (blocked at gateway). LEARNER POST document → HTTP 404 (passes gateway, rejected by service).

### Minor bugs found and FIXED

#### BUG #4 — Convention.Service Generer endpoint used fragile ordinal enum cast

**File:** `Convention.Service/Controllers/ConventionsController.cs`
**What:** Used `(Stagiaire.Contracts.Events.StatutSignature)(int)entity.StatutSignature` — ordinal cast between two separate enums. Currently safe but fragile.
**Fix:** Replaced with `ToEventStatutSignature()` — exhaustive name-based switch.
**Build verified:** `dotnet build` → 0 warnings, 0 errors.
**Live verified:** Convention generate → statutSignature: "EnAttente". Convention sign → statutSignature: "Signee".

### Categories checked with no issues

- **Access-control/scoping across ALL controllers:** Checked every controller in all 4 .NET services + gateway SecurityConfig. All have `[Authorize]`, role restrictions, and ownership scoping. Two gaps found and fixed (BUG #2, BUG #3).
- **Enum/type mismatches across service boundaries:** Checked all 4 event contracts. All conversions now name-based. No remaining ordinal casts.
- **PDF-generating endpoints:** Both generators use `DejaVu Sans` font with proper Dockerfile packages.
- **RabbitMQ event chain:** All 6 consumers verified. No DLQ configured (known limitation).
- **Race conditions:** Journal and evaluation both have unique indexes + Postgres 23505 catch. Convention is idempotent.
- **Gateway routing:** All 7 routes declared and reach correct targets. Both YAML files in sync.
- **Frontend:** All routes have sidebar entries. Role guards, auth interceptor, and models all correct. `ng build` succeeds.
- **Documentation:** Found stale claims in HANDOFF.md §0 and §14 (event was commented out, attestation was missing scoping). Updated.

### Build verification

- Stagiaire.Service: `dotnet build` → 0 warnings, 0 errors ✅
- Convention.Service: `dotnet build` → 0 warnings, 0 errors ✅
- Evaluation.Service: `dotnet build` → 0 warnings, 0 errors ✅
- Angular: `npm run build` → success (budget warnings only) ✅
- Gateway tests: **41/41 passed** (was 39/39, +2 new document route tests)

### Files changed

| File | Change |
|---|---|
| `Stagiaire.Service/Controllers/CandidaturesController.cs` | Uncommented event publish |
| `Evaluation.Service/Controllers/StatsController.cs` | Added scoping to GetAttestation |
| `Backend/api-gateway/.../SecurityConfig.java` | Added document upload route rule |
| `Backend/api-gateway/.../SecurityConfigTest.java` | Added 2 tests for document route |
| `Convention.Service/Controllers/ConventionsController.cs` | Replaced ordinal cast with name-based switch |
| `HANDOFF.md` | Updated §26 with full bug hunt findings |
| `PROJECT_WORK_LOG.md` | Updated §25 with full bug hunt findings |

---

## 26. Refresh Token One-Time-Use Fix (2026-08-29)

### Root cause

The `AuthService.refresh()` method used a read-check-then-write pattern via Hibernate:
1. `findByEmail()` loaded User entity into persistence context
2. Checked `user.getRefreshToken().equals(refreshToken)`
3. Set new token, called `save()`

The `@Transactional` + `save()` pattern did not guarantee the UPDATE was flushed before the next request. Hibernate's persistence context could hold stale entity state, and on transaction commit Hibernate might flush the old token back — overwriting the atomic update. Confirmed by testing: `@Modifying @Query` (JPQL and native) with `clearAutomatically = true` did not fully resolve it.

### Fix

Replaced Hibernate `save()` with direct `JdbcTemplate.update()`:
```java
int rowsAffected = jdbcTemplate.update(
    "UPDATE users SET refresh_token = ? WHERE email = ? AND refresh_token = ?",
    newRefreshToken, email, refreshToken);
```
This is a single SQL UPDATE — atomic at the DB level, bypassing Hibernate entirely.

### Live verification

- **Direct to auth-service (port 8081):** First refresh → 200, all 10 reuses → 401 ✅
- **Through gateway (port 18080):** First refresh → 200, reuse 1 → 200 (gateway-side issue), reuse 2+ → 401

### Known residual

The gateway still allows the first reuse to return 200. This is a gateway-side issue (Spring Cloud Gateway reactive HTTP client), not an auth-service issue. The auth-service itself is now correctly one-time-use.

### Files changed

| File | Change |
|---|---|
| `Backend/auth-service/.../AuthService.java` | Replaced Hibernate `save()` with `JdbcTemplate.update()` |
| `Backend/auth-service/.../UserRepository.java` | Added `atomicSwapRefreshToken` method (now unused, kept for reference) |
| `HANDOFF.md` | Added §26 refresh token fix |
| `PROJECT_WORK_LOG.md` | Added §26 refresh token fix |
| `APPLICATION_MAP.md` | Updated gap description |


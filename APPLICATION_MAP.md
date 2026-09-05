# APPLICATION_MAP.md — Full Structural Inventory

**Snapshot date:** 2026-08-29  
**Purpose:** Complete, current, accurate map of everything in the application for manual test planning.

---

## 1. Services

| Service | Container | Port (host→container) | Language/Framework | Database | Responsibility |
|---------|-----------|----------------------|-------------------|----------|---------------|
| **API Gateway** | `smartek-gateway` | 18080→18080 | Java / Spring Cloud Gateway 3.2 | None | Single entry point: JWT auth, route dispatch, rate limiting, CORS, trace ID propagation |
| **Auth Service** | `smartek-auth` | 8081→8081 | Java / Spring Boot 3.2 | MySQL (`smartek_db`) | User registration, login, refresh tokens, role management (ADMIN/TRAINER/LEARNER), admin seed |
| **Eureka Server** | `smartek-eureka` | 8761→8761 | Java / Spring Boot 3.2 | None | Service discovery registry |
| **Config Server** | `smartek-config` | 8888→8888 | Java / Spring Boot 3.2 | None | Centralized configuration (serves `api-gateway.yml`) |
| **Stagiaire Service** | `smartek-stagiaire` | 5070→8080 | C# / .NET 8.0 | PostgreSQL (`stagiaire_service_db`) | Candidature lifecycle, stagiaire CRUD, journal de bord, audit log, stats, CV/document storage |
| **Convention Service** | `smartek-convention` | 5071→8080 | C# / .NET 8.0 | PostgreSQL (`convention_service_db`) | Convention auto-generation from events, PDF rendering (QuestPDF), signing |
| **Evaluation Service** | `smartek-evaluation` | 5072→8080 | C# / .NET 8.0 | PostgreSQL (`evaluation_service_db`) | Evaluations (create/validate), attestation PDF, stats, StagiaireAffectation projection |
| **Notification Service** | `smartek-notification` | 5073→8080 | C# / .NET 8.0 | PostgreSQL (`notification_service_db`) | Email notifications triggered by RabbitMQ events (Gmail SMTP or Mailhog) |
| **PostgreSQL** | `smartek-postgres` | 5432→5432 | PostgreSQL 15 | — | Hosts all 4 .NET service databases |
| **MySQL** | `smartek-mysql` | 3306→3306 | MySQL 8.0 | — | Hosts auth-service database |
| **RabbitMQ** | `smartek-rabbitmq` | 5672→5672, 15672→15672 | RabbitMQ 3-management | — | Event bus for async inter-service communication |
| **Mailhog** | `smartek-mailhog` | 1025→1025 (SMTP), 8025→8025 (UI) | MailHog | — | Local dev email capture (alternative to Gmail SMTP) |
| **Angular Frontend** | Not containerized | 4200 (local `ng serve`) | Angular 18 | None | SPA frontend — run separately with `cd Frontend/angular-app && npm start` |

---

## 2. Every API Endpoint

All endpoints are accessed through the gateway at `http://localhost:18080`. The gateway rewrites `/api/v1/auth/**` to `/api/auth/**` on the auth-service.

### 2.1 Auth Service (`/api/v1/auth/**` → rewritten to `/api/auth/**`)

| Method | Gateway Path | Roles (Gateway) | Service Endpoint | Description |
|--------|-------------|----------------|-----------------|-------------|
| `POST` | `/api/v1/auth/register` | Public | `POST /api/auth/register` | Register a new learner account (role forced to LEARNER server-side) |
| `POST` | `/api/v1/auth/login` | Public | `POST /api/auth/login` | Login, returns JWT access + refresh tokens |
| `POST` | `/api/v1/auth/refresh` | Public | `POST /api/auth/refresh` | Exchange refresh token for new access + refresh pair (one-time use) |
| `GET` | `/api/v1/auth/health` | Public | `GET /api/auth/health` | Health check |
| `POST` | `/api/v1/auth/encadrants` | ADMIN | `POST /api/auth/encadrants` | Create a TRAINER account (returns temp password) |
| `GET` | `/api/v1/auth/users?role=X` | ADMIN | `GET /api/auth/users?role=X` | List users by role (summaries, no sensitive data) |
| `GET` | `/api/v1/auth/user/{userId}` | ADMIN | `GET /api/auth/user/{userId}` | Get user details by ID |
| `GET` | `/api/v1/auth/validate/{userId}` | ADMIN | `GET /api/auth/validate/{userId}` | Validate if a user ID exists |

### 2.2 Stagiaire Service (`/api/v1/stagiaires/**`)

| Method | Path | Roles (Gateway) | Scoping | Description |
|--------|------|----------------|---------|-------------|
| `GET` | `/api/v1/stagiaires` | ADMIN, TRAINER, LEARNER | `ApplyReadScope(User)` | List stagiaires (paged, filtered) — scoped by role |
| `GET` | `/api/v1/stagiaires/{id}` | ADMIN, TRAINER, LEARNER | `ApplyReadScope(User)` | Get stagiaire by ID — scoped by role |
| `POST` | `/api/v1/stagiaires` | ADMIN | ADMIN only | Create stagiaire directly (admin only) |
| `PUT` | `/api/v1/stagiaires/{id}` | ADMIN | ADMIN only | Update stagiaire (full replacement) |
| `DELETE` | `/api/v1/stagiaires/{id}` | ADMIN | ADMIN only | Delete stagiaire |
| `GET` | `/api/v1/stagiaires/stats` | ADMIN | ADMIN only | Aggregate stats for admin dashboard |
| `POST` | `/api/v1/stagiaires/{id}/journal` | ADMIN, TRAINER, LEARNER | `CanWrite(stagiaire)` | Create journal entry (stagiaire or admin) |
| `GET` | `/api/v1/stagiaires/{id}/journal` | ADMIN, TRAINER, LEARNER | `ApplyReadScope` on parent | List journal entries (paged) |
| `GET` | `/api/v1/stagiaires/{id}/journal/{entryId}` | ADMIN, TRAINER, LEARNER | `ApplyReadScope` on parent | Get journal entry by ID |
| `PUT` | `/api/v1/stagiaires/{id}/journal/{entryId}` | ADMIN, TRAINER, LEARNER | `CanWrite(stagiaire)` | Edit journal entry (author only, before comment) |
| `POST` | `/api/v1/stagiaires/{id}/journal/{entryId}/commentaire` | ADMIN, TRAINER, LEARNER | `CanSupervise(stagiaire)` | Add/replace encadrant comment on journal entry |
| `DELETE` | `/api/v1/stagiaires/{id}/journal/{entryId}` | ADMIN | ADMIN only | Delete journal entry |

### 2.3 Candidatures (`/api/v1/candidatures/**` — served by Stagiaire Service)

| Method | Path | Roles (Gateway) | Scoping | Description |
|--------|------|----------------|---------|-------------|
| `GET` | `/api/v1/candidatures` | ADMIN, TRAINER, LEARNER | `ApplyReadScope(User)` | List all candidatures (paged, scoped) |
| `GET` | `/api/v1/candidatures/{id}` | ADMIN, TRAINER, LEARNER | `FindVisibleAsync` | Get candidature by ID |
| `GET` | `/api/v1/candidatures/moi` | ADMIN, TRAINER, LEARNER | `UtilisateurId == caller` | Get caller's own candidature |
| `POST` | `/api/v1/candidatures` | ADMIN, LEARNER | `UtilisateurId = caller` | Submit a new candidature (always EnAttente) |
| `POST` | `/api/v1/candidatures/{id}/cv` | ADMIN, LEARNER | `FindOwnedAsync` | Upload/replace CV (multipart) |
| `GET` | `/api/v1/candidatures/{id}/cv` | ADMIN, TRAINER, LEARNER | `FindVisibleAsync` | Download CV |
| `POST` | `/api/v1/candidatures/{id}/document` | ADMIN, LEARNER | `FindOwnedAsync` | Upload/replace candidature document (multipart) |
| `GET` | `/api/v1/candidatures/{id}/document` | ADMIN, TRAINER, LEARNER | `FindVisibleAsync` | Download candidature document |
| `POST` | `/api/v1/candidatures/{id}/accepter` | ADMIN | ADMIN only | Accept candidature (assigns encadrant, fires CandidatureAccepted event) |
| `POST` | `/api/v1/candidatures/{id}/rejeter` | ADMIN | ADMIN only | Reject candidature with reason (fires CandidatureRejected event) |

### 2.4 Audit Log (`/api/v1/audit/**` — served by Stagiaire Service)

| Method | Path | Roles (Gateway) | Description |
|--------|------|----------------|-------------|
| `GET` | `/api/v1/audit` | ADMIN | List audit entries (paged, filterable by action/entity/user/date) |
| `GET` | `/api/v1/audit/{id}` | ADMIN | Get audit entry by ID |

### 2.5 Convention Service (`/api/v1/conventions/**`)

| Method | Path | Roles (Gateway) | Scoping | Description |
|--------|------|----------------|---------|-------------|
| `GET` | `/api/v1/conventions` | ADMIN, TRAINER, LEARNER | `ApplyVisibilityScope` | List conventions (paged, scoped) |
| `GET` | `/api/v1/conventions/{id}` | ADMIN, TRAINER, LEARNER | `ApplyVisibilityScope` | Get convention by ID |
| `POST` | `/api/v1/conventions` | ADMIN | ADMIN only | Manually create a convention draft |
| `PUT` | `/api/v1/conventions/{id}` | ADMIN | ADMIN only | Update convention (dates, statut — ownership not settable) |
| `DELETE` | `/api/v1/conventions/{id}` | ADMIN | ADMIN only | Delete convention + PDF blob |
| `POST` | `/api/v1/conventions/{id}/generer` | ADMIN | ADMIN only | Generate PDF from convention data (fires ConventionGenerated event) |
| `GET` | `/api/v1/conventions/{id}/pdf` | ADMIN, TRAINER, LEARNER | `ApplyVisibilityScope` | Download convention PDF |
| `POST` | `/api/v1/conventions/{id}/signer` | ADMIN | ADMIN only | Mark convention as signed |

### 2.6 Evaluation Service (`/api/v1/evaluations/**`)

| Method | Path | Roles (Gateway) | Scoping | Description |
|--------|------|----------------|---------|-------------|
| `GET` | `/api/v1/evaluations` | ADMIN, TRAINER, LEARNER | `ApplyReadScope(User)` | List evaluations (paged, scoped) |
| `GET` | `/api/v1/evaluations/{id}` | ADMIN, TRAINER, LEARNER | `ApplyReadScope(User)` | Get evaluation by ID |
| `POST` | `/api/v1/evaluations` | ADMIN, TRAINER | `CanEvaluate(affectation)` | Create evaluation (assigned encadrant or admin) |
| `PUT` | `/api/v1/evaluations/{id}` | ADMIN, TRAINER | `CanModify(evaluation)` | Update evaluation (before validation only) |
| `POST` | `/api/v1/evaluations/{id}/valider` | ADMIN | ADMIN only | Validate evaluation (Soumise → Validee) |
| `DELETE` | `/api/v1/evaluations/{id}` | ADMIN | ADMIN only | Delete evaluation |
| `GET` | `/api/v1/evaluations/stats` | ADMIN | ADMIN only | Evaluation statistics for admin dashboard |
| `GET` | `/api/v1/evaluations/{id}/attestation` | ADMIN, TRAINER, LEARNER | `ApplyReadScope(User)` | Download attestation PDF (validated evaluations only) |

### 2.7 Notification Service (`/api/v1/notifications/**`)

| Method | Path | Roles (Gateway) | Scoping | Description |
|--------|------|----------------|---------|-------------|
| `GET` | `/api/v1/notifications` | ADMIN, TRAINER, LEARNER | `ApplyReadScope` | List notifications (paged, scoped) |
| `GET` | `/api/v1/notifications/{id}` | ADMIN, TRAINER, LEARNER | `ApplyReadScope` | Get notification by ID |
| `POST` | `/api/v1/notifications` | ADMIN | ADMIN only | Manually create a notification |
| `PUT` | `/api/v1/notifications/{id}` | ADMIN | ADMIN only | Update notification (destinataire not settable) |
| `DELETE` | `/api/v1/notifications/{id}` | ADMIN | ADMIN only | Delete notification |

### 2.8 Infrastructure Endpoints

| Service | Endpoint | Auth | Description |
|---------|----------|------|-------------|
| Gateway | `GET /actuator/health` | Public | Gateway health check |
| Gateway | `GET /actuator/prometheus` | Public | Gateway Prometheus metrics |
| Stagiaire | `GET /health/live` | Public | Process health check |
| Stagiaire | `GET /health/ready` | Public | DB + RabbitMQ health check |
| Stagiaire | `GET /metrics` | Public | Prometheus HTTP request histograms |
| Stagiaire | `GET /swagger` | Public | Swagger/OpenAPI docs |
| Convention | `GET /health/live`, `/health/ready`, `/metrics`, `/swagger` | Public | Same as above |
| Evaluation | `GET /health/live`, `/health/ready`, `/metrics`, `/swagger` | Public | Same as above |
| Notification | `GET /health/live`, `/health/ready`, `/metrics`, `/swagger` | Public | Same as above |
| Eureka | `GET /` | Public | Service registry dashboard |
| Config | `GET /actuator/health` | Public | Config server health |
| RabbitMQ | `http://localhost:15672` | guest/guest | Management UI |
| Mailhog | `http://localhost:8025` | None | Email capture UI |

---

## 3. Every Database Table

### 3.1 MySQL — `smartek_db` (Auth Service)

**Table: `users`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `userId` | BIGINT | PK, auto-increment | auth-service user ID — referenced by .NET services as `UtilisateurId` / `EncadrantId` / `DestinataireId` |
| `email` | VARCHAR(100) | UNIQUE, NOT NULL | Login identifier |
| `password` | VARCHAR(255) | NOT NULL | BCrypt hash |
| `firstName` | VARCHAR(50) | NOT NULL | |
| `role` | VARCHAR(30) | NOT NULL | Enum: `ADMIN`, `TRAINER`, `LEARNER` |
| `image` | BLOB | NULLABLE | Profile image |
| `phone` | VARCHAR(20) | NULLABLE | |
| `experience` | INT | DEFAULT 0 | |
| `refreshToken` | VARCHAR(512) | NULLABLE | One-time-use refresh token |

**Unique constraint:** `email`

### 3.2 PostgreSQL — `stagiaire_service_db` (Stagiaire Service)

**Table: `Stagiaires`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `Id` | UUID | PK | Stagiaire/candidature ID |
| `UtilisateurId` | BIGINT | NULLABLE, indexed | FK to auth-service `users.userId` (learner owner) |
| `Nom` | VARCHAR(100) | NOT NULL | |
| `Prenom` | VARCHAR(100) | NOT NULL | |
| `Email` | VARCHAR(200) | NOT NULL, UNIQUE, indexed | |
| `Departement` | VARCHAR(150) | NOT NULL | |
| `TypeStage` | VARCHAR(20) | NOT NULL | Enum: `PFE`, `StageEte`, `StageOuvrier` |
| `Ecole` | VARCHAR(200) | NOT NULL | |
| `DateDebut` | DATE | NOT NULL | |
| `DateFin` | DATE | NOT NULL | |
| `Motivation` | VARCHAR(2000) | NULLABLE | |
| `CvCheminFichier` | VARCHAR(500) | NULLABLE | Path on `stagiaire-storage` volume |
| `CvNomFichier` | VARCHAR(255) | NULLABLE | Original upload filename |
| `DocumentCheminFichier` | VARCHAR(500) | NULLABLE | Path on `stagiaire-storage` volume |
| `DocumentNomFichier` | VARCHAR(255) | NULLABLE | Original upload filename |
| `Statut` | VARCHAR(20) | NOT NULL, indexed | Enum: `EnAttente`, `Acceptee`, `Rejetee`, `EnCours`, `Termine` |
| `EncadrantId` | BIGINT | NULLABLE, indexed | FK to auth-service `users.userId` (assigned trainer) |
| `EncadrantNom` | VARCHAR(200) | NULLABLE | Denormalized |
| `MotifRejet` | VARCHAR(1000) | NULLABLE | Required when Statut=Rejetee |
| `DateSoumission` | TIMESTAMP | NOT NULL, indexed | UTC |
| `DateDecision` | TIMESTAMP | NULLABLE | UTC |

**Indexes:** `Email` (unique), `Statut`, `UtilisateurId`, `EncadrantId`, `DateSoumission`

**Table: `JournalEntrees`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `Id` | UUID | PK | |
| `StagiaireId` | UUID | NOT NULL, FK→Stagiaires.Id (CASCADE) | |
| `DateEntree` | DATE | NOT NULL | Week the entry covers |
| `Texte` | VARCHAR(4000) | NOT NULL | |
| `CommentaireEncadrant` | VARCHAR(2000) | NULLABLE | Null until encadrant comments |
| `CommentaireParId` | BIGINT | NULLABLE | auth-service userId of commenter |
| `DateCommentaire` | TIMESTAMP | NULLABLE | |
| `DateCreation` | TIMESTAMP | NOT NULL | |
| `DateModification` | TIMESTAMP | NULLABLE | |

**Unique constraint:** `(StagiaireId, DateEntree)` — one entry per stagiaire per week

**Table: `AuditEntries`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `Id` | BIGINT | PK, auto-increment | |
| `Action` | VARCHAR(100) | NOT NULL, indexed | e.g. `CANDIDATURE_SUBMITTED`, `CANDIDATURE_ACCEPTED`, `JOURNAL_COMMENT` |
| `UserId` | BIGINT | NULLABLE, indexed | auth-service userId |
| `UserRole` | VARCHAR(20) | NULLABLE | |
| `UserEmail` | VARCHAR(200) | NULLABLE | |
| `EntityType` | VARCHAR(50) | NOT NULL, indexed | e.g. `Stagiaire`, `JournalEntry` |
| `EntityId` | VARCHAR(100) | NULLABLE | |
| `Details` | VARCHAR(500) | NULLABLE | Human-readable summary |
| `Timestamp` | TIMESTAMP | NOT NULL, indexed | UTC |
| `TraceId` | VARCHAR(100) | NULLABLE | Cross-service trace ID |

**Indexes:** `Action`, `UserId`, `EntityType`, `Timestamp`

### 3.3 PostgreSQL — `convention_service_db` (Convention Service)

**Table: `Conventions`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `Id` | UUID | PK | |
| `StagiaireId` | UUID | NOT NULL, indexed | FK to Stagiaire.Service `Stagiaires.Id` (logical, not DB-level) |
| `UtilisateurId` | BIGINT | NULLABLE, indexed | auth-service userId (learner owner) |
| `EncadrantId` | BIGINT | NULLABLE, indexed | auth-service userId (assigned trainer) |
| `StagiaireNom` | VARCHAR(100) | NOT NULL | Denormalized from CandidatureAccepted event |
| `StagiairePrenom` | VARCHAR(100) | NOT NULL | |
| `StagiaireEmail` | VARCHAR(200) | NOT NULL | |
| `Departement` | VARCHAR(150) | NOT NULL | |
| `DateDebut` | DATE | NOT NULL | |
| `DateFin` | DATE | NOT NULL | |
| `DateGeneration` | DATE | NOT NULL | |
| `StatutSignature` | VARCHAR(20) | NOT NULL, indexed | Enum: `EnAttente`, `Signee`, `Refusee` |
| `CheminPdf` | VARCHAR(500) | NOT NULL | Path on `convention-storage` volume |

**Indexes:** `UtilisateurId`, `EncadrantId`, `StagiaireId`, `StatutSignature`

### 3.4 PostgreSQL — `evaluation_service_db` (Evaluation Service)

**Table: `Evaluations`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `Id` | UUID | PK | |
| `StagiaireId` | UUID | NOT NULL, indexed | FK to Stagiaire.Service `Stagiaires.Id` (logical) |
| `EncadrantId` | BIGINT | NOT NULL, indexed | auth-service userId (assigned trainer) |
| `UtilisateurId` | BIGINT | NULLABLE, indexed | auth-service userId (learner owner) |
| `StagiaireNom` | VARCHAR(100) | NOT NULL | Denormalized |
| `StagiairePrenom` | VARCHAR(100) | NOT NULL | |
| `StagiaireEmail` | VARCHAR(200) | | |
| `TypeEvaluation` | VARCHAR(20) | NOT NULL | Enum: `MiParcours`, `Finale` |
| `DateEvaluation` | DATE | NOT NULL | |
| `Note` | DECIMAL(3,1) | NOT NULL | 0.0–20.0 |
| `Commentaire` | TEXT | NOT NULL | |
| `Statut` | VARCHAR(20) | NOT NULL, indexed | Enum: `EnAttente`, `Soumise`, `Validee` |

**Unique constraint:** `(StagiaireId, TypeEvaluation)` — one evaluation of each type per stagiaire  
**Indexes:** `StagiaireId`, `EncadrantId`, `UtilisateurId`, `Statut`

**Table: `StagiaireAffectations`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `StagiaireId` | UUID | PK | Projection of Stagiaire.Service data |
| `UtilisateurId` | BIGINT | NULLABLE, indexed | Learner owner |
| `EncadrantId` | BIGINT | NULLABLE, indexed | Assigned trainer |
| `Nom` | VARCHAR(100) | NOT NULL | |
| `Prenom` | VARCHAR(100) | NOT NULL | |
| `Email` | VARCHAR(200) | | |
| `DateMiseAJour` | TIMESTAMP | NOT NULL | Last update from CandidatureAccepted event |

**Indexes:** `EncadrantId`, `UtilisateurId`

### 3.5 PostgreSQL — `notification_service_db` (Notification Service)

**Table: `Notifications`**

| Column | Type | Constraints | Notes |
|--------|------|------------|-------|
| `Id` | UUID | PK | |
| `DestinataireId` | BIGINT | NOT NULL, indexed | auth-service userId (recipient) |
| `DestinataireRole` | VARCHAR(20) | NOT NULL | Enum: `Stagiaire`, `Encadrant`, `AdminRH` |
| `Type` | VARCHAR(40) | NOT NULL | Enum: `CandidatureAcceptee`, `ConventionGeneree`, `EvaluationSoumise`, `RappelDelai` |
| `Message` | TEXT | NOT NULL | |
| `Lu` | BOOLEAN | DEFAULT false | Read status |
| `DateCreation` | TIMESTAMP | NOT NULL | |

**Index:** `DestinataireId`

---

## 4. Every RabbitMQ Event

| Event | Publisher | Consumers | Consumer Action | Integration Test Coverage |
|-------|-----------|-----------|-----------------|--------------------------|
| **`CandidatureAccepted`** | `Stagiaire.Service` → `CandidaturesController.Accepter()` | `Convention.Service` `CandidatureAcceptedConsumer` | Creates draft convention row with denormalized stagiaire data | ✅ Fixture polls for convention creation |
| | | `Evaluation.Service` `CandidatureAcceptedConsumer` | Upserts `StagiaireAffectation` projection (owner + encadrant lookup) | ✅ Evaluation creation depends on this |
| | | `Notification.Service` `CandidatureAcceptedConsumer` | Sends "candidature accepted" email via SMTP | ✅ Verified via notification service logs |
| **`CandidatureRejected`** | `Stagiaire.Service` → `CandidaturesController.Rejeter()` | `Notification.Service` `CandidatureRejectedConsumer` | Sends "candidature rejected" email with motif | ❌ Not covered by integration tests |
| **`ConventionGenerated`** | `Convention.Service` → `ConventionsController.Generer()` | `Notification.Service` `ConventionGeneratedConsumer` | Sends "convention ready for download" email | ❌ Not covered by integration tests |
| **`EvaluationSubmitted`** | `Evaluation.Service` → `EvaluationsController.Create()` | `Notification.Service` `EvaluationSubmittedConsumer` | Sends "evaluation recorded" email | ❌ Not covered by integration tests |

### Event Queue Names (MassTransit defaults)

| Queue | Event |
|-------|-------|
| `candidature-accepted-queue` | `CandidatureAccepted` |
| `candidature-rejected-queue` | `CandidatureRejected` |
| `convention-generated-queue` | `ConventionGenerated` |
| `evaluation-submitted-queue` | `EvaluationSubmitted` |

---

## 5. Frontend Routes/Screens by Role

### 5.1 Public (no auth required)

| Route | Component | Description |
|-------|-----------|-------------|
| `/` | `HomePageComponent` | Landing page with hero, companies, courses, mentors, testimonials, contact, newsletter |
| `/auth/sign-in` | `SignInComponent` | Login form (email + password) |
| `/auth/sign-up` | `SignUpComponent` | Registration form (always creates LEARNER) |

### 5.2 LEARNER

| Route | Sidebar Label | Component | Description |
|-------|--------------|-----------|-------------|
| `/dashboard` | Tableau de bord | `DashboardPageComponent` | Dashboard with stats cards (admin) or welcome message |
| `/dashboard/ma-candidature` | Ma candidature | `MaCandidatureComponent` | Submit candidature, upload CV/document, track status |
| `/dashboard/ma-convention` | Ma convention | `MaConventionComponent` | View convention status, download PDF |
| `/dashboard/mon-journal` | Mon journal de bord | `MonJournalComponent` | Write weekly journal entries, view encadrant comments |
| `/dashboard/mes-evaluations` | Mon évaluation | `MonEvaluationComponent` | View own evaluation results |

### 5.3 TRAINER (Encadrant)

| Route | Sidebar Label | Component | Description |
|-------|--------------|-----------|-------------|
| `/dashboard` | Tableau de bord | `DashboardPageComponent` | Dashboard |
| `/dashboard/mes-stagiaires` | Mes stagiaires | `StagiairesListComponent` | View assigned stagiaires (API scopes by role) |
| `/dashboard/journaux` | Journaux de bord | `JournalEncadrantComponent` | Read stagiaire journal entries, add comments |
| `/dashboard/evaluations` | Évaluations | `EvaluationEncadrantComponent` | Create/edit evaluations, validate (admin only) |

### 5.4 ADMIN

| Route | Sidebar Label | Component | Description |
|-------|--------------|-----------|-------------|
| `/dashboard` | Tableau de bord | `DashboardPageComponent` | Dashboard with real stats (total stagiaires, pending, accepted, avg score, dept breakdown) |
| `/dashboard/creer-encadrant` | Créer un encadrant | `CreateEncadrantComponent` | Create TRAINER account |
| `/dashboard/candidatures` | Candidatures | `CandidaturesAdminComponent` | Review candidatures, accept/reject with modals |
| `/dashboard/conventions` | Conventions | `ConventionsAdminComponent` | Manage conventions, generate PDF, sign |
| `/dashboard/stagiaires` | Stagiaires | `StagiairesListComponent` | Full stagiaire roster (same component as trainer) |
| `/dashboard/audit` | Journal d'audit | `AuditLogComponent` | View audit trail with filtering |
| `/dashboard/ma-candidature` | (shared) | `MaCandidatureComponent` | Admin can also submit candidatures |
| `/dashboard/ma-convention` | (shared) | `MaConventionComponent` | Admin can view conventions |
| `/dashboard/mon-journal` | (shared) | `MonJournalComponent` | Admin can view journals |
| `/dashboard/mes-evaluations` | (shared) | `MonEvaluationComponent` | Admin can view evaluations |
| `/dashboard/mes-stagiaires` | (shared) | `StagiairesListComponent` | Admin sees all stagiaires |
| `/dashboard/journaux` | (shared) | `JournalEncadrantComponent` | Admin can comment on journals |
| `/dashboard/evaluations` | (shared) | `EvaluationEncadrantComponent` | Admin can create/validate/delete evaluations |

### 5.5 Orphaned/Unreachable Screens

**None found.** All routes in `app.routes.ts` have matching sidebar entries in `menu.config.ts` for at least one role.

---

## 6. Known Gaps and Deferred Items

### 6.1 Deferred Features

| Item | Priority | Status | Notes |
|------|----------|--------|-------|
| **Real-time WebSockets** | HIGH | Deferred | Design decision pending: Spring STOMP (gateway) vs SignalR (.NET) vs Socket.IO. Recommended: Spring STOMP at gateway level. |
| **Optimistic UI updates** | LOW | Deferred | Candidature accept/reject involves server-side event chain — not safe for optimistic updates. Skeleton loaders cover perceived latency. |
| **Dark mode** | LOW | Deferred | Soft UI Dashboard template uses hardcoded gradients. Would require significant CSS rework. |

### 6.2 Known Technical Gaps

| Gap | Severity | Notes |
|-----|----------|-------|
| **No dead-letter queue (DLQ) on RabbitMQ** | MEDIUM | Failed message processing retries then silently drops. No dead-letter exchange configured. Could lose notification emails on transient failures. |
| **Refresh token one-time-use bug through gateway** | MEDIUM | Auth-service fix: replaced Hibernate `save()` with direct `JdbcTemplate.update()` to bypass persistence context. Direct access now correctly rejects all reuses (401). Through gateway, first reuse still returns 200 — likely Spring Cloud Gateway response caching/connection pooling issue, separate from the auth-service root cause. |
| **No audit coverage on Convention/Evaluation services** | LOW | Only Stagiaire.Service controllers are hooked into `AuditService`. Convention signing and evaluation validation are not audited. |
| **No HTTPS termination** | MEDIUM | All traffic is HTTP. Would need a reverse proxy (nginx) or TLS at the gateway for production. |
| **No database connection SSL** | LOW | PostgreSQL and MySQL connections are unencrypted. Fine for local dev, not for production. |
| **No log aggregation** | LOW | Logs go to stdout/docker. No centralized logging (ELK, Loki, etc.). |
| **No Kubernetes readiness/liveness probes** | LOW | k8s manifests exist in `k8s/` but may be stale — need updating for current service configuration. |
| **`environment.prod.ts` points at localhost** | LOW | `apiUrl: 'http://localhost:18080'` — wrong for any real deployment. |
| **Role enum incomplete in frontend** | LOW | `role.enum.ts` has 5 values (`LEARNER`, `TRAINER`, `RH_COMPANY`, `RH_SMARTEK`, `ADMIN`) — `RH_COMPANY` and `RH_SMARTEK` have no screens or backend support. |
| **No candidature status transition to `EnCours`/`Termine`** | LOW | The `StatutStagiaire` enum has `EnCours` and `Termine` values, but no endpoint or UI transitions a candidature to these states. The stage lifecycle beyond acceptance is not implemented. |
| **No `ConventionGenerated` event consumer test** | LOW | Integration tests don't verify the convention-generated notification email. |
| **No `EvaluationSubmitted` event consumer test** | LOW | Integration tests don't verify the evaluation-submitted notification email. |
| **No `CandidatureRejected` event consumer test** | LOW | Integration tests don't verify the rejection notification email. |

---

## 7. How to Run the Stack for Manual Testing

### 7.1 Prerequisites

- Docker Desktop running
- Node.js v24+ and npm v11+ (for Angular)
- `.env` file (copy from `.env.example`, set `JWT_SECRET` to a random 32+ byte string)

### 7.2 Start the Backend

```bash
# Fresh start (wipes all data)
docker compose down -v && docker compose up -d --build

# Or restart without rebuilding
docker compose up -d
```

Wait ~60 seconds for all health checks to pass. Verify with:
```bash
docker compose ps                    # All 11 containers should be "Up (healthy)"
curl http://localhost:18080/actuator/health  # Should return {"status":"UP"}
```

### 7.3 Start the Frontend

```bash
cd Frontend/angular-app
npm install   # first time only
npm start     # → http://localhost:4200
```

### 7.4 Test Accounts

| Role | Email | Password | How Created |
|------|-------|----------|-------------|
| **ADMIN** | `admin@stb.tn` | `Admin123!` | Seeded on first boot from `.env` (`ADMIN_EMAIL`/`ADMIN_PASSWORD`) |
| **TRAINER** | Create via UI | Auto-generated temp password | Admin → "Créer un encadrant" screen, or `POST /api/v1/auth/encadrants` |
| **LEARNER** | Any email | Any password 8+ chars | Register at `/auth/sign-up` (role forced to LEARNER server-side) |

### 7.5 URLs for Manual Testing

| URL | What |
|-----|------|
| `http://localhost:4200` | Angular frontend (login, dashboard, all screens) |
| `http://localhost:18080` | API Gateway (all API calls go through here) |
| `http://localhost:5070/swagger` | Stagiaire Service Swagger docs |
| `http://localhost:5071/swagger` | Convention Service Swagger docs |
| `http://localhost:5072/swagger` | Evaluation Service Swagger docs |
| `http://localhost:5073/swagger` | Notification Service Swagger docs |
| `http://localhost:15672` | RabbitMQ Management UI (guest/guest) |
| `http://localhost:8025` | Mailhog email capture UI |
| `http://localhost:8761` | Eureka Service Registry dashboard |

### 7.6 Full Manual Test Flow (Happy Path)

1. **Register** a learner at `/auth/sign-up`
2. **Login** as learner → see dashboard
3. **Submit candidature** at `/dashboard/ma-candidature` (fill form, upload CV)
4. **Login as admin** (`admin@stb.tn` / `Admin123!`)
5. **Create a trainer** at `/dashboard/creer-encadrant` → note the temp password
6. **Accept the candidature** at `/dashboard/candidatures` → assign the trainer
7. **Login as trainer** (use temp password, change on first login)
8. **View assigned stagiaires** at `/dashboard/mes-stagiaires`
9. **Add journal comment** at `/dashboard/journaux` → read learner's entry, add comment
10. **Create evaluation** at `/dashboard/evaluations` → select stagiaire, fill form
11. **Login as admin** → validate the evaluation at `/dashboard/evaluations`
12. **Login as learner** → view evaluation result at `/dashboard/mes-evaluations`
13. **Login as admin** → generate convention PDF at `/dashboard/conventions` → download → sign
14. **Login as learner** → download convention PDF at `/dashboard/ma-convention`
15. **Check audit log** at `/dashboard/audit` → verify entries for all actions above
16. **Check RabbitMQ** at `http://localhost:15672` → verify queues received messages
17. **Check Mailhog** at `http://localhost:8025` → verify notification emails sent

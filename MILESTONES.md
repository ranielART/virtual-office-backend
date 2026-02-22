# Virtual Office Booking System — Backend Development Milestones

**Project:** Virtual Office Booking System — REST API Backend
**Platform:** ASP.NET Core 9.0 / PostgreSQL / Microsoft Azure
**Repository:** `virtual-office-booking-system-backend`

---

## Overview

This document details the completed engineering milestones for the Virtual Office Booking System backend. The backend is a production-grade REST API built on ASP.NET Core 9.0, following industry-standard Clean Architecture principles. All milestones listed below are fully implemented, tested, and deployed to a live production environment on Microsoft Azure.

---

## Milestone 1 — Project Architecture & Infrastructure Foundation

**Status:** ✅ Complete

### What Was Built

The entire backend was architected from the ground up using **Clean Architecture** — a professional, industry-standard software design pattern that enforces strict separation of concerns across five independent layers:

| Layer | Responsibility |
|---|---|
| **Domain** | Core business entities and enumerations — zero external dependencies |
| **Application** | Business logic orchestration, command/query handling, validation contracts |
| **Infrastructure** | External service implementations (JWT, token cryptography) |
| **Persistence** | Database access, Entity Framework Core configuration, migrations, seeding |
| **WebApi** | HTTP interface, controllers, middleware, dependency injection wiring |

Each layer is a standalone C# project with explicit dependency boundaries, ensuring that business logic is completely decoupled from infrastructure concerns. This architecture makes the system maintainable, testable, and extensible for the long term.

### Key Deliverables

- Established the five-project solution with strict dependency flow
- Configured **PostgreSQL** as the production database via **Npgsql** and **Entity Framework Core 9**
- Applied `snake_case` database naming conventions globally, aligning with PostgreSQL best practices
- Set up **Docker Compose** for a local PostgreSQL development environment, enabling consistent local development across all team members without manual database setup
- Implemented a **layered Dependency Injection** pattern where each layer self-registers through a single `DependencyInjection.cs` extension method, keeping `Program.cs` clean and scalable
- Configured **Serilog** for structured, production-grade logging with:
  - Colourised console output with timestamp and log level
  - Rolling daily log files at `Logs/log-{date}.txt`
  - Suppression of low-value framework noise (EF Core SQL logs set to Warning)
- Integrated **Swagger (Swashbuckle)** with JWT Bearer authentication support for interactive API documentation

---

## Milestone 2 — Domain Modeling & Database Schema Design

**Status:** ✅ Complete

### What Was Built

The full database schema was designed and implemented using **Entity Framework Core Fluent API** configurations, covering all identity and business entities.

### Entities Designed & Configured

**`User`** — Extends ASP.NET Core Identity's `IdentityUser` with business-specific fields:
- `FirstName`, `LastName`, `MiddleName`, `Address`
- `RegistrationStatus` — a typed enum (`Pending`, `Approved`, `Rejected`) stored as a string for human-readable database values
- `IsActive` — boolean flag enabling soft deletion without permanent data loss
- `RequiresPasswordChange` — flag that forces newly provisioned users to set their own password on first login, a critical security requirement

**`RefreshToken`** — A dedicated table that tracks the complete lifecycle of every refresh token issued by the system:
- Stores the token string, the JWT ID (`jti`) it is linked to, creation and expiry timestamps
- `Used` flag for rotation enforcement
- `Invalidated` flag for manual revocation and breach response
- Cascade-delete relationship with `User` — when a user is deleted, all their tokens are cleaned up automatically

**`Role` / `UserRole`** — Custom extensions of ASP.NET Identity's role system with explicit many-to-many join entity, enabling bi-directional navigation between users and roles

**Entity Framework Configurations** — Every entity has a dedicated `IEntityTypeConfiguration<T>` class in the `Persistence/Configurations/` folder, keeping the DbContext clean and configurations explicit and testable.

**Migrations** — Two production EF Core migrations were created and applied:
1. `ReqpassAndStatuses` — Initial schema with all identity tables, `RegistrationStatus`, and `RequiresPasswordChange`
2. `AddedIsActiveBooleanInUserEntity` — Added the `IsActive` soft-deletion column to the Users table

**Automatic Migration on Startup** — The application applies all pending migrations automatically at startup, ensuring the database schema is always in sync without any manual intervention during deployments.

---

## Milestone 3 — Database Seeding & Role Initialisation

**Status:** ✅ Complete

### What Was Built

A robust **DataSeeder** that runs automatically at every application startup via an `IDataSeeder` interface and scoped service registration.

### Seeding Logic

- **Three roles** are seeded idempotently — `Admin`, `Customer`, `Frontdesk` — meaning the seeder is safe to run on every startup without creating duplicates
- **Default system administrator account** is seeded if one does not exist, enabling immediate first-run access without manual database setup
- Admin accounts are created with `Status = Approved` and `EmailConfirmed = true`, bypassing the standard customer registration approval flow
- Seeding failure throws an exception with a descriptive error message, preventing the application from starting in a broken state

---

## Milestone 4 — Authentication System

**Status:** ✅ Complete

### What Was Built

A complete, production-grade **JWT-based authentication system** with secure refresh token handling.

### User Registration (`POST /api/auth/register`)

- Customers can self-register with full profile information
- New accounts are set to `Status = Pending` and `RequiresPasswordChange = true`, requiring admin approval before access is granted
- Input is validated with **FluentValidation** before any database operations occur
- ASP.NET Identity errors (e.g. duplicate email) are mapped to structured validation responses

### User Login (`POST /api/auth/login`)

- Authenticates users by email and password
- Generates a signed **JWT access token** (HMAC SHA-256) containing: user ID (`sub`), email, full name, role, and a unique JWT ID (`jti`) for token tracking
- Simultaneously generates a cryptographically secure **refresh token** (64 random bytes, Base64-encoded) and persists it to the database linked to the access token's `jti`
- The refresh token is delivered to the client in an **HttpOnly, Secure, SameSite=None cookie** — inaccessible to JavaScript, protecting against XSS attacks
- Returns the access token and user profile information in the response body

### JWT Configuration

- Access token lifetime is fully configurable via environment variable (`JWT_EXPIRY_MINUTES`)
- Refresh token lifetime is fully configurable via environment variable (`JWT_REFRESH_TOKEN_EXPIRY_DAYS`)
- JWT validation enforces issuer, audience, signing key, and token lifetime

---

## Milestone 5 — Refresh Token Rotation & Session Security

**Status:** ✅ Complete

### What Was Built

A sophisticated **refresh token rotation system** implementing industry best-practice security patterns for session management.

### Token Rotation (`POST /api/auth/refresh`)

When an access token expires, the client presents both the expired access token and their current refresh token. The system:

1. Validates the access token's **signature** (lifetime is intentionally skipped — expired tokens are expected)
2. Extracts the user identity and JWT ID (`jti`) from the expired token's claims
3. Looks up the refresh token in the database and runs a full validation chain:
   - Token exists in the database
   - Token belongs to the user from the access token claims
   - Token's `jti` matches the access token's `jti` (prevents token substitution attacks)
   - Token is not expired
   - Token has not been revoked
4. Issues a **brand new access token and a brand new refresh token**, linked by a new `jti`
5. Marks the old refresh token as `Used = true` — it can never be used again

### Breach Detection

If a refresh token that has already been consumed (`Used = true`) is presented again, the system **immediately invalidates every single refresh token** belonging to that user and returns a 401 response. This is a defence against token theft — if an attacker has stolen and used an old refresh token, the legitimate user's next rotation attempt will trigger this mechanism, locking out the attacker and alerting the system.

### Logout (`POST /api/auth/logout`)

- Reads the refresh token from the HttpOnly cookie
- Marks it as `Invalidated = true` in the database — it cannot be used for future token rotation
- Explicitly deletes the cookie from the client's browser

---

## Milestone 6 — Admin User Management (Full CRUD)

**Status:** ✅ Complete

### What Was Built

A complete **Admin management module** accessible exclusively to authenticated users holding the `Admin` role.

### Endpoints

| Endpoint | Description |
|---|---|
| `POST /api/admin/create-admin` | Provisions a new Admin account with a strict password policy |
| `GET /api/admin/get-admin/{id}` | Retrieves a specific Admin's profile |
| `PUT /api/admin/edit-admin/{id}` | Updates an Admin's profile information |
| `DELETE /api/admin/delete-admin/{id}` | Soft-deletes an Admin (sets `IsActive = false`, locks account immediately) |
| `GET /api/admin/active-admins` | Lists all currently active Admin accounts |
| `GET /api/admin/archived-admins` | Lists all deactivated/archived Admin accounts |

### Key Design Decisions

- **Soft deletion** — Deleted admins are deactivated (`IsActive = false`) rather than permanently removed, preserving audit history and enabling account restoration
- **Immediate lockout** — Soft-deleted users are immediately locked out via ASP.NET Identity's lockout mechanism, even if they hold a valid access token
- **Role-based filtering** — The active/archived list endpoints use a **generic, role-agnostic query** (`GetActiveUsersByRoleQuery`, `GetArchivedUsersByRoleQuery`) that accepts a role parameter, eliminating code duplication across future role modules
- **Strict password requirements** for admin-provisioned accounts: minimum 10 characters, at least one uppercase letter, one lowercase letter, and one number

---

## Milestone 7 — FrontDesk Staff Management (Full CRUD)

**Status:** ✅ Complete

### What Was Built

A complete **FrontDesk staff management module**, following the exact same architectural patterns as Admin management. FrontDesk accounts are created and managed by Administrators through a dedicated controller (`/api/frontdesk`), maintaining clean separation of concerns.

### Endpoints

| Endpoint | Auth Required | Description |
|---|---|---|
| `POST /api/frontdesk/create-frontdesk` | Admin only | Provisions a new FrontDesk account |
| `GET /api/frontdesk/get-frontdesk/{id}` | Admin or FrontDesk | Retrieves a FrontDesk user's profile |
| `PUT /api/frontdesk/edit-frontdesk/{id}` | Admin or FrontDesk | Updates a FrontDesk user's profile |
| `DELETE /api/frontdesk/delete-frontdesk/{id}` | Admin only | Soft-deletes a FrontDesk account |
| `GET /api/frontdesk/active-frontdesks` | Admin only | Lists all active FrontDesk staff |
| `GET /api/frontdesk/archived-frontdesks` | Admin only | Lists all archived FrontDesk staff |

### Key Design Decisions

- **Granular per-endpoint authorization** — Each endpoint carries its own `[Authorize(Roles = "...")]` attribute. Get and Edit are accessible by both `Admin` and `Frontdesk` roles (a FrontDesk user can view and update their own profile), while Create, Delete, and List operations are Admin-only
- **Command reuse** — Update and Delete operations reuse the generic `UpdateUserCommand` and `DeleteUserCommand` rather than duplicating logic, following the DRY (Don't Repeat Yourself) principle
- **Consistent validation** — The FrontDesk validator enforces the same strict password and phone number rules as Admin creation, ensuring uniform security standards across all provisioned user types

---

## Milestone 8 — Password Management

**Status:** ✅ Complete

### What Was Built

A two-tier password management system covering both self-service and administrative password operations.

### Self-Service Password Change (`POST /api/auth/change-password`)

- Available to **any authenticated user** regardless of role
- Requires the user's **current password** as verification — prevents unauthorized changes even with a stolen access token
- Enforces the full strict password policy on the new password
- Explicitly rejects new passwords that are identical to the current password
- Clears the `RequiresPasswordChange` flag upon success, signalling to the frontend that the forced-change flow is complete
- User identity is extracted directly from the **JWT claims** (`sub`) — users can only change their own password; there is no route parameter that could be manipulated

### Admin Password Reset (`POST /api/admin/reset-password/{userId}`)

- Restricted to `Admin` role only
- Resets **any user's password without requiring knowledge of the current password**, using ASP.NET Identity's `RemovePasswordAsync` + `AddPasswordAsync` flow
- Sets `RequiresPasswordChange = true` on the target user, forcing them to set a new password of their own choosing on next login
- Targeted user is identified by `userId` route parameter, giving administrators full control over any account

---

## Milestone 9 — Structured Error Handling & Exception Architecture

**Status:** ✅ Complete

### What Was Built

A centralised, production-grade **exception handling architecture** that eliminates scattered try-catch blocks and enforces a consistent, structured error response format across every endpoint.

### Global Exception Handler

A single `GlobalExceptionHandler` implementing `IExceptionHandler` intercepts all unhandled exceptions at the middleware level and maps them to appropriate HTTP responses in **RFC 7807 ProblemDetails** format — the industry standard for REST API error responses.

### Custom Exception Hierarchy

Two domain-specific exception types were introduced:
- **`UnauthorizedException`** → `401 Unauthorized` — thrown for all authentication and authorization failures (invalid credentials, expired tokens, token reuse, revoked tokens)
- **`NotFoundException`** → `404 Not Found` — thrown when a requested resource does not exist

### Exception-to-HTTP Mapping

| Exception | HTTP Status | Scenario |
|---|---|---|
| `ValidationException` | `400` | FluentValidation or Identity validation failures |
| `ArgumentException` / `InvalidOperationException` | `400` | Business rule violations |
| `UnauthorizedException` | `401` | Auth failures of any kind |
| `NotFoundException` | `404` | Resource not found |
| Unhandled `Exception` | `500` | Unexpected server errors (detail hidden from client) |

### Controller Simplification

All controller actions were **stripped of try-catch blocks** entirely. Controllers are now pure orchestration — they construct a command, send it through MediatR, and return the result. Error handling is the sole responsibility of the global handler, resulting in dramatically cleaner, more readable controller code.

### Structured Validation Error Response

Validation errors are grouped by field name and returned in a consistent structure that frontend clients can parse deterministically:

```json
{
  "title": "Validation Failed",
  "status": 400,
  "errors": {
    "Request.Password": ["Must be at least 10 characters.", "Must contain an uppercase letter."],
    "Request.Email": ["Invalid email format"]
  }
}
```

---

## Milestone 10 — MediatR Pipeline Behaviors (Validation & Logging)

**Status:** ✅ Complete

### What Was Built

Two **MediatR pipeline behaviors** that intercept every command and query transparently, providing cross-cutting concerns without polluting business logic.

### Validation Behavior

Automatically runs all registered FluentValidation validators for a command **before** the command handler executes. If validation fails, a `ValidationException` is thrown immediately — the handler never runs. This guarantees that no command handler ever receives invalid input.

### Logging Behavior

Wraps every command/query execution with rich, contextual structured logging:

- **`[START]`** log on entry — records the command name, authenticated user ID, and client IP address
- **`[END]`** log on success — records the command name and execution time in milliseconds
- **`[SLOW]`** warning — automatically flags any operation taking longer than 500ms for performance monitoring
- **`[ERROR]`** log on failure — records the full exception, the request data that caused it, and the user who triggered it

Sensitive operations (login, password changes) are automatically **redacted** in logs — request data is replaced with `[REDACTED SENSITIVE DATA]` to prevent credentials from appearing in log files.

Log context properties (`User_Id`, `User_Name`, `Client_IP`, `Trace_Id`) are propagated through the entire call stack using Serilog's `LogContext`, meaning every log line written anywhere in a request is enriched with these properties automatically.

---

## Milestone 11 — CI/CD Pipeline & Azure Cloud Deployment

**Status:** ✅ Complete
**Implemented by:** Fellow Developer

### What Was Built

A fully automated **Continuous Integration and Continuous Deployment (CI/CD) pipeline** using **GitHub Actions**, with the application live on **Microsoft Azure App Service**.

### GitHub Actions Pipeline

The pipeline (`.github/workflows/dev_communside.yml`) triggers automatically on every push to the `dev` branch and consists of two sequential jobs:

**Build Job:**
1. Checks out the repository on a Windows runner
2. Sets up the .NET 9 SDK
3. Restores all NuGet package dependencies
4. Builds the solution in **Release configuration**
5. Publishes the `WebApi` project to an output directory
6. Uploads the publish artifact for the deploy job

**Deploy Job:**
1. Downloads the build artifact from the preceding job
2. Authenticates to Azure using **Federated Identity Credentials (OIDC)** — a passwordless, token-based authentication method that is significantly more secure than storing service principal secrets
3. Deploys the artifact directly to the **Azure App Service** production slot

### Security — Secrets Management

All sensitive configuration values are injected into the pipeline as **GitHub Encrypted Secrets** and exposed to the application as environment variables at runtime. No secrets are present in the codebase or configuration files:

- Database connection parameters (`DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`)
- JWT signing key and token lifetime settings
- ASP.NET Core environment configuration
- Azure service principal credentials (Client ID, Tenant ID, Subscription ID)

### Live Production Environment

- **Platform:** Microsoft Azure App Service
- **App Name:** `communside`
- **Deployment Slot:** Production
- **Trigger:** Automatic on merge to `dev` branch — zero manual deployment steps required

---

## Milestone 12 — Email Service Integration

**Status:** ✅ Complete
**Implemented by:** Fellow Developer

### What Was Built

A transactional **email service** integrated into the backend to support automated system communications. The service enables the application to send programmatic emails to users, supporting workflows such as account notifications, registration confirmations, and administrative communications.

The email service is fully wired into the application's dependency injection system and is available across all application layers, ready to be consumed by any command or workflow that requires user notification.

---

## Milestone 13 — Developer Documentation

**Status:** ✅ Complete

### What Was Built

A comprehensive `README.md` covering the entire system for onboarding new developers:

- Full architecture diagram with dependency rules explained
- Complete project structure with every file described
- Step-by-step local setup guide
- All environment variables documented
- Full API endpoint reference with auth requirements
- CQRS pattern explanation with file layout guide
- Validation rules, error response formats, and logging behaviour
- Step-by-step guide for adding new commands, queries, roles, and exception types

---

## Summary of Deliverables

| # | Milestone | Status |
|---|---|---|
| 1 | Clean Architecture setup, Docker, Serilog, Swagger | ✅ Complete |
| 2 | Domain modeling, EF Core schema, migrations | ✅ Complete |
| 3 | Role seeding, admin seeding, auto-migration on startup | ✅ Complete |
| 4 | JWT authentication, user registration & login | ✅ Complete |
| 5 | Refresh token rotation, breach detection, logout | ✅ Complete |
| 6 | Admin CRUD with soft delete and role-based access | ✅ Complete |
| 7 | FrontDesk CRUD with granular per-endpoint authorization | ✅ Complete |
| 8 | Self-service password change and admin password reset | ✅ Complete |
| 9 | Centralised exception handling, custom exception types | ✅ Complete |
| 10 | MediatR validation and logging pipeline behaviors | ✅ Complete |
| 11 | GitHub Actions CI/CD pipeline, Azure App Service deployment | ✅ Complete |
| 12 | Email service integration | ✅ Complete |
| 13 | Full developer documentation (README) | ✅ Complete |

---

*All backend milestones are complete and live in the production environment.*

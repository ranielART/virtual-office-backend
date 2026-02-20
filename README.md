# Virtual Office Booking System — Backend

ASP.NET Core 9.0 REST API for the Virtual Office Booking System. Built with Clean Architecture, CQRS via MediatR, JWT authentication with refresh token rotation, and PostgreSQL as the primary database.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Tech Stack](#tech-stack)
3. [Project Structure](#project-structure)
4. [Getting Started](#getting-started)
5. [Environment Variables](#environment-variables)
6. [Database](#database)
7. [Authentication & Authorization](#authentication--authorization)
8. [Refresh Token Lifecycle](#refresh-token-lifecycle)
9. [Role System](#role-system)
10. [API Endpoints](#api-endpoints)
11. [CQRS Pattern](#cqrs-pattern)
12. [Validation](#validation)
13. [Error Handling](#error-handling)
14. [Logging](#logging)
15. [Adding New Features](#adding-new-features)

---

## Architecture Overview

The solution follows **Clean Architecture** with strict dependency rules — outer layers depend on inner layers, never the reverse.

```
┌─────────────────────────────────────┐
│             WebApi                  │  ← HTTP, Controllers, DI wiring
├─────────────────────────────────────┤
│           Infrastructure            │  ← Token generation, external services
├─────────────────────────────────────┤
│            Persistence              │  ← EF Core, DbContext, Migrations, Seeding
├─────────────────────────────────────┤
│            Application              │  ← Commands, Queries, Validators, Interfaces
├─────────────────────────────────────┤
│              Domain                 │  ← Entities, Enums (no dependencies)
└─────────────────────────────────────┘
```

**Dependency rule:** Domain ← Application ← Infrastructure/Persistence ← WebApi

Each layer exposes its registrations through a single `DependencyInjection.cs` extension method that is called from `Program.cs`.

---

## Tech Stack

| Concern              | Technology                            |
| -------------------- | ------------------------------------- |
| Framework            | ASP.NET Core 9.0                      |
| Language             | C# 13                                 |
| ORM                  | Entity Framework Core 9.0             |
| Database             | PostgreSQL (via Npgsql)               |
| Authentication       | JWT Bearer + Refresh Tokens           |
| CQRS / Mediator      | MediatR 12                            |
| Validation           | FluentValidation 12                   |
| Object Mapping       | AutoMapper 12                         |
| Logging              | Serilog (console + rolling file)      |
| API Documentation    | Swagger (Swashbuckle)                 |
| Environment Config   | DotNetEnv                             |
| DB Naming Convention | snake_case (EFCore.NamingConventions) |

---

## Project Structure

```
virtual-office-booking-system-backend/
│
├── Domain/                            # Core domain — no external dependencies
│   ├── Common/
│   │   └── GuidBaseEntity.cs          # Base class: Id (Guid), CreatedAt, UpdatedAt
│   ├── Entity/Identity/
│   │   ├── User.cs                    # Extends IdentityUser; adds profile + status fields
│   │   ├── Role.cs                    # Extends IdentityRole<string>
│   │   ├── UserRole.cs                # Join entity for User ↔ Role (many-to-many)
│   │   └── RefreshToken.cs            # Tracks refresh token lifecycle per user
│   └── Enum/
│       └── RegistrationStatus.cs      # Pending | Approved | Rejected
│
├── Application/                       # Business logic — orchestrates domain
│   ├── Common/
│   │   ├── Behaviors/
│   │   │   ├── ValidationBehavior.cs  # MediatR pipeline: runs FluentValidation before handler
│   │   │   └── LoggingBehavior.cs     # MediatR pipeline: structured logging + perf monitoring
│   │   ├── Exceptions/
│   │   │   ├── UnauthorizedException.cs  # Thrown for auth failures → 401
│   │   │   └── NotFoundException.cs      # Thrown for missing resources → 404
│   │   └── Interfaces/
│   │       └── ITokenService.cs       # Contract for token operations
│   └── Services/User/
│       ├── Command/                   # Write operations (create, update, delete, auth)
│       ├── Query/                     # Read operations (get by id, list by role)
│       ├── Request/                   # Input DTOs bound from controller
│       ├── Response/                  # Output DTOs returned to client
│       └── Validator/                 # FluentValidation rules per command
│
├── Infrastructure/                    # External service implementations
│   └── Services/
│       └── TokenService.cs            # JWT generation, refresh token creation, claim extraction
│
├── Persistence/                       # Data access
│   ├── Configurations/                # EF Fluent API configurations per entity
│   ├── Data/
│   │   └── ApplicationDbContext.cs    # IdentityDbContext with custom entities
│   ├── Migrations/                    # EF Core migration history
│   └── Seeders/
│       └── DataSeeder.cs              # Seeds roles and default admin on startup
│
└── WebApi/                            # HTTP layer
    ├── Controllers/
    │   ├── AuthController.cs          # Public auth endpoints (login, register, refresh, logout)
    │   ├── AdminController.cs         # Admin CRUD + password reset (Admin role only)
    │   └── FrontDeskController.cs     # FrontDesk CRUD (Admin only, except get/edit)
    ├── Infrastructure/
    │   └── GlobalExceptionHandler.cs  # Centralised exception → ProblemDetails mapping
    ├── Program.cs                     # App entry point: DI wiring, middleware, startup
    ├── DependencyInjection.cs         # JWT auth, Swagger, CORS, Serilog setup
    └── appsettings.json               # Non-secret config (log levels, issuer, audience)
```

---

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (local instance or cloud)

### Setup

1. **Clone the repository**

2. **Create a `.env` file** at the repository root (same level as the `.sln` file). See [Environment Variables](#environment-variables) for the required keys.

3. **Run the application**

   ```bash
   dotnet run --project WebApi
   ```

   On startup, the application will:
   - Apply any pending EF Core migrations automatically
   - Seed the three roles (`Admin`, `Customer`, `Frontdesk`)
   - Seed a default admin user if one does not exist

4. **Open Swagger UI** at `https://localhost:{port}/swagger`

### Running Migrations Manually

```bash
# From the repository root
dotnet ef migrations add <MigrationName> --project Persistence --startup-project WebApi

dotnet ef database update --project Persistence --startup-project WebApi
```

---

## Environment Variables

All secrets and environment-specific configuration are loaded from a `.env` file at the repository root via [DotNetEnv](https://github.com/tonerdo/dotnet-env). **This file must never be committed.**

The following variables are required:

| Variable                          | Description                                        |
| --------------------------------- | -------------------------------------------------- |
| `DB_HOST`                         | PostgreSQL host                                    |
| `DB_PORT`                         | PostgreSQL port                                    |
| `DB_NAME`                         | Database name                                      |
| `DB_USER`                         | Database username                                  |
| `DB_PASSWORD`                     | Database password                                  |
| `JWT_SECRET`                      | Signing key for JWT tokens (keep long and random)  |
| `JWT_EXPIRY_MINUTES`              | Access token lifetime in minutes                   |
| `JWT_REFRESH_TOKEN_EXPIRY_DAYS`   | Refresh token lifetime in days                     |
| `AZURE_STORAGE_CONNECTION_STRING` | Azure Blob Storage connection string               |
| `AZURE_STORAGE_CONTAINER_NAME`    | Container name for blob storage (default: "files") |

`appsettings.json` stores non-secret config only (Issuer, Audience, log levels). Secrets are injected into `IConfiguration` at startup from the environment variables above.

---

## Database

- **Engine:** PostgreSQL
- **Naming convention:** All table and column names are automatically converted to `snake_case` by `EFCore.NamingConventions`
- **Identity tables:** The `AspNet` prefix is stripped from all Identity tables (e.g. `AspNetUsers` → `Users`)
- **Cascade delete:** Deleting a `User` cascades to their `RefreshToken` records
- **Enum storage:** `RegistrationStatus` is stored as a string (`"Pending"`, `"Approved"`, `"Rejected"`)

### Key Tables

| Table            | Description                                              |
| ---------------- | -------------------------------------------------------- |
| `users`          | All users across all roles                               |
| `roles`          | Role definitions (Admin, Customer, Frontdesk)            |
| `user_roles`     | Join table linking users to roles                        |
| `refresh_tokens` | One row per issued refresh token, tracks lifecycle flags |

---

## Authentication & Authorization

The API uses a **stateless JWT + stateful Refresh Token** strategy.

### Access Token (JWT)

- Short-lived (configured via `JWT_EXPIRY_MINUTES`)
- Signed with HMAC SHA-256
- Claims embedded: `sub` (userId), `email`, `name`, `role`, `jti` (unique token ID)
- Sent in the `Authorization: Bearer <token>` header
- Once issued, valid until expiry — logout does not invalidate it. This is by design with stateless JWTs; the short lifetime limits the exposure window.

### Refresh Token

- Long-lived (configured via `JWT_REFRESH_TOKEN_EXPIRY_DAYS`)
- Cryptographically random (64-byte Base64 string)
- Stored in the database for full lifecycle tracking
- Sent to the client in an **HttpOnly, Secure, SameSite=None cookie** — inaccessible to JavaScript
- Also included in the login/refresh response body for clients that need it

### Authorization

Endpoints are protected with `[Authorize]` attributes:

| Attribute                                | Who can access                    |
| ---------------------------------------- | --------------------------------- |
| `[Authorize]`                            | Any authenticated user (any role) |
| `[Authorize(Roles = "Admin")]`           | Admin only                        |
| `[Authorize(Roles = "Admin,Frontdesk")]` | Admin or Frontdesk                |
| _(no attribute)_                         | Public                            |

---

## Refresh Token Lifecycle

```
Login
  └─► Issue Access Token (AT) + Refresh Token (RT)
      └─► Store RT in DB (Used=false, Invalidated=false)
          └─► Set RT in HttpOnly cookie

POST /api/auth/refresh  (AT expired, send both tokens)
  ├─► Validate AT signature (lifetime ignored)
  ├─► Look up RT in DB
  ├─► RT already Used? ──► BREACH: invalidate ALL user tokens → 401
  ├─► RT Invalidated? ──────────────────────────────────────── → 401
  ├─► RT Expired? ──────────────────────────────────────────── → 401
  ├─► JTI mismatch (AT ↔ RT)? ─────────────────────────────── → 401
  ├─► Mark old RT as Used=true
  ├─► Issue new AT + new RT (linked by new JTI)
  └─► Return new tokens, set new cookie

POST /api/auth/logout
  ├─► Read RT from cookie
  ├─► Mark RT as Invalidated=true in DB
  └─► Delete cookie from response
```

**Breach detection:** If a refresh token that has already been consumed (`Used=true`) is presented again, the system treats it as a token theft scenario and immediately invalidates every refresh token belonging to that user, forcing a full re-login.

---

## Role System

Three roles are seeded automatically on startup:

| Role        | Casing      | Description        |
| ----------- | ----------- | ------------------ |
| `Admin`     | `Admin`     | Full system access |
| `Frontdesk` | `Frontdesk` | Front desk staff   |
| `Customer`  | `Customer`  | End users          |

> **Important:** Role names are case-sensitive throughout the codebase. Always use the exact casing shown above when passing role strings to queries or `UserManager` calls.

### User Status

Users also have a `RegistrationStatus` enum:

| Status     | Meaning                                                    |
| ---------- | ---------------------------------------------------------- |
| `Pending`  | Default for self-registered users; awaiting admin approval |
| `Approved` | User can log in                                            |
| `Rejected` | Registration rejected                                      |

Admin and Frontdesk accounts are created with `Status = Approved` and `EmailConfirmed = true` by default since they are provisioned by an administrator.

### `RequiresPasswordChange`

When an admin creates or resets a user's password, `RequiresPasswordChange` is set to `true`. The frontend should check this flag in the login response and redirect to a change-password screen before allowing access. It is cleared to `false` when the user successfully calls `POST /api/auth/change-password`.

---

## API Endpoints

### Auth — `api/auth`

| Method | Endpoint           | Auth     | Description                                      |
| ------ | ------------------ | -------- | ------------------------------------------------ |
| `GET`  | `/index`           | Public   | Health check                                     |
| `POST` | `/register`        | Public   | Self-register a new user (Customer)              |
| `POST` | `/login`           | Public   | Authenticate; returns AT + RT in body and cookie |
| `POST` | `/refresh`         | Public   | Rotate refresh token; returns new AT + RT        |
| `POST` | `/change-password` | Any role | Change own password (requires current password)  |
| `POST` | `/logout`          | Public   | Invalidate refresh token, delete cookie          |

### Admin Management — `api/admin`

All endpoints require `Admin` role.

| Method   | Endpoint                   | Description                                               |
| -------- | -------------------------- | --------------------------------------------------------- |
| `POST`   | `/create-admin`            | Create a new Admin user                                   |
| `PUT`    | `/edit-admin/{id}`         | Update an Admin's profile                                 |
| `DELETE` | `/delete-admin/{id}`       | Soft-delete an Admin (sets `IsActive=false`)              |
| `GET`    | `/get-admin/{id}`          | Get Admin by ID                                           |
| `GET`    | `/active-admins`           | List all active Admins                                    |
| `GET`    | `/archived-admins`         | List all deactivated Admins                               |
| `POST`   | `/reset-password/{userId}` | Reset any user's password without knowing the current one |

### FrontDesk Management — `api/frontdesk`

| Method   | Endpoint                 | Auth             | Description                       |
| -------- | ------------------------ | ---------------- | --------------------------------- |
| `POST`   | `/create-frontdesk`      | Admin            | Create a new FrontDesk user       |
| `PUT`    | `/edit-frontdesk/{id}`   | Admin, Frontdesk | Update a FrontDesk profile        |
| `DELETE` | `/delete-frontdesk/{id}` | Admin            | Soft-delete a FrontDesk user      |
| `GET`    | `/get-frontdesk/{id}`    | Admin, Frontdesk | Get FrontDesk user by ID          |
| `GET`    | `/active-frontdesks`     | Admin            | List all active FrontDesk users   |
| `GET`    | `/archived-frontdesks`   | Admin            | List all archived FrontDesk users |

---

## CQRS Pattern

Every operation follows the same structure. There are no raw service calls in controllers — everything goes through MediatR.

```
Controller
    │
    └─► new XxxCommand(request)  ← wraps the Request DTO
            │
            └─► _mediator.Send(command)
                    │
                    ├─► ValidationBehavior  ← FluentValidation runs first
                    ├─► LoggingBehavior     ← logs start/end/errors
                    └─► XxxCommandHandler   ← actual business logic
```

### File Layout Per Feature

For any new command, create these files:

```
Application/Services/User/
├── Request/XxxRequest.cs           # Input DTO (bound from controller body)
├── Response/XxxResponse.cs         # Output DTO (returned to client)
├── Command/XxxCommand.cs           # Command class + Handler (same file)
└── Validator/XxxCommandValidator.cs # FluentValidation rules
```

For queries, same structure but in `Query/` instead of `Command/`.

### Command vs Query

|         | Command                                  | Query                                           |
| ------- | ---------------------------------------- | ----------------------------------------------- |
| Purpose | Writes / state changes                   | Reads only                                      |
| Returns | Response DTO or `Unit`                   | DTO or `List<DTO>`                              |
| Example | `LoginCommand`, `CreateFrontDeskCommand` | `GetUserByIdQuery`, `GetActiveUsersByRoleQuery` |

---

## Validation

Validation is enforced at the **command level** via `AbstractValidator<TCommand>`, not on the Request DTO directly. This ensures validation runs consistently through the MediatR pipeline regardless of how a command is created.

### Standard Password Rules

All admin-provisioned users (Admin, Frontdesk) require:

- Minimum 10 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one number

### Standard Phone Rules

- Exactly 10 digits
- Digits only (no spaces, dashes, or leading zeros)

### How Validation Errors Are Returned

Validation failures are thrown as `FluentValidation.ValidationException` and caught by `GlobalExceptionHandler`, which returns a `400 Bad Request` `ProblemDetails` response with an `errors` object grouped by field:

```json
{
  "type": "ValidationException",
  "title": "Validation Failed",
  "status": 400,
  "detail": "Invalid Inputs.",
  "errors": {
    "Request.Password": [
      "Password must contain at least one uppercase letter.",
      "Password must be at least 10 characters long."
    ],
    "Request.Email": ["Invalid email format"]
  }
}
```

---

## Error Handling

All exceptions propagate up to `GlobalExceptionHandler` (`IExceptionHandler`). Controllers contain no try-catch blocks.

| Exception Type              | HTTP Status | When Thrown                                                    |
| --------------------------- | ----------- | -------------------------------------------------------------- |
| `ValidationException`       | `400`       | FluentValidation failures or Identity errors                   |
| `ArgumentException`         | `400`       | Invalid argument                                               |
| `InvalidOperationException` | `400`       | General business rule violations                               |
| `UnauthorizedException`     | `401`       | Auth failures (bad credentials, invalid/expired/reused tokens) |
| `NotFoundException`         | `404`       | Entity not found by ID                                         |
| Unhandled `Exception`       | `500`       | Unexpected errors (detail hidden from client)                  |

All responses use the [RFC 7807 ProblemDetails](https://datatracker.ietf.org/doc/html/rfc7807) format.

---

## Logging

Structured logging is provided by **Serilog** and configured in `WebApi/DependencyInjection.cs`.

**Sinks:**

- Console — formatted with `AnsiConsoleTheme.Code`
- Rolling file — writes to `WebApi/Logs/log-{date}.txt`, rotates daily

**Log levels** (configured in `appsettings.json`):

- `Default`: Information
- `Microsoft` / `System` / `Microsoft.EntityFrameworkCore`: Warning (suppresses EF SQL noise)

**`LoggingBehavior`** wraps every MediatR command/query and provides:

- `[START]` log on entry with userId and client IP
- `[END]` log on success with elapsed milliseconds
- `[SLOW]` warning if elapsed > 500ms
- `[ERROR]` log with full exception + request data on failure
- Sensitive requests (`Login`, `Password`) have their request body automatically redacted (`[REDACTED SENSITIVE DATA]`)

Log properties attached via `LogContext`: `User_Id`, `User_Name`, `Client_IP`, `Trace_Id`, `Request_Data`.

---

## Adding New Features

### New Command (Write Operation)

1. Create `Application/Services/User/Request/XxxRequest.cs`
2. Create `Application/Services/User/Response/XxxResponse.cs`
3. Create `Application/Services/User/Command/XxxCommand.cs`:
   - `public class XxxCommand : IRequest<XxxResponse>` — wraps the request
   - `public class XxxCommandHandler : IRequestHandler<XxxCommand, XxxResponse>` — handles it
4. Create `Application/Services/User/Validator/XxxCommandValidator.cs` — extends `AbstractValidator<XxxCommand>`
5. Add the endpoint to the appropriate controller — no try-catch needed

### New Query (Read Operation)

Same as above but in `Query/` folder. Return `UserDto` (or `List<UserDto>`) for user-related reads to stay consistent with existing patterns.

### New Role

1. Add the role name to the `roles` list in `Persistence/Seeders/DataSeeder.cs`
2. Create a migration if the role needs to be seeded at the DB level (optional — seeder handles it at runtime)
3. Add a new controller or add endpoints to an existing one with `[Authorize(Roles = "YourRole")]`

### Custom Exception Type

1. Create `Application/Common/Exceptions/XxxException.cs` extending `Exception`
2. Add a `case XxxException:` block in `WebApi/Infrastructure/GlobalExceptionHandler.cs` mapping it to the appropriate HTTP status code

---

## Health Check

`GET /health` — returns `200 OK` with service status. No authentication required.

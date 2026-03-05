# Coworking Module — Phase 1 & 2 Documentation

This document covers everything implemented in Phase 1 (Foundation) and Phase 2 (Admin Management CRUD) of the Coworking Space feature. It is intended for developers joining the project or reviewing the codebase, and describes what each file does, why design decisions were made, and how the pieces fit together.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Phase 1 — Foundation](#phase-1--foundation)
   - [Domain Enums](#domain-enums)
   - [Domain Entities](#domain-entities)
   - [EF Core Configurations](#ef-core-configurations)
   - [Database Seeder](#database-seeder)
   - [Supporting Infrastructure](#supporting-infrastructure)
3. [Phase 2 — Admin Management CRUD](#phase-2--admin-management-crud)
   - [Data Transfer Objects](#data-transfer-objects)
   - [Commands](#commands)
   - [Queries](#queries)
   - [Validators](#validators)
   - [Controller](#controller)
4. [Key Design Decisions](#key-design-decisions)
5. [Database Setup](#database-setup)
6. [API Reference](#api-reference)
7. [API Testing Guide](#api-testing-guide)
   - [Prerequisites](#prerequisites)
   - [Testing Resources](#testing-resources)
   - [Testing Service Packages](#testing-service-packages)
   - [Testing Authorization](#testing-authorization)

---

## Architecture Overview

The Coworking module follows the same Clean Architecture pattern used throughout this project:

```
Domain       → Enums, Entities (pure business model, no dependencies)
Persistence  → EF Core Configurations, Migrations, Seeders
Application  → Commands, Queries, Validators, Request/Response DTOs
WebApi       → Controllers (HTTP entry points only)
```

**Key constraint:** The `Application` layer references `Persistence` directly (this is an existing project-wide decision). Handlers inject `ApplicationDbContext` rather than an abstraction interface. This is intentional and consistent with all other modules.

**Pattern used:** CQRS with MediatR. Every write operation is a `Command` and every read operation is a `Query`. Validation is handled by FluentValidation validators that are automatically invoked through the `ValidationBehavior` MediatR pipeline behavior before any handler runs.

---

## Phase 1 — Foundation

Phase 1 establishes the entire data model, database schema, and infrastructure needed to support all future Coworking phases. No application logic or HTTP endpoints are exposed in this phase — it is purely foundational.

**Files added in Phase 1:**

| Layer | File | Purpose |
|-------|------|---------|
| Domain | `Domain/Enum/ResourceType.cs` | Types of bookable spaces |
| Domain | `Domain/Enum/PackageType.cs` | Duration tiers for packages |
| Domain | `Domain/Enum/BookingType.cs` | How a booking was created |
| Domain | `Domain/Enum/BookingStatus.cs` | Full booking lifecycle states |
| Domain | `Domain/Enum/PaymentStatus.cs` | Payment progress states |
| Domain | `Domain/Enum/PaymentMethod.cs` | Accepted payment channels |
| Domain | `Domain/Enum/PaymentValidationStatus.cs` | Manual proof validation states |
| Domain | `Domain/Enum/PaymentType.cs` | Classification of a payment record |
| Domain | `Domain/Entity/Coworking/CoworkingResource.cs` | A bookable physical space |
| Domain | `Domain/Entity/Coworking/ServicePackage.cs` | A pricing option for a resource |
| Domain | `Domain/Entity/Coworking/Booking.cs` | Central booking record |
| Domain | `Domain/Entity/Coworking/BookingGuest.cs` | Guest details for a booking |
| Domain | `Domain/Entity/Coworking/BookingPayment.cs` | Individual payment transaction |
| Domain | `Domain/Entity/Coworking/BookingExtension.cs` | Time extension added to a booking |
| Persistence | `Persistence/Configurations/CoworkingResourceConfiguration.cs` | EF table/column mapping |
| Persistence | `Persistence/Configurations/ServicePackageConfiguration.cs` | EF table/column mapping |
| Persistence | `Persistence/Configurations/BookingConfiguration.cs` | EF table/column/index mapping |
| Persistence | `Persistence/Configurations/BookingGuestConfiguration.cs` | 1-to-1 relationship config |
| Persistence | `Persistence/Configurations/BookingPaymentConfiguration.cs` | EF table/column mapping |
| Persistence | `Persistence/Configurations/BookingExtensionConfiguration.cs` | EF table/column mapping |
| Persistence | `Persistence/Seeders/CoworkingSeeder.cs` | Initial resources and packages |
| Application | `Application/Common/Exceptions/ConflictException.cs` | 409 conflict error type |
| Application | `Application/Common/Settings/CoworkingSettings.cs` | App-level config values |

**Modified files in Phase 1:**

| File | What changed |
|------|-------------|
| `Persistence/Data/ApplicationDbContext.cs` | Added 6 `DbSet<>` properties for all coworking entities |
| `Persistence/Seeders/DataSeeder.cs` | Injected and called `CoworkingSeeder` |
| `Persistence/DependencyInjection.cs` | Registered `CoworkingSeeder` as scoped |
| `WebApi/Infrastructure/GlobalExceptionHandler.cs` | Added `ConflictException → 409` mapping |
| `WebApi/appsettings.json` | Added `CoworkingSettings` section |
| `WebApi/DependencyInjection.cs` | Added rate limiting policies and `CoworkingSettings` binding |
| `WebApi/Program.cs` | Added `app.UseRateLimiter()` to the middleware pipeline |

---

### Domain Enums

Enums are defined in `Domain/Enum/` and are the shared vocabulary for the entire module. All enum values are assigned explicit integer values so that stored database integers remain stable even if enum members are reordered.

---

#### `ResourceType`

Identifies which type of physical space a resource or package belongs to.

```csharp
public enum ResourceType
{
    CoworkingSeats = 1,     // Individual open-plan desks
    ExecutiveRoom = 2,      // Small private meeting room (max 3 pax)
    CollaborationRoom = 3   // Large meeting/event room (max 20 pax)
}
```

Used on both `CoworkingResource` and `ServicePackage` to associate packages with the correct space type. It is also used as a query filter on both admin list endpoints.

---

#### `PackageType`

Defines the duration tier of a service package.

```csharp
public enum PackageType
{
    Hourly = 1,               // Pay-per-hour, metered (DurationHours is null)
    DayPass = 2,              // Full day access (typically 10 hours)
    HalfDay = 3,              // Half-day block (4–5 hours depending on room)
    FullDay = 4,              // Full-day block (8 hours)
    WeeklyPass = 5,           // 5 consecutive business days
    MonthlyDedicatedDesk = 6  // 30 days, dedicated seat assignment
}
```

Flat packages (`HalfDay`, `FullDay`, `DayPass`, `WeeklyPass`, `MonthlyDedicatedDesk`) have a fixed `PriceAmount` regardless of actual hours used within the block. `Hourly` packages are metered — each hour consumes `PriceAmount` from the balance.

---

#### `BookingType`

Distinguishes how a booking was initiated.

```csharp
public enum BookingType
{
    Online = 1,  // Guest booked through the public website
    WalkIn = 2   // Front desk staff created the booking on-site
}
```

This affects payment flow: online bookings require a downpayment before confirmation; walk-in bookings can be paid in full at the desk.

---

#### `BookingStatus`

Tracks a booking through its full lifecycle.

```csharp
public enum BookingStatus
{
    Pending = 1,                      // Created, awaiting payment/confirmation
    PendingStudentVerification = 2,   // Student discount requested; ID not yet verified
    Confirmed = 3,                    // Payment received; awaiting check-in time
    CheckedIn = 4,                    // Guest is currently in the space
    CheckedOut = 5,                   // Session ended on time; fully settled
    Cancelled = 6,                    // Booking was cancelled before check-in
    NoShow = 7,                       // Guest never arrived; forfeited
    Overstayed = 8                    // Guest exceeded scheduled end time; extra fee pending
}
```

**Important:** `Overstayed` is not a terminal state. The booking transitions from `Overstayed` to `CheckedOut` once the overstay fee is paid in full. This is enforced in the payment processing handler (Phase 4+).

State transitions are only allowed via the domain methods on the `Booking` entity — handlers must not mutate `BookingStatus` directly.

---

#### `PaymentStatus`

Tracks the payment progress of a booking independently from its operational status.

```csharp
public enum PaymentStatus
{
    Unpaid = 1,           // No payment recorded yet
    DownpaymentPaid = 2,  // Only the required downpayment has been received
    PartiallyPaid = 3,    // Some payment exists but balance remains
    FullyPaid = 4         // Complete — no outstanding balance
}
```

---

#### `PaymentMethod`

The channel through which a payment was made.

```csharp
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    GCash = 3,
    Maya = 4,
    BankTransfer = 5,
    Paymongo = 6  // Online card/e-wallet via Paymongo gateway (Phase 11)
}
```

`GCash`, `Maya`, and `BankTransfer` require manual proof-of-payment upload and admin validation. `Paymongo` is handled automatically via webhook in Phase 11.

---

#### `PaymentValidationStatus`

Tracks the admin review state of a manually uploaded payment proof.

```csharp
public enum PaymentValidationStatus
{
    NotRequired = 1,       // Cash or card — no proof needed
    PendingValidation = 2, // Proof uploaded; waiting for admin review
    Validated = 3,         // Admin confirmed the payment
    Rejected = 4           // Admin rejected the proof
}
```

---

#### `PaymentType`

Classifies what a `BookingPayment` record represents.

```csharp
public enum PaymentType
{
    Downpayment = 1,   // Required upfront amount for online bookings
    Balance = 2,       // Remaining amount due
    OverstayFee = 3,   // Additional charge for exceeding scheduled time
    ExtensionFee = 4,  // Fee for an explicitly requested time extension
    Refund = 5         // Returned amount (partial or full cancellation refund)
}
```

---

### Domain Entities

All entities inherit from `GuidBaseEntity` which provides `Id (Guid)`, `CreatedAt (DateTime)`, and `UpdatedAt (DateTime)`.

---

#### `CoworkingResource`

**File:** `Domain/Entity/Coworking/CoworkingResource.cs`

A physical bookable space — either an individual seat or a meeting room. This is what gets reserved when a booking is made.

```
Properties:
  Name         — Display name (e.g., "Seat 01", "Executive Room")
  ResourceType — Categorizes the space (CoworkingSeats, ExecutiveRoom, CollaborationRoom)
  Capacity     — Maximum number of people the space can hold
  Description  — Optional human-readable notes
  IsActive     — Whether the resource is currently available for booking
  SortOrder    — Controls display order in the admin UI

Navigations:
  Bookings        — All bookings made for this resource
  ServicePackages — All pricing packages associated with this resource type
```

Resources are never deleted — they are deactivated via the toggle endpoint. Deleting a resource with existing bookings is blocked at the database level (`DeleteBehavior.Restrict`).

---

#### `ServicePackage`

**File:** `Domain/Entity/Coworking/ServicePackage.cs`

A pricing option that defines how long a guest can use a resource and what it costs. Packages belong to a `ResourceType`, not to a specific resource instance.

```
Properties:
  Name                  — Package display name
  ResourceType          — Which space type this package applies to
  PackageType           — Duration tier (Hourly, DayPass, HalfDay, etc.)
  PriceAmount           — Base price in PHP
  DurationHours         — Fixed duration in hours (null for Hourly = metered)
  DurationDays          — Fixed duration in days (for weekly/monthly packages)
  MaxCapacity           — Optional cap on pax count for this package
  IsStudentEligible     — Whether students can use the student discount on this package
  StudentDiscountPercent — Percentage off the base price for eligible students
  DownpaymentPercent    — Required upfront percentage for online bookings (default: 50%)
  IsActive              — Whether this package is currently offered
```

Package prices are never directly used for financial calculations at checkout. Instead, prices are **snapshotted** into the `Booking` at creation time so historical bookings remain accurate even after prices change.

---

#### `Booking`

**File:** `Domain/Entity/Coworking/Booking.cs`

The central record of the module. Every coworking session — whether walk-in or online — produces one `Booking`. It holds the complete financial picture and lifecycle state.

**Identity and classification:**
```
ReferenceNumber    — Human-readable unique ID (e.g., "VO-000123"), generated via PostgreSQL sequence
BookingType        — Online or WalkIn
BookingStatus      — Current lifecycle state (see BookingStatus enum)
PaymentStatus      — Payment progress (see PaymentStatus enum)
```

**Who and what:**
```
ResourceId         — Which space is being booked (FK → CoworkingResource)
ServicePackageId   — Which package was selected (FK → ServicePackage)
UserId             — Optional: registered member making the booking (post-MVP)
FrontDeskUserId    — Staff who processed the booking (string FK to AspNetUsers)
PaxCount           — Number of people (1 by default)
IsStudent          — Whether student pricing applies
```

**Schedule:**
```
ScheduledStart  — Planned start time (UTC)
ScheduledEnd    — Planned end time (UTC)
ActualStart     — When the guest actually checked in
ActualEnd       — When the guest actually checked out
```

**Price snapshot (frozen at booking creation):**
```
SnapshotPackageName   — Package name at time of booking
SnapshotPricePerUnit  — Package price at time of booking
SnapshotHourlyRate    — Hourly equivalent rate (used for overstay/extension calculations)
```

The `SnapshotHourlyRate` computation rule:
- For `Hourly` packages: `PriceAmount` (already a per-hour rate)
- For flat packages: `PriceAmount / DurationHours`

This ensures overstay and extension fees are always calculated against the rate the guest originally agreed to, regardless of any future price changes.

**Financials:**
```
BaseAmount        — Pre-discount total
DiscountAmount    — Student or other discount applied
DownpaymentAmount — Required upfront amount
BalanceAmount     — Remaining after downpayment
OverstayAmount    — Extra fees accrued from exceeding ScheduledEnd
TotalAmount       — Final billed amount including overstay
```

**Background job fields:**
```
ReminderSentAt        — Set when the pre-session reminder email is sent
EndingReminderSentAt  — Set when the session-ending warning email is sent
```

These fields are included in Phase 1 to avoid requiring new migrations when background jobs are added in later phases.

**Domain state transition methods:**

Rather than letting handlers set `BookingStatus` freely, the `Booking` entity exposes explicit methods. This keeps business rules centralized and prevents invalid transitions.

```csharp
CheckIn(DateTime actualStart)
```
Sets `ActualStart` and transitions status to `CheckedIn`.

```csharp
CheckOut(DateTime actualEnd, decimal snapshotHourlyRate)
```
Sets `ActualEnd`, computes overstay hours (`Max(0, actualHours - scheduledHours)`), calculates `OverstayAmount`, and sets status to either `CheckedOut` (no overstay) or `Overstayed` (overstay detected). Handlers must not compute overstay manually.

```csharp
Cancel(string reason)
```
Sets status to `Cancelled` and records the reason.

```csharp
MarkNoShow(string? reason = null)
```
Sets status to `NoShow` and immediately sets `PaymentStatus` to `FullyPaid` — the downpayment is forfeited in full.

---

#### `BookingGuest`

**File:** `Domain/Entity/Coworking/BookingGuest.cs`

Stores the personal details of a guest for an online or walk-in booking. This is a separate entity from `Booking` to keep the booking table clean and to allow the guest record to be populated independently (e.g., online form submitted by the guest before admin confirms).

```
FirstName, LastName     — Guest's name
Email                   — Contact email
Phone                   — Contact phone number
StudentIdImageUrl       — URL of uploaded student ID image (Phase 8)
IsStudentVerified       — Whether an admin has verified the student ID
BookingId               — FK to Booking (dependent side of 1-to-1)
```

**Relationship note:** `BookingGuest` is the dependent side of the 1-to-1 relationship with `Booking`. The `Booking` entity does not have a `GuestId` column — only `BookingGuest.BookingId` exists as the foreign key. This avoids a circular reference in the EF model.

---

#### `BookingPayment`

**File:** `Domain/Entity/Coworking/BookingPayment.cs`

One row per payment transaction. A single booking can have multiple payment records (downpayment, balance, overstay fee, extension fee, refund).

```
BookingId               — FK to the parent Booking
Amount                  — Transaction amount
PaymentMethod           — How the payment was made
PaymentType             — What the payment is for (Downpayment, Balance, etc.)
PaymentValidationStatus — Review state for manually uploaded proofs
ProofOfPaymentUrl       — File URL for GCash/Maya/BankTransfer screenshots
ReferenceNumber         — Guest-provided transaction reference number
ExternalPaymentRef      — Paymongo event ID, stored for webhook idempotency
PaidAt                  — Timestamp of payment
ValidatedByUserId       — Staff who approved the proof (string FK)
ValidatedAt             — When the validation was performed
ProcessedByUserId       — Staff who recorded the payment (string FK)
```

The `ExternalPaymentRef` field is included in the initial schema specifically to prevent a migration in Phase 11 when the Paymongo webhook integration is added.

---

#### `BookingExtension`

**File:** `Domain/Entity/Coworking/BookingExtension.cs`

Records each time a guest requests additional hours beyond their original booking end time.

```
BookingId           — FK to the parent Booking
AddedHours          — How many hours were added
SnapshotHourlyRate  — Rate frozen from Booking.SnapshotHourlyRate at extension time
Amount              — AddedHours × SnapshotHourlyRate
ProcessedByUserId   — Staff who processed the extension (string FK)
Notes               — Optional remarks
```

The `SnapshotHourlyRate` is copied from the parent `Booking` at extension time — not recalculated from the package — ensuring the guest is charged the same rate they were originally quoted.

---

### EF Core Configurations

All configurations live in `Persistence/Configurations/` and implement `IEntityTypeConfiguration<T>`. They are discovered automatically by `ApplyConfigurationsFromAssembly` in `ApplicationDbContext`.

PostgreSQL snake_case naming is applied globally via `UseSnakeCaseNamingConvention()`, so a property named `BookingStatus` maps to the column `booking_status` without needing explicit column name annotations.

---

#### `BookingConfiguration`

The most important configuration file. Key decisions:

- **`ReferenceNumber` unique index** — Enforced at DB level to prevent duplicate reference numbers.
- **Decimal precision (`18, 2`)** — All monetary fields use `HasPrecision(18, 2)` for financial accuracy.
- **`PaymentStatus` default** — Defaults to `Unpaid (1)` at the database level so new rows are consistent even if application code forgets to set it.
- **Restrict deletes on Resource and ServicePackage FKs** — A resource or package with existing bookings cannot be deleted. The admin must deactivate it instead.
- **Composite index on `(ResourceId, ScheduledStart, ScheduledEnd)`** — The availability overlap query (`WHERE resource_id = X AND scheduled_start < requestedEnd AND scheduled_end > requestedStart`) hits this index.
- **Index on `BookingStatus`** — Used by background jobs and admin list queries to filter by status efficiently.

---

#### `BookingGuestConfiguration`

Configures the 1-to-1 relationship between `BookingGuest` and `Booking`:

```csharp
builder.HasOne(g => g.Booking)
    .WithOne(b => b.Guest)
    .HasForeignKey<BookingGuest>(g => g.BookingId)
    .OnDelete(DeleteBehavior.Cascade);
```

Cascading delete is intentional — if a booking is deleted, its guest record should be removed too.

---

### Database Seeder

**File:** `Persistence/Seeders/CoworkingSeeder.cs`

Seeds the initial coworking resources and service packages on first run. The seeder is idempotent — it checks `AnyAsync()` before inserting and exits early if data already exists, so it is safe to re-run.

**`SeedResourcesAsync()`** creates:
- 20 individual coworking seats (Seat 01 through Seat 20), each with `Capacity = 1`
- Executive Room — `Capacity = 3`, `SortOrder = 21`
- Collaboration Room — `Capacity = 20`, `SortOrder = 22`

**`SeedServicePackagesAsync()`** creates 11 service packages:

| Package | Resource Type | Type | Price (PHP) | Duration |
|---------|--------------|------|-------------|----------|
| Hourly — Regular | Coworking Seats | Hourly | ₱50/hr | Metered |
| Hourly — Student | Coworking Seats | Hourly | ₱40/hr | Metered |
| Day Pass | Coworking Seats | DayPass | ₱600 | 10 hours |
| Weekly Pass | Coworking Seats | WeeklyPass | ₱2,700 | 5 days |
| Monthly Dedicated Desk | Coworking Seats | MonthlyDedicatedDesk | ₱8,500 | 30 days |
| Executive Room — Hourly | Executive Room | Hourly | ₱800/hr | Metered |
| Executive Room — Half Day | Executive Room | HalfDay | ₱3,000 | 5 hours |
| Executive Room — Full Day | Executive Room | FullDay | ₱5,500 | 8 hours |
| Collaboration Room — Hourly | Collaboration Room | Hourly | ₱1,500/hr | Metered |
| Collaboration Room — Half Day | Collaboration Room | HalfDay | ₱5,500 | 4 hours |
| Collaboration Room — Full Day | Collaboration Room | FullDay | ₱9,500 | 8 hours |

**Note on student packages:** "Hourly — Student" is a separate package with its own base price (₱40), not a discount applied to the regular hourly rate. Packages with `IsStudentEligible = true` (Day Pass, Weekly Pass, Monthly) apply a 10% student discount on top of the base price when `IsStudent = true` on the booking.

The seeder is registered as a scoped service and invoked from `DataSeeder.SeedAsync()`, which is the existing entry point called at application startup.

---

### Supporting Infrastructure

#### `ConflictException`

**File:** `Application/Common/Exceptions/ConflictException.cs`

A custom exception that signals a business rule conflict (e.g., duplicate names, resource in use). The global exception handler maps it to HTTP `409 Conflict`.

```csharp
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
```

Used in commands to reject operations like creating a resource with a name that already exists, or disabling a resource while a guest is checked in.

---

#### `CoworkingSettings`

**File:** `Application/Common/Settings/CoworkingSettings.cs`

A POCO bound to `appsettings.json` under the `CoworkingSettings` key. Injected via `IOptions<CoworkingSettings>` in future handlers.

```csharp
public class CoworkingSettings
{
    public int AutoForfeitMinutes { get; set; } = 30;
    public int SessionEndingReminderMinutes { get; set; } = 15;
    public string AdminNotificationEmail { get; set; } = "ranielmontebon24@gmail.com";
}
```

| Setting | Default | Purpose |
|---------|---------|---------|
| `AutoForfeitMinutes` | 30 | Minutes after scheduled start before a no-show is auto-cancelled |
| `SessionEndingReminderMinutes` | 15 | Minutes before scheduled end to send a session-ending warning |
| `AdminNotificationEmail` | ranielmontebon24@gmail.com | Email address that receives new booking notifications |

---

#### Rate Limiting

**Configured in:** `WebApi/DependencyInjection.cs` and `WebApi/Program.cs`

Two fixed-window rate limit policies are registered using ASP.NET Core's built-in `AddRateLimiter`:

| Policy Name | Limit | Window | Used For |
|-------------|-------|--------|---------|
| `"booking"` | 10 requests | 1 minute | Public booking submission endpoint (Phase 6) |
| `"email-verification"` | 3 requests | 5 minutes | Email verification resend endpoint (Phase 6) |

Rejected requests return HTTP `429 Too Many Requests`. The policies are applied to individual controller actions with `[EnableRateLimiting("booking")]` when those endpoints are added in Phase 6.

---

## Phase 2 — Admin Management CRUD

Phase 2 exposes a full admin API for managing coworking resources and service packages. All endpoints require the `Admin` role.

**Files added in Phase 2:**

| Layer | File | Purpose |
|-------|------|---------|
| Application | `Request/CreateCoworkingResourceRequest.cs` | Create resource payload |
| Application | `Request/UpdateCoworkingResourceRequest.cs` | Partial update resource payload |
| Application | `Request/CreateServicePackageRequest.cs` | Create package payload |
| Application | `Request/UpdateServicePackageRequest.cs` | Partial update package payload |
| Application | `Response/CoworkingResourceResponse.cs` | Resource read model |
| Application | `Response/ServicePackageResponse.cs` | Package read model |
| Application | `Command/CreateCoworkingResourceCommand.cs` | Create resource |
| Application | `Command/UpdateCoworkingResourceCommand.cs` | Update resource fields |
| Application | `Command/ToggleCoworkingResourceCommand.cs` | Enable/disable resource |
| Application | `Command/CreateServicePackageCommand.cs` | Create service package |
| Application | `Command/UpdateServicePackageCommand.cs` | Update package fields |
| Application | `Command/ToggleServicePackageCommand.cs` | Activate/deactivate package |
| Application | `Query/GetAllCoworkingResourcesQuery.cs` | List resources with filters |
| Application | `Query/GetCoworkingResourceByIdQuery.cs` | Get single resource |
| Application | `Query/GetAllServicePackagesQuery.cs` | List packages with filters |
| Application | `Query/GetServicePackageByIdQuery.cs` | Get single package |
| Application | `Validator/CreateCoworkingResourceCommandValidator.cs` | Validation for create resource |
| Application | `Validator/UpdateCoworkingResourceCommandValidator.cs` | Validation for update resource |
| Application | `Validator/CreateServicePackageCommandValidator.cs` | Validation for create package |
| Application | `Validator/UpdateServicePackageCommandValidator.cs` | Validation for update package |
| WebApi | `Controllers/CoworkingAdminController.cs` | HTTP endpoints |

---

### Data Transfer Objects

#### Request DTOs

**`CreateCoworkingResourceRequest`** — Required fields for a new resource:
```
Name         — Resource display name (required, max 100 chars)
ResourceType — Space category (required, must be valid enum value)
Capacity     — Max headcount (required, min 1)
Description  — Optional notes
SortOrder    — Display position (required, min 0)
```

**`UpdateCoworkingResourceRequest`** — All fields are optional. Only provided fields are updated (partial update pattern). `ResourceType` is intentionally excluded — the type of a resource cannot be changed after creation as existing bookings and packages rely on it.
```
Name?        — New display name
Capacity?    — New max headcount
Description? — New notes
SortOrder?   — New display position
```

**`CreateServicePackageRequest`** — Required fields for a new package:
```
Name                   — Package name (required, max 150 chars)
ResourceType           — Which space type (required)
PackageType            — Duration tier (required)
PriceAmount            — Base price (required, > 0)
DurationHours?         — Fixed hours (null = metered/hourly)
DurationDays?          — Fixed days (for weekly/monthly)
MaxCapacity?           — Optional pax limit
IsStudentEligible      — Whether student discount applies
StudentDiscountPercent — Discount percentage (0–100)
DownpaymentPercent     — Required upfront % for online bookings (0–100)
```

**`UpdateServicePackageRequest`** — All fields optional. Every updatable package field can be changed:
```
Name?                   ResourceType?
PackageType?            PriceAmount?
DurationHours?          DurationDays?
MaxCapacity?            IsStudentEligible?
StudentDiscountPercent? DownpaymentPercent?
```

#### Response DTOs

**`CoworkingResourceResponse`** — Returned by all resource reads/writes:
```
Id, Name, ResourceType, Capacity, Description, IsActive, SortOrder, CreatedAt, UpdatedAt
```

**`ServicePackageResponse`** — Returned by all package reads/writes:
```
Id, Name, ResourceType, PackageType, PriceAmount,
DurationHours, DurationDays, MaxCapacity,
IsStudentEligible, StudentDiscountPercent, DownpaymentPercent,
IsActive, CreatedAt, UpdatedAt
```

---

### Commands

Commands are write operations. Each command class carries the input data; the handler class contains all business logic.

---

#### `CreateCoworkingResourceCommand`

Creates a new coworking resource. The handler checks for name uniqueness globally (case-insensitive) across all resources before inserting. New resources are always created with `IsActive = true`.

**Business rules:**
- Name must be unique (case-insensitive) across all existing resources.
- Throws `ConflictException (409)` if the name is already taken.

---

#### `UpdateCoworkingResourceCommand`

Updates one or more fields of an existing resource. Uses the partial update pattern — only fields explicitly provided in the request are applied, all others are left unchanged.

**Business rules:**
- Throws `NotFoundException (404)` if the resource ID does not exist.
- If `Name` is provided, checks for name conflicts with other resources (excludes the current resource from the check).
- `ResourceType` cannot be changed. It is excluded from the update request entirely.

---

#### `ToggleCoworkingResourceCommand`

Flips `IsActive` on a resource. If the resource is currently active, it is disabled; if disabled, it is re-enabled.

**Business rules:**
- Throws `NotFoundException (404)` if the resource does not exist.
- Throws `ConflictException (409)` if disabling is attempted while a guest is currently `CheckedIn` to the resource. The admin must process the active session first.
- Re-enabling a resource has no restrictions.

**Response:** Returns a lightweight object with the resource `id`, `name`, the new `isActive` state, and a plain-English confirmation `message`.

---

#### `CreateServicePackageCommand`

Creates a new service package. Name uniqueness is scoped to `ResourceType` — two packages can share the same name if they belong to different resource types.

**Business rules:**
- Name must be unique (case-insensitive) within the same `ResourceType`.
- Throws `ConflictException (409)` if a duplicate name-and-type combination exists.
- New packages are always created with `IsActive = true`.

**`MapToResponse` is shared:** `CreateServicePackageCommandHandler.MapToResponse` is declared `internal static` so it can be called from `UpdateServicePackageCommand`, `GetAllServicePackagesQuery`, and `GetServicePackageByIdQuery` without duplication. This is intentional — there is one mapping definition for the entire module.

---

#### `UpdateServicePackageCommand`

Updates one or more fields of an existing package. All fields are optional.

**Business rules:**
- Throws `NotFoundException (404)` if the package does not exist.
- If `Name` is provided, the uniqueness check uses `ResourceType` from the request if it was also changed, otherwise falls back to the existing package's `ResourceType`.
- Reuses `CreateServicePackageCommandHandler.MapToResponse` for the response.

---

#### `ToggleServicePackageCommand`

Flips `IsActive` on a service package. Simpler than the resource toggle — no session conflict check is needed because disabling a package does not affect bookings that have already been made with it (the price is snapshotted).

**Response:** Returns `id`, `name`, new `isActive` state, and a confirmation `message`.

---

### Queries

Queries are read-only operations that never modify state.

---

#### `GetAllCoworkingResourcesQuery`

Returns a filtered, ordered list of all coworking resources.

**Filters (both optional):**
- `ResourceType?` — Return only resources of a specific type
- `IsActive?` — Return only active or only inactive resources; omit to return both

**Ordering:** `SortOrder ASC`, then `Name ASC` as a tiebreaker.

---

#### `GetCoworkingResourceByIdQuery`

Returns a single resource by its GUID. Throws `NotFoundException (404)` if not found.

---

#### `GetAllServicePackagesQuery`

Returns a filtered, ordered list of all service packages.

**Filters (both optional):**
- `ResourceType?` — Return only packages for a specific resource type
- `IsActive?` — Return only active or inactive packages

**Ordering:** `ResourceType ASC`, then `PackageType ASC`, then `Name ASC`.

Reuses `CreateServicePackageCommandHandler.MapToResponse` for projection.

---

#### `GetServicePackageByIdQuery`

Returns a single package by its GUID. Throws `NotFoundException (404)` if not found. Also reuses the shared `MapToResponse`.

---

### Validators

Validators use FluentValidation and target the **command** class, not the request DTO. This is required by the `ValidationBehavior` pipeline, which intercepts the command before the handler runs and returns a `400 Bad Request` with field-level error details if any rule fails.

---

#### `CreateCoworkingResourceCommandValidator`

Validates the `CreateCoworkingResourceCommand`. All fields are required on create.

| Field | Rules |
|-------|-------|
| `Request.Name` | Not empty, max 100 characters |
| `Request.ResourceType` | Must be a valid `ResourceType` enum value |
| `Request.Capacity` | Must be ≥ 1 |
| `Request.SortOrder` | Must be ≥ 0 |

---

#### `UpdateCoworkingResourceCommandValidator`

Validates the `UpdateCoworkingResourceCommand`. Fields are optional — rules only fire when the field is present.

| Field | Condition | Rules |
|-------|-----------|-------|
| `Id` | Always | Not empty (Guid must be provided) |
| `Request.Name` | Only if provided (not null) | Not empty, max 100 characters |
| `Request.Capacity` | Only if provided (HasValue) | Must be ≥ 1 |
| `Request.SortOrder` | Only if provided (HasValue) | Must be ≥ 0 |

---

#### `CreateServicePackageCommandValidator`

Validates the `CreateServicePackageCommand`.

| Field | Rules |
|-------|-------|
| `Request.Name` | Not empty, max 150 characters |
| `Request.ResourceType` | Valid enum value |
| `Request.PackageType` | Valid enum value |
| `Request.PriceAmount` | Must be > 0 |
| `Request.StudentDiscountPercent` | Must be between 0 and 100 (inclusive) |
| `Request.DownpaymentPercent` | Must be between 0 and 100 (inclusive) |

---

#### `UpdateServicePackageCommandValidator`

Validates the `UpdateServicePackageCommand`. Rules are conditional — they only apply when the field is present in the request.

| Field | Condition | Rules |
|-------|-----------|-------|
| `Id` | Always | Not empty |
| `Request.Name` | Only if provided | Not empty, max 150 characters |
| `Request.PriceAmount` | Only if provided | Must be > 0 |
| `Request.StudentDiscountPercent` | Only if provided | Between 0 and 100 |
| `Request.DownpaymentPercent` | Only if provided | Between 0 and 100 |

---

### Controller

**File:** `WebApi/Controllers/CoworkingAdminController.cs`

**Route base:** `api/coworking/admin`

**Authorization:** `[Authorize(Roles = "Admin")]` is applied at the controller class level — all 10 endpoints require the Admin role. No individual endpoint overrides are needed.

The controller is intentionally thin. It instantiates the appropriate command or query, calls `_mediator.Send()`, and returns `Ok(result)`. All business logic lives in the handlers, all validation lives in the validators.

**Route constraints:** All `{id}` parameters use `:guid` route constraints, so a non-GUID value in the URL is rejected by the framework before the handler is ever called.

---

## Key Design Decisions

### Price Snapshot Pattern

When a booking is created, the package's current price, name, and hourly rate are copied into the `Booking` row as `SnapshotPricePerUnit`, `SnapshotPackageName`, and `SnapshotHourlyRate`. This means admin can change or deactivate packages at any time without affecting the financial accuracy of past bookings. All overstay and extension fee calculations use the frozen snapshot values.

### PostgreSQL Sequence for Reference Numbers

Rather than computing `MAX(id) + 1` or `COUNT + 1`, reference numbers are generated using a PostgreSQL sequence (`booking_ref_seq`). Sequences are atomic and race-safe — two concurrent booking requests can never receive the same reference number. The sequence must be manually added to the migration's `Up()` method:

```csharp
migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS booking_ref_seq START 1;");
```

The reference number format is `VO-{nextval('booking_ref_seq'):D6}` (e.g., `VO-000001`).

### Partial Update Pattern

Both update commands (`UpdateCoworkingResourceCommand` and `UpdateServicePackageCommand`) use nullable types for all optional fields. The handler checks each field individually and only applies it if it is non-null (or `HasValue` for value types). This avoids the need for a separate PATCH DTO and keeps the API consistent — clients always `PUT` to the update endpoint but can send only the fields they want to change.

### Shared `MapToResponse`

`CreateServicePackageCommandHandler.MapToResponse` is `internal static` and reused by the update handler and both query handlers. This ensures there is exactly one place where a `ServicePackage` entity is mapped to a `ServicePackageResponse` DTO — adding a new field to the response requires changing exactly one method.

### No Navigation Properties to `ApplicationUser`

Both `Booking` and `BookingPayment` have user-tracking fields (`FrontDeskUserId`, `ValidatedByUserId`, etc.) stored as `string?` with no navigation property. This avoids circular dependencies between the Coworking domain and the Identity module. User lookups are done independently when needed.

### Domain Methods Over Direct Mutation

Booking state transitions (`CheckIn`, `CheckOut`, `Cancel`, `MarkNoShow`) are methods on the `Booking` entity. Handlers that attempt to directly set `BookingStatus` should use these methods instead. This keeps the transition logic (including the overstay calculation in `CheckOut`) in one place and makes the intent explicit.

---

## Database Setup

After pulling this code for the first time, create and apply the migration:

```bash
dotnet ef migrations add CoworkingInitialSchema --project Persistence --startup-project WebApi
```

Before applying, open the generated migration file and add the sequence creation inside the `Up()` method:

```csharp
migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS booking_ref_seq START 1;");
```

Then apply:

```bash
dotnet ef database update --project Persistence --startup-project WebApi
```

The seeder runs automatically on startup and populates resources and packages if the tables are empty.

---

## API Reference

All endpoints are under `api/coworking/admin` and require `Authorization: Bearer <admin-token>`.

### Resources

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/resources` | Create a new coworking resource |
| `PUT` | `/resources/{id}` | Update resource fields (partial) |
| `PATCH` | `/resources/{id}/toggle` | Enable or disable a resource |
| `GET` | `/resources` | List resources (filter by `resourceType`, `isActive`) |
| `GET` | `/resources/{id}` | Get a single resource by ID |

**Create/Update request example:**
```json
{
  "name": "Seat 21",
  "resourceType": 1,
  "capacity": 1,
  "description": "Newly added seat near the window.",
  "sortOrder": 23
}
```

**Toggle response example:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Seat 21",
  "isActive": false,
  "message": "'Seat 21' has been disabled."
}
```

### Service Packages

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/packages` | Create a new service package |
| `PUT` | `/packages/{id}` | Update package fields (partial) |
| `PATCH` | `/packages/{id}/toggle` | Activate or deactivate a package |
| `GET` | `/packages` | List packages (filter by `resourceType`, `isActive`) |
| `GET` | `/packages/{id}` | Get a single package by ID |

**Create request example:**
```json
{
  "name": "Day Pass",
  "resourceType": 1,
  "packageType": 2,
  "priceAmount": 600.00,
  "durationHours": 10,
  "isStudentEligible": true,
  "studentDiscountPercent": 10,
  "downpaymentPercent": 50
}
```

### `ResourceType` Enum Values

| Value | Integer |
|-------|---------|
| CoworkingSeats | 1 |
| ExecutiveRoom | 2 |
| CollaborationRoom | 3 |

### `PackageType` Enum Values

| Value | Integer |
|-------|---------|
| Hourly | 1 |
| DayPass | 2 |
| HalfDay | 3 |
| FullDay | 4 |
| WeeklyPass | 5 |
| MonthlyDedicatedDesk | 6 |

### Error Responses

| HTTP Status | When |
|------------|------|
| `400 Bad Request` | Validation failed — response body contains field-level error messages |
| `401 Unauthorized` | No token or token is invalid |
| `403 Forbidden` | Token is valid but user is not Admin |
| `404 Not Found` | Resource or package ID does not exist |
| `409 Conflict` | Duplicate name, or toggle blocked due to active session |

---

## API Testing Guide

This section walks through every Phase 2 endpoint with complete request bodies, realistic test data, and the exact response shapes you should expect. Test cases are ordered so that outputs from earlier tests (like created IDs) feed into later ones.

All tests assume:
- The application is running at `https://localhost:7000` (adjust to your local port).
- The migration has been applied and the seeder has run.
- A tool like **Postman**, **Insomnia**, or the Swagger UI at `/swagger` is being used.

---

### Prerequisites

#### Step 1 — Obtain an Admin Token

All Phase 2 endpoints require `Authorization: Bearer <token>` with an Admin-role account. Log in first using the existing auth endpoint.

**Request:**
```
POST https://localhost:7000/api/auth/login
Content-Type: application/json
```
```json
{
  "email": "admin@virtualoffice.com",
  "password": "Admin@123456"
}
```

> Use whatever admin credentials were seeded in your environment. If you don't have one, create one via `POST /api/admin/create-admin` (no auth needed for first-time seeded admin, or check the `DataSeeder`).

**Expected response — `200 OK`:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiration": "2026-03-07T10:00:00Z"
}
```

Copy the `token` value. In every subsequent request, add this header:

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

#### Step 2 — Verify Seeded Data

Before testing writes, confirm that the seeder ran correctly.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

An array of 22 objects. The first 20 are individual seats (`resourceType: 1`), followed by the Executive Room (`resourceType: 2`) and Collaboration Room (`resourceType: 3`). Abbreviated example:

```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "Seat 01",
    "resourceType": 1,
    "capacity": 1,
    "description": "Individual coworking seat #01",
    "isActive": true,
    "sortOrder": 1,
    "createdAt": "2026-03-06T08:00:00Z",
    "updatedAt": "2026-03-06T08:00:00Z"
  },
  {
    "id": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "name": "Seat 02",
    "resourceType": 1,
    "capacity": 1,
    "description": "Individual coworking seat #02",
    "isActive": true,
    "sortOrder": 2,
    "createdAt": "2026-03-06T08:00:00Z",
    "updatedAt": "2026-03-06T08:00:00Z"
  },
  // ... seats 03–20 ...
  {
    "id": "c3d4e5f6-a7b8-9012-cdef-123456789012",
    "name": "Executive Room",
    "resourceType": 2,
    "capacity": 3,
    "description": "Private executive meeting room. Maximum 3 pax.",
    "isActive": true,
    "sortOrder": 21,
    "createdAt": "2026-03-06T08:00:00Z",
    "updatedAt": "2026-03-06T08:00:00Z"
  },
  {
    "id": "d4e5f6a7-b8c9-0123-defa-234567890123",
    "name": "Collaboration Room",
    "resourceType": 3,
    "capacity": 20,
    "description": "Large collaboration meeting room. Maximum 20 pax.",
    "isActive": true,
    "sortOrder": 22,
    "createdAt": "2026-03-06T08:00:00Z",
    "updatedAt": "2026-03-06T08:00:00Z"
  }
]
```

> **Note:** The GUIDs in your environment will differ from the examples above. Copy an actual `id` from your response to use in the GET/PUT/PATCH tests below.

---

### Testing Resources

---

#### R-01 — Create Resource (Success)

Creates a new coworking seat that is not part of the seeded data.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Seat 21",
  "resourceType": 1,
  "capacity": 1,
  "description": "Additional seat added after launch. Near the power strip wall.",
  "sortOrder": 23
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "e5f6a7b8-c9d0-1234-efab-345678901234",
  "name": "Seat 21",
  "resourceType": 1,
  "capacity": 1,
  "description": "Additional seat added after launch. Near the power strip wall.",
  "isActive": true,
  "sortOrder": 23,
  "createdAt": "2026-03-06T09:15:00Z",
  "updatedAt": "2026-03-06T09:15:00Z"
}
```

> Save the returned `id` as `{seat-21-id}` for use in tests R-04 through R-08.

---

#### R-02 — Create Resource (Success — Meeting Room)

Creates a second Executive Room to confirm meeting rooms can be created the same way.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Executive Room B",
  "resourceType": 2,
  "capacity": 4,
  "description": "Second private executive room on the second floor.",
  "sortOrder": 24
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "f6a7b8c9-d0e1-2345-fabc-456789012345",
  "name": "Executive Room B",
  "resourceType": 2,
  "capacity": 4,
  "description": "Second private executive room on the second floor.",
  "isActive": true,
  "sortOrder": 24,
  "createdAt": "2026-03-06T09:16:00Z",
  "updatedAt": "2026-03-06T09:16:00Z"
}
```

---

#### R-03 — Create Resource (Fail — Duplicate Name)

Attempts to create a resource whose name already exists in the database (seeded "Seat 01").

**Request:**
```
POST https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Seat 01",
  "resourceType": 1,
  "capacity": 1,
  "description": "Duplicate attempt.",
  "sortOrder": 99
}
```

**Expected response — `409 Conflict`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "A resource named 'Seat 01' already exists."
}
```

---

#### R-04 — Create Resource (Fail — Validation Errors)

Sends a body with multiple invalid fields to verify all validation rules fire together.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "",
  "resourceType": 99,
  "capacity": 0,
  "sortOrder": -5
}
```

**Expected response — `400 Bad Request`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Request.Name": ["Resource name is required."],
    "Request.ResourceType": ["Invalid resource type."],
    "Request.Capacity": ["Capacity must be at least 1."],
    "Request.SortOrder": ["Sort order must be 0 or greater."]
  }
}
```

---

#### R-05 — Create Resource (Fail — Name Too Long)

Sends a name that exceeds the 100-character limit.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "This resource name is intentionally way too long and should be rejected because it exceeds one hundred characters total",
  "resourceType": 1,
  "capacity": 1,
  "sortOrder": 0
}
```

**Expected response — `400 Bad Request`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Request.Name": ["Resource name must not exceed 100 characters."]
  }
}
```

---

#### R-06 — Get Resource by ID (Success)

Fetches the resource created in R-01. Replace `{seat-21-id}` with the actual ID.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources/{seat-21-id}
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**
```json
{
  "id": "{seat-21-id}",
  "name": "Seat 21",
  "resourceType": 1,
  "capacity": 1,
  "description": "Additional seat added after launch. Near the power strip wall.",
  "isActive": true,
  "sortOrder": 23,
  "createdAt": "2026-03-06T09:15:00Z",
  "updatedAt": "2026-03-06T09:15:00Z"
}
```

---

#### R-07 — Get Resource by ID (Fail — Not Found)

Requests a GUID that does not exist in the database.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources/00000000-0000-0000-0000-000000000000
Authorization: Bearer <token>
```

**Expected response — `404 Not Found`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Resource with ID 00000000-0000-0000-0000-000000000000 was not found."
}
```

---

#### R-08 — Get All Resources (No Filter)

Returns all resources, ordered by `sortOrder` then `name`.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

An array of all resources currently in the database (at minimum the 22 seeded + the 2 created in R-01 and R-02 = 24 total, if tests ran in order). Items are sorted by `sortOrder ASC`.

---

#### R-09 — Get All Resources (Filter by ResourceType)

Returns only coworking seats.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources?resourceType=1
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

Array containing only resources with `resourceType: 1`. After seeding + R-01, this should be 21 items (Seat 01–21).

---

#### R-10 — Get All Resources (Filter by IsActive)

Returns only currently active resources. Since no resources have been disabled yet, this returns everything.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources?isActive=true
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

All resources with `isActive: true`.

---

#### R-11 — Get All Resources (Combined Filter)

Returns only active coworking seats.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources?resourceType=1&isActive=true
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

Array of coworking seats where `isActive` is `true`.

---

#### R-12 — Update Resource (Success — Partial)

Updates only the `description` and `sortOrder` of Seat 21. Name and capacity remain unchanged.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/resources/{seat-21-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "description": "Corner seat near the window. Best natural lighting.",
  "sortOrder": 25
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "{seat-21-id}",
  "name": "Seat 21",
  "resourceType": 1,
  "capacity": 1,
  "description": "Corner seat near the window. Best natural lighting.",
  "isActive": true,
  "sortOrder": 25,
  "createdAt": "2026-03-06T09:15:00Z",
  "updatedAt": "2026-03-06T09:20:00Z"
}
```

Note that `updatedAt` has changed but `createdAt` has not.

---

#### R-13 — Update Resource (Success — Rename)

Renames Seat 21 to something unique.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/resources/{seat-21-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Window Seat"
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "{seat-21-id}",
  "name": "Window Seat",
  "resourceType": 1,
  "capacity": 1,
  "description": "Corner seat near the window. Best natural lighting.",
  "isActive": true,
  "sortOrder": 25,
  "createdAt": "2026-03-06T09:15:00Z",
  "updatedAt": "2026-03-06T09:22:00Z"
}
```

---

#### R-14 — Update Resource (Fail — Rename to Existing Name)

Tries to rename Seat 21 to "Seat 01", which already exists.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/resources/{seat-21-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Seat 01"
}
```

**Expected response — `409 Conflict`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "A resource named 'Seat 01' already exists."
}
```

---

#### R-15 — Update Resource (Fail — Not Found)

Attempts to update a resource with a non-existent ID.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/resources/00000000-0000-0000-0000-000000000000
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "capacity": 2
}
```

**Expected response — `404 Not Found`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Resource with ID 00000000-0000-0000-0000-000000000000 was not found."
}
```

---

#### R-16 — Update Resource (Fail — Validation Error)

Sends capacity of 0, which violates the minimum-1 rule.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/resources/{seat-21-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "capacity": 0
}
```

**Expected response — `400 Bad Request`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Request.Capacity": ["Capacity must be at least 1."]
  }
}
```

---

#### R-17 — Toggle Resource (Disable — Success)

Disables the resource created in R-01. Since no booking is currently checked in to it, the toggle succeeds.

**Request:**
```
PATCH https://localhost:7000/api/coworking/admin/resources/{seat-21-id}/toggle
Authorization: Bearer <token>
```

> No request body is needed.

**Expected response — `200 OK`:**
```json
{
  "id": "{seat-21-id}",
  "name": "Window Seat",
  "isActive": false,
  "message": "'Window Seat' has been disabled."
}
```

---

#### R-18 — Verify Disable (Filtered List)

Confirms the disabled resource no longer appears when filtering for active-only resources.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources?isActive=false
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

An array containing only resources where `isActive: false`. "Window Seat" should appear here.

---

#### R-19 — Toggle Resource (Re-enable — Success)

Re-enables the resource disabled in R-17.

**Request:**
```
PATCH https://localhost:7000/api/coworking/admin/resources/{seat-21-id}/toggle
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**
```json
{
  "id": "{seat-21-id}",
  "name": "Window Seat",
  "isActive": true,
  "message": "'Window Seat' has been enabled."
}
```

---

#### R-20 — Toggle Resource (Fail — Not Found)

Attempts to toggle a resource that does not exist.

**Request:**
```
PATCH https://localhost:7000/api/coworking/admin/resources/00000000-0000-0000-0000-000000000000/toggle
Authorization: Bearer <token>
```

**Expected response — `404 Not Found`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Resource with ID 00000000-0000-0000-0000-000000000000 was not found."
}
```

---

#### R-21 — Toggle Resource (Fail — Active Session Conflict)

This scenario cannot be fully tested until Phase 3 (walk-in check-in) is implemented, since it requires a booking with `BookingStatus = CheckedIn` to be present. The expected behavior when that condition is met is documented here for reference.

**Precondition:** A booking for resource `{seat-21-id}` exists with `BookingStatus = 4 (CheckedIn)`.

**Request:**
```
PATCH https://localhost:7000/api/coworking/admin/resources/{seat-21-id}/toggle
Authorization: Bearer <token>
```

**Expected response — `409 Conflict`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Cannot disable this resource while a guest is currently checked in. Please process the active session first."
}
```

---

### Testing Service Packages

---

#### P-01 — Create Package (Success — Hourly, No Student Discount)

Creates a new hourly package for coworking seats. `durationHours` is `null` because hourly packages are metered.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/packages
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Evening Hourly",
  "resourceType": 1,
  "packageType": 1,
  "priceAmount": 45.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 50
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "a1a1a1a1-b2b2-c3c3-d4d4-e5e5e5e5e5e5",
  "name": "Evening Hourly",
  "resourceType": 1,
  "packageType": 1,
  "priceAmount": 45.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-06T09:30:00Z",
  "updatedAt": "2026-03-06T09:30:00Z"
}
```

> Save the returned `id` as `{evening-hourly-id}` for use in tests P-06 through P-10.

---

#### P-02 — Create Package (Success — Flat Duration with Student Discount)

Creates a half-day package for the collaboration room with a fixed duration and student eligibility.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/packages
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Collaboration Room — Morning Half Day",
  "resourceType": 3,
  "packageType": 3,
  "priceAmount": 4800.00,
  "durationHours": 4,
  "durationDays": null,
  "maxCapacity": 15,
  "isStudentEligible": true,
  "studentDiscountPercent": 15,
  "downpaymentPercent": 30
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "b2b2b2b2-c3c3-d4d4-e5e5-f6f6f6f6f6f6",
  "name": "Collaboration Room — Morning Half Day",
  "resourceType": 3,
  "packageType": 3,
  "priceAmount": 4800.00,
  "durationHours": 4,
  "durationDays": null,
  "maxCapacity": 15,
  "isStudentEligible": true,
  "studentDiscountPercent": 15.0,
  "downpaymentPercent": 30.0,
  "isActive": true,
  "createdAt": "2026-03-06T09:32:00Z",
  "updatedAt": "2026-03-06T09:32:00Z"
}
```

---

#### P-03 — Create Package (Success — Same Name, Different ResourceType)

Verifies that name uniqueness is scoped per `resourceType`. "Evening Hourly" already exists for `resourceType: 1` (CoworkingSeats), but it can be created for `resourceType: 2` (ExecutiveRoom).

**Request:**
```
POST https://localhost:7000/api/coworking/admin/packages
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Evening Hourly",
  "resourceType": 2,
  "packageType": 1,
  "priceAmount": 700.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 50
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "c3c3c3c3-d4d4-e5e5-f6f6-a7a7a7a7a7a7",
  "name": "Evening Hourly",
  "resourceType": 2,
  "packageType": 1,
  "priceAmount": 700.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-06T09:34:00Z",
  "updatedAt": "2026-03-06T09:34:00Z"
}
```

---

#### P-04 — Create Package (Fail — Duplicate Name within Same ResourceType)

Tries to create another package named "Evening Hourly" for `resourceType: 1`, which already exists from P-01.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/packages
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Evening Hourly",
  "resourceType": 1,
  "packageType": 1,
  "priceAmount": 35.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 50
}
```

**Expected response — `409 Conflict`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "A package named 'Evening Hourly' already exists for CoworkingSeats."
}
```

---

#### P-05 — Create Package (Fail — Validation Errors)

Sends multiple invalid values in a single request to verify all rules fire together.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/packages
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "",
  "resourceType": 0,
  "packageType": 99,
  "priceAmount": -100,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 150,
  "downpaymentPercent": -10
}
```

**Expected response — `400 Bad Request`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Request.Name": ["Package name is required."],
    "Request.ResourceType": ["Invalid resource type."],
    "Request.PackageType": ["Invalid package type."],
    "Request.PriceAmount": ["Price must be greater than 0."],
    "Request.StudentDiscountPercent": ["Student discount must be between 0 and 100."],
    "Request.DownpaymentPercent": ["Downpayment percent must be between 0 and 100."]
  }
}
```

---

#### P-06 — Create Package (Fail — Price is Zero)

`priceAmount` must be strictly greater than 0.

**Request:**
```
POST https://localhost:7000/api/coworking/admin/packages
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Free Trial",
  "resourceType": 1,
  "packageType": 1,
  "priceAmount": 0,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 0
}
```

**Expected response — `400 Bad Request`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Request.PriceAmount": ["Price must be greater than 0."]
  }
}
```

---

#### P-07 — Get Package by ID (Success)

Fetches the "Evening Hourly" package created in P-01.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/packages/{evening-hourly-id}
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**
```json
{
  "id": "{evening-hourly-id}",
  "name": "Evening Hourly",
  "resourceType": 1,
  "packageType": 1,
  "priceAmount": 45.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-06T09:30:00Z",
  "updatedAt": "2026-03-06T09:30:00Z"
}
```

---

#### P-08 — Get Package by ID (Fail — Not Found)

**Request:**
```
GET https://localhost:7000/api/coworking/admin/packages/00000000-0000-0000-0000-000000000000
Authorization: Bearer <token>
```

**Expected response — `404 Not Found`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Service package with ID 00000000-0000-0000-0000-000000000000 was not found."
}
```

---

#### P-09 — Get All Packages (No Filter)

Returns all packages ordered by `resourceType`, then `packageType`, then `name`.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/packages
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

An array of all packages (11 seeded + any created in tests P-01 through P-03). The ordering groups all `resourceType: 1` packages first, then `resourceType: 2`, then `resourceType: 3`. Within each group, packages are ordered by their `packageType` integer value (Hourly=1 first, then DayPass=2, etc.).

Abbreviated expected shape:
```json
[
  {
    "id": "...",
    "name": "Evening Hourly",
    "resourceType": 1,
    "packageType": 1,
    "priceAmount": 45.00,
    "durationHours": null,
    "durationDays": null,
    "maxCapacity": null,
    "isStudentEligible": false,
    "studentDiscountPercent": 0.0,
    "downpaymentPercent": 50.0,
    "isActive": true,
    "createdAt": "...",
    "updatedAt": "..."
  },
  {
    "id": "...",
    "name": "Hourly — Regular",
    "resourceType": 1,
    "packageType": 1,
    "priceAmount": 50.00,
    "durationHours": null,
    "durationDays": null,
    "maxCapacity": null,
    "isStudentEligible": false,
    "studentDiscountPercent": 0.0,
    "downpaymentPercent": 50.0,
    "isActive": true,
    "createdAt": "...",
    "updatedAt": "..."
  }
  // ... more packages ...
]
```

---

#### P-10 — Get All Packages (Filter by ResourceType)

Returns only Executive Room packages.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/packages?resourceType=2
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

Array of packages where `resourceType: 2`. After seeding + P-03, this should include:

```json
[
  {
    "name": "Evening Hourly",
    "resourceType": 2,
    "packageType": 1,
    "priceAmount": 700.00,
    ...
  },
  {
    "name": "Executive Room — Hourly",
    "resourceType": 2,
    "packageType": 1,
    "priceAmount": 800.00,
    ...
  },
  {
    "name": "Executive Room — Half Day",
    "resourceType": 2,
    "packageType": 3,
    "priceAmount": 3000.00,
    ...
  },
  {
    "name": "Executive Room — Full Day",
    "resourceType": 2,
    "packageType": 4,
    "priceAmount": 5500.00,
    ...
  }
]
```

---

#### P-11 — Get All Packages (Filter by IsActive)

Returns only currently active packages. Since none have been deactivated yet, this returns all packages.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/packages?isActive=true
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

Full list of all packages with `isActive: true`.

---

#### P-12 — Update Package (Success — Price Only)

Updates only the price of the "Evening Hourly" package. All other fields remain unchanged.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/packages/{evening-hourly-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "priceAmount": 55.00
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "{evening-hourly-id}",
  "name": "Evening Hourly",
  "resourceType": 1,
  "packageType": 1,
  "priceAmount": 55.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-06T09:30:00Z",
  "updatedAt": "2026-03-06T09:45:00Z"
}
```

Note that `priceAmount` changed from `45.00` to `55.00` and `updatedAt` is refreshed. Everything else is identical to the P-07 GET response.

---

#### P-13 — Update Package (Success — Enable Student Discount)

Adds a student discount and changes the name in a single request.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/packages/{evening-hourly-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Evening Hourly — Student Promo",
  "isStudentEligible": true,
  "studentDiscountPercent": 20
}
```

**Expected response — `200 OK`:**
```json
{
  "id": "{evening-hourly-id}",
  "name": "Evening Hourly — Student Promo",
  "resourceType": 1,
  "packageType": 1,
  "priceAmount": 55.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": true,
  "studentDiscountPercent": 20.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-06T09:30:00Z",
  "updatedAt": "2026-03-06T09:47:00Z"
}
```

---

#### P-14 — Update Package (Fail — Not Found)

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/packages/00000000-0000-0000-0000-000000000000
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "priceAmount": 100.00
}
```

**Expected response — `404 Not Found`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Service package with ID 00000000-0000-0000-0000-000000000000 was not found."
}
```

---

#### P-15 — Update Package (Fail — Duplicate Name within Same ResourceType)

Tries to rename "Evening Hourly — Student Promo" to "Day Pass", which already exists for `resourceType: 1`.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/packages/{evening-hourly-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "name": "Day Pass"
}
```

**Expected response — `409 Conflict`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "A package named 'Day Pass' already exists for CoworkingSeats."
}
```

---

#### P-16 — Update Package (Fail — Validation Errors)

Sends an invalid price and out-of-range discount in the same request.

**Request:**
```
PUT https://localhost:7000/api/coworking/admin/packages/{evening-hourly-id}
Authorization: Bearer <token>
Content-Type: application/json
```
```json
{
  "priceAmount": 0,
  "studentDiscountPercent": 110,
  "downpaymentPercent": -5
}
```

**Expected response — `400 Bad Request`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Request.PriceAmount": ["Price must be greater than 0."],
    "Request.StudentDiscountPercent": ["Student discount must be between 0 and 100."],
    "Request.DownpaymentPercent": ["Downpayment percent must be between 0 and 100."]
  }
}
```

---

#### P-17 — Toggle Package (Deactivate — Success)

Deactivates the "Evening Hourly — Student Promo" package.

**Request:**
```
PATCH https://localhost:7000/api/coworking/admin/packages/{evening-hourly-id}/toggle
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**
```json
{
  "id": "{evening-hourly-id}",
  "name": "Evening Hourly — Student Promo",
  "isActive": false,
  "message": "'Evening Hourly — Student Promo' has been deactivated."
}
```

---

#### P-18 — Verify Deactivation (Filtered List)

Confirms the deactivated package is excluded from active-only queries.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/packages?isActive=false
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**

Array containing only packages with `isActive: false`. "Evening Hourly — Student Promo" should appear here.

---

#### P-19 — Toggle Package (Reactivate — Success)

Re-enables the package deactivated in P-17.

**Request:**
```
PATCH https://localhost:7000/api/coworking/admin/packages/{evening-hourly-id}/toggle
Authorization: Bearer <token>
```

**Expected response — `200 OK`:**
```json
{
  "id": "{evening-hourly-id}",
  "name": "Evening Hourly — Student Promo",
  "isActive": true,
  "message": "'Evening Hourly — Student Promo' has been activated."
}
```

---

#### P-20 — Toggle Package (Fail — Not Found)

**Request:**
```
PATCH https://localhost:7000/api/coworking/admin/packages/00000000-0000-0000-0000-000000000000/toggle
Authorization: Bearer <token>
```

**Expected response — `404 Not Found`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Service package with ID 00000000-0000-0000-0000-000000000000 was not found."
}
```

---

### Testing Authorization

These tests verify that the authorization layer works correctly regardless of which endpoint is called. Test using any endpoint — `GET /resources` is used here as a representative example.

---

#### A-01 — No Token (Unauthorized)

Sends a request with no `Authorization` header at all.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources
```

**Expected response — `401 Unauthorized`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401
}
```

---

#### A-02 — Expired or Invalid Token

Sends a request with a malformed or expired token.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer this.is.not.a.valid.token
```

**Expected response — `401 Unauthorized`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401
}
```

---

#### A-03 — Valid Token, Wrong Role (Forbidden)

Logs in as a FrontDesk user (not Admin) and attempts to access the admin coworking endpoints.

**Step 1 — Get a FrontDesk token:**
```
POST https://localhost:7000/api/auth/login
Content-Type: application/json
```
```json
{
  "email": "frontdesk@virtualoffice.com",
  "password": "FrontDesk@123456"
}
```

**Step 2 — Use the FrontDesk token on an Admin endpoint:**
```
GET https://localhost:7000/api/coworking/admin/resources
Authorization: Bearer <frontdesk-token>
```

**Expected response — `403 Forbidden`:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403
}
```

---

#### A-04 — Invalid Route ID (Not a GUID)

Sends a non-GUID value in the `{id}` path segment. The `:guid` route constraint rejects it before the handler runs.

**Request:**
```
GET https://localhost:7000/api/coworking/admin/resources/not-a-valid-guid
Authorization: Bearer <token>
```

**Expected response — `404 Not Found`:**

The framework returns 404 because no route matches `resources/not-a-valid-guid` (the `:guid` constraint does not match). No handler is invoked.

---

### Quick Reference — All Test Cases

| # | Method | Endpoint | Scenario | Expected Status |
|---|--------|----------|----------|----------------|
| R-01 | POST | `/resources` | Create new seat | 200 |
| R-02 | POST | `/resources` | Create meeting room | 200 |
| R-03 | POST | `/resources` | Duplicate name | 409 |
| R-04 | POST | `/resources` | Empty name, invalid enum, capacity 0, negative sortOrder | 400 |
| R-05 | POST | `/resources` | Name exceeds 100 chars | 400 |
| R-06 | GET | `/resources/{id}` | Fetch created resource | 200 |
| R-07 | GET | `/resources/{id}` | Non-existent ID | 404 |
| R-08 | GET | `/resources` | All resources, no filter | 200 |
| R-09 | GET | `/resources?resourceType=1` | Filter by CoworkingSeats | 200 |
| R-10 | GET | `/resources?isActive=true` | Filter active only | 200 |
| R-11 | GET | `/resources?resourceType=1&isActive=true` | Combined filter | 200 |
| R-12 | PUT | `/resources/{id}` | Partial update (description, sortOrder) | 200 |
| R-13 | PUT | `/resources/{id}` | Rename to unique name | 200 |
| R-14 | PUT | `/resources/{id}` | Rename to existing name | 409 |
| R-15 | PUT | `/resources/{id}` | Non-existent ID | 404 |
| R-16 | PUT | `/resources/{id}` | Invalid capacity | 400 |
| R-17 | PATCH | `/resources/{id}/toggle` | Disable active resource | 200 |
| R-18 | GET | `/resources?isActive=false` | Verify disable reflects in filter | 200 |
| R-19 | PATCH | `/resources/{id}/toggle` | Re-enable disabled resource | 200 |
| R-20 | PATCH | `/resources/{id}/toggle` | Non-existent ID | 404 |
| R-21 | PATCH | `/resources/{id}/toggle` | Disable with active checked-in session | 409 |
| P-01 | POST | `/packages` | Create hourly (no student discount) | 200 |
| P-02 | POST | `/packages` | Create flat with student discount | 200 |
| P-03 | POST | `/packages` | Same name, different resourceType | 200 |
| P-04 | POST | `/packages` | Duplicate name + same resourceType | 409 |
| P-05 | POST | `/packages` | All fields invalid | 400 |
| P-06 | POST | `/packages` | Price is zero | 400 |
| P-07 | GET | `/packages/{id}` | Fetch created package | 200 |
| P-08 | GET | `/packages/{id}` | Non-existent ID | 404 |
| P-09 | GET | `/packages` | All packages, no filter | 200 |
| P-10 | GET | `/packages?resourceType=2` | Filter by ExecutiveRoom | 200 |
| P-11 | GET | `/packages?isActive=true` | Filter active only | 200 |
| P-12 | PUT | `/packages/{id}` | Update price only | 200 |
| P-13 | PUT | `/packages/{id}` | Update name and student discount | 200 |
| P-14 | PUT | `/packages/{id}` | Non-existent ID | 404 |
| P-15 | PUT | `/packages/{id}` | Rename to existing name in same type | 409 |
| P-16 | PUT | `/packages/{id}` | Invalid price and discounts | 400 |
| P-17 | PATCH | `/packages/{id}/toggle` | Deactivate package | 200 |
| P-18 | GET | `/packages?isActive=false` | Verify deactivation in filter | 200 |
| P-19 | PATCH | `/packages/{id}/toggle` | Reactivate package | 200 |
| P-20 | PATCH | `/packages/{id}/toggle` | Non-existent ID | 404 |
| A-01 | GET | `/resources` | No auth token | 401 |
| A-02 | GET | `/resources` | Invalid/expired token | 401 |
| A-03 | GET | `/resources` | Valid token, FrontDesk role | 403 |
| A-04 | GET | `/resources/not-a-guid` | Non-GUID route segment | 404 |

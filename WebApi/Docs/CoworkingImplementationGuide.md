# Coworking Space Module — Phased Implementation Guide

**Project:** CommunSide — Virtual Office Booking System
**Module:** Coworking Space
**Stack:** .NET 9 / Clean Architecture / CQRS (MediatR) / PostgreSQL / Azure Blob Storage
**Strategy:** Incremental delivery — each phase produces a stable, independently runnable system state

> This document is the authoritative development roadmap for the Coworking module. Every phase is independently functional. The application must compile, run, and operate correctly after each phase even if all subsequent phases are incomplete. Phases must be implemented in the order defined here — later phases depend on entities, schema, and commands established in earlier ones.

---

## Phase Summary

| # | Phase | Core Deliverable | DB Changes |
|---|---|---|---|
| 1 | Domain Foundation & Schema | All entities, enums, EF configs, migrations, seed data | Yes — initial schema |
| 2 | Admin Resource & Package Management | Admin CRUD for catalog configuration | No |
| 3 | Availability Check | Public availability query used by all booking flows | No |
| 4 | Walk-In Core Flow | Complete walk-in lifecycle: check-in → check-out → cash/card payment | No |
| 5 | Manual Payment Proof & Validation | GCash/Maya/Bank screenshot upload + FrontDesk validation | No |
| 6 | Online Guest Booking — Creation | Guest creates a pending booking (no payment yet) | No |
| 7 | Online Booking — Payment & Confirmation | Downpayment confirmation + confirmation email | No |
| 8 | Student Verification | FrontDesk verifies uploaded student IDs, applies/reverts discount | No |
| 9 | No-Show Detection & Background Jobs | Automated no-show marking, grace period email | Yes — `ReminderSentAt` |
| 10 | Session Extension | FrontDesk extends active sessions with availability check | No |
| 11 | Paymongo Integration | Automated payment via Paymongo webhook | Yes — `ExternalPaymentRef` |
    
---

## Module Folder Structure

Before writing a single line of code, establish this folder layout. All coworking-specific code lives under these paths.

```
Domain/
└── Entity/
│   └── Coworking/
│       ├── Booking.cs
│       ├── BookingExtension.cs
│       ├── BookingGuest.cs
│       ├── BookingPayment.cs
│       ├── CoworkingResource.cs
│       └── ServicePackage.cs
└── Enum/
    ├── ResourceType.cs
    ├── PackageType.cs
    ├── BookingType.cs
    ├── BookingStatus.cs
    ├── PaymentStatus.cs
    ├── PaymentMethod.cs
    ├── PaymentValidationStatus.cs
    └── PaymentType.cs

Application/
└── Services/
    └── Coworking/
        ├── Command/
        ├── Query/
        ├── Validator/
        ├── Request/
        └── Response/

Persistence/
└── Configurations/
    ├── BookingConfiguration.cs
    ├── BookingExtensionConfiguration.cs
    ├── BookingGuestConfiguration.cs
    ├── BookingPaymentConfiguration.cs
    ├── CoworkingResourceConfiguration.cs
    └── ServicePackageConfiguration.cs

WebApi/
└── Controllers/
    ├── CoworkingController.cs              ← Public (no auth)
    ├── CoworkingFrontDeskController.cs     ← Admin + Frontdesk
    └── CoworkingAdminController.cs         ← Admin only
```

---

## Cross-Cutting Concerns (Apply From Phase 1 Onward)

These apply across the entire module and must be established before or alongside Phase 1.

### New Exception Type
Add to `Application/Common/Exceptions/`:
- **`ConflictException.cs`** — thrown when a booking slot is unavailable, extension conflicts, etc.
- Register in `WebApi/Infrastructure/GlobalExceptionHandler.cs` → HTTP 409

### CoworkingSettings Configuration
`appsettings.json`:
```json
"CoworkingSettings": {
  "AutoForfeitMinutes": 30,
  "SessionEndingReminderMinutes": 15
}
```
Bound to a `CoworkingSettings` POCO in `Application/Common/Settings/CoworkingSettings.cs`.
Registered in `WebApi/Program.cs` via `services.Configure<CoworkingSettings>(configuration.GetSection("CoworkingSettings"))`.

---

## Phase 1 — Domain Foundation & Database Schema

### Objective

Establish the complete database schema, domain entities, enums, EF Core configurations, migrations, and seed data. No business logic. No HTTP endpoints. No commands or queries. The sole purpose of this phase is to build the data foundation that every subsequent phase depends on. After this phase, the application starts cleanly, all migrations run without errors, and the database is populated with reference data.

### Scope
- All 8 enums for the coworking module
- All 6 domain entities (`Booking`, `BookingGuest`, `BookingPayment`, `BookingExtension`, `CoworkingResource`, `ServicePackage`)
- EF Core Fluent API configurations for all 6 entities
- One EF Core migration: `CoworkingInitialSchema`
- `CoworkingSeeder.cs` — seeds 20 individual seats, 2 meeting rooms, and all service packages
- `DbSet<>` registrations in `ApplicationDbContext`

### Intentional Exclusions
- No commands, no queries, no validators
- No controllers or HTTP endpoints
- No Hangfire or background job setup
- No business logic of any kind

---

### 1.1 — New Enums

Location: `Domain/Enum/`

**`ResourceType.cs`**
```csharp
public enum ResourceType
{
    CoworkingSeats,
    ExecutiveRoom,
    CollaborationRoom
}
```

**`PackageType.cs`**
```csharp
public enum PackageType
{
    Hourly,
    DayPass,
    HalfDay,
    FullDay,
    WeeklyPass,
    MonthlyDedicatedDesk
}
```

**`BookingType.cs`**
```csharp
public enum BookingType
{
    Online,
    WalkIn
}
```

**`BookingStatus.cs`**
```csharp
public enum BookingStatus
{
    Pending,
    PendingStudentVerification,
    Confirmed,
    CheckedIn,
    CheckedOut,
    Cancelled,
    NoShow,
    Overstayed
}
```

**`PaymentStatus.cs`**
```csharp
public enum PaymentStatus
{
    Unpaid,
    DownpaymentPaid,
    PartiallyPaid,
    FullyPaid
}
```

**`PaymentMethod.cs`**
```csharp
public enum PaymentMethod
{
    Cash,
    Card,
    GCash,
    Maya,
    BankTransfer,
    Paymongo
}
```

**`PaymentValidationStatus.cs`**
```csharp
public enum PaymentValidationStatus
{
    NotRequired,
    PendingValidation,
    Validated,
    Rejected
}
```

**`PaymentType.cs`**
```csharp
public enum PaymentType
{
    Downpayment,
    Balance,
    OverstayFee,
    ExtensionFee,
    Refund
}
```

---

### 1.2 — Domain Entities

Location: `Domain/Entity/Coworking/`

All entities extend `GuidBaseEntity` (existing base class that provides `Id (Guid)`, `CreatedAt`, `UpdatedAt`).

---

**`CoworkingResource.cs`**

| Field | Type | Notes |
|---|---|---|
| `Name` | `string` | e.g. "Seat 01", "Executive Room" |
| `ResourceType` | `ResourceType` | Enum |
| `Capacity` | `int` | Max occupants |
| `Description` | `string?` | Optional |
| `IsActive` | `bool` | Soft disable for maintenance |
| `SortOrder` | `int` | Controls display order in UI |

---

**`ServicePackage.cs`**

| Field | Type | Notes |
|---|---|---|
| `Name` | `string` | e.g. "Day Pass", "Hourly — Student" |
| `ResourceType` | `ResourceType` | Which resource type this applies to |
| `PackageType` | `PackageType` | Enum |
| `PriceAmount` | `decimal` | Base price (precision: 18,2) |
| `DurationHours` | `int?` | Null for Hourly (metered) |
| `DurationDays` | `int?` | For WeeklyPass, MonthlyDedicatedDesk |
| `MaxCapacity` | `int?` | For meeting room packages |
| `IsStudentEligible` | `bool` | Whether student discount applies |
| `StudentDiscountPercent` | `decimal` | e.g. 10.00 (precision: 5,2) |
| `DownpaymentPercent` | `decimal` | e.g. 50.00 — for online booking split |
| `IsActive` | `bool` | Soft disable |

---

**`BookingGuest.cs`**

| Field | Type | Notes |
|---|---|---|
| `FirstName` | `string` | |
| `LastName` | `string` | |
| `Email` | `string?` | Null for walk-in guests |
| `Phone` | `string` | |
| `StudentIdImageUrl` | `string?` | Azure Blob URL — online students only |
| `IsStudentVerified` | `bool` | Default false; set by FrontDesk |
| `BookingId` | `Guid` | FK — 1-to-1 with Booking |

---

**`Booking.cs`**

| Field | Type | Notes |
|---|---|---|
| `ReferenceNumber` | `string` | e.g. "BKG-0042" — unique, generated at creation |
| `BookingType` | `BookingType` | Online or WalkIn |
| `BookingStatus` | `BookingStatus` | State machine |
| `PaymentStatus` | `PaymentStatus` | Separate from booking status |
| `ResourceId` | `Guid` | FK → CoworkingResource |
| `ServicePackageId` | `Guid` | FK → ServicePackage (for audit/reporting only — financials use snapshot) |
| `GuestId` | `Guid?` | FK → BookingGuest (null for VO Members) |
| `UserId` | `string?` | FK → AspNetUsers (VO Member bookings — post-MVP) |
| `FrontDeskUserId` | `string?` | FK → AspNetUsers — who processed the walk-in |
| `ScheduledStart` | `DateTime` | UTC |
| `ScheduledEnd` | `DateTime` | UTC |
| `ActualStart` | `DateTime?` | Set on check-in |
| `ActualEnd` | `DateTime?` | Set on check-out |
| `PaxCount` | `int` | Default 1; relevant for meeting rooms |
| `IsStudent` | `bool` | Guest declared student status |
| `SnapshotPackageName` | `string` | Frozen at creation |
| `SnapshotPricePerUnit` | `decimal` | Frozen at creation (precision: 18,2) |
| `SnapshotHourlyRate` | `decimal` | Frozen at creation — used for overstay/extension |
| `BaseAmount` | `decimal` | Before discount |
| `DiscountAmount` | `decimal` | Student discount (may be zeroed on rejection) |
| `DownpaymentAmount` | `decimal` | 0 for walk-ins |
| `DownpaymentPaidAt` | `DateTime?` | Set when downpayment confirmed |
| `BalanceAmount` | `decimal` | Remaining after downpayment |
| `OverstayAmount` | `decimal` | Computed at checkout |
| `TotalAmount` | `decimal` | Final total |
| `Notes` | `string?` | Internal FrontDesk notes |
| `CancellationReason` | `string?` | Set on cancel or no-show |
| `ReminderSentAt` | `DateTime?` | Set by no-show reminder job (Phase 9). Included in initial schema to avoid a surprise migration mid-project. |
| `EndingReminderSentAt` | `DateTime?` | Set by session-ending reminder job (Phase 10). Included in initial schema for the same reason. |

**Concurrency Token:**
Add PostgreSQL `xmin` as a concurrency token on `Booking`. This provides optimistic concurrency at the ORM level as a second line of defense — if two transactions both read the same booking row and one commits first, EF Core will throw a `DbUpdateConcurrencyException` on the second, preventing silent overwrites.
```csharp
// In BookingConfiguration.cs:
builder.Property<uint>("xmin").IsRowVersion();
```

**Domain Methods (state transitions — handlers must use these, not direct field mutation):**
```csharp
public void CheckIn(DateTime actualStart)
public void CheckOut(DateTime actualEnd)
public void Cancel(string reason)
public void MarkNoShow()
```

---

**`BookingPayment.cs`**

| Field | Type | Notes |
|---|---|---|
| `BookingId` | `Guid` | FK → Booking |
| `Amount` | `decimal` | (precision: 18,2) |
| `PaymentMethod` | `PaymentMethod` | |
| `PaymentType` | `PaymentType` | |
| `PaymentValidationStatus` | `PaymentValidationStatus` | |
| `ProofOfPaymentUrl` | `string?` | Azure Blob URL |
| `ReferenceNumber` | `string?` | Guest-provided transaction ref |
| `PaidAt` | `DateTime?` | |
| `ValidatedByUserId` | `string?` | FK → AspNetUsers |
| `ValidatedAt` | `DateTime?` | |
| `ProcessedByUserId` | `string?` | FK → AspNetUsers (FrontDesk who entered it) |

---

**`BookingExtension.cs`**

| Field | Type | Notes |
|---|---|---|
| `BookingId` | `Guid` | FK → Booking |
| `AddedHours` | `decimal` | |
| `SnapshotHourlyRate` | `decimal` | Copied from `Booking.SnapshotHourlyRate` at extension time |
| `Amount` | `decimal` | `AddedHours × SnapshotHourlyRate` |
| `ProcessedByUserId` | `string?` | FK → AspNetUsers |
| `Notes` | `string?` | |

---

### 1.3 — EF Core Configurations

Location: `Persistence/Configurations/`

| File | Key Configuration Points |
|---|---|
| `CoworkingResourceConfiguration.cs` | Table: `coworking_resources`. Index on `ResourceType`, `IsActive`. `Name` max length 100. |
| `ServicePackageConfiguration.cs` | Table: `service_packages`. Decimal precision (18,2) on `PriceAmount`. Precision (5,2) on `StudentDiscountPercent` and `DownpaymentPercent`. |
| `BookingGuestConfiguration.cs` | Table: `booking_guests`. 1-to-1 relationship with `Booking` via `BookingId`. |
| `BookingConfiguration.cs` | Table: `bookings`. Decimal precision (18,2) on all money fields. Index on `BookingStatus`, `ResourceId`, `ScheduledStart`. FK to `CoworkingResource` (restrict delete). FK to `ServicePackage` (restrict delete). `ReferenceNumber` is unique. |
| `BookingPaymentConfiguration.cs` | Table: `booking_payments`. 1-to-many with `Booking`. Decimal precision (18,2) on `Amount`. Index on `PaymentValidationStatus`. |
| `BookingExtensionConfiguration.cs` | Table: `booking_extensions`. 1-to-many with `Booking`. Decimal precision (18,2) on `Amount` and `SnapshotHourlyRate`. |

---

### 1.4 — ApplicationDbContext Changes

File: `Persistence/Data/ApplicationDbContext.cs`

Add the following `DbSet<>` properties:
```csharp
public DbSet<CoworkingResource> CoworkingResources => Set<CoworkingResource>();
public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
public DbSet<Booking> Bookings => Set<Booking>();
public DbSet<BookingGuest> BookingGuests => Set<BookingGuest>();
public DbSet<BookingPayment> BookingPayments => Set<BookingPayment>();
public DbSet<BookingExtension> BookingExtensions => Set<BookingExtension>();
```

---

### 1.5 — Migration

```bash
dotnet ef migrations add CoworkingInitialSchema --project Persistence --startup-project WebApi
dotnet ef database update --project Persistence --startup-project WebApi
```

---

### 1.6 — Seeder

File: `Persistence/Seeders/CoworkingSeeder.cs`

**Seeds `coworking_resources`:**
- 20 rows: `Name = "Seat 01"` through `"Seat 20"`, `ResourceType = CoworkingSeats`, `Capacity = 1`, `IsActive = true`, `SortOrder = 1–20`
- 1 row: `Name = "Executive Room"`, `ResourceType = ExecutiveRoom`, `Capacity = 3`, `SortOrder = 21`
- 1 row: `Name = "Collaboration Room"`, `ResourceType = CollaborationRoom`, `Capacity = 20`, `SortOrder = 22`

**Seeds `service_packages`** (per pricing matrix in `CoworkingImplementationDocs.md Section 15`):

| Name | ResourceType | PackageType | PriceAmount | DurationHours | IsStudentEligible | StudentDiscountPercent | DownpaymentPercent |
|---|---|---|---|---|---|---|---|
| Hourly — Regular | CoworkingSeats | Hourly | 50.00 | null | false | 0 | 50.00 |
| Hourly — Student | CoworkingSeats | Hourly | 40.00 | null | false | 0 | 50.00 |
| Day Pass | CoworkingSeats | DayPass | 600.00 | 10 | true | 10.00 | 50.00 |
| Weekly Pass | CoworkingSeats | WeeklyPass | 2700.00 | null | true | 10.00 | 50.00 |
| Monthly Dedicated Desk | CoworkingSeats | MonthlyDedicatedDesk | 8500.00 | null | true | 10.00 | 50.00 |
| Executive Room — Hourly | ExecutiveRoom | Hourly | 800.00 | null | false | 0 | 50.00 |
| Executive Room — Half Day | ExecutiveRoom | HalfDay | 3000.00 | 5 | false | 0 | 50.00 |
| Executive Room — Full Day | ExecutiveRoom | FullDay | 5500.00 | 8 | false | 0 | 50.00 |
| Collaboration Room — Hourly | CollaborationRoom | Hourly | 1500.00 | null | false | 0 | 50.00 |
| Collaboration Room — Half Day | CollaborationRoom | HalfDay | 5500.00 | 4 | false | 0 | 50.00 |
| Collaboration Room — Full Day | CollaborationRoom | FullDay | 9500.00 | 8 | false | 0 | 50.00 |

Register `CoworkingSeeder` in `Persistence/DependencyInjection.cs` alongside the existing `DataSeeder`.

---

### 1.7 — User Flows Operational After Phase 1

None — this phase is infrastructure only.

### 1.8 — Exit Criteria

- [ ] `dotnet build` passes with 0 errors across all 5 projects
- [ ] `dotnet ef database update` runs without errors
- [ ] All 6 new tables exist in PostgreSQL with correct column types and constraints
- [ ] 22 rows visible in `coworking_resources` (20 seats + 2 rooms)
- [ ] 11 rows visible in `service_packages`
- [ ] All existing functionality remains unaffected (auth, admin CRUD, frontdesk CRUD, file upload)

---

## Phase 2 — Admin Resource & Service Package Management

### Objective

Give the Admin the ability to manage the entire coworking catalog through the API. Resources can be added, renamed, or disabled without database migrations or code changes. Pricing packages can be created, modified, or deactivated at will without affecting historical bookings (snapshot strategy handles that). This phase validates the entire CQRS structure for the Coworking module, establishes the `CoworkingAdminController`, and makes the catalog fully configurable before any guest-facing booking logic exists.

This phase is independently useful in production: the Admin can set up and tune the catalog before the booking features go live.

### Scope
- Full CRUD for `CoworkingResource` (create, update, toggle active, list all, get by ID)
- Full CRUD for `ServicePackage` (create, update, toggle active, list all, get by ID)
- `CoworkingAdminController` — route: `api/coworking/admin`

### Intentional Exclusions
- No booking logic
- No availability checks
- No guest-facing endpoints
- No changes to existing controllers

### Database Changes

None — Phase 1 schema is used as-is.

---

### 2.1 — Commands

Location: `Application/Services/Coworking/Command/`

| Command | Handler Responsibility |
|---|---|
| `CreateCoworkingResourceCommand` | Validates name uniqueness. Inserts new `CoworkingResource`. Returns created resource. |
| `UpdateCoworkingResourceCommand` | Validates resource exists. Updates `Name`, `Capacity`, `Description`, `SortOrder`. Does not affect bookings. |
| `ToggleCoworkingResourceCommand` | Flips `IsActive`. If disabling: check for active `CheckedIn` bookings on the resource and throw `ConflictException` if any exist. |
| `CreateServicePackageCommand` | Validates name uniqueness within the same `ResourceType`. Inserts new `ServicePackage`. |
| `UpdateServicePackageCommand` | Validates package exists. Updates pricing fields. Has no effect on existing bookings (snapshots are already frozen). |
| `ToggleServicePackageCommand` | Flips `IsActive`. No conflict check needed — existing bookings are unaffected. |

---

### 2.2 — Queries

Location: `Application/Services/Coworking/Query/`

| Query | Input | Output |
|---|---|---|
| `GetAllCoworkingResourcesQuery` | `ResourceType?` (optional filter), `IsActive?` (optional filter) | `List<CoworkingResourceResponse>` |
| `GetCoworkingResourceByIdQuery` | `Guid Id` | `CoworkingResourceResponse` — throws `NotFoundException` if not found |
| `GetAllServicePackagesQuery` | `ResourceType?` (optional filter), `IsActive?` (optional filter) | `List<ServicePackageResponse>` |
| `GetServicePackageByIdQuery` | `Guid Id` | `ServicePackageResponse` — throws `NotFoundException` if not found |

---

### 2.3 — Validators

Location: `Application/Services/Coworking/Validator/`

**`CreateCoworkingResourceCommandValidator`**
- `Name`: required, max 100 characters
- `ResourceType`: must be a valid enum value
- `Capacity`: must be ≥ 1
- `SortOrder`: must be ≥ 0

**`UpdateCoworkingResourceCommandValidator`**
- `Id`: required
- Same field rules as create (all fields optional on update — only validate what is provided)

**`CreateServicePackageCommandValidator`**
- `Name`: required, max 150 characters
- `ResourceType`: valid enum
- `PackageType`: valid enum
- `PriceAmount`: > 0
- `StudentDiscountPercent`: 0–100
- `DownpaymentPercent`: 0–100

**`UpdateServicePackageCommandValidator`**
- `Id`: required
- Same field rules as create for any field provided

---

### 2.4 — Request / Response DTOs

Location: `Application/Services/Coworking/Request/` and `Application/Services/Coworking/Response/`

**Requests:**
- `CreateCoworkingResourceRequest` — `Name`, `ResourceType`, `Capacity`, `Description?`, `SortOrder`
- `UpdateCoworkingResourceRequest` — `Name?`, `Capacity?`, `Description?`, `SortOrder?`
- `CreateServicePackageRequest` — all `ServicePackage` fields except `Id`, `CreatedAt`, `UpdatedAt`
- `UpdateServicePackageRequest` — same as create but all fields optional

**Responses:**
- `CoworkingResourceResponse` — `Id`, `Name`, `ResourceType`, `Capacity`, `Description`, `IsActive`, `SortOrder`, `CreatedAt`
- `ServicePackageResponse` — all package fields including `Id`, `CreatedAt`

---

### 2.5 — Controller

File: `WebApi/Controllers/CoworkingAdminController.cs`
Base route: `api/coworking/admin`
Auth: `[Authorize(Roles = "Admin")]` on the entire controller

| Method | Route | Command / Query |
|---|---|---|
| POST | `resources` | `CreateCoworkingResourceCommand` |
| PUT | `resources/{id}` | `UpdateCoworkingResourceCommand` |
| PATCH | `resources/{id}/toggle` | `ToggleCoworkingResourceCommand` |
| GET | `resources` | `GetAllCoworkingResourcesQuery` |
| GET | `resources/{id}` | `GetCoworkingResourceByIdQuery` |
| POST | `packages` | `CreateServicePackageCommand` |
| PUT | `packages/{id}` | `UpdateServicePackageCommand` |
| PATCH | `packages/{id}/toggle` | `ToggleServicePackageCommand` |
| GET | `packages` | `GetAllServicePackagesQuery` |
| GET | `packages/{id}` | `GetServicePackageByIdQuery` |

---

### 2.6 — User Flows Operational After Phase 2

- Admin views all 22 seeded resources and 11 seeded service packages
- Admin adds "Seat 21" to accommodate business expansion — no migration needed
- Admin disables "Seat 05" for maintenance — it will be excluded from availability results in Phase 3
- Admin creates a new pricing tier (e.g., "Monthly Student Desk") — immediately available for booking in Phase 4+
- Admin edits the Day Pass price from ₱600 to ₱650 — existing bookings are unaffected because their snapshots are frozen

### 2.7 — Stubbed / Mocked

Nothing. This phase has no dependencies on unbuilt components.

### 2.8 — Exit Criteria

- [ ] All 10 endpoints return correct responses in Swagger
- [ ] `POST api/coworking/admin/resources` creates a row in `coworking_resources`
- [ ] `PATCH resources/{id}/toggle` flips `IsActive` and persists the change
- [ ] `ToggleCoworkingResourceCommand` on a resource with active `CheckedIn` bookings returns HTTP 409 (tested properly in Phase 4 — for now, verify the logic path exists)
- [ ] `POST api/coworking/admin/packages` with `PriceAmount = 0` returns HTTP 400 with a validation error message
- [ ] Non-admin JWT (e.g., Frontdesk role) returns HTTP 403 on all endpoints
- [ ] No-auth request returns HTTP 401 on all endpoints
- [ ] `UpdateServicePackageCommand` updates the live package but does not modify any booking snapshot fields

---

## Phase 3 — Availability Check (Public Endpoint)

### Objective

Build the availability query that every booking flow in this module depends on. This is the first public (unauthenticated) endpoint in the Coworking module. At this point, with no bookings in the system, every active resource returns as available — this is the correct and expected behavior and makes it easy to verify. This phase validates the availability algorithm and query structure in isolation before any state-changing operations are layered on top.

### Scope
- `CheckAvailabilityQuery` — returns all available resources for a requested date/time window
- `CheckWalkInAvailabilityQuery` — checks a specific resource for availability right now
- `CoworkingController` (new file) with two public GET endpoints

### Intentional Exclusions
- No booking creation
- No authentication requirements
- No payment logic

### Database Changes

None.

---

### 3.1 — Core Availability Algorithm

This SQL logic is the heart of every availability check in the system. It is used identically for all resource types (seats and meeting rooms).

```sql
SELECT COUNT(*) FROM bookings
WHERE resource_id = @ResourceId
  AND booking_status NOT IN ('Cancelled', 'NoShow', 'CheckedOut', 'Overstayed')
  AND scheduled_start < @RequestedEnd
  AND scheduled_end   > @RequestedStart
```

- `COUNT = 0` → resource is **available** for the requested window
- `COUNT > 0` → resource is **unavailable** (an active booking overlaps)

> **Why `Overstayed` is excluded:** `Overstayed` is set at the moment `ProcessWalkInTimeOutCommand` is called — meaning the guest has physically checked out. The booking remains in `Overstayed` until the overstay payment clears and transitions to `CheckedOut`. Without this exclusion, a departed overstayed guest would incorrectly block the resource from new bookings.

The EF Core equivalent inside the query handler:
```csharp
var isUnavailable = await _dbContext.Bookings
    .AnyAsync(b =>
        b.ResourceId == resourceId &&
        b.BookingStatus != BookingStatus.Cancelled &&
        b.BookingStatus != BookingStatus.NoShow &&
        b.BookingStatus != BookingStatus.CheckedOut &&
        b.BookingStatus != BookingStatus.Overstayed &&
        b.ScheduledStart < requestedEnd &&
        b.ScheduledEnd   > requestedStart,
        cancellationToken);
```

> **Important:** This same logic block will be copy-referenced (not literally copied) in `CreateGuestBookingCommand` (Phase 6) and `ExtendSessionCommand` (Phase 10). In both cases it runs inside a DB transaction to prevent race conditions.

---

### 3.2 — Queries

Location: `Application/Services/Coworking/Query/`

**`CheckAvailabilityQuery`**

Input:
- `Date (DateOnly)` — the booking date
- `StartTime (TimeOnly)` — session start
- `EndTime (TimeOnly)` — session end
- `ResourceType? (ResourceType)` — optional filter

Logic:
1. Convert `Date + StartTime` → `RequestedStart (DateTime UTC)`
2. Convert `Date + EndTime` → `RequestedEnd (DateTime UTC)`
3. Fetch all active resources (filtered by `ResourceType` if provided)
4. For each resource, run the availability algorithm
5. Return only the resources where the result is available

Output: `List<AvailabilityResponse>`

---

**`CheckWalkInAvailabilityQuery`**

Input:
- `ResourceId (Guid)`
- `EstimatedHours (decimal)`

Logic:
1. `RequestedStart = DateTime.UtcNow`
2. `RequestedEnd = DateTime.UtcNow + TimeSpan.FromHours(EstimatedHours)`
3. Run availability algorithm for the single resource
4. If unavailable: find the next `ScheduledEnd` for that resource to return `NextAvailableAt`

Output: `CheckWalkInAvailabilityResponse` — `IsAvailable (bool)`, `NextAvailableAt (DateTime?)`

---

### 3.3 — Validators

**`CheckAvailabilityQueryValidator`**
- `Date`: must be today or a future date
- `StartTime` must be before `EndTime`
- Time window must be ≤ 24 hours
- `ResourceType`: if provided, must be a valid enum value

**`CheckWalkInAvailabilityQueryValidator`**
- `ResourceId`: required, must be a valid Guid
- `EstimatedHours`: > 0, ≤ 24

---

### 3.4 — Request / Response DTOs

**`CheckAvailabilityRequest`** — `Date`, `StartTime`, `EndTime`, `ResourceType?`

**`AvailabilityResponse`**
```
ResourceId      Guid
ResourceName    string      (e.g. "Seat 07")
ResourceType    ResourceType
Capacity        int
IsAvailable     bool
```

**`CheckWalkInAvailabilityResponse`**
```
ResourceId      Guid
ResourceName    string
IsAvailable     bool
NextAvailableAt DateTime?   (null if available)
```

---

### 3.5 — Controller

File: `WebApi/Controllers/CoworkingController.cs`
Base route: `api/coworking`
Auth: No `[Authorize]` attribute — availability is fully public

| Method | Route | Query |
|---|---|---|
| GET | `availability` | `CheckAvailabilityQuery` |
| GET | `availability/{resourceId}` | `CheckWalkInAvailabilityQuery` |

Query parameters for the first endpoint are passed as query string: `?date=2026-02-25&startTime=09:00&endTime=17:00&resourceType=CoworkingSeats`

---

### 3.6 — User Flows Operational After Phase 3

- Frontend booking page calls `GET api/coworking/availability?date=...&startTime=...&endTime=...` and receives a list of available seats to display
- FrontDesk app can verify if a specific seat is free right now before checking a guest in
- With no bookings in the system, all 22 active resources are returned as available — this is the expected baseline state
- Resources toggled inactive in Phase 2 correctly do not appear in results

### 3.7 — Stubbed / Mocked

Nothing. With no bookings in the system, all active resources return as available. This is correct behavior and fully testable without seeded bookings.

### 3.8 — Exit Criteria

- [ ] `GET api/coworking/availability` with a valid future date/time window returns all 22 active resources
- [ ] Resources marked `IsActive = false` in Phase 2 do not appear in results
- [ ] `ResourceType` filter correctly returns only seats or only meeting rooms
- [ ] `GET api/coworking/availability/{resourceId}` returns `IsAvailable = true` for all resources with no bookings
- [ ] Validator rejects: past dates, `StartTime >= EndTime`, window > 24 hours, invalid enum value
- [ ] No authentication required — unauthenticated request returns HTTP 200

---

## Phase 4 — Walk-In Core Flow

### Objective

Deliver the first complete end-to-end business transaction: a FrontDesk staff member checks a guest in, the guest uses the space, and checks out with a cash or card payment. This is the highest-frequency daily operation in the system. Every architectural component of the Coworking module — price snapshotting, domain state transitions, the booking aggregate, overstay calculation — is exercised in this phase.

After Phase 4, the system is operationally useful for a real business day. FrontDesk can fully run walk-in operations without any other phase being complete.

### Scope
- `ProcessWalkInTimeInCommand` — creates booking in `CheckedIn` state immediately, freezes price snapshot, sets `ActualStart`
- `ProcessWalkInTimeOutCommand` — computes actual hours, overstay, transitions to `CheckedOut` or `Overstayed`
- `ProcessPaymentCommand` — records Cash or Card payment, transitions `PaymentStatus`
- Reference number generation logic
- FrontDesk read queries: active sessions, bookings by date, single booking detail
- `CoworkingFrontDeskController` (new file)

### Intentional Exclusions
- Manual payment proofs (GCash/Maya/Bank screenshot) — Phase 5
- Online booking — Phase 6+
- Session extensions — Phase 10
- Student discount — Phase 8 (the `IsStudent` flag is stored now but discount is not computed yet)
- No-show detection — Phase 9
- Email notifications — Phase 7

### Database Changes

None — Phase 1 schema is used as-is.

---

### 4.1 — Domain Methods on `Booking`

These methods encapsulate all state transitions. Command handlers must call these methods instead of mutating entity fields directly. This ensures business rules are enforced at the domain level regardless of which handler triggers the transition.

```csharp
// Sets ActualStart, transitions BookingStatus → CheckedIn
public void CheckIn(DateTime actualStart)

// Computes overstay, sets ActualEnd, transitions → CheckedOut or Overstayed
// Updates OverstayAmount using SnapshotHourlyRate
public void CheckOut(DateTime actualEnd)

// Transitions → Cancelled, sets CancellationReason
public void Cancel(string reason)

// Transitions → NoShow, sets PaymentStatus = FullyPaid (downpayment forfeited)
public void MarkNoShow()
```

---

### 4.2 — Commands

**`ProcessWalkInTimeInCommand`**

Steps executed by the handler:
1. Verify resource exists and `IsActive = true` (throw `NotFoundException` if not)
2. Verify service package exists and `IsActive = true` (throw `NotFoundException` if not)
3. Re-check availability inside a DB transaction (prevents double-booking even for walk-ins):
   ```csharp
   await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
   try
   {
       // Re-run availability check — if COUNT > 0, throw ConflictException
       // Insert BookingGuest + Booking
       await _dbContext.SaveChangesAsync(cancellationToken);
       await transaction.CommitAsync(cancellationToken);
   }
   catch
   {
       await transaction.RollbackAsync(cancellationToken);
       throw;
   }
   ```
4. Generate `ReferenceNumber` using a PostgreSQL sequence (race-safe):
   ```sql
   -- Added in the CoworkingInitialSchema migration:
   migrationBuilder.Sql("CREATE SEQUENCE booking_ref_seq START 1;");
   ```
   ```csharp
   // In the command handler:
   var nextVal = await _dbContext.Database
       .SqlQuery<long>($"SELECT nextval('booking_ref_seq')")
       .FirstAsync(cancellationToken);
   var referenceNumber = $"BKG-{nextVal:D4}";
   ```
   > **Why not `COUNT + 1`:** Under concurrent load, two requests can read the same `COUNT` and generate duplicate reference numbers. Since `ReferenceNumber` has a unique index, one insert would fail with a database exception. A PostgreSQL sequence issues a strictly sequential, atomic value — no two calls ever receive the same number, regardless of concurrency.
5. Create `BookingGuest` with `FirstName`, `LastName`, `Phone`
   - Walk-in with student: `IsStudentVerified = true` immediately (physical ID presented)
6. Freeze price snapshot:
   ```
   SnapshotPackageName  = servicePackage.Name
   SnapshotPricePerUnit = servicePackage.PriceAmount
   SnapshotHourlyRate   = servicePackage.PriceAmount  (for Hourly packages)
                        = resolve hourly-equivalent for flat packages (see note below)
   ```
7. Compute pricing:
   ```
   BaseAmount      = package is Hourly ? EstimatedHours × PricePerUnit : PricePerUnit (flat)

   // Walk-in student discount is applied HERE (not deferred to Phase 8)
   // because IsStudentVerified = true is set in step 6 — physical ID is already confirmed.
   // Phase 8 only adds the online ID upload + FrontDesk verification workflow.
   DiscountAmount  = IsStudent && IsStudentVerified && servicePackage.IsStudentEligible
                       ? BaseAmount × (servicePackage.StudentDiscountPercent / 100)
                       : 0

   TotalAmount     = BaseAmount - DiscountAmount
   BalanceAmount   = TotalAmount  (walk-ins: no downpayment split)
   DownpaymentAmount = 0
   ```
8. Create `Booking`:
   ```
   BookingType    = WalkIn
   BookingStatus  = CheckedIn     (no Pending/Confirmed for walk-ins)
   PaymentStatus  = Unpaid
   ActualStart    = DateTime.UtcNow
   ScheduledStart = DateTime.UtcNow
   ScheduledEnd   = DateTime.UtcNow + TimeSpan.FromHours(EstimatedHours)
   FrontDeskUserId = userId from JWT
   ```

> **SnapshotHourlyRate — committed decision:** Always compute and freeze at snapshot time. Do NOT add a field to `ServicePackage` — that would tie the rate to a live config value that can change, breaking historical overstay/extension accuracy.
>
> | Package Type | SnapshotHourlyRate formula |
> |---|---|
> | `Hourly` | `= ServicePackage.PriceAmount` (already a per-hour rate) |
> | `DayPass`, `HalfDay`, `FullDay`, `WeeklyPass` | `= ServicePackage.PriceAmount / ServicePackage.DurationHours` |
> | `MonthlyDedicatedDesk` | `= ServicePackage.PriceAmount / (ServicePackage.DurationDays * 8)` (assume 8hr workday) |
>
> For meeting rooms, always look up the Hourly package rate for the same `ResourceType` (e.g., Executive Room Hourly = ₱800, Collaboration Room Hourly = ₱1,500) and use that as `SnapshotHourlyRate` regardless of which flat package was booked. The handler resolves this by querying the active Hourly package for the resource type during snapshot creation.

---

**`ProcessWalkInTimeOutCommand`**

Input: `BookingId (Guid)`

Steps:
1. Fetch booking, throw `NotFoundException` if not found
2. Validate booking is in `CheckedIn` status (throw `ConflictException` otherwise)
3. Set `ActualEnd = DateTime.UtcNow`
4. Compute:
   ```
   ActualHours      = (ActualEnd - ActualStart).TotalHours
   ScheduledHours   = (ScheduledEnd - ScheduledStart).TotalHours
   OverstayHours    = Math.Max(0, ActualHours - ScheduledHours)
   OverstayAmount   = OverstayHours × SnapshotHourlyRate

   ExtensionTotal   = SUM of all BookingExtension.Amount for this booking
   TotalAmount      = BaseAmount - DiscountAmount + OverstayAmount + ExtensionTotal
   BalanceAmount    = TotalAmount - SUM(Payments where PaymentType = Downpayment)
   ```
5. If `OverstayAmount > 0`: call `booking.CheckOut(ActualEnd)` → status = `Overstayed`
6. Else: `status = CheckedOut`
7. Save changes

---

**`ProcessPaymentCommand`**

Input: `BookingId`, `Amount`, `PaymentMethod`, `PaymentType`, `Notes?`

Steps:
1. Fetch booking, validate it exists
2. **In Phase 4:** if `PaymentMethod` is `GCash`, `Maya`, `BankTransfer`, or `Paymongo` — throw `ConflictException("Use the manual proof upload endpoint for this payment method")`. These are Phase 5+ concerns.
3. Create `BookingPayment`:
   ```
   PaymentValidationStatus = NotRequired   (Cash and Card need no proof)
   PaidAt                  = DateTime.UtcNow
   ProcessedByUserId       = userId from JWT
   ```
4. Recalculate `PaymentStatus` on the booking:
   - Sum all `BookingPayment.Amount` for this booking
   - If total paid ≥ `TotalAmount` → `FullyPaid`
   - If total paid > 0 and total paid < `TotalAmount` → `PartiallyPaid`
5. **If `PaymentStatus` just became `FullyPaid` AND `BookingStatus == Overstayed`:**
   ```csharp
   booking.BookingStatus = BookingStatus.CheckedOut;
   ```
   > A booking in `Overstayed` means the guest physically left but had an unpaid overstay fee. Once fully paid, the booking must resolve to `CheckedOut` — otherwise it stays in `Overstayed` indefinitely and (without the fix from suggestion #2) would block the resource in the availability query.

---

### 4.3 — Queries

**`GetActiveSessionsQuery`**

Returns all bookings in `CheckedIn` status.

Output per row: `BookingId`, `ReferenceNumber`, `GuestName` (First + Last), `ResourceName`, `ActualStart`, `ScheduledEnd`, `SnapshotPackageName`, `TotalAmount`

**`GetBookingsByDateQuery`**

Input: `Date (DateOnly)`

Returns all bookings where `ScheduledStart` falls on the given date. Includes bookings in all statuses.

Output: same fields as above plus `BookingStatus`, `PaymentStatus`

**`GetBookingByIdQuery`**

Input: `BookingId (Guid)`

Returns full booking detail:
- All booking fields
- Nested `BookingGuest`
- Nested `List<BookingPayment>`
- Nested `List<BookingExtension>`
- Computed: `TotalPaid`, `OutstandingBalance`

---

### 4.4 — Validators

**`ProcessWalkInTimeInCommandValidator`**
- `ResourceId`: required, valid Guid
- `ServicePackageId`: required, valid Guid
- `EstimatedHours`: > 0, ≤ 24
- `GuestFirstName`: required, max 100 characters
- `GuestLastName`: required, max 100 characters
- `GuestPhone`: required, exactly 10 digits

**`ProcessPaymentCommandValidator`**
- `BookingId`: required
- `Amount`: > 0
- `PaymentMethod`: valid enum value
- `PaymentType`: valid enum value

---

### 4.5 — Request / Response DTOs

**`WalkInTimeInRequest`**
```
ResourceId          Guid
ServicePackageId    Guid
EstimatedHours      decimal
GuestFirstName      string
GuestLastName       string
GuestPhone          string
IsStudent           bool
```

**`WalkInTimeInResponse`**
```
BookingId           Guid
ReferenceNumber     string
ResourceName        string
ScheduledStart      DateTime
ScheduledEnd        DateTime
SnapshotPackageName string
BaseAmount          decimal
TotalAmount         decimal
```

**`ProcessPaymentRequest`**
```
BookingId       Guid
Amount          decimal
PaymentMethod   PaymentMethod
PaymentType     PaymentType
Notes           string?
```

**`BookingDetailResponse`**
```
BookingId           Guid
ReferenceNumber     string
BookingType         BookingType
BookingStatus       BookingStatus
PaymentStatus       PaymentStatus
ResourceName        string
GuestName           string
ScheduledStart      DateTime
ScheduledEnd        DateTime
ActualStart         DateTime?
ActualEnd           DateTime?
SnapshotPackageName string
BaseAmount          decimal
DiscountAmount      decimal
OverstayAmount      decimal
TotalAmount         decimal
DownpaymentAmount   decimal
BalanceAmount       decimal
TotalPaid           decimal         (computed from payments)
OutstandingBalance  decimal         (computed)
Payments            List<BookingPaymentResponse>
Extensions          List<BookingExtensionResponse>
```

---

### 4.6 — Controller

File: `WebApi/Controllers/CoworkingFrontDeskController.cs`
Base route: `api/coworking/frontdesk`
Auth: `[Authorize(Roles = "Admin,Frontdesk")]` on the entire controller

| Method | Route | Command / Query |
|---|---|---|
| POST | `time-in` | `ProcessWalkInTimeInCommand` |
| POST | `time-out/{bookingId}` | `ProcessWalkInTimeOutCommand` |
| POST | `payment` | `ProcessPaymentCommand` |
| GET | `sessions/active` | `GetActiveSessionsQuery` |
| GET | `bookings` | `GetBookingsByDateQuery` (`?date=2026-02-25`) |
| GET | `bookings/{id}` | `GetBookingByIdQuery` |

---

### 4.7 — User Flows Operational After Phase 4

- FrontDesk opens the app on a fresh morning → calls `GET frontdesk/sessions/active` → empty list
- FrontDesk checks in guest "Juan Dela Cruz" on Seat 07, Day Pass package, estimated 8 hours → booking created in `CheckedIn` state
- `GET api/coworking/availability` now shows Seat 07 as **unavailable** for the booked window (Phase 3 query is now meaningful)
- FrontDesk views full day schedule via `GET frontdesk/bookings?date=2026-02-25`
- Guest finishes early → FrontDesk triggers time-out → system shows `TotalAmount`
- FrontDesk collects ₱600 cash → `POST frontdesk/payment` → `PaymentStatus = FullyPaid`
- Guest overstays by 45 minutes → system adds overstay fee → FrontDesk collects the additional amount

### 4.8 — Stubbed / Mocked

- `ProcessPaymentCommand` for Phase 4 only accepts `Cash` and `Card`. Attempts to use `GCash`, `Maya`, `BankTransfer`, or `Paymongo` return HTTP 409 with a message directing to the proof upload endpoint (which will exist in Phase 5).
- No email notifications are sent in this phase.
- Walk-in student discount **is applied in this phase** — `IsStudentVerified = true` is set immediately on physical ID presentation, so the discount is computed at time-in. Phase 8 adds the online ID upload + FrontDesk verification workflow for online bookings only.

### 4.9 — Exit Criteria

- [ ] `POST frontdesk/time-in` creates one row in `bookings` and one row in `booking_guests`
- [ ] Booking status is immediately `CheckedIn` (no `Pending` or `Confirmed` intermediate states for walk-ins)
- [ ] All price snapshot fields (`SnapshotPackageName`, `SnapshotPricePerUnit`, `SnapshotHourlyRate`) are populated on the booking row
- [ ] `GET api/coworking/availability` from Phase 3 now shows the booked seat as **unavailable** during its time window
- [ ] `GET frontdesk/sessions/active` returns the checked-in booking
- [ ] `POST frontdesk/time-out/{id}` sets `ActualEnd` and transitions status to `CheckedOut`
- [ ] Overstay: if `ActualEnd > ScheduledEnd` by 45 min, `OverstayAmount = 0.75 × SnapshotHourlyRate` and status = `Overstayed`
- [ ] `POST frontdesk/payment` with Cash creates a `booking_payments` row and sets `PaymentStatus = FullyPaid`
- [ ] Full walk-in lifecycle (time-in → time-out → payment) leaves booking in `CheckedOut` + `FullyPaid`
- [ ] `GET frontdesk/bookings/{id}` returns the full booking detail including computed `OutstandingBalance`
- [ ] Concurrent time-in requests for the same seat at the same time: one succeeds, one returns HTTP 409

---

## Phase 5 — Manual Payment Proof & FrontDesk Validation

### Objective

Extend the payment system to support GCash, Maya, and Bank Transfer — the three methods that require screenshot upload and FrontDesk validation. This completes the walk-in payment story for all supported methods and introduces the manual proof validation workflow that is directly reused by the online booking flow in Phase 7. After this phase, no payment method is blocked from walk-in operations.

### Scope
- `SubmitManualPaymentProofCommand` — uploads a screenshot to Azure Blob, sets `PaymentValidationStatus = PendingValidation`
- `ValidateManualPaymentCommand` — FrontDesk approves or rejects the proof, transitions payment and booking states accordingly
- `GetPendingPaymentValidationsQuery` — FrontDesk dashboard queue of unvalidated proofs
- Extend `ProcessPaymentCommand` (from Phase 4) to allow `GCash`, `Maya`, and `BankTransfer` as valid methods (remove the Phase 4 stub rejection)

### Intentional Exclusions
- Online booking payment flow — Phase 7
- `ConfirmDownpaymentCommand` — Phase 7 (downpayment is an online-only concept)
- Paymongo — Phase 11

### Database Changes

None — the schema already has `ProofOfPaymentUrl`, `PaymentValidationStatus`, `ValidatedByUserId`, and `ValidatedAt` on `booking_payments`.

### Infrastructure Note

Azure Blob Storage (`IStorageService`) is already implemented and registered in the project (`Infrastructure/Services/StorageService.cs`). `SubmitManualPaymentProofCommand` calls `IStorageService.UploadAsync()` with the uploaded file stream and stores the returned URL in `BookingPayment.ProofOfPaymentUrl`. Use a blob container path like `payment-proofs/{bookingId}/{fileName}`.

---

### 5.1 — Commands

**`SubmitManualPaymentProofCommand`**

Input: `BookingId`, `PaymentMethod` (must be GCash, Maya, or BankTransfer), `PaymentType`, `Amount`, file stream + file name

Steps:
1. Fetch booking, throw `NotFoundException` if not found
2. Validate `PaymentMethod` is one of the manual methods (throw `ConflictException` if Cash/Card/Paymongo — those use `ProcessPaymentCommand`)
3. Upload file to Azure Blob: `IStorageService.UploadAsync("payment-proofs/{bookingId}/{fileName}", stream)`
4. Create `BookingPayment`:
   ```
   ProofOfPaymentUrl       = returned blob URL
   PaymentValidationStatus = PendingValidation
   Amount                  = provided amount
   ProcessedByUserId       = userId from JWT (null if submitted by guest — anonymous in walk-in edge cases)
   ```
5. Do NOT update `PaymentStatus` on booking yet — validation must happen first

---

**`ValidateManualPaymentCommand`**

Input: `PaymentId (Guid)`, `IsApproved (bool)`, `RejectionReason (string?)` (required if `IsApproved = false`)

Steps:
1. Fetch `BookingPayment`, throw `NotFoundException` if not found
2. Validate current `PaymentValidationStatus = PendingValidation` (throw `ConflictException` if already validated or rejected)
3. Set `ValidatedByUserId = userId from JWT`, `ValidatedAt = DateTime.UtcNow`

**If `IsApproved = true`:**
```
PaymentValidationStatus = Validated
PaidAt                  = DateTime.UtcNow
```
Then recalculate booking `PaymentStatus`:
- Sum all `Validated` payments for the booking
- If total ≥ `TotalAmount` → `FullyPaid`
- If downpayment amount is now covered → `DownpaymentPaid`
- Else → `PartiallyPaid`

> **Note for Phase 7:** When this command is used to validate an online booking's downpayment proof, Phase 7 will add a step here: after setting `Validated`, internally dispatch `ConfirmDownpaymentCommand`. This is a Phase 7 concern — do not implement it now. In this phase, `ValidateManualPaymentCommand` only updates payment state.

**If `IsApproved = false`:**
```
PaymentValidationStatus = Rejected
```
Booking `PaymentStatus` remains unchanged. No state transition on the booking.

---

### 5.2 — Query

**`GetPendingPaymentValidationsQuery`**

Returns all `BookingPayment` rows where `PaymentValidationStatus = PendingValidation`.

Output per row:
```
PaymentId           Guid
BookingId           Guid
ReferenceNumber     string      (from Booking)
GuestName           string      (from BookingGuest)
ResourceName        string      (from CoworkingResource via Booking)
Amount              decimal
PaymentMethod       PaymentMethod
PaymentType         PaymentType
ProofOfPaymentUrl   string
CreatedAt           DateTime
```

---

### 5.3 — Validators

**`SubmitManualPaymentProofCommandValidator`**
- `BookingId`: required, valid Guid
- `PaymentMethod`: must be `GCash`, `Maya`, or `BankTransfer` only
- `Amount`: > 0
- File: must be provided; allowed content types: `image/jpeg`, `image/png`; max size: 5 MB

**`ValidateManualPaymentCommandValidator`**
- `PaymentId`: required, valid Guid
- `IsApproved`: required bool
- `RejectionReason`: required when `IsApproved = false`, max 500 characters

---

### 5.4 — Request / Response DTOs

**`SubmitManualPaymentProofRequest`** — `BookingId`, `PaymentMethod`, `PaymentType`, `Amount`, `ReferenceNumber?` (guest-provided transaction ref)

**`ValidateManualPaymentRequest`** — `IsApproved`, `RejectionReason?`

**`PendingPaymentValidationResponse`** — all fields listed in the query output above

---

### 5.5 — Controller Changes

Add to `WebApi/Controllers/CoworkingFrontDeskController.cs`:

| Method | Route | Command / Query |
|---|---|---|
| POST (multipart/form-data) | `payments/submit-proof` | `SubmitManualPaymentProofCommand` |
| POST | `payments/{paymentId}/validate` | `ValidateManualPaymentCommand` |
| GET | `payments/pending` | `GetPendingPaymentValidationsQuery` |

Also update `ProcessPaymentCommand` (Phase 4): remove the stub rejection for `GCash`, `Maya`, and `BankTransfer`. These three methods now create a `BookingPayment` with `PaymentValidationStatus = PendingValidation` via `SubmitManualPaymentProofCommand`. `ProcessPaymentCommand` remains the handler for `Cash` and `Card` only.

---

### 5.6 — User Flows Operational After Phase 5

- Walk-in guest pays via GCash → FrontDesk uploads the screenshot on their behalf → payment sits in `PendingValidation`
- FrontDesk dashboard shows "Payments Awaiting Review" list via `GET frontdesk/payments/pending`
- FrontDesk reviews and approves → `PaymentStatus = FullyPaid` on booking
- FrontDesk rejects (wrong screenshot) → `PaymentValidationStatus = Rejected` → FrontDesk can ask guest to resubmit
- Guest can resubmit another proof via a second `SubmitManualPaymentProofCommand` call

### 5.7 — Stubbed / Mocked

- `ValidateManualPaymentCommand` does not yet dispatch `ConfirmDownpaymentCommand` — that is a Phase 7 addition. In this phase it only updates the payment record.
- No email notifications in this phase.

### 5.8 — Exit Criteria

- [ ] `POST frontdesk/payments/submit-proof` with a valid image creates a `booking_payments` row with `PaymentValidationStatus = PendingValidation` and a non-null `ProofOfPaymentUrl`
- [ ] The uploaded file is retrievable from Azure Blob Storage via the stored URL
- [ ] `GET frontdesk/payments/pending` returns the pending payment
- [ ] `POST frontdesk/payments/{id}/validate` with `IsApproved = true` sets `Validated`, `ValidatedByUserId`, `ValidatedAt`, `PaidAt`, and updates booking `PaymentStatus`
- [ ] `POST frontdesk/payments/{id}/validate` with `IsApproved = false` sets `Rejected` and does not change `PaymentStatus`
- [ ] Attempting to validate an already-validated payment returns HTTP 409
- [ ] File type validation: non-image upload returns HTTP 400
- [ ] File size validation: file > 5 MB returns HTTP 400
- [ ] `Cash` and `Card` are not accepted by `SubmitManualPaymentProofCommand` (returns HTTP 409 directing to `ProcessPaymentCommand`)

---

## Phase 6 — Online Guest Booking (Creation Only)

### Objective

Allow an anonymous guest to create a coworking booking via the public API without requiring an account. This phase handles only booking creation — the booking is created in `Pending` state and stays there until payment is handled in Phase 7. This deliberate separation isolates the booking creation complexity (race condition protection, price computation, guest capture, reference number generation) from the payment flow.

After this phase, the system can receive and record online bookings. Guests receive a booking reference and know exactly how much downpayment is due. The slot is held against other bookings immediately.

### Scope
- `CreateGuestBookingCommand` — creates `BookingGuest` + `Booking` in `Pending` state, freezes price snapshot, generates reference number
- `CancelBookingCommand` — guest can cancel their own pending booking before check-in
- Availability re-check inside a DB transaction (race condition protection)
- `CoworkingController` receives two new POST endpoints

### Intentional Exclusions
- Payment handling — Phase 7
- Student discount application — Phase 8 (`IsStudent` flag stored, `DiscountAmount = 0` for now)
- Student ID image upload — Phase 8
- Confirmation email — Phase 7
- No-show detection — Phase 9

### Database Changes

None.

---

### 6.1 — Commands

**`CreateGuestBookingCommand`**

Steps executed by the handler:

1. Validate resource is active (throw `NotFoundException` if not)
2. Validate service package is active (throw `NotFoundException` if not)
3. Validate `ScheduledStart` is in the future (throw `ConflictException` if not)
4. **Begin DB transaction**
5. Re-run availability algorithm for `ResourceId + ScheduledStart + ScheduledEnd` inside the transaction
   - If `COUNT > 0`: throw `ConflictException("This resource is no longer available for the selected time. Please check availability again.")`
6. Generate reference number: `$"BKG-{(count + 1):D4}"`
7. Create `BookingGuest`:
   ```
   FirstName          = request.GuestFirstName
   LastName           = request.GuestLastName
   Email              = request.GuestEmail
   Phone              = request.GuestPhone
   StudentIdImageUrl  = null  (Phase 8 adds this)
   IsStudentVerified  = false
   ```
8. Compute pricing (Phase 8 will insert student discount logic here):
   ```
   BaseAmount        = package is Hourly
                         ? (ScheduledEnd - ScheduledStart).TotalHours × PricePerUnit
                         : PricePerUnit (flat)
   DiscountAmount    = 0  (deferred to Phase 8)
   TotalAmount       = BaseAmount
   DownpaymentAmount = TotalAmount × (DownpaymentPercent / 100)
   BalanceAmount     = TotalAmount - DownpaymentAmount
   ```
9. Create `Booking`:
   ```
   BookingType    = Online
   BookingStatus  = Pending
   PaymentStatus  = Unpaid
   ```
10. **Commit transaction**
11. Return `BookingId`, `ReferenceNumber`, and pricing breakdown to frontend

---

**`CancelBookingCommand`**

Input: `BookingId (Guid)`, `CancellationReason (string)`

Steps:
1. Fetch booking, throw `NotFoundException` if not found
2. Validate booking is in `Pending`, `PendingStudentVerification`, or `Confirmed` status — throw `ConflictException` if `CheckedIn`, `CheckedOut`, `Cancelled`, or `NoShow`
3. Call `booking.Cancel(CancellationReason)`
4. Save changes
5. The slot is now free — `CheckAvailabilityQuery` will no longer exclude this resource for the cancelled window

---

### 6.2 — Validators

**`CreateGuestBookingCommandValidator`**
- `ResourceId`: required, valid Guid
- `ServicePackageId`: required, valid Guid
- `ScheduledStart`: required, must be a future datetime
- `ScheduledEnd`: required, must be after `ScheduledStart`
- Minimum session duration: 1 hour
- `GuestFirstName`: required, max 100 characters
- `GuestLastName`: required, max 100 characters
- `GuestEmail`: required, valid email format
- `GuestPhone`: required, exactly 10 digits

**`CancelBookingCommandValidator`**
- `BookingId`: required, valid Guid
- `CancellationReason`: required, max 500 characters

---

### 6.3 — Request / Response DTOs

**`CreateGuestBookingRequest`**
```
ResourceId          Guid
ServicePackageId    Guid
ScheduledStart      DateTime
ScheduledEnd        DateTime
GuestFirstName      string
GuestLastName       string
GuestEmail          string
GuestPhone          string
IsStudent           bool
PaxCount            int         (default 1, relevant for meeting rooms)
```

**`CreateGuestBookingResponse`**
```
BookingId           Guid
ReferenceNumber     string          ("BKG-0042")
ResourceName        string
ScheduledStart      DateTime
ScheduledEnd        DateTime
SnapshotPackageName string
BaseAmount          decimal
DiscountAmount      decimal         (0 in this phase)
TotalAmount         decimal
DownpaymentAmount   decimal         (frontend shows this on the payment step)
BalanceAmount       decimal
BookingStatus       BookingStatus   (Pending)
```

**`CancelBookingRequest`** — `CancellationReason (string)`

---

### 6.4 — Controller Changes

Add to `WebApi/Controllers/CoworkingController.cs`:

| Method | Route | Command | Auth |
|---|---|---|---|
| POST | `bookings` | `CreateGuestBookingCommand` | None (public) |
| POST | `bookings/{id}/cancel` | `CancelBookingCommand` | None (public) |

---

### 6.5 — Race Condition Protection Details

Two guests simultaneously attempt to book "Seat 07" for 10:00–18:00. Both pass the pre-check validation. Both enter the handler concurrently.

The transaction serializes them:
```
Request A → BEGIN TRANSACTION
Request A → SELECT COUNT(*) ... = 0  → proceed
Request A → INSERT booking for Seat 07
Request A → COMMIT

Request B → BEGIN TRANSACTION
Request B → SELECT COUNT(*) ... = 1  → ConflictException("No longer available")
Request B → ROLLBACK
```

EF Core implementation:
```csharp
await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
try
{
    // availability re-check
    // insert guest + booking
    await _dbContext.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
}
catch
{
    await transaction.RollbackAsync(cancellationToken);
    throw;
}
```

For PostgreSQL, this is sufficient. The index on `(resource_id, scheduled_start, scheduled_end)` ensures the constraint check is fast.

---

### 6.6 — User Flows Operational After Phase 6

- Guest visits the booking page → selects date/time → calls `GET api/coworking/availability` → sees available seats
- Guest selects "Seat 07", "Day Pass", enters name/email/phone → submits form
- System creates booking in `Pending` state and returns `ReferenceNumber = "BKG-0043"` and `DownpaymentAmount = ₱300.00`
- Frontend displays: "Your booking is reserved. Please complete payment of ₱300.00 to confirm."
- `CheckAvailabilityQuery` now shows Seat 07 as **unavailable** for that window (even in `Pending` state, the slot is held)
- Two guests simultaneously attempt the same slot: one receives the confirmation, the other receives HTTP 409
- Guest changes mind → calls `POST bookings/{id}/cancel` → slot becomes available again

### 6.7 — Stubbed / Mocked

- Payment step: the response includes `DownpaymentAmount` so the frontend can display it, but there is no payment endpoint yet in the public controller. The booking stays in `Pending` state indefinitely until Phase 7 is deployed.
- Student discount: `DiscountAmount = 0` and `IsStudent` flag is stored. Phase 8 will insert the discount computation here.
- Student ID image: not accepted yet. If `IsStudent = true`, the flag is stored but no upload is requested.

### 6.8 — Exit Criteria

- [ ] `POST api/coworking/bookings` creates one row in `bookings` (status `Pending`) and one row in `booking_guests`
- [ ] All price snapshot fields are populated correctly on the booking row
- [ ] `DownpaymentAmount` = 50% of `TotalAmount` for a Day Pass (₱300.00)
- [ ] Response includes `ReferenceNumber` in `BKG-XXXX` format
- [ ] `GET api/coworking/availability` now shows the booked resource as unavailable for the reserved window
- [ ] Concurrent requests for the same slot: first succeeds, second returns HTTP 409
- [ ] `POST bookings/{id}/cancel` transitions booking to `Cancelled` and slot becomes available again
- [ ] Attempting to cancel a `CheckedIn` booking returns HTTP 409
- [ ] Booking with a past `ScheduledStart` returns HTTP 400 with a validation error
- [ ] Missing required guest fields (email, phone) return HTTP 400 with field-level errors

---

## Phase 7 — Online Booking Payment & Confirmation

### Objective

Complete the online booking lifecycle by adding downpayment confirmation and the booking confirmation email. This phase wires together the manual proof validation from Phase 5 with the new `ConfirmDownpaymentCommand`, sends the confirmation email through the existing `IEmailService`, and gives FrontDesk visibility into upcoming confirmed bookings. After this phase, a guest can book entirely online, pay via a manual method, and receive a confirmation email — making the system publicly usable.

### Scope
- `ConfirmDownpaymentCommand` — transitions `Pending` → `Confirmed` (or `PendingStudentVerification` for students)
- Wire `ValidateManualPaymentCommand` (Phase 5) to dispatch `ConfirmDownpaymentCommand` upon approval
- Confirmation email via existing `IEmailService`
- `GetUpcomingBookingsQuery` — FrontDesk sees all confirmed online bookings
- Webhook stub endpoint for future Paymongo integration

### Intentional Exclusions
- Student discount and discount reversal — Phase 8
- Paymongo automated payment — Phase 11 (stub only added here)

### Database Changes

None.

---

### 7.1 — Commands

**`ConfirmDownpaymentCommand`**

Input: `BookingId (Guid)`, `PaymentId (Guid)` (the `BookingPayment` record to mark as downpayment)

Steps:
1. Fetch booking, throw `NotFoundException` if not found
2. Validate booking is in `Pending` status (throw `ConflictException` if already confirmed)
3. Update `BookingPayment` record:
   ```
   PaymentType = Downpayment
   PaidAt      = DateTime.UtcNow
   ```
4. Update booking:
   ```
   DownpaymentPaidAt = DateTime.UtcNow
   PaymentStatus     = DownpaymentPaid
   ```
5. **Branch on student verification:**
   ```
   If booking.IsStudent == true AND bookingGuest.IsStudentVerified == false:
       BookingStatus = PendingStudentVerification
       → Alert FrontDesk (log only in this phase; Phase 8 adds the query)
   Else:
       BookingStatus = Confirmed
   ```
6. Send confirmation email (see Section 7.3)
7. Save changes

---

### 7.2 — Wiring ConfirmDownpaymentCommand into ValidateManualPaymentCommand

Update `ValidateManualPaymentCommand` (from Phase 5). Add this step after setting `PaymentValidationStatus = Validated` and only when the `PaymentType = Downpayment`:

```
If payment.PaymentType == Downpayment:
    → Dispatch ConfirmDownpaymentCommand internally (via MediatR ISender)
```

This means the FrontDesk validation action on a downpayment proof automatically triggers the full booking confirmation flow. FrontDesk does not need to call a separate endpoint.

The dispatch path:
```
FrontDesk: POST /frontdesk/payments/{id}/validate  (IsApproved = true)
  → ValidateManualPaymentCommand
      → PaymentValidationStatus = Validated
      → If PaymentType == Downpayment:
           → ConfirmDownpaymentCommand
               → BookingStatus = Confirmed (or PendingStudentVerification)
               → Confirmation email sent
```

---

### 7.3 — Confirmation Email

Uses the existing `IEmailService`. Email content follows the template from `CoworkingImplementationDocs.md Section 16`.

Template fields to populate from the `Booking` + `BookingGuest` + `CoworkingResource`:

```
Subject: Your Booking is Confirmed — CommunSide (#{ReferenceNumber})

To: {GuestEmail}

Booking Reference:  #{ReferenceNumber}
Guest Name:         {GuestFirstName} {GuestLastName}
Resource:           {ResourceName}
Date:               {ScheduledStart.ToString("MMMM dd, yyyy")}
Time:               {ScheduledStart.ToString("hh:mm tt")} – {ScheduledEnd.ToString("hh:mm tt")}

--- PAYMENT SUMMARY ---
Package:            {SnapshotPackageName}
Total Fee:          ₱{TotalAmount:F2}
Downpayment Paid:   ₱{DownpaymentAmount:F2}  ✓
Balance Due:        ₱{BalanceAmount:F2}  (payable at check-in)

--- CHECK-IN INSTRUCTIONS ---
• Please arrive on time. A 10-minute grace period applies.
• [If IsStudent] Bring a valid Student ID for verification upon arrival.
• No-show policy: downpayment is non-refundable if you fail to check in
  within {AutoForfeitMinutes} minutes of your scheduled start time.

--- CONTACT ---
hello@communside.com
```

If student ID is still unverified, append:
```
⚠ Note: Your student discount is pending verification of your Student ID.
FrontDesk will review your submitted ID before your session begins.
```

---

### 7.4 — Queries

**`GetUpcomingBookingsQuery`**

Returns all bookings in `Confirmed` or `PendingStudentVerification` status where `ScheduledStart > DateTime.UtcNow`.

Output per row: `BookingId`, `ReferenceNumber`, `GuestName`, `ResourceName`, `ScheduledStart`, `ScheduledEnd`, `SnapshotPackageName`, `TotalAmount`, `DownpaymentAmount`, `BalanceAmount`, `BookingStatus`, `PaymentStatus`

---

### 7.5 — Webhook Stub (Paymongo Preparation)

Add this endpoint to `CoworkingController.cs` now as a stub. It will be fully implemented in Phase 11.

| Method | Route | Auth |
|---|---|---|
| POST | `payments/webhook` | None (verified by signature in Phase 11) |

Current stub behavior:
```csharp
[HttpPost("payments/webhook")]
public IActionResult PaymongoWebhook([FromBody] object payload)
{
    // Phase 11: implement signature verification and ConfirmDownpaymentCommand dispatch
    return Ok();
}
```

This ensures the endpoint exists and is harmless if Paymongo fires it during testing.

---

### 7.6 — Controller Changes

Add to `WebApi/Controllers/CoworkingFrontDeskController.cs`:

| Method | Route | Query |
|---|---|---|
| GET | `bookings/upcoming` | `GetUpcomingBookingsQuery` |

Add to `WebApi/Controllers/CoworkingController.cs`:

| Method | Route | Handler |
|---|---|---|
| POST | `payments/webhook` | Stub (returns 200 OK) |

---

### 7.7 — User Flows Operational After Phase 7

- Guest submits online booking → receives `ReferenceNumber` and `DownpaymentAmount` (Phase 6)
- Guest uploads GCash screenshot → `SubmitManualPaymentProofCommand` (Phase 5) → `PendingValidation`
- FrontDesk reviews proof → approves → `ValidateManualPaymentCommand` fires → `ConfirmDownpaymentCommand` fires automatically → booking `Confirmed` → confirmation email delivered to guest
- FrontDesk opens "Upcoming Bookings" dashboard → sees the confirmed reservation with schedule and balance due
- Guest arrives → FrontDesk calls `POST frontdesk/time-in` → `ProcessWalkInTimeInCommand` works for both walk-ins and online arrivals → booking transitions `Confirmed` → `CheckedIn`
- Balance collected at checkout → same Phase 4 time-out and payment flow

### 7.8 — Stubbed / Mocked

- **Paymongo webhook**: endpoint returns 200 OK but triggers nothing. This is intentional — the business can operate with 100% manual validation until Phase 11.
- **Student bookings after confirmation**: booking reaches `PendingStudentVerification` and stays there until Phase 8 is implemented. FrontDesk can still manually check the guest in (the `ProcessWalkInTimeInCommand` does not block on student verification for check-in).

### 7.9 — Exit Criteria

- [ ] After FrontDesk approves a downpayment proof, `ConfirmDownpaymentCommand` fires automatically without a separate API call
- [ ] `BookingPayment` record has `PaymentType = Downpayment`, `PaidAt` set
- [ ] `Booking.DownpaymentPaidAt` is set
- [ ] `BookingStatus = Confirmed` for non-student bookings
- [ ] `BookingStatus = PendingStudentVerification` for bookings where `IsStudent = true` and `IsStudentVerified = false`
- [ ] Confirmation email is sent to `GuestEmail` (verified via email service logs or test mailbox)
- [ ] Email subject contains the reference number
- [ ] Email body contains correct `TotalAmount`, `DownpaymentAmount`, and `BalanceAmount`
- [ ] `GET frontdesk/bookings/upcoming` returns the confirmed booking
- [ ] `POST api/coworking/payments/webhook` returns HTTP 200 without crashing
- [ ] Attempting to confirm downpayment on an already-confirmed booking returns HTTP 409

---

## Phase 8 — Student Verification

### Objective

Implement the complete student discount lifecycle: ID image upload during booking creation, FrontDesk verification queue, discount application on approval, and discount reversal on rejection. This phase retroactively completes the pricing logic that was intentionally left at zero in Phases 6 and 7. After this phase, student discounts are correctly applied, conditionally reversed, and accurately reflected in all receipts and balance calculations.

### Scope
- Student ID image upload via Azure Blob during `CreateGuestBookingCommand` (update from Phase 6)
- Student discount computation inserted into `CreateGuestBookingCommand` pricing step
- `VerifyStudentIdCommand` — FrontDesk approves or rejects, adjusts discount if rejected
- `GetPendingStudentVerificationsQuery` — FrontDesk dashboard queue
- Walk-in student: `IsStudentVerified = true` immediately in `ProcessWalkInTimeInCommand` (update from Phase 4)

### Intentional Exclusions
- Email notification for discount rejection (the booking still proceeds; email enhancement can be added later)
- Paymongo — Phase 11

### Database Changes

None — `StudentIdImageUrl` and `IsStudentVerified` already exist on `booking_guests`.

---

### 8.1 — Updates to Existing Commands

**`CreateGuestBookingCommand` (update from Phase 6)**

Add student ID image upload:
```
If request.IsStudent == true && request.StudentIdImageFile != null:
    blobUrl = await _storageService.UploadAsync(
        $"student-ids/{bookingId}/{fileName}", stream)
    bookingGuest.StudentIdImageUrl = blobUrl
```

Insert student discount into pricing step:
```
BaseAmount     = (as before)
DiscountAmount = request.IsStudent && servicePackage.IsStudentEligible
                   ? BaseAmount × (servicePackage.StudentDiscountPercent / 100)
                   : 0
TotalAmount    = BaseAmount - DiscountAmount

Note: DiscountAmount is tentative. It is applied as a computed value but the
booking enters PendingStudentVerification (not Confirmed) after downpayment
until FrontDesk approves the ID. If rejected, DiscountAmount is zeroed here.
```

Update `CreateGuestBookingResponse` to return the actual `DiscountAmount` (may now be non-zero).

---

**`ProcessWalkInTimeInCommand` (update from Phase 4)**

Walk-in student flow:
```
If request.IsStudent == true:
    bookingGuest.IsStudentVerified = true   ← Physical ID presented to FrontDesk in person
    Apply discount immediately:
      DiscountAmount = servicePackage.IsStudentEligible
                         ? BaseAmount × (StudentDiscountPercent / 100) : 0
      TotalAmount    = BaseAmount - DiscountAmount
```

---

### 8.2 — New Command

**`VerifyStudentIdCommand`**

Input: `BookingId (Guid)`, `IsApproved (bool)`

Steps:
1. Fetch booking + `BookingGuest`, throw `NotFoundException` if not found
2. Validate `BookingStatus = PendingStudentVerification` (throw `ConflictException` otherwise)

**If `IsApproved = true`:**
```
bookingGuest.IsStudentVerified = true
booking.BookingStatus          = Confirmed
```
No change to `DiscountAmount` — the tentative discount computed at creation is now confirmed.

**If `IsApproved = false`:**
```
bookingGuest.IsStudentVerified = false
booking.DiscountAmount         = 0
booking.TotalAmount            = booking.BaseAmount
booking.BalanceAmount          = booking.TotalAmount - booking.DownpaymentAmount
booking.BookingStatus          = Confirmed   (booking proceeds at full rate)
```
> The guest already paid the downpayment based on the discounted total. The revised `BalanceAmount` now reflects the full-rate balance. FrontDesk collects the higher balance at check-in. No refund is issued on the downpayment.

---

### 8.3 — Query

**`GetPendingStudentVerificationsQuery`**

Returns all bookings in `PendingStudentVerification` status.

Output per row:
```
BookingId             Guid
ReferenceNumber       string
GuestName             string
ResourceName          string
ScheduledStart        DateTime
SnapshotPackageName   string
DiscountAmount        decimal     (tentative — subject to verification outcome)
StudentIdImageUrl     string      (FrontDesk clicks to view the image)
```

---

### 8.4 — Validator

**`VerifyStudentIdCommandValidator`**
- `BookingId`: required, valid Guid
- `IsApproved`: required bool

---

### 8.5 — Request / Response DTOs

**`VerifyStudentIdRequest`** — `IsApproved (bool)`

**`PendingStudentVerificationResponse`** — all fields from the query output above

---

### 8.6 — Controller Changes

Add to `WebApi/Controllers/CoworkingFrontDeskController.cs`:

| Method | Route | Command / Query |
|---|---|---|
| POST | `bookings/{id}/verify-student` | `VerifyStudentIdCommand` |
| GET | `student-verifications/pending` | `GetPendingStudentVerificationsQuery` |

---

### 8.7 — User Flows Operational After Phase 8

**Online Student — Full Flow:**
1. Guest declares `IsStudent = true` during booking → uploads student ID image
2. Discount tentatively applied: Day Pass ₱600 → ₱540
3. Downpayment = ₱270 (50% of discounted total)
4. Guest pays ₱270 via GCash → FrontDesk validates → `ConfirmDownpaymentCommand` fires → `BookingStatus = PendingStudentVerification`
5. FrontDesk opens "Pending Student Verifications" queue → sees the booking with image URL → clicks to view ID
6. **Approval path:** FrontDesk approves → `BookingStatus = Confirmed` — discount stands, balance = ₱270
7. **Rejection path:** FrontDesk rejects → `DiscountAmount = 0`, `TotalAmount = ₱600`, `BalanceAmount = ₱330` — booking is still confirmed but at full rate

**Walk-In Student — Full Flow:**
1. FrontDesk checks `IsStudent = true` and physically verifies ID
2. `IsStudentVerified = true` immediately
3. Discount applied in-place: ₱600 Day Pass → ₱540
4. Guest pays at check-out

### 8.8 — Stubbed / Mocked

Nothing. This phase has no external dependencies beyond what is already built.

### 8.9 — Exit Criteria

- [ ] `CreateGuestBookingCommand` with `IsStudent = true` and a provided image uploads the file to Azure Blob and stores `StudentIdImageUrl` on `booking_guests`
- [ ] `DiscountAmount` is non-zero for a Day Pass booking with a student-eligible package
- [ ] After downpayment confirmation, a student booking status = `PendingStudentVerification`
- [ ] `GET frontdesk/student-verifications/pending` returns the booking with `StudentIdImageUrl`
- [ ] `POST frontdesk/bookings/{id}/verify-student` with `IsApproved = true`: `IsStudentVerified = true`, `BookingStatus = Confirmed`, `DiscountAmount` unchanged
- [ ] `POST frontdesk/bookings/{id}/verify-student` with `IsApproved = false`: `DiscountAmount = 0`, `TotalAmount = BaseAmount`, `BalanceAmount` recalculated, `BookingStatus = Confirmed`
- [ ] Walk-in with `IsStudent = true`: `IsStudentVerified = true` immediately and discount applied in checkout total
- [ ] Non-student-eligible packages (meeting rooms) always have `DiscountAmount = 0` regardless of `IsStudent` flag
- [ ] Calling `VerifyStudentIdCommand` on a booking in `Confirmed` status returns HTTP 409

---

## Phase 9 — No-Show Detection & Background Jobs

### Objective

Automate the no-show policy with a scheduled background job. This phase introduces the background job infrastructure (Hangfire) and wires it to two automated operations: a grace period reminder email at T+10 minutes and an automatic no-show marking at the configurable `AutoForfeitMinutes` threshold. FrontDesk retains the ability to manually mark a no-show at any time. After this phase, the system requires no human intervention to enforce the no-show policy — it runs autonomously.

### Scope
- Hangfire installation and setup
- `NoShowDetectionJob` — recurring job that runs every minute
- `SendNoShowReminderCommand` — background-triggered at T+10 min (internal, no HTTP endpoint)
- `AutoMarkNoShowCommand` — background-triggered at T+AutoForfeitMinutes (internal, no HTTP endpoint)
- `MarkNoShowCommand` — FrontDesk manual override (HTTP endpoint)
- `AutoForfeitMinutes` and `SessionEndingReminderMinutes` from `CoworkingSettings` configuration

### Intentional Exclusions
- `SendSessionEndingReminderCommand` (15-min before end) — Phase 10 (tied to the extension flow)
- Paymongo — Phase 11

### Database Changes

None. `ReminderSentAt` is included in `CoworkingInitialSchema` (Phase 1) to avoid a surprise mid-project migration. No new migration is needed here.

---

### 9.1 — Hangfire Setup

**NuGet packages to add to `Infrastructure.csproj`:**
```
Hangfire.Core
Hangfire.AspNetCore
Hangfire.PostgreSql
```

**Registration in `Infrastructure/DependencyInjection.cs`:**
```csharp
services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(configuration.GetConnectionString("DefaultConnection")));

services.AddHangfireServer();
```

**Registration in `WebApi/Program.cs`:**
```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthFilter()]   // Only Admin JWT should access
});

RecurringJob.AddOrUpdate<NoShowDetectionJob>(
    "no-show-detection",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Minutely());

RecurringJob.AddOrUpdate<SessionEndingReminderJob>(
    "session-ending-reminder",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Minutely());   // Phase 10 adds this — register stub now to avoid touching Program.cs again
```

**Hangfire Dashboard Auth Filter:**
File: `WebApi/Infrastructure/HangfireAdminAuthFilter.cs`
Implement `IDashboardAuthorizationFilter` — allow access only if the request has a valid JWT with `Admin` role.

> Hangfire auto-creates its required tables in PostgreSQL on first run. No manual migration is needed for Hangfire tables.

---

### 9.2 — Background Job: NoShowDetectionJob

File: `Infrastructure/Services/BackgroundJobs/NoShowDetectionJob.cs`

```csharp
public class NoShowDetectionJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var settings = // resolve CoworkingSettings

        // Find all confirmed bookings past their start time
        var overdueBookings = await _dbContext.Bookings
            .Where(b => b.BookingStatus == BookingStatus.Confirmed
                     && b.ScheduledStart < now)
            .ToListAsync(cancellationToken);

        foreach (var booking in overdueBookings)
        {
            var minutesPast = (now - booking.ScheduledStart).TotalMinutes;

            // Grace period reminder at T+10 min (send once)
            if (minutesPast >= 10 && booking.ReminderSentAt == null)
            {
                await _mediator.Send(new SendNoShowReminderCommand(booking.Id), cancellationToken);
            }

            // Auto-forfeit at T+AutoForfeitMinutes
            if (minutesPast >= settings.AutoForfeitMinutes)
            {
                await _mediator.Send(new AutoMarkNoShowCommand(booking.Id), cancellationToken);
            }
        }
    }
}
```

---

### 9.3 — Commands

**`SendNoShowReminderCommand`** (internal — no HTTP endpoint)

Input: `BookingId (Guid)`

Steps:
1. Fetch booking + guest, throw `NotFoundException` if not found
2. Guard: if `booking.ReminderSentAt != null` → return (already sent, idempotency guard)
3. Guard: if `booking.BookingStatus != Confirmed` → return (guest may have already checked in or been marked no-show between the check and this command)
4. Send email to `BookingGuest.Email`:
   ```
   Subject: Reminder — Your CommunSide Booking Starts Now (#BKG-XXXX)
   Body:    "Your booking for {ResourceName} was scheduled to start at {ScheduledStart}.
            Please proceed to the front desk. If you cannot make it, please contact us."
   ```
5. Set `booking.ReminderSentAt = DateTime.UtcNow`
6. Save changes

---

**`AutoMarkNoShowCommand`** (internal — no HTTP endpoint)

Input: `BookingId (Guid)`

Steps:
1. Fetch booking + guest
2. Guard: if `booking.BookingStatus != Confirmed` → return (idempotent — may already be `CheckedIn` or `NoShow`)
3. Call `booking.MarkNoShow()`:
   ```
   BookingStatus  = NoShow
   PaymentStatus  = FullyPaid   (downpayment is non-refundable)
   CancellationReason = "Automatically marked as no-show after {AutoForfeitMinutes} minutes"
   ```
4. Send email to `BookingGuest.Email`:
   ```
   Subject: No-Show Notice — CommunSide (#BKG-XXXX)
   Body:    "Your booking for {ResourceName} on {Date} has been marked as a no-show.
            As per our policy, your downpayment of ₱{DownpaymentAmount} has been forfeited."
   ```
5. Save changes

The slot is freed automatically — `CheckAvailabilityQuery` excludes `NoShow` bookings.

---

**`MarkNoShowCommand`** (FrontDesk manual — HTTP endpoint)

Input: `BookingId (Guid)`, `Reason (string?)`

Steps:
1. Fetch booking, throw `NotFoundException` if not found
2. Validate `BookingStatus` is `Pending`, `PendingStudentVerification`, or `Confirmed` — throw `ConflictException` if `CheckedIn` (cannot mark a guest as no-show after they have already arrived)
3. Call `booking.MarkNoShow()` with the provided reason
4. Save changes

---

### 9.4 — Validator

**`MarkNoShowCommandValidator`**
- `BookingId`: required, valid Guid

---

### 9.5 — Request / Response DTOs

**`MarkNoShowRequest`** — `Reason (string?)`

---

### 9.6 — Controller Changes

Add to `WebApi/Controllers/CoworkingFrontDeskController.cs`:

| Method | Route | Command |
|---|---|---|
| POST | `bookings/{id}/no-show` | `MarkNoShowCommand` |

---

### 9.7 — Email Failure Handling

Email failures must not block no-show state transitions. The booking state change is the critical operation. Email is best-effort:

```csharp
try
{
    await _emailService.SendAsync(email, cancellationToken);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to send no-show email for booking {BookingId}", booking.Id);
    // Do not rethrow — state transition has already been saved
}
```

---

### 9.8 — CoworkingSettings (establish in this phase)

File: `Application/Common/Settings/CoworkingSettings.cs`
```csharp
public class CoworkingSettings
{
    public int AutoForfeitMinutes { get; set; } = 30;
    public int SessionEndingReminderMinutes { get; set; } = 15;
}
```

Registered in `WebApi/Program.cs`:
```csharp
services.Configure<CoworkingSettings>(configuration.GetSection("CoworkingSettings"));
```

Injected via `IOptions<CoworkingSettings>` in `NoShowDetectionJob`.

---

### 9.9 — User Flows Operational After Phase 9

- Guest books online, pays downpayment, booking is `Confirmed`
- Guest doesn't show up → 10 min after start time → reminder email sent automatically
- 30 min after start time (configurable) → booking auto-marked `NoShow`, `PaymentStatus = FullyPaid`, seat freed
- FrontDesk can manually mark no-show at any point before the automated job fires
- Admin visits `/hangfire` dashboard → sees job schedule and execution history
- Admin changes `AutoForfeitMinutes` to 60 in `appsettings.json` → takes effect on next deployment without code change

### 9.10 — Stubbed / Mocked

- `SessionEndingReminderJob` (Phase 10): registered in `Program.cs` now as a stub class with an empty `ExecuteAsync`. This avoids touching `Program.cs` again in Phase 10.

### 9.11 — Exit Criteria

- [ ] Hangfire NuGet packages installed and Hangfire tables auto-created in PostgreSQL on first startup
- [ ] Hangfire dashboard accessible at `/hangfire` (returns 401 without valid Admin JWT)
- [ ] Recurring `no-show-detection` job visible in Hangfire dashboard running every minute
- [ ] A `Confirmed` booking 35 minutes past its `ScheduledStart` (with `AutoForfeitMinutes = 30`) transitions to `NoShow` and `FullyPaid`
- [ ] `booking.ReminderSentAt` is set after the T+10 reminder — job does not re-send email on subsequent runs
- [ ] After no-show marking, `GET api/coworking/availability` shows the resource as available again
- [ ] `MarkNoShowCommand` via `POST frontdesk/bookings/{id}/no-show` produces identical state transitions to the automated job
- [ ] Attempting `MarkNoShowCommand` on a `CheckedIn` booking returns HTTP 409
- [ ] Email failure (simulated by disabling email service) does not prevent booking status transition from completing
- [ ] Changing `AutoForfeitMinutes` in `appsettings.json` takes effect without recompilation
- [ ] `AddReminderSentAtToBookings` migration runs without errors

---

## Phase 10 — Session Extension

### Objective

Allow FrontDesk to extend an active guest session when more time is needed. This phase introduces the `BookingExtension` entity into the live system, implements the 15-minute pre-end reminder (completing the `SessionEndingReminderJob` stub from Phase 9), and handles the full extension billing lifecycle including conflict detection. After this phase, extensions and their fees are fully tracked and reflected in checkout totals.

### Scope
- `ExtendSessionCommand` — availability conflict check, creates `BookingExtension`, pushes `ScheduledEnd` forward
- `ProcessPaymentCommand` extension: add `PaymentType = ExtensionFee` handling
- `SendSessionEndingReminderCommand` — background job runs 15 min before `ScheduledEnd`
- `SessionEndingReminderJob` — implement the stub registered in Phase 9
- Update `ProcessWalkInTimeOutCommand` to include extension amounts in final total

### Intentional Exclusions
- Paymongo — Phase 11

### Database Changes

None. `EndingReminderSentAt` is included in `CoworkingInitialSchema` (Phase 1) alongside `ReminderSentAt`. No new migration is needed here.

---

### 10.1 — Command

**`ExtendSessionCommand`**

Input: `BookingId (Guid)`, `AddedHours (decimal)`, `Notes (string?)`

Steps:
1. Fetch booking, throw `NotFoundException` if not found
2. Validate `BookingStatus = CheckedIn` (throw `ConflictException` otherwise — can only extend an active session)
3. Validate `AddedHours > 0` (already caught by validator, but guard defensively)
4. **Availability conflict check for the extension window:**
   ```sql
   SELECT COUNT(*) FROM bookings
   WHERE resource_id       = @booking.ResourceId
     AND id               != @booking.Id             ← exclude the current booking
     AND booking_status NOT IN ('Cancelled', 'NoShow', 'CheckedOut')
     AND scheduled_start   < @booking.ScheduledEnd + AddedHours
     AND scheduled_end     > @booking.ScheduledEnd
   ```
   If `COUNT > 0` → throw `ConflictException("The resource is booked during the requested extension window. Next available: {nextStartTime}")`
5. Freeze extension snapshot:
   ```
   extension.SnapshotHourlyRate = booking.SnapshotHourlyRate
   extension.Amount             = AddedHours × SnapshotHourlyRate
   extension.ProcessedByUserId  = userId from JWT
   ```
6. Create `BookingExtension` record
7. Push `Booking.ScheduledEnd` forward:
   ```
   booking.ScheduledEnd = booking.ScheduledEnd.AddHours((double)AddedHours)
   ```
8. Save changes and return new `ScheduledEnd` + extension `Amount`

---

### 10.2 — Update ProcessWalkInTimeOutCommand (from Phase 4)

Add extension total to the checkout computation:

```csharp
var extensionTotal = await _dbContext.BookingExtensions
    .Where(e => e.BookingId == booking.Id)
    .SumAsync(e => e.Amount, cancellationToken);

booking.TotalAmount  = booking.BaseAmount
                     - booking.DiscountAmount
                     + booking.OverstayAmount
                     + extensionTotal;

booking.BalanceAmount = booking.TotalAmount
                      - (sum of Downpayment payments already recorded);
```

This update is a non-breaking additive change. Before Phase 10, `BookingExtensions` is always empty, so `extensionTotal = 0` and the formula produces the same result as the Phase 4 implementation.

---

### 10.3 — Background Job: SessionEndingReminderJob

File: `Infrastructure/Services/BackgroundJobs/SessionEndingReminderJob.cs`

Implement the stub registered in Phase 9:

```csharp
public class SessionEndingReminderJob
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var settings = // resolve CoworkingSettings
        var reminderWindow = now.AddMinutes(settings.SessionEndingReminderMinutes);

        var endingSoon = await _dbContext.Bookings
            .Where(b => b.BookingStatus == BookingStatus.CheckedIn
                     && b.ScheduledEnd  <= reminderWindow
                     && b.ScheduledEnd  >  now
                     && b.EndingReminderSentAt == null)
            .ToListAsync(cancellationToken);

        foreach (var booking in endingSoon)
        {
            await _mediator.Send(
                new SendSessionEndingReminderCommand(booking.Id), cancellationToken);
        }
    }
}
```

---

### 10.4 — Background Command

**`SendSessionEndingReminderCommand`** (internal — no HTTP endpoint)

Input: `BookingId (Guid)`

Steps:
1. Fetch booking + guest + resource
2. Guard: if `booking.EndingReminderSentAt != null` → return (idempotency)
3. Guard: if `booking.BookingStatus != CheckedIn` → return
4. Write a `BookingReminder` record OR set `EndingReminderSentAt = DateTime.UtcNow` on the booking (use the flag approach to avoid a new table)
5. This alert is FrontDesk-facing. Since real-time push (SignalR) is not in scope, the FrontDesk dashboard polls `GetActiveSessionsQuery` and the UI computes "ending in X minutes" from `ScheduledEnd`. The reminder flag is the persistence signal. Optionally, also send an email to the guest.

---

### 10.5 — Validator

**`ExtendSessionCommandValidator`**
- `BookingId`: required, valid Guid
- `AddedHours`: > 0, ≤ 12 (reasonable upper bound per single extension)

---

### 10.6 — Request / Response DTOs

**`ExtendSessionRequest`** — `AddedHours (decimal)`, `Notes (string?)`

**`ExtendSessionResponse`**
```
BookingId           Guid
ReferenceNumber     string
NewScheduledEnd     DateTime
ExtensionAmount     decimal
UpdatedTotalAmount  decimal     (base + extensions so far — overstay not yet computed)
```

---

### 10.7 — Controller Changes

Add to `WebApi/Controllers/CoworkingFrontDeskController.cs`:

| Method | Route | Command |
|---|---|---|
| POST | `bookings/{id}/extend` | `ExtendSessionCommand` |

---

### 10.8 — Invoice Breakdown Example

At checkout after two extensions on a Day Pass booking:
```
Base Package:   Day Pass (10hrs)        ₱600.00
Extension 1:    +2hrs @ ₱50/hr         ₱100.00
Extension 2:    +1hr  @ ₱50/hr         ₱50.00
Overstay:       0                       ₱0.00
Student Disc:   10%                    -₱60.00
─────────────────────────────────────────────────
Total:                                  ₱690.00
Downpayment:                           -₱270.00
Balance Due:                            ₱420.00
```

---

### 10.9 — User Flows Operational After Phase 10

- 15 min before session ends → `SessionEndingReminderJob` sets `EndingReminderSentAt` → FrontDesk frontend (polling `GetActiveSessionsQuery`) detects the flag and shows a badge: "Seat 07 — ends in 15 min"
- FrontDesk asks guest: "Do you want to extend?"
- Guest wants +2 hours → `POST frontdesk/bookings/{id}/extend` → system checks if next 2 hours are free on Seat 07
  - **Available**: extension created, `ScheduledEnd` pushed, response shows new end time and extension fee
  - **Conflicted**: HTTP 409 with message "Seat 07 is booked from 5:00 PM — cannot extend beyond 5:00 PM"
- At checkout: `ProcessWalkInTimeOutCommand` includes extension amounts in `TotalAmount`
- Extension payment recorded via `ProcessPaymentCommand` with `PaymentType = ExtensionFee`

### 10.10 — Stubbed / Mocked

- `SendSessionEndingReminderCommand` email: optional enhancement. The flag-based polling approach is the primary mechanism and is fully implemented. Email can be layered on without changing any existing logic.

### 10.11 — Exit Criteria

- [ ] `POST frontdesk/bookings/{id}/extend` with valid `AddedHours` creates a `booking_extensions` row with correct `SnapshotHourlyRate` and `Amount`
- [ ] `Booking.ScheduledEnd` is pushed forward by `AddedHours`
- [ ] Extending into a window occupied by another booking returns HTTP 409 with a descriptive message
- [ ] `ProcessWalkInTimeOutCommand` includes the sum of all extension amounts in the final `TotalAmount`
- [ ] Receipt: base + extensions + overstay − discount = correct total
- [ ] `SessionEndingReminderJob` sets `EndingReminderSentAt` on bookings within the 15-minute window
- [ ] `EndingReminderSentAt` prevents duplicate alerts on subsequent job runs
- [ ] `AddEndingReminderSentAtToBookings` migration runs without errors
- [ ] Multiple extensions on the same booking accumulate correctly — each creates a separate `booking_extensions` row
- [ ] Attempting to extend a booking in `CheckedOut` status returns HTTP 409

---

## Phase 11 — Paymongo Integration (Deferred MVP)

### Objective

Automate the online downpayment flow by integrating Paymongo as a payment gateway. The webhook endpoint stub from Phase 7 is now fully implemented. After this phase, guests can pay via card or e-wallet through Paymongo, and their booking is confirmed automatically without FrontDesk intervention. This phase is designated deferred — the system is 100% operational without it using manual payment validation.

### Scope
- `IPaymongoService` interface + `PaymongoService` implementation
- `CreatePaymentIntentCommand` — generates a Paymongo checkout URL for the guest
- `PaymongoWebhookHandler` — replaces the Phase 7 stub with full signature verification and booking confirmation
- `ExternalPaymentRef` field added to `booking_payments` for transaction reconciliation

### Intentional Exclusions
- Refund automation via Paymongo (manual refund process is sufficient for MVP)
- Paymongo subscription/recurring billing (not applicable to coworking)

### Database Changes

Add `ExternalPaymentRef (string? nullable)` to `booking_payments`.

Migration name: `AddExternalPaymentRefToBookingPayments`

This stores the Paymongo payment intent ID or source ID for reconciliation and idempotency.

---

### 11.1 — IPaymongoService Interface

File: `Application/Common/Interfaces/IPaymongoService.cs`

```csharp
public interface IPaymongoService
{
    Task<string> CreatePaymentIntentAsync(
        decimal amount,
        string referenceNumber,
        string bookingId,
        CancellationToken cancellationToken);

    bool VerifyWebhookSignature(string rawPayload, string signatureHeader);
    string ExtractEventIdFromPayload(string rawPayload);      // data.id — used as idempotency key
    string ExtractBookingIdFromPayload(string rawPayload);    // data.attributes.metadata.bookingId
    bool IsPaymentSuccessful(string rawPayload);
}
```

---

### 11.2 — PaymongoService Implementation

File: `Infrastructure/Services/PaymongoService.cs`

**CreatePaymentIntentAsync:**
- Calls `POST https://api.paymongo.com/v1/payment_intents` with the amount (in centavos), description, and metadata containing `bookingId`
- Returns the checkout URL from the response for frontend redirect

**VerifyWebhookSignature:**
- Paymongo signs webhooks using HMAC-SHA256 with the webhook secret
- Compute: `HMAC-SHA256(rawPayload, WebhookSecret)` and compare to the `Paymongo-Signature` header
- Return `false` if mismatch — the handler must return 400

**ExtractBookingIdFromPayload:**
- Parses the JSON body and returns `data.attributes.metadata.bookingId`

**IsPaymentSuccessful:**
- Returns `true` if `data.attributes.status == "paid"` or `data.attributes.type == "payment.paid"`

**Configuration in `appsettings.json`:**
```json
"PaymongoSettings": {
  "PublicKey": "pk_live_...",
  "SecretKey": "sk_live_...",
  "WebhookSecret": "whsec_..."
}
```

Register `IPaymongoService` → `PaymongoService` in `Infrastructure/DependencyInjection.cs`.

---

### 11.3 — Command

**`CreatePaymentIntentCommand`**

Input: `BookingId (Guid)`

Steps:
1. Fetch booking, throw `NotFoundException` if not found
2. Validate `BookingStatus = Pending` and `PaymentStatus = Unpaid`
3. Call `IPaymongoService.CreatePaymentIntentAsync(booking.DownpaymentAmount, booking.ReferenceNumber, booking.Id.ToString())`
4. Create a `BookingPayment` placeholder record:
   ```
   Amount                  = booking.DownpaymentAmount
   PaymentMethod           = Paymongo
   PaymentType             = Downpayment
   PaymentValidationStatus = NotRequired   (Paymongo handles validation automatically)
   ExternalPaymentRef      = paymentIntentId  (from Paymongo response)
   PaidAt                  = null  (set after webhook confirms payment)
   ```
5. Return `CheckoutUrl` to the frontend

---

### 11.4 — Webhook Handler

Replace the Phase 7 stub in `CoworkingController.cs`:

```csharp
[HttpPost("payments/webhook")]
public async Task<IActionResult> PaymongoWebhook()
{
    // 1. Read raw body (must not use [FromBody] — signature verification needs the raw bytes)
    using var reader = new StreamReader(Request.Body);
    var rawPayload = await reader.ReadToEndAsync();

    // 2. Verify signature
    var signatureHeader = Request.Headers["Paymongo-Signature"].ToString();
    if (!_paymongoService.VerifyWebhookSignature(rawPayload, signatureHeader))
        return BadRequest("Invalid webhook signature");

    // 3. Check if payment was successful
    if (!_paymongoService.IsPaymentSuccessful(rawPayload))
        return Ok();   // Acknowledge non-payment events without processing

    // 4. Extract Paymongo event ID for idempotency check
    var eventId = _paymongoService.ExtractEventIdFromPayload(rawPayload);

    // 5. Idempotency guard — check if this event was already processed
    if (await _dbContext.BookingPayments.AnyAsync(p => p.ExternalPaymentRef == eventId))
        return Ok(); // Duplicate webhook delivery — already processed

    // 6. Extract booking ID from metadata
    var bookingId = _paymongoService.ExtractBookingIdFromPayload(rawPayload);

    // 7. Find the BookingPayment placeholder created by CreatePaymentIntentCommand
    var bookingPayment = await _dbContext.BookingPayments
        .FirstOrDefaultAsync(p =>
            p.BookingId == Guid.Parse(bookingId) &&
            p.PaymentMethod == PaymentMethod.Paymongo &&
            p.PaidAt == null);

    if (bookingPayment == null)
        return Ok(); // No matching pending payment — log and ignore

    // 8. Stamp the Paymongo event ID before confirming (persists before the command runs)
    bookingPayment.ExternalPaymentRef = eventId;
    await _dbContext.SaveChangesAsync();

    // 9. Dispatch ConfirmDownpaymentCommand using the resolved BookingPayment ID
    await _mediator.Send(new ConfirmDownpaymentCommand(Guid.Parse(bookingId), bookingPayment.Id));

    // Always return 200 — Paymongo retries on non-2xx
    return Ok();
}
```

**Idempotency:** The webhook handler stores the Paymongo event ID (`data.id`) in `ExternalPaymentRef` before dispatching `ConfirmDownpaymentCommand`. On any subsequent delivery of the same event, the handler checks `AnyAsync(p => p.ExternalPaymentRef == eventId)` and returns `200 OK` immediately without reprocessing. This is more robust than relying on booking status alone — a future status change between `Confirmed` and webhook delivery would not break the guard.

---

### 11.5 — Validator

**`CreatePaymentIntentCommandValidator`**
- `BookingId`: required, valid Guid

---

### 11.6 — Request / Response DTOs

**`CreatePaymentIntentResponse`**
```
BookingId       Guid
ReferenceNumber string
CheckoutUrl     string      (Paymongo hosted checkout page URL)
DownpaymentAmount decimal
```

---

### 11.7 — Controller Changes

Replace stub and add new endpoint in `WebApi/Controllers/CoworkingController.cs`:

| Method | Route | Handler / Command | Auth |
|---|---|---|---|
| POST | `bookings/{id}/create-payment-intent` | `CreatePaymentIntentCommand` | None (public) |
| POST | `payments/webhook` | `PaymongoWebhookHandler` (full implementation) | None (signature-verified) |

---

### 11.8 — Online Booking Payment Flow (After Phase 11)

```
Guest creates booking (Phase 6) → receives BookingId + DownpaymentAmount

Guest chooses Paymongo:
  POST api/coworking/bookings/{id}/create-payment-intent
    → Backend calls Paymongo API → returns CheckoutUrl
    → Frontend redirects guest to CheckoutUrl

Guest completes payment on Paymongo-hosted page:
  → Paymongo fires webhook to POST api/coworking/payments/webhook
    → Signature verified ✓
    → IsPaymentSuccessful = true ✓
    → ConfirmDownpaymentCommand dispatched
        → BookingPayment.PaidAt set
        → BookingStatus = Confirmed (or PendingStudentVerification)
        → Confirmation email sent to guest
```

The manual payment proof path (Phase 5 + 7) remains available in parallel for guests who prefer GCash/Maya/Bank Transfer. Both paths converge at `ConfirmDownpaymentCommand`.

---

### 11.9 — User Flows Operational After Phase 11

- Guest can complete the entire booking-to-confirmation flow online without any FrontDesk involvement
- Payment is confirmed automatically within seconds of completing the Paymongo checkout
- FrontDesk still sees the confirmed booking in the upcoming bookings list
- Manual payment proof path remains available for guests who prefer it
- Admin can reconcile payments using `ExternalPaymentRef` against Paymongo's dashboard

### 11.10 — Stubbed / Mocked

Nothing. This is the final phase — all stubs from prior phases are now resolved.

### 11.11 — Exit Criteria

- [ ] `POST api/coworking/bookings/{id}/create-payment-intent` returns a valid Paymongo checkout URL
- [ ] `ExternalPaymentRef` (Paymongo payment intent ID) is stored on the `BookingPayment` record
- [ ] Paymongo webhook with a valid signature triggers `ConfirmDownpaymentCommand` and booking reaches `Confirmed`
- [ ] Webhook with an invalid/tampered signature returns HTTP 400 — no booking state change occurs
- [ ] Duplicate webhook delivery (same event ID sent twice) — second delivery is a no-op (idempotency guard)
- [ ] `AddExternalPaymentRefToBookingPayments` migration runs without errors
- [ ] Manual payment proof path (Phase 5 + 7) still works independently alongside Paymongo

---

## Deferred Features (Post-MVP)

These features are excluded from all 11 phases. The schema and command structure are designed to accommodate them without breaking changes.

| Feature | Why Deferred | What Is Already Prepared |
|---|---|---|
| **VO Credits / Wallet** | Virtual Office module not yet built | `CalculateFeeQuery` structure allows a credit-deduction step to be injected between discount and total computation |
| **Priority Booking for Premium VO** | Depends on VO module | Availability query is already parameterized — a "reserve window" flag can be added to `coworking_resources` |
| **SMS Notifications** | Low priority vs. email | No blockers — `INotificationService` abstraction can wrap both email and SMS |
| **Recurring / Auto-Renewing Bookings** | Complex billing logic | `Booking` entity is designed per-session; a `RecurringBookingSchedule` table can be added as a separate aggregate |

---

## Recommended Enhancements (Not In Current Phases)

These were reviewed but deliberately kept out of the phase plan to preserve scope. Discuss with the team before adding.

### Phase 0 — Test Infrastructure
Consider inserting a **Phase 0** before Phase 1 to establish the testing foundation:
- xUnit project with `Testcontainers` for a real PostgreSQL test database
- `WebApplicationFactory` integration test base class
- One smoke test: `GET /health` returns 200

Every phase's exit criteria currently reference manual Swagger testing. With Phase 0, each exit criterion becomes an automated integration test that runs in CI. This investment pays off throughout all 11 phases.

### Phase 7 Split (7a / 7b)
Phase 7 currently combines downpayment confirmation + email sending in one phase. Email introduces an external SMTP dependency that can fail silently.

Consider splitting:
- **Phase 7a:** Downpayment confirmation + booking status transitions (no email)
- **Phase 7b:** All email notifications (confirmation, reminder, discount rejection)

This makes email failures non-blocking for the booking flow and each sub-phase independently deployable. Trade-off: increases total phase count to 12 and renumbers Phases 8–11.

---

## Migration Reference

| Migration Name | Phase | What It Adds |
|---|---|---|
| `CoworkingInitialSchema` | Phase 1 | All 6 coworking tables, PostgreSQL `booking_ref_seq` sequence, `reminder_sent_at` and `ending_reminder_sent_at` on `bookings` (added upfront to avoid mid-project surprise migrations) |
| `AddExternalPaymentRefToBookingPayments` | Phase 11 | `booking_payments.external_payment_ref` |

All migrations use the naming convention `dotnet ef migrations add {MigrationName} --project Persistence --startup-project WebApi`.

Never modify a migration after it has been applied to any environment. Always create a new migration for schema corrections.

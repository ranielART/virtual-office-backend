# Coworking Module — Implementation Guide: Phases 3, 4A, 4B, and 5

This guide covers the implementation of the public availability endpoints, the full walk-in booking lifecycle, cash payment handling, and manual payment proof upload with FrontDesk validation. It is written for developers already familiar with the project's Clean Architecture / CQRS conventions.

## Phase Overview

| Phase | Deliverable | DB Changes |
|---|---|---|
| 3 | Availability Check (2 public GET endpoints) | None |
| 4A | Walk-In Time-In (booking creation) | Yes — `booking_ref_seq` sequence |
| 4B | Walk-In Lifecycle: Time-Out + Cash Payment + Read Queries | None |
| 5 | Manual Payment Proof: Upload + FrontDesk Validation | None |

---

## Shared Logic (Cross-Phase)

These algorithms are used in multiple phases. Define them as private helper methods or static utilities to avoid duplication.

### Availability Algorithm

Used in Phase 3 (query handlers) and Phase 4A (inside the DB transaction before creating a booking).

```csharp
var isUnavailable = await _dbContext.Bookings.AnyAsync(b =>
    b.ResourceId == resourceId &&
    b.BookingStatus != BookingStatus.Cancelled &&
    b.BookingStatus != BookingStatus.NoShow &&
    b.BookingStatus != BookingStatus.CheckedOut &&
    b.BookingStatus != BookingStatus.Overstayed &&
    b.ScheduledStart < requestedEnd &&
    b.ScheduledEnd   > requestedStart,
    cancellationToken);
```

`COUNT = 0` → available. `COUNT > 0` → unavailable.

### SnapshotHourlyRate Formula

Used in Phase 4A during booking creation to freeze the per-hour rate at time-in.

| Package Type | Formula |
|---|---|
| `Hourly` | `PriceAmount` (already per-hour) |
| `DayPass`, `HalfDay`, `FullDay`, `WeeklyPass` | `PriceAmount / DurationHours` |
| `MonthlyDedicatedDesk` | `PriceAmount / (DurationDays × 8)` — 8-hour workday assumed |
| Meeting room flat package (no `DurationHours`/`DurationDays` on the booked package) | Query the active `Hourly` `ServicePackage` for the same `ResourceType`; use its `PriceAmount` |

### Payment Status Recalculation Helper

Used in Phase 4B and Phase 5 whenever a payment is recorded or validated. Call this after any payment write and before `SaveChangesAsync`.

```csharp
var totalValidatedPaid = booking.Payments
    .Where(p => p.PaidAt != null &&
                (p.PaymentValidationStatus == PaymentValidationStatus.NotRequired ||
                 p.PaymentValidationStatus == PaymentValidationStatus.Validated))
    .Sum(p => p.Amount);

if (totalValidatedPaid >= booking.TotalAmount)
    booking.PaymentStatus = PaymentStatus.FullyPaid;
else if (totalValidatedPaid > 0)
    booking.PaymentStatus = PaymentStatus.PartiallyPaid;
else
    booking.PaymentStatus = PaymentStatus.Unpaid;
```

Then apply the Overstayed → CheckedOut transition:

```csharp
if (booking.PaymentStatus == PaymentStatus.FullyPaid &&
    booking.BookingStatus == BookingStatus.Overstayed)
{
    booking.BookingStatus = BookingStatus.CheckedOut;
}
```

### FrontDeskUserId from JWT

Used in Phase 4A and all subsequent write commands in `CoworkingFrontDeskController`. Either:

**Option A — extract in the controller and pass as a command property:**
```csharp
var frontDeskUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
```

**Option B — inject `IHttpContextAccessor` in the command handler:**
```csharp
var frontDeskUserId = _httpContextAccessor.HttpContext!.User
    .FindFirstValue(ClaimTypes.NameIdentifier)!;
```

Pick one and apply consistently across all handlers.

---

## Phase 3 — Availability Check

**Objective:** Two public GET endpoints that return availability. No auth required. No bookings yet in the system means all active resources return as available — this is the correct baseline state.

### New Files

| File | Purpose |
|---|---|
| `Application/Services/Coworking/Query/CheckAvailabilityQuery.cs` | List view — all resources for a time window |
| `Application/Services/Coworking/Query/CheckWalkInAvailabilityQuery.cs` | Single resource check — right now + estimated hours |
| `Application/Services/Coworking/Validator/CheckAvailabilityQueryValidator.cs` | |
| `Application/Services/Coworking/Validator/CheckWalkInAvailabilityQueryValidator.cs` | |
| `Application/Services/Coworking/Response/AvailabilityResponse.cs` | |
| `Application/Services/Coworking/Response/CheckWalkInAvailabilityResponse.cs` | |
| `WebApi/Controllers/CoworkingController.cs` | Public controller, `api/coworking` base route |

### CheckAvailabilityQuery

**Input:**

| Property | Type | Notes |
|---|---|---|
| `Date` | `DateOnly` | The date to check |
| `StartTime` | `TimeOnly` | Start of the window |
| `EndTime` | `TimeOnly` | End of the window |
| `ResourceType` | `ResourceType?` | Optional filter |

**Handler steps:**

1. Convert to UTC `DateTime` values:
   ```csharp
   var requestedStart = Date.ToDateTime(StartTime).ToUniversalTime();
   var requestedEnd   = Date.ToDateTime(EndTime).ToUniversalTime();
   ```
   > **Important:** `DateOnly.ToDateTime(TimeOnly)` produces a `DateTime` with `Kind = Unspecified`. Call `.ToUniversalTime()` immediately so PostgreSQL comparisons against UTC-stored values are correct.

2. Fetch all `CoworkingResource` where `IsActive = true`. If `ResourceType` is provided, add `.Where(r => r.ResourceType == ResourceType)`.

3. For each resource, run the [Availability Algorithm](#availability-algorithm) with `resourceId = resource.Id`, `requestedStart`, `requestedEnd`. Set `IsAvailable = !isUnavailable`.

4. Return all resources (both available and unavailable) so the frontend can render a complete grid.

**Output:** `List<AvailabilityResponse>`

### AvailabilityResponse

```csharp
public record AvailabilityResponse(
    Guid         ResourceId,
    string       ResourceName,
    ResourceType ResourceType,
    int          Capacity,
    bool         IsAvailable
);
```

### CheckWalkInAvailabilityQuery

**Input:**

| Property | Type |
|---|---|
| `ResourceId` | `Guid` |
| `EstimatedHours` | `decimal` |

**Handler steps:**

1. ```csharp
   var requestedStart = DateTime.UtcNow;
   var requestedEnd   = DateTime.UtcNow.AddHours((double)EstimatedHours);
   ```

2. Fetch the `CoworkingResource`; throw `NotFoundException` if not found or `IsActive = false`.

3. Run the [Availability Algorithm](#availability-algorithm) for the single resource.

4. If unavailable, compute `NextAvailableAt`:
   ```csharp
   var nextAvailableAt = await _dbContext.Bookings
       .Where(b => b.ResourceId == ResourceId &&
                   b.BookingStatus != BookingStatus.Cancelled &&
                   b.BookingStatus != BookingStatus.NoShow &&
                   b.BookingStatus != BookingStatus.CheckedOut &&
                   b.BookingStatus != BookingStatus.Overstayed)
       .OrderBy(b => b.ScheduledEnd)
       .Select(b => (DateTime?)b.ScheduledEnd)
       .FirstOrDefaultAsync(cancellationToken);
   ```

**Output:** `CheckWalkInAvailabilityResponse`

### CheckWalkInAvailabilityResponse

```csharp
public record CheckWalkInAvailabilityResponse(
    Guid      ResourceId,
    string    ResourceName,
    bool      IsAvailable,
    DateTime? NextAvailableAt   // null if available
);
```

### Validators

**CheckAvailabilityQueryValidator:**

```csharp
RuleFor(x => x.Date)
    .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today))
    .WithMessage("Date must not be in the past.");

RuleFor(x => x)
    .Must(q => q.StartTime < q.EndTime)
    .WithMessage("Start time must be before end time.");

RuleFor(x => x)
    .Must(q => (q.Date.ToDateTime(q.EndTime) - q.Date.ToDateTime(q.StartTime)).TotalHours <= 24)
    .WithMessage("Time window must be 24 hours or less.");

RuleFor(x => x.ResourceType)
    .IsInEnum()
    .When(x => x.ResourceType.HasValue);
```

**CheckWalkInAvailabilityQueryValidator:**

```csharp
RuleFor(x => x.ResourceId).NotEmpty();

RuleFor(x => x.EstimatedHours)
    .GreaterThan(0)
    .LessThanOrEqualTo(24);
```

### Controller — CoworkingController.cs

```csharp
[ApiController]
[Route("api/coworking")]
public class CoworkingController : ControllerBase
{
    private readonly IMediator _mediator;
    public CoworkingController(IMediator mediator) => _mediator = mediator;

    // GET api/coworking/availability?date=2026-03-15&startTime=09:00&endTime=17:00&resourceType=CoworkingSeats
    [HttpGet("availability")]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] CheckAvailabilityQuery query, CancellationToken ct)
        => Ok(await _mediator.Send(query, ct));

    // GET api/coworking/availability/{resourceId}?estimatedHours=3
    [HttpGet("availability/{resourceId:guid}")]
    public async Task<IActionResult> CheckWalkInAvailability(
        Guid resourceId,
        [FromQuery] decimal estimatedHours,
        CancellationToken ct)
        => Ok(await _mediator.Send(
               new CheckWalkInAvailabilityQuery(resourceId, estimatedHours), ct));
}
```

No `[Authorize]` attribute — this controller is fully public.

### Exit Criteria

- [ ] `GET api/coworking/availability` with a valid future time window returns all active resources with `IsAvailable = true` (no bookings yet)
- [ ] `ResourceType` filter correctly scopes results; inactive resources are excluded
- [ ] Walk-in endpoint returns `IsAvailable = true`, `NextAvailableAt = null` with no bookings in the system
- [ ] Validator rejects: past date, `StartTime >= EndTime`, window > 24 hours
- [ ] Validator rejects: `EstimatedHours <= 0` or `> 24`, empty `ResourceId`

---

## Phase 4A — Walk-In Time-In

**Objective:** FrontDesk creates a walk-in booking for a guest. The booking is immediately in `CheckedIn` state with all pricing fields frozen at creation time. Requires a DB migration for the reference number sequence.

### New Files

| File | Purpose |
|---|---|
| `Persistence/Migrations/[timestamp]_AddBookingReferenceSequence.cs` | Adds `booking_ref_seq` PostgreSQL sequence |
| `Application/Services/Coworking/Command/ProcessWalkInTimeInCommand.cs` | |
| `Application/Services/Coworking/Validator/ProcessWalkInTimeInCommandValidator.cs` | |
| `Application/Services/Coworking/Request/WalkInTimeInRequest.cs` | |
| `Application/Services/Coworking/Response/WalkInTimeInResponse.cs` | |
| `WebApi/Controllers/CoworkingFrontDeskController.cs` | `api/coworking/frontdesk`, Admin + Frontdesk auth |

### Migration

Generate the migration file:

```bash
dotnet ef migrations add AddBookingReferenceSequence
```

Then edit the generated migration to add the sequence:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("CREATE SEQUENCE IF NOT EXISTS booking_ref_seq START 1;");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("DROP SEQUENCE IF EXISTS booking_ref_seq;");
}
```

### WalkInTimeInRequest

```csharp
public record WalkInTimeInRequest(
    Guid    ResourceId,
    Guid    ServicePackageId,
    decimal EstimatedHours,
    string  GuestFirstName,
    string  GuestLastName,
    string  GuestPhone,
    string? GuestEmail,       // optional — collect when possible for future notifications
    bool    IsStudent
);
```

### ProcessWalkInTimeInCommand Handler

**Steps:**

1. Fetch `CoworkingResource` by `ResourceId`; throw `NotFoundException` if not found or `IsActive = false`.

2. Fetch `ServicePackage` by `ServicePackageId`; throw `NotFoundException` if not found or `IsActive = false`.

3. **Begin DB transaction** (`await using var tx = await _dbContext.Database.BeginTransactionAsync()`).

4. Re-run the [Availability Algorithm](#availability-algorithm) inside the transaction:
   ```csharp
   var requestedStart = DateTime.UtcNow;
   var requestedEnd   = DateTime.UtcNow.AddHours((double)command.EstimatedHours);
   ```
   If `isUnavailable` → throw `ConflictException("This resource is currently occupied.")`.

5. Generate the reference number:
   ```csharp
   var nextVal = await _dbContext.Database
       .SqlQuery<long>($"SELECT nextval('booking_ref_seq')")
       .FirstAsync(cancellationToken);
   var referenceNumber = $"BKG-{nextVal:D4}";
   ```

6. Compute `SnapshotHourlyRate` using the [SnapshotHourlyRate Formula](#snapshothourlyrateformula). If the package is a flat meeting-room type with no `DurationHours` or `DurationDays`, query for the active `Hourly` `ServicePackage` with the same `ResourceType`:
   ```csharp
   var hourlyPackage = await _dbContext.ServicePackages
       .FirstOrDefaultAsync(p => p.ResourceType == resource.ResourceType &&
                                 p.PackageType   == PackageType.Hourly &&
                                 p.IsActive,
                            cancellationToken);
   snapshotHourlyRate = hourlyPackage?.PriceAmount ?? 0m;
   ```

7. Compute pricing:
   ```csharp
   var baseAmount = package.PackageType == PackageType.Hourly
       ? command.EstimatedHours * package.PriceAmount
       : package.PriceAmount; // flat rate

   var discountAmount = command.IsStudent && package.IsStudentEligible
       ? baseAmount * (package.StudentDiscountPercent / 100m)
       : 0m;

   var totalAmount   = baseAmount - discountAmount;
   var balanceAmount = totalAmount; // walk-ins: no downpayment split
   ```

8. Create `BookingGuest`:
   ```csharp
   var guest = new BookingGuest
   {
       FirstName         = command.GuestFirstName,
       LastName          = command.GuestLastName,
       Phone             = command.GuestPhone,
       Email             = command.GuestEmail,   // nullable — store when provided
       IsStudentVerified = command.IsStudent      // physical ID verified by FrontDesk at counter
   };
   ```

9. Create `Booking`:
   ```csharp
   var booking = new Booking
   {
       ReferenceNumber      = referenceNumber,
       BookingType          = BookingType.WalkIn,
       BookingStatus        = BookingStatus.CheckedIn,
       PaymentStatus        = PaymentStatus.Unpaid,
       ResourceId           = command.ResourceId,
       ServicePackageId     = command.ServicePackageId,
       FrontDeskUserId      = frontDeskUserId,          // from JWT (see Shared Logic)
       ScheduledStart       = requestedStart,
       ScheduledEnd         = requestedEnd,
       ActualStart          = requestedStart,
       IsStudent            = command.IsStudent,
       DownpaymentAmount    = 0m,
       SnapshotPackageName  = package.Name,
       SnapshotPricePerUnit = package.PriceAmount,
       SnapshotHourlyRate   = snapshotHourlyRate,
       BaseAmount           = baseAmount,
       DiscountAmount       = discountAmount,
       TotalAmount          = totalAmount,
       BalanceAmount        = balanceAmount,
       Guest                = guest
   };
   ```

10. Persist and commit:
    ```csharp
    _dbContext.Bookings.Add(booking);
    await _dbContext.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);
    ```

11. Return `WalkInTimeInResponse`.

### WalkInTimeInResponse

```csharp
public record WalkInTimeInResponse(
    Guid     BookingId,
    string   ReferenceNumber,
    string   GuestName,          // FirstName + " " + LastName
    string   ResourceName,
    DateTime ScheduledStart,
    DateTime ScheduledEnd,
    string   SnapshotPackageName,
    bool     IsStudent,
    decimal  BaseAmount,
    decimal  DiscountAmount,
    decimal  TotalAmount
);
```

### Validator — ProcessWalkInTimeInCommandValidator

```csharp
RuleFor(x => x.ResourceId).NotEmpty();
RuleFor(x => x.ServicePackageId).NotEmpty();

RuleFor(x => x.EstimatedHours)
    .GreaterThan(0)
    .LessThanOrEqualTo(24);

RuleFor(x => x.GuestFirstName).NotEmpty().MaximumLength(100);
RuleFor(x => x.GuestLastName).NotEmpty().MaximumLength(100);

RuleFor(x => x.GuestPhone)
    .NotEmpty()
    .Matches(@"^\d{10,11}$")
    .WithMessage("Phone must be 10 or 11 digits.");

RuleFor(x => x.GuestEmail)
    .EmailAddress()
    .When(x => !string.IsNullOrWhiteSpace(x.GuestEmail))
    .WithMessage("Guest email must be a valid email address.");
```

### Controller — CoworkingFrontDeskController.cs

```csharp
[ApiController]
[Route("api/coworking/frontdesk")]
[Authorize(Roles = "Admin,Frontdesk")]
public class CoworkingFrontDeskController : ControllerBase
{
    private readonly IMediator _mediator;
    public CoworkingFrontDeskController(IMediator mediator) => _mediator = mediator;

    // POST api/coworking/frontdesk/time-in
    [HttpPost("time-in")]
    public async Task<IActionResult> TimeIn(
        [FromBody] WalkInTimeInRequest request, CancellationToken ct)
    {
        var frontDeskUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _mediator.Send(
            new ProcessWalkInTimeInCommand(request, frontDeskUserId), ct);
        return Ok(result);
    }
}
```

### Exit Criteria

- [ ] `POST time-in` creates one row in `bookings` and one row in `booking_guests`
- [ ] `BookingStatus = CheckedIn`, `PaymentStatus = Unpaid`
- [ ] All snapshot fields (`SnapshotPackageName`, `SnapshotPricePerUnit`, `SnapshotHourlyRate`) are populated
- [ ] Student discount is applied when `IsStudent = true` and `package.IsStudentEligible = true`
- [ ] `ReferenceNumber = "BKG-0001"` for the first booking
- [ ] `GET api/coworking/availability` (Phase 3) now returns the booked resource as unavailable during its scheduled window
- [ ] Concurrent duplicate requests targeting the same resource: one succeeds (HTTP 200), one returns HTTP 409

---

## Phase 4B — Walk-In Lifecycle + Cash Payment + Read Queries

**Objective:** Complete the walk-in flow with time-out (including overstay detection), cash payment recording, and three read queries for FrontDesk operations.

### New Files

| File | Purpose |
|---|---|
| `Application/Services/Coworking/Command/ProcessWalkInTimeOutCommand.cs` | |
| `Application/Services/Coworking/Command/ProcessPaymentCommand.cs` | Cash only in this phase |
| `Application/Services/Coworking/Query/GetActiveSessionsQuery.cs` | All `CheckedIn` bookings |
| `Application/Services/Coworking/Query/GetBookingsByDateQuery.cs` | All bookings for a given date |
| `Application/Services/Coworking/Query/GetBookingByIdQuery.cs` | Full booking detail |
| `Application/Services/Coworking/Validator/ProcessPaymentCommandValidator.cs` | |
| `Application/Services/Coworking/Response/BookingSessionResponse.cs` | Used by active sessions and by-date queries |
| `Application/Services/Coworking/Response/BookingDetailResponse.cs` | Full booking detail |
| `Application/Services/Coworking/Response/BookingPaymentResponse.cs` | Nested inside `BookingDetailResponse` |
| `Application/Services/Coworking/Request/ProcessPaymentRequest.cs` | |

### ProcessWalkInTimeOutCommand Handler

**Input:** `BookingId (Guid)`

**Steps:**

1. Fetch booking with `Include(b => b.Guest)`, `Include(b => b.Payments)`, `Include(b => b.Extensions)`; throw `NotFoundException` if not found.

2. Validate `BookingStatus == BookingStatus.CheckedIn`; throw `ConflictException("Booking is not currently checked in.")` otherwise.

3. Call the domain method:
   ```csharp
   booking.CheckOut(DateTime.UtcNow, booking.SnapshotHourlyRate);
   ```
   This sets `ActualEnd`, computes `OverstayAmount`, and transitions `BookingStatus` to `CheckedOut` or `Overstayed`.

4. Recalculate financials after checkout:
   ```csharp
   var extensionTotal = booking.Extensions.Sum(e => e.Amount); // 0 until Phase 10
   booking.TotalAmount = booking.BaseAmount - booking.DiscountAmount
                         + booking.OverstayAmount + extensionTotal;

   var totalPaid = booking.Payments
       .Where(p => p.PaidAt != null &&
                   p.PaymentValidationStatus != PaymentValidationStatus.Rejected)
       .Sum(p => p.Amount);

   booking.BalanceAmount = booking.TotalAmount - totalPaid;
   ```

5. `await _dbContext.SaveChangesAsync(cancellationToken)`.

6. Return a response containing: `BookingId`, `BookingStatus`, `ActualEnd`, `OverstayAmount`, `TotalAmount`, `BalanceAmount`. You may return a `BookingDetailResponse` (defined below) or a dedicated slim DTO — pick one and apply consistently.

### ProcessPaymentCommand Handler

**Input:** `BookingId (Guid)`, `Amount (decimal)`, `PaymentMethod (PaymentMethod)`, `PaymentType (PaymentType)`, `Notes (string?)`

**Steps:**

1. Fetch booking with `Include(b => b.Payments)`; throw `NotFoundException` if not found.

2. Guard on accepted payment methods. Only `Cash` is processed here:

   | PaymentMethod | Exception |
   |---|---|
   | `ImageAttachment` | `ConflictException("Use the proof upload endpoint (POST frontdesk/payments/submit-proof) for image-based payments.")` |
   | `Card` | `ConflictException("Card payments are not yet supported.")` |
   | `Paymongo` | `ConflictException("Paymongo integration is not yet available.")` |

3. Create `BookingPayment`:
   ```csharp
   var payment = new BookingPayment
   {
       BookingId               = command.BookingId,
       Amount                  = command.Amount,
       PaymentMethod           = PaymentMethod.Cash,
       PaymentType             = command.PaymentType,
       PaymentValidationStatus = PaymentValidationStatus.NotRequired,
       PaidAt                  = DateTime.UtcNow,
       ProcessedByUserId       = frontDeskUserId,  // from JWT
       Notes                   = command.Notes
   };
   _dbContext.BookingPayments.Add(payment);
   booking.Payments.Add(payment);
   ```

4. Recalculate `booking.PaymentStatus` using the [Payment Status Recalculation Helper](#payment-status-recalculation-helper).

5. Apply the Overstayed → CheckedOut transition if applicable (per Shared Logic).

6. `await _dbContext.SaveChangesAsync(cancellationToken)`.

### ProcessPaymentCommandValidator

```csharp
RuleFor(x => x.BookingId).NotEmpty();
RuleFor(x => x.Amount).GreaterThan(0);
RuleFor(x => x.PaymentMethod).IsInEnum();
RuleFor(x => x.PaymentType).IsInEnum();
```

### ProcessPaymentRequest

```csharp
public record ProcessPaymentRequest(
    Guid          BookingId,
    decimal       Amount,
    PaymentMethod PaymentMethod,
    PaymentType   PaymentType,
    string?       Notes
);
```

### GetActiveSessionsQuery

- **Input:** None
- **Logic:** Return all bookings where `BookingStatus == BookingStatus.CheckedIn`, ordered by `ActualStart` ascending. Include `Resource` and `Guest`.
- **Output:** `List<BookingSessionResponse>`

### GetBookingsByDateQuery

- **Input:** `Date (DateOnly)`
- **Logic:** Return all bookings where the date portion of `ScheduledStart` equals `Date`:
  ```csharp
  .Where(b => b.ScheduledStart.Date == Date.ToDateTime(TimeOnly.MinValue).Date)
  ```
  Order by `ScheduledStart` ascending. Include `Resource` and `Guest`.
- **Output:** `List<BookingSessionResponse>`

### BookingSessionResponse

```csharp
public record BookingSessionResponse(
    Guid          BookingId,
    string        ReferenceNumber,
    string        GuestName,          // FirstName + " " + LastName
    string        ResourceName,
    BookingStatus BookingStatus,
    PaymentStatus PaymentStatus,
    DateTime      ScheduledStart,
    DateTime      ScheduledEnd,
    DateTime?     ActualStart,
    string        SnapshotPackageName,
    decimal       TotalAmount,
    decimal       BalanceAmount
);
```

### GetBookingByIdQuery

- **Input:** `BookingId (Guid)`
- **Logic:** Fetch booking with all navigations:
  ```csharp
  .Include(b => b.Guest)
  .Include(b => b.Payments)
  .Include(b => b.Extensions)
  .Include(b => b.Resource)
  .Include(b => b.ServicePackage)
  ```
  Throw `NotFoundException` if not found.
- **Computed fields:**
  ```csharp
  var totalPaid = booking.Payments
      .Where(p => p.PaidAt != null &&
                  p.PaymentValidationStatus != PaymentValidationStatus.Rejected)
      .Sum(p => p.Amount);
  var outstandingBalance = booking.TotalAmount - totalPaid;
  ```
- **Output:** `BookingDetailResponse`

### BookingDetailResponse

```csharp
public record BookingDetailResponse(
    Guid                        BookingId,
    string                      ReferenceNumber,
    BookingType                 BookingType,
    BookingStatus               BookingStatus,
    PaymentStatus               PaymentStatus,
    string                      ResourceName,
    string                      GuestName,
    string                      GuestPhone,
    string?                     GuestEmail,
    bool                        IsStudent,
    bool                        IsStudentVerified,
    DateTime                    ScheduledStart,
    DateTime                    ScheduledEnd,
    DateTime?                   ActualStart,
    DateTime?                   ActualEnd,
    string                      SnapshotPackageName,
    decimal                     BaseAmount,
    decimal                     DiscountAmount,
    decimal                     OverstayAmount,
    decimal                     TotalAmount,
    decimal                     DownpaymentAmount,
    decimal                     BalanceAmount,
    decimal                     TotalPaid,
    decimal                     OutstandingBalance,
    string?                     Notes,
    List<BookingPaymentResponse> Payments
);
```

### BookingPaymentResponse

```csharp
public record BookingPaymentResponse(
    Guid                    PaymentId,
    decimal                 Amount,
    PaymentMethod           PaymentMethod,
    PaymentType             PaymentType,
    PaymentValidationStatus PaymentValidationStatus,
    string?                 ProofOfPaymentUrl,
    DateTime?               PaidAt,
    DateTime                CreatedAt
);
```

### Add to CoworkingFrontDeskController

```csharp
// POST api/coworking/frontdesk/time-out/{bookingId}
[HttpPost("time-out/{bookingId:guid}")]
public async Task<IActionResult> TimeOut(Guid bookingId, CancellationToken ct)
    => Ok(await _mediator.Send(new ProcessWalkInTimeOutCommand(bookingId), ct));

// POST api/coworking/frontdesk/payment
[HttpPost("payment")]
public async Task<IActionResult> ProcessPayment(
    [FromBody] ProcessPaymentRequest request, CancellationToken ct)
{
    var frontDeskUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var result = await _mediator.Send(new ProcessPaymentCommand(request, frontDeskUserId), ct);
    return Ok(result);
}

// GET api/coworking/frontdesk/sessions/active
[HttpGet("sessions/active")]
public async Task<IActionResult> GetActiveSessions(CancellationToken ct)
    => Ok(await _mediator.Send(new GetActiveSessionsQuery(), ct));

// GET api/coworking/frontdesk/bookings?date=2026-03-15
[HttpGet("bookings")]
public async Task<IActionResult> GetBookingsByDate(
    [FromQuery] DateOnly date, CancellationToken ct)
    => Ok(await _mediator.Send(new GetBookingsByDateQuery(date), ct));

// GET api/coworking/frontdesk/bookings/{id}
[HttpGet("bookings/{id:guid}")]
public async Task<IActionResult> GetBookingById(Guid id, CancellationToken ct)
    => Ok(await _mediator.Send(new GetBookingByIdQuery(id), ct));
```

### Exit Criteria

- [ ] Full walk-in lifecycle works end-to-end: time-in → time-out → cash payment → `BookingStatus = CheckedOut`, `PaymentStatus = FullyPaid`
- [ ] Time-out after `ScheduledEnd` sets `BookingStatus = Overstayed` and `OverstayAmount > 0`
- [ ] Paying the overstay balance transitions to `CheckedOut + FullyPaid`
- [ ] `GET sessions/active` reflects live `CheckedIn` state; checked-out bookings do not appear
- [ ] `GET bookings?date=...` returns all bookings (any status) for that date
- [ ] `GET bookings/{id}` returns full detail with correctly computed `TotalPaid` and `OutstandingBalance`
- [ ] `ImageAttachment` payment via `POST payment` returns HTTP 409 directing to `submit-proof`
- [ ] Card payment via `POST payment` returns HTTP 409 with the not-supported message
- [ ] Time-out on a non-`CheckedIn` booking returns HTTP 409

---

## Phase 5 — Manual Payment Proof

**Objective:** Image-based payments require a screenshot upload to Azure Blob Storage and FrontDesk validation before the payment is counted toward the booking balance. The FrontDesk uploads any proof-of-payment image (GCash, Maya, bank transfer, etc.) under a single `ImageAttachment` method — no need to specify the channel. After this phase, both walk-in payment methods (Cash and ImageAttachment) are fully supported.

### Domain Change — PaymentMethod Enum

Add a new value to the existing `PaymentMethod` enum in `Domain/Enum/PaymentMethod.cs`:

```csharp
public enum PaymentMethod
{
    Cash,
    Card,
    GCash,           // reserved — online booking (future phase)
    Maya,            // reserved — online booking (future phase)
    BankTransfer,    // reserved — online booking (future phase)
    Paymongo,        // reserved — Phase 11
    ImageAttachment  // walk-in proof upload: any e-wallet/bank screenshot
}
```

> `GCash`, `Maya`, and `BankTransfer` remain in the enum for future online booking phases where the guest specifies their payment channel. For walk-in FrontDesk use, `ImageAttachment` covers all proof-based payments without requiring a specific channel.
>
> Since EF Core stores enum values as integers by default, adding `ImageAttachment` at the end does not require a migration.

### New Files

| File | Purpose |
|---|---|
| `Application/Services/Coworking/Command/SubmitManualPaymentProofCommand.cs` | |
| `Application/Services/Coworking/Command/ValidateManualPaymentCommand.cs` | |
| `Application/Services/Coworking/Query/GetPendingPaymentValidationsQuery.cs` | |
| `Application/Services/Coworking/Validator/SubmitManualPaymentProofCommandValidator.cs` | |
| `Application/Services/Coworking/Validator/ValidateManualPaymentCommandValidator.cs` | |
| `Application/Services/Coworking/Request/SubmitManualPaymentProofRequest.cs` | |
| `Application/Services/Coworking/Request/ValidateManualPaymentRequest.cs` | |
| `Application/Services/Coworking/Response/PendingPaymentValidationResponse.cs` | |

### SubmitManualPaymentProofCommand Handler

**Input:** `BookingId (Guid)`, `PaymentType (PaymentType)`, `Amount (decimal)`, `GuestProvidedReference (string?)`, `IFormFile File`, `ProcessedByUserId (string)`

`PaymentMethod` is not part of the input — the handler always sets it to `PaymentMethod.ImageAttachment`.

**Steps:**

1. Fetch booking; throw `NotFoundException` if not found.

2. Validate booking exists and is in an active state (not `Cancelled`, `CheckedOut`, `NoShow`). Throw `ConflictException` if the booking cannot accept payments.

3. Upload the file to Azure Blob Storage:
   ```csharp
   var fileName  = Path.GetFileName(command.File.FileName);
   var key       = $"payment-proofs/{command.BookingId}/{Guid.NewGuid()}_{fileName}";
   var proofUrl  = await _storageService.UploadFileAsync(
                       key, command.File.OpenReadStream(), command.File.ContentType);
   ```

4. Create `BookingPayment`:
   ```csharp
   var payment = new BookingPayment
   {
       BookingId               = command.BookingId,
       Amount                  = command.Amount,
       PaymentMethod           = PaymentMethod.ImageAttachment,
       PaymentType             = command.PaymentType,
       PaymentValidationStatus = PaymentValidationStatus.PendingValidation,
       ProofOfPaymentUrl       = proofUrl,
       ReferenceNumber         = command.GuestProvidedReference,
       ProcessedByUserId       = command.ProcessedByUserId,
       PaidAt                  = null  // set only after FrontDesk approves
   };
   _dbContext.BookingPayments.Add(payment);
   ```

5. Do **not** update `booking.PaymentStatus` yet — that happens in `ValidateManualPaymentCommand`.

6. `await _dbContext.SaveChangesAsync(cancellationToken)`.

### SubmitManualPaymentProofCommandValidator

```csharp
RuleFor(x => x.BookingId).NotEmpty();

RuleFor(x => x.Amount).GreaterThan(0);

RuleFor(x => x.File)
    .NotNull()
    .WithMessage("A proof of payment file is required.");

When(x => x.File != null, () =>
{
    RuleFor(x => x.File.ContentType)
        .Must(ct => ct == "image/jpeg" || ct == "image/png")
        .WithMessage("Only JPEG and PNG images are accepted.");

    RuleFor(x => x.File.Length)
        .LessThanOrEqualTo(5 * 1024 * 1024)
        .WithMessage("File must not exceed 5 MB.");
});
```

### ValidateManualPaymentCommand Handler

**Input:** `PaymentId (Guid)`, `IsApproved (bool)`, `RejectionReason (string?)`, `ValidatedByUserId (string)`

**Steps:**

1. Fetch `BookingPayment` with its booking and sibling payments:
   ```csharp
   var payment = await _dbContext.BookingPayments
       .Include(p => p.Booking)
           .ThenInclude(b => b.Payments)
       .FirstOrDefaultAsync(p => p.Id == command.PaymentId, cancellationToken)
       ?? throw new NotFoundException(nameof(BookingPayment), command.PaymentId);
   ```

2. Validate `payment.PaymentValidationStatus == PaymentValidationStatus.PendingValidation`; throw `ConflictException("This payment has already been validated.")` otherwise.

3. Set audit fields:
   ```csharp
   payment.ValidatedByUserId = command.ValidatedByUserId;
   payment.ValidatedAt       = DateTime.UtcNow;
   ```

4. **If `IsApproved = true`:**
   ```csharp
   payment.PaymentValidationStatus = PaymentValidationStatus.Validated;
   payment.PaidAt                  = DateTime.UtcNow;
   ```
   Then recalculate `payment.Booking.PaymentStatus` using the [Payment Status Recalculation Helper](#payment-status-recalculation-helper) and apply the Overstayed → CheckedOut transition if applicable.

5. **If `IsApproved = false`:**
   ```csharp
   payment.PaymentValidationStatus = PaymentValidationStatus.Rejected;
   // booking.PaymentStatus unchanged; no booking status change
   ```

   > **Note for Phase 7 (online bookings):** When this command validates a downpayment for an online booking, Phase 7 will dispatch a `ConfirmDownpaymentCommand` here. Do not implement this now — add a comment placeholder.

6. `await _dbContext.SaveChangesAsync(cancellationToken)`.

### ValidateManualPaymentCommandValidator

```csharp
RuleFor(x => x.PaymentId).NotEmpty();

// IsApproved is a required bool; no special rule needed if it's non-nullable in the request.

RuleFor(x => x.RejectionReason)
    .NotEmpty()
    .MaximumLength(500)
    .When(x => !x.IsApproved)
    .WithMessage("A rejection reason is required when declining a payment.");
```

### SubmitManualPaymentProofRequest

The `IFormFile` is bound separately via `[FromForm]` in the controller.

```csharp
public record SubmitManualPaymentProofRequest(
    Guid        BookingId,
    PaymentType PaymentType,
    decimal     Amount,
    string?     GuestProvidedReference
);
```

`PaymentMethod` is omitted — `ImageAttachment` is always used and set by the handler.

### ValidateManualPaymentRequest

```csharp
public record ValidateManualPaymentRequest(
    bool    IsApproved,
    string? RejectionReason
);
```

### GetPendingPaymentValidationsQuery

- **Input:** None
- **Logic:**
  ```csharp
  await _dbContext.BookingPayments
      .Include(p => p.Booking)
          .ThenInclude(b => b.Resource)
      .Include(p => p.Booking)
          .ThenInclude(b => b.Guest)
      .Where(p => p.PaymentValidationStatus == PaymentValidationStatus.PendingValidation)
      .OrderBy(p => p.CreatedAt)
      .ToListAsync(cancellationToken);
  ```
- **Output:** `List<PendingPaymentValidationResponse>`

### PendingPaymentValidationResponse

```csharp
public record PendingPaymentValidationResponse(
    Guid          PaymentId,
    Guid          BookingId,
    string        ReferenceNumber,
    string        GuestName,
    string        ResourceName,
    decimal       Amount,
    PaymentMethod PaymentMethod,
    PaymentType   PaymentType,
    string        ProofOfPaymentUrl,
    DateTime      CreatedAt
);
```

### Add to CoworkingFrontDeskController

```csharp
// POST api/coworking/frontdesk/payments/submit-proof
[HttpPost("payments/submit-proof")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> SubmitPaymentProof(
    [FromForm] SubmitManualPaymentProofRequest request,
    IFormFile file,
    CancellationToken ct)
{
    var frontDeskUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var result = await _mediator.Send(
        new SubmitManualPaymentProofCommand(request, file, frontDeskUserId), ct);
    return Ok(result);
}

// POST api/coworking/frontdesk/payments/{paymentId}/validate
[HttpPost("payments/{paymentId:guid}/validate")]
public async Task<IActionResult> ValidatePayment(
    Guid paymentId,
    [FromBody] ValidateManualPaymentRequest request,
    CancellationToken ct)
{
    var frontDeskUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var result = await _mediator.Send(
        new ValidateManualPaymentCommand(paymentId, request, frontDeskUserId), ct);
    return Ok(result);
}

// GET api/coworking/frontdesk/payments/pending
[HttpGet("payments/pending")]
public async Task<IActionResult> GetPendingValidations(CancellationToken ct)
    => Ok(await _mediator.Send(new GetPendingPaymentValidationsQuery(), ct));
```

### Exit Criteria

- [ ] `POST payments/submit-proof` with a valid image creates a `BookingPayment` with `PaymentValidationStatus = PendingValidation` and a non-null `ProofOfPaymentUrl`
- [ ] The uploaded file is retrievable from Azure Blob Storage via the stored URL
- [ ] `GET payments/pending` returns the newly created pending payment
- [ ] `POST payments/{id}/validate` with `IsApproved = true` sets `PaymentValidationStatus = Validated`, `PaidAt` is set, and `booking.PaymentStatus` is recalculated
- [ ] `POST payments/{id}/validate` with `IsApproved = false` sets `PaymentValidationStatus = Rejected` and leaves `booking.PaymentStatus` unchanged
- [ ] Validating an already-validated payment returns HTTP 409
- [ ] Uploading a non-image file returns HTTP 400
- [ ] Uploading a file larger than 5 MB returns HTTP 400
- [ ] Submitting Cash or Card via `submit-proof` returns HTTP 409 directing to `POST frontdesk/payment`

---

## Full API Reference

### Public — `CoworkingController` (`api/coworking`) — No Auth Required

| Method | Route | Description |
|---|---|---|
| GET | `availability` | Check availability for a time window. Query: `date`, `startTime`, `endTime`, `resourceType?` |
| GET | `availability/{resourceId}` | Check a specific resource right now. Query: `estimatedHours` |

### FrontDesk — `CoworkingFrontDeskController` (`api/coworking/frontdesk`) — Roles: Admin, Frontdesk

| Method | Route | Description |
|---|---|---|
| POST | `time-in` | Check in a walk-in guest. Body: `WalkInTimeInRequest` |
| POST | `time-out/{bookingId}` | Check out a guest |
| POST | `payment` | Record a cash payment. Body: `ProcessPaymentRequest` |
| POST | `payments/submit-proof` | Upload image proof (any channel). Form: `SubmitManualPaymentProofRequest` + `file` |
| POST | `payments/{paymentId}/validate` | Approve or reject a proof. Body: `ValidateManualPaymentRequest` |
| GET | `sessions/active` | List all currently checked-in sessions |
| GET | `bookings` | List bookings for a date. Query: `date` |
| GET | `bookings/{id}` | Full booking detail |
| GET | `payments/pending` | List all pending proof validations |

### Already Existing — `CoworkingAdminController` (`api/coworking/admin`) — Role: Admin Only

Admin CRUD endpoints (resource management, package management, seeder triggers) are already implemented in Phases 1 and 2. No changes needed from these phases.

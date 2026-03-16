# Payment Scheme Design

## Core Idea

Payment scheme is a **per-booking choice made by the FrontDesk at time-in**, not a
fixed property of the package. The package defines which schemes are *allowed*;
the FrontDesk picks one based on what the customer agrees to.

```
Package  →  AllowsUpfront   = true/false
            AllowsPostpaid  = true/false
            AllowsDaily     = true/false   ← only meaningful for monthly packages

Time-In  →  FrontDesk selects one allowed scheme → stored on the Booking
```

This avoids creating duplicate packages just because different customers prefer
different payment terms. Admin configures options once; FrontDesk exercises them
per customer.

---

## PaymentScheme Enum

```csharp
public enum PaymentScheme
{
    Upfront  = 1,   // full payment collected at time-in, before session starts
    Postpaid = 2,   // payment collected at checkout, after total is finalized
    Daily    = 3    // one day's portion collected per visit (monthly packages only)
}
```

---

## Behavior Per Scheme

### Upfront
```
Time-In  → Booking created, TotalAmount known (fixed-price packages)
         → FrontDesk collects full amount now via existing payment endpoint
         → PaymentStatus → FullyPaid before member is seated
Time-Out → Checkout; no payment step needed
```

### Postpaid *(current default behavior — no change)*
```
Time-In  → Booking created, PaymentStatus = Unpaid
         → Member uses space
Time-Out → Checkout; TotalAmount finalized (overstay included)
         → FrontDesk collects payment
         → PaymentStatus → FullyPaid
```

### Daily *(monthly packages only)*
```
Time-In  → Booking created for full monthly period
         → TotalAmount = full package price
         → DailyRate = TotalAmount / DurationDays (returned in time-in response)
         → FrontDesk collects today's daily rate
         → PaymentStatus = PartiallyPaid

Each Day → Member arrives; FrontDesk records another daily rate payment
         → BalanceAmount decreases per payment

Time-Out → End of monthly period; FrontDesk checks out the booking
         → Outstanding balance settled before checkout
```

---

## What Changes in the Schema

### ServicePackage — three boolean flags replace one enum field

```csharp
public bool AllowsUpfront  { get; set; } = false;
public bool AllowsPostpaid { get; set; } = true;   // default on for all packages
public bool AllowsDaily    { get; set; } = false;  // only set for monthly packages
```

Validation rules on package create/update:
- At least one of the three must be `true`
- `AllowsDaily = true` is only valid when `PackageType == MonthlyDedicatedDesk`
- `AllowsUpfront = true` is only valid when `PackageType != Hourly` (total unknown at time-in for open-ended sessions)

### Booking — snapshot the chosen scheme

```csharp
public PaymentScheme SnapshotPaymentScheme { get; set; }
```

Frozen at time-in. Future package edits don't affect existing bookings.

### WalkInTimeInRequest — FrontDesk passes chosen scheme

```csharp
public PaymentScheme PaymentScheme { get; set; }
```

Validation: the chosen scheme must be one the package allows. If only one scheme
is allowed, the frontend can auto-select it and hide the picker.

### ScheduledEnd fix for non-hourly packages

Currently broken — always `now + EstimatedHours` regardless of package type.

| PackageType          | Correct ScheduledEnd                        |
|----------------------|---------------------------------------------|
| Hourly               | `now + EstimatedHours` ✓ (unchanged)        |
| HalfDay / FullDay / DayPass | `now + DurationHours`              |
| WeeklyPass           | `now + DurationDays calendar days`          |
| MonthlyDedicatedDesk | `now + DurationDays calendar days`          |

`EstimatedHours` becomes nullable in `WalkInTimeInRequest` — required only for Hourly.

### DailyRate in time-in response

```csharp
public decimal? DailyRate { get; set; }
```

Populated only when `SnapshotPaymentScheme == Daily`. FrontDesk uses this to know
how much to collect per visit without doing the math.

---

## Daily Attendance Tracking (Deferred)

A monthly member checks in/out each day. The current `Booking` entity has a single
`ActualStart`/`ActualEnd` — it cannot represent 22 separate daily sessions.

**This phase:** the booking stays `CheckedIn` for the entire monthly period.
The FrontDesk records a daily payment each visit via the existing payment endpoints.
Per-day attendance is not tracked yet.

**Future phase:** `MonthlyAttendance` table — one row per daily visit linked to the
parent booking. Required for occupancy reporting and automated daily payment prompts.

---

## Files to Create / Modify

| File | Change |
|------|--------|
| `Domain/Enum/PaymentScheme.cs` | New enum |
| `Domain/Entity/Coworking/ServicePackage.cs` | Add `AllowsUpfront`, `AllowsPostpaid`, `AllowsDaily` |
| `Domain/Entity/Coworking/Booking.cs` | Add `SnapshotPaymentScheme` |
| EF Core migration | New columns on both tables |
| Create/Update Package validator | At-least-one rule; Daily and Upfront restrictions |
| `WalkInTimeInRequest.cs` | Add `PaymentScheme`; make `EstimatedHours` nullable |
| `ProcessWalkInTimeInCommand.cs` | Fix `ScheduledEnd`; validate chosen scheme; snapshot it; compute `DailyRate` |
| `WalkInTimeInResponse.cs` | Add `SnapshotPaymentScheme`, `DailyRate` |

---

## Open Decisions

1. **Upfront + Hourly:** total is unknown at time-in since duration is open-ended.
   Recommendation: disallow at the package validator level — Hourly packages cannot
   have `AllowsUpfront = true`.

2. **DurationDays: calendar days or working days?**
   Recommendation: calendar days (e.g., 30). Working days require business-day
   calculation and timezone awareness — defer that complexity.

3. **Enforce Upfront collection before member is seated?**
   The system records the booking first, then the FrontDesk records payment via the
   existing endpoint. Hard enforcement (block time-in until payment recorded) is a
   UX decision — not technically required for the data model to be correct.

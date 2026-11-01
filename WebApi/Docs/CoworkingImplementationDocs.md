# Coworking Space Module — Implementation Plan

**Project:** CommunSide — Virtual Office Booking System
**Module:** Coworking Space
**Stack:** .NET 9 / Clean Architecture / CQRS (MediatR) / PostgreSQL / Next.js + TanStack Query

> All architectural decisions in this document are final and based on confirmed answers to the initial design questions. Deferred items are clearly marked and excluded from MVP scope.

---

## Table of Contents

1. [Architecture Decisions Log](#1-architecture-decisions-log)
2. [ERD / Database Schema](#2-erd--database-schema)
3. [Enums](#3-enums)
4. [Pricing Snapshot Strategy](#4-pricing-snapshot-strategy-historical-accuracy)
5. [Availability Algorithm](#5-availability-algorithm)
6. [Student Verification Flow](#6-student-verification-flow)
7. [Payment Handling](#7-payment-handling)
8. [No-Show Detection & Background Jobs](#8-no-show-detection--background-jobs)
9. [Session Extension Rules](#9-session-extension-rules)
10. [Domain Entities & Value Objects](#10-domain-entities--value-objects)
11. [CQRS Commands & Queries](#11-cqrs-commands--queries)
12. [Controller Structure](#12-controller-structure)
13. [Walk-In Flow Step-by-Step](#13-walk-in-flow-step-by-step)
14. [Online Booking Flow Step-by-Step](#14-online-booking-flow-step-by-step)
15. [Pricing Calculation Logic](#15-pricing-calculation-logic)
16. [Confirmation Email Content](#16-confirmation-email-content)
17. [Deferred Features (Post-MVP)](#17-deferred-features-post-mvp)

---

## 1. Architecture Decisions Log

A record of every confirmed decision that shaped this design.

| # | Decision | Outcome |
|---|---|---|
| 1 | Payment gateway | **Hybrid:** Manual uploads (GCash, Maya, Bank Transfer) requiring FrontDesk validation + automated Paymongo integration |
| 2 | VO Credits | **Deferred.** No wallet/credits tables in MVP. `CalculateFeeQuery` will be structured for easy credit-deduction injection later |
| 3 | Priority booking for Premium VO | **Deferred.** Shelved until the Virtual Office module is built |
| 4 | No-show detection | **Hybrid:** Background job (Hangfire or Quartz.NET) with grace period + manual FrontDesk override |
| 5 | Seat assignment | **Individual labeled seats.** Each of the 20 seats is a distinct row in `coworking_resources` (Seat 01–Seat 20). Admin manages them via CRUD — no code or migrations needed to add/disable seats |
| 6 | Student verification | **Verification required.** Online bookings: ID image upload (stored in Azure Blob). Walk-ins: physical ID presentation to FrontDesk. Discount held as `PendingStudentVerification` until FrontDesk approves |
| 7 | Confirmation email | Serves as a digital pass + receipt. Full field list defined in Section 16 |
| 8 | Extension billing | **Hourly rate only.** Extensions are separate line items billed at the standard hourly rate, tracked in a dedicated `booking_extensions` table |

---

## 2. ERD / Database Schema

```
┌─────────────────────────────────┐        ┌──────────────────────────────────┐
│      coworking_resources        │        │         service_packages         │
├─────────────────────────────────┤        ├──────────────────────────────────┤
│ Id (Guid) PK                    │        │ Id (Guid) PK                     │
│ Name (e.g. "Seat 01")           │        │ Name                             │
│ ResourceType (enum)             │        │ ResourceType (enum)              │
│ Capacity                        │        │ PackageType (enum)               │
│ Description                     │        │ PriceAmount (decimal)            │
│ IsActive                        │        │ DurationHours (int?)             │
│ SortOrder (int)                 │        │ DurationDays (int?)              │
│ CreatedAt                       │        │ MaxCapacity (int?)               │
│ UpdatedAt                       │        │ IsStudentEligible (bool)         │
└─────────────────────────────────┘        │ StudentDiscountPercent (decimal) │
                                           │ DownpaymentPercent (decimal)     │
                                           │ IsActive (bool)                  │
                                           │ CreatedAt                        │
                                           │ UpdatedAt                        │
                                           └──────────────────────────────────┘
                                                          │
              ┌───────────────────────────────────────────┤
              │                                           │
┌─────────────────────────────┐        ┌──────────────────────────────────────────┐
│       booking_guests        │        │                bookings                  │
├─────────────────────────────┤        ├──────────────────────────────────────────┤
│ Id (Guid) PK                │◄───────│ Id (Guid) PK                             │
│ FirstName                   │        │ ReferenceNumber (string, e.g. BKG-1042)  │
│ LastName                    │        │ BookingType (enum)                       │
│ Email                       │        │ BookingStatus (enum)                     │
│ Phone                       │        │ PaymentStatus (enum)                     │
│ StudentIdImageUrl (string?) │        │ ResourceId (FK → coworking_resources)    │
│ IsStudentVerified (bool)    │        │ ServicePackageId (FK → service_packages) │
│ CreatedAt                   │        │ GuestId (FK → booking_guests, nullable)  │
└─────────────────────────────┘        │ UserId (FK → users, nullable)            │ ← VO Member
                                       │ FrontDeskUserId (FK → users, nullable)   │ ← Who processed it
┌───────────────────┐                  │                                          │
│      users        │◄─────────────────│ ── SCHEDULE ──                           │
│  (existing table) │                  │ ScheduledStart                           │
└───────────────────┘                  │ ScheduledEnd                             │
                                       │ ActualStart (nullable)                   │ ← Set on Time In
                                       │ ActualEnd (nullable)                     │ ← Set on Time Out
                                       │ PaxCount (int)                           │ ← For meeting rooms
                                       │ IsStudent (bool)                         │
                                       │                                          │
                                       │ ── PRICE SNAPSHOT (frozen at creation) ──│
                                       │ SnapshotPackageName                      │
                                       │ SnapshotPricePerUnit (decimal)           │
                                       │ SnapshotHourlyRate (decimal)             │ ← For overstay/extension billing
                                       │ BaseAmount (decimal)                     │
                                       │ DiscountAmount (decimal)                 │
                                       │ DownpaymentAmount (decimal)              │
                                       │ DownpaymentPaidAt (nullable)             │
                                       │ BalanceAmount (decimal)                  │
                                       │ OverstayAmount (decimal)                 │
                                       │ TotalAmount (decimal)                    │
                                       │                                          │
                                       │ Notes                                    │
                                       │ CancellationReason                       │
                                       │ CreatedAt                                │
                                       │ UpdatedAt                                │
                                       └──────────────────────────────────────────┘
                                                          │
                       ┌──────────────────────────────────┴──────────────────────────────────┐
                       │                                                                     │
        ┌──────────────────────────────────┐              ┌──────────────────────────────────┐
        │         booking_payments         │              │        booking_extensions        │
        ├──────────────────────────────────┤              ├──────────────────────────────────┤
        │ Id (Guid) PK                     │              │ Id (Guid) PK                     │
        │ BookingId (FK)                   │              │ BookingId (FK)                   │
        │ Amount (decimal)                 │              │ AddedHours (decimal)             │
        │ PaymentMethod (enum)             │              │ SnapshotHourlyRate (decimal)     │ ← Rate at time of extension
        │ PaymentType (enum)               │              │ Amount (decimal)                 │
        │ PaymentValidationStatus (enum)   │              │ ProcessedByUserId (FK → users)   │
        │ ProofOfPaymentUrl (string?)      │              │ Notes                            │
        │ ReferenceNumber (string?)        │              │ CreatedAt                        │
        │ PaidAt (nullable)                │              └──────────────────────────────────┘
        │ ValidatedByUserId (FK, nullable) │
        │ ValidatedAt (nullable)           │
        │ ProcessedByUserId (FK, nullable) │
        │ CreatedAt                        │
        └──────────────────────────────────┘
```

### Table Descriptions

| Table | Description |
|---|---|
| `coworking_resources` | Every bookable physical resource. Initially seeded with 20 individual seats (Seat 01–20) + 2 meeting rooms. Admin can add, rename, or disable resources without migrations |
| `service_packages` | Fully admin-configurable pricing tiers. All rates, durations, discount eligibility, and downpayment percentages live here |
| `booking_guests` | Anonymous guest profile captured at booking time. Holds student ID image URL and verification flag. Separate from `users` — online guests have no account |
| `bookings` | Central aggregate and source of truth for every booking lifecycle. Price snapshots ensure historical receipt accuracy |
| `booking_payments` | Append-only payment ledger per booking. Supports manual proof uploads with FrontDesk validation workflow |
| `booking_extensions` | One-to-many child of `bookings`. Each FrontDesk-triggered extension is a separate record billed at the hourly rate |

---

## 3. Enums

```
ResourceType:
  CoworkingSeats          → An individual labeled coworking seat (Seat 01, Seat 02, etc.)
  ExecutiveRoom           → Executive Meeting Room (max 3 pax)
  CollaborationRoom       → Collaboration Meeting Room (max 20 pax)

PackageType:
  Hourly                  → Per-hour rate (billed on actual hours used)
  DayPass                 → Fixed 10-hour block
  WeeklyPass              → Fixed 5-day block
  MonthlyDedicatedDesk    → Fixed monthly rate
  HalfDay                 → Fixed half-day block (Executive: 5hrs | Collaboration: 4hrs)
  FullDay                 → Fixed full-day block (8hrs, both rooms)

BookingType:
  Online                  → Self-service guest booking with mandatory downpayment
  WalkIn                  → Handled entirely by FrontDesk in real time

BookingStatus:
  Pending                       → Online booking created, awaiting downpayment
  PendingStudentVerification    → Downpayment received but student ID not yet verified by FrontDesk
  Confirmed                     → Fully confirmed (downpayment paid + student verified if applicable)
  CheckedIn                     → Session active — ActualStart is set
  CheckedOut                    → Session ended — ActualEnd is set, payment finalized
  Cancelled                     → Cancelled before check-in
  NoShow                        → Auto or manually marked; downpayment forfeited
  Overstayed                    → Guest exceeded scheduled time; OverstayAmount applied

PaymentStatus:
  Unpaid                  → No payment received
  DownpaymentPaid         → 50% received, balance outstanding
  PartiallyPaid           → Balance partially settled
  FullyPaid               → Entire amount settled

PaymentMethod:
  Cash
  Card
  GCash                   → Manual upload, requires FrontDesk validation
  Maya                    → Manual upload, requires FrontDesk validation
  BankTransfer            → Manual upload, requires FrontDesk validation
  Paymongo                → Automated via payment gateway

PaymentValidationStatus:
  NotRequired             → Cash, Card, or Paymongo (no proof needed)
  PendingValidation       → Manual payment (GCash/Maya/Bank) — screenshot uploaded, awaiting FrontDesk
  Validated               → FrontDesk confirmed the payment proof
  Rejected                → FrontDesk rejected the proof (invalid screenshot, wrong amount)

PaymentType:
  Downpayment             → 50% advance for online bookings
  Balance                 → Remaining amount after downpayment
  OverstayFee             → Auto-billed for time beyond scheduled end
  ExtensionFee            → Billed per extension at the hourly rate
  Refund                  → Partial or full refund
```

---

## 4. Pricing Snapshot Strategy (Historical Accuracy)

**The Problem:** If an admin raises the Executive Room rate from ₱800/hr to ₱1,000/hr next year, old receipts must still show ₱800/hr. A live FK join on `service_packages.price_amount` at query time breaks historical accuracy.

**The Solution — Price Denormalization at Booking Creation:**

When a booking is created, the command handler immediately freezes:

| Source Field | Copied To |
|---|---|
| `ServicePackage.Name` | `Booking.SnapshotPackageName` |
| `ServicePackage.PriceAmount` | `Booking.SnapshotPricePerUnit` |
| Resolved hourly rate for this resource | `Booking.SnapshotHourlyRate` |

The `ServicePackageId` FK stays on the booking for auditing and reporting, but **every financial computation — including overstay and extension billing — uses snapshot fields only, never the live package.** The same snapshot pattern applies to `BookingExtension.SnapshotHourlyRate`.

This is the standard "price snapshot" or "order line item" pattern used in all production e-commerce systems.

---

## 5. Availability Algorithm

Because seats are now **individually labeled resources**, availability works the same way for all resource types: the slot is unavailable if any non-terminated booking overlaps the requested window.

### All Resources (Seats and Meeting Rooms — Unified Query)

```sql
SELECT COUNT(*) FROM bookings
WHERE resource_id = @ResourceId
  AND booking_status NOT IN ('Cancelled', 'NoShow', 'CheckedOut')
  AND scheduled_start < @RequestedEnd
  AND scheduled_end > @RequestedStart
```

- If `COUNT > 0` → **That specific resource is unavailable for the requested window**
- For the **online booking page**: run this query for every one of the 20 seats and return the list of available seat IDs
- For **walk-in**: run with `RequestedStart = now`, `RequestedEnd = now + EstimatedHours`

**Race condition protection:** `CreateGuestBookingCommand` must re-run the availability check inside a database transaction before inserting, preventing two simultaneous bookings from claiming the same slot.

### Extension Availability Check

Before executing `ExtendSessionCommand`, run the same query with:
- `ResourceId` = current booking's resource
- `RequestedStart` = current `ScheduledEnd`
- `RequestedEnd` = current `ScheduledEnd` + requested extension hours
- Exclude the current booking's own ID from the check

---

## 6. Student Verification Flow

### Online Booking

```
Guest declares IsStudent = true during booking form
        │
        ▼
CreateGuestBookingCommand
  → Student discount is applied tentatively to DiscountAmount
  → BookingStatus = Pending (awaiting downpayment)
  → Guest uploads StudentIdImageUrl to Azure Blob Storage
        │
        ▼
ConfirmDownpaymentCommand
  → Payment recorded
  → BookingStatus = PendingStudentVerification  ← Held until FrontDesk confirms ID
        │
        ▼
VerifyStudentIdCommand (FrontDesk)
  → FrontDesk reviews StudentIdImageUrl
  → If approved:
       IsStudentVerified = true
       BookingStatus = Confirmed
  → If rejected:
       IsStudentVerified = false
       DiscountAmount = 0  (recalculate TotalAmount, adjust BalanceAmount)
       BookingStatus = Confirmed  (booking proceeds but at full rate)
       → Notify guest by email of discount rejection
```

### Walk-In Booking

```
FrontDesk: guest presents physical student ID
        │
        ▼
ProcessWalkInTimeInCommand
  → FrontDesk sets IsStudent = true
  → IsStudentVerified = true  (verified in person)
  → Student rate applied immediately
```

**Fields affected:** `booking_guests.StudentIdImageUrl`, `booking_guests.IsStudentVerified`, `bookings.IsStudent`, `bookings.DiscountAmount`

---

## 7. Payment Handling

### Payment Method Matrix

| Method | Proof Required | Who Validates | Auto-Confirmed? |
|---|---|---|---|
| Cash | No | — | Yes (FrontDesk enters it) |
| Card | No | — | Yes (FrontDesk enters it) |
| GCash | Screenshot upload | FrontDesk | No — `PendingValidation` |
| Maya | Screenshot upload | FrontDesk | No — `PendingValidation` |
| BankTransfer | Screenshot upload | FrontDesk | No — `PendingValidation` |
| Paymongo | None | Automated webhook | Yes — webhook fires `ConfirmDownpaymentCommand` |

### Manual Payment Validation Flow (GCash / Maya / Bank Transfer)

```
Guest uploads payment screenshot during online booking
        │
        ▼
SubmitManualPaymentProofCommand
  → ProofOfPaymentUrl saved to Azure Blob Storage
  → PaymentValidationStatus = PendingValidation
  → FrontDesk dashboard shows alert: "Payment proof awaiting review"
        │
        ▼
ValidateManualPaymentCommand (FrontDesk)
  → If Validated:
       PaymentValidationStatus = Validated
       ValidatedByUserId = frontDeskUserId
       ValidatedAt = DateTime.UtcNow
       → Trigger same logic as ConfirmDownpaymentCommand
  → If Rejected:
       PaymentValidationStatus = Rejected
       → Notify guest by email to resubmit or choose another method
```

### Paymongo Webhook Flow

```
Paymongo fires webhook to POST /api/coworking/payments/webhook
        │
        ▼
PaymongoWebhookHandler
  → Verify webhook signature
  → Extract bookingId from metadata
  → Call ConfirmDownpaymentCommand internally
  → BookingStatus updated automatically
```

---

## 8. No-Show Detection & Background Jobs

### Mechanism

A background worker (Hangfire or Quartz.NET) runs on a regular interval and queries for confirmed bookings where `ScheduledStart` has passed.

### Grace Period Reminder (T + 10 minutes)

```
Background job detects: BookingStatus = Confirmed AND ScheduledStart < (now - 10 min)
        │
        ▼
→ Send reminder email to guest (are you on your way?)
→ Trigger real-time alert on FrontDesk dashboard: "[Guest Name] hasn't checked in yet"
→ No status change yet — FrontDesk may manually time them in
```

### Auto-Forfeit (T + 30 or 60 minutes — configurable)

```
Background job detects: BookingStatus = Confirmed AND ScheduledStart < (now - AutoForfeitMinutes)
        │
        ▼
AutoMarkNoShowCommand
  → BookingStatus   = NoShow
  → PaymentStatus   = FullyPaid  (downpayment is kept, no balance owed)
  → Seat is freed (booking is excluded from future availability queries)
  → Notify guest by email: downpayment forfeited per no-show policy
```

### Manual Override

FrontDesk can always call `MarkNoShowCommand` manually at any point before check-in, regardless of the automated schedule.

### Configuration

`AutoForfeitMinutes` will be stored as a system-level configuration setting (not hardcoded), allowing the business to change the threshold without a code deployment.

---

## 9. Session Extension Rules

### Workflow (15-Minute Reminder Loop)

```
Background job: ScheduledEnd is within 15 minutes
        │
        ▼
→ Alert FrontDesk dashboard: "[Guest Name] on Seat 07 ending in 15 min"
→ (Optional) Notify guest

FrontDesk asks guest: "Do you want to extend?"
        │
        ├── No → Proceed to Time Out as normal
        │
        └── Yes
              │
              ▼
        ExtendSessionCommand
          → CheckAvailabilityQuery for the resource (ScheduledEnd to ScheduledEnd + AddedHours)
          → If NOT available:
               Return conflict error — cannot extend, seat is booked
          → If available:
               Create BookingExtension record:
                 AddedHours          = requested hours
                 SnapshotHourlyRate  = Booking.SnapshotHourlyRate (frozen from original booking)
                 Amount              = AddedHours × SnapshotHourlyRate
               Push Booking.ScheduledEnd forward by AddedHours
               → FrontDesk collects extension payment (ProcessPaymentCommand, PaymentType = ExtensionFee)
```

### Invoice Breakdown (Example)

```
Base Package:  Day Pass (10hrs)     ₱600.00
Extension 1:   +2hrs @ ₱50/hr      ₱100.00
Extension 2:   +1hr  @ ₱50/hr      ₱50.00
─────────────────────────────────────────────
Total:                               ₱750.00
Downpayment paid:                   -₱300.00
Balance due at checkout:             ₱450.00
```

---

## 10. Domain Entities & Value Objects

### Aggregate Roots

- **`Booking`** — Central aggregate. All state transitions (`CheckIn`, `CheckOut`, `Cancel`, `MarkNoShow`, `ApplyOverstay`) are domain methods on this entity. No handler should mutate booking state directly.
- **`ServicePackage`** — Admin-managed pricing. Effectively immutable from a booking's perspective once the snapshot is taken.
- **`CoworkingResource`** — Each physical seat or room. Admin-managed via CRUD.

### Entities (Children of Booking)

- **`BookingGuest`** — Captures anonymous guest identity and student verification state. Exists only in the context of a booking.
- **`BookingPayment`** — Append-only payment record. Never mutated after creation. Validation status is updated by FrontDesk.
- **`BookingExtension`** — One record per extension event. Billed separately at the hourly snapshot rate.

### Value Objects (Computed, Not Persisted)

- **`BookingDuration`** — Wraps `(ActualEnd - ActualStart)`, exposes `TotalHours` and `OvertimeHours`.
- **`PricingCalculation`** — Encapsulates the full pricing logic: base amount, student discount, downpayment split, overstay charge, extension fees. Produced by a domain service and written to snapshot fields on the `Booking`.

---

## 11. CQRS Commands & Queries

### Admin — Service Package Management (`api/coworking/admin`)

| Command / Query | Description |
|---|---|
| `CreateServicePackageCommand` | Add a new pricing tier |
| `UpdateServicePackageCommand` | Edit a package (does not affect historical bookings — snapshots are frozen) |
| `ToggleServicePackageCommand` | Activate / deactivate a package |
| `GetAllServicePackagesQuery` | List all packages, filterable by `ResourceType` |
| `GetServicePackageByIdQuery` | Single package detail |

### Admin — Resource (Seat/Room) Management (`api/coworking/admin`)

| Command / Query | Description |
|---|---|
| `CreateCoworkingResourceCommand` | Add a new seat or room (e.g. "Seat 21" when expanding) |
| `UpdateCoworkingResourceCommand` | Rename or reconfigure a resource |
| `ToggleCoworkingResourceCommand` | Disable a seat for maintenance or re-enable it |
| `GetAllCoworkingResourcesQuery` | List all resources with current status |
| `GetCoworkingResourceByIdQuery` | Single resource detail |

### Online Guest Booking (`api/coworking`)

| Command / Query | Description |
|---|---|
| `CheckAvailabilityQuery` | Returns list of available resources for a given date/time window |
| `CreateGuestBookingCommand` | Creates booking (`Pending`), captures guest info, freezes price snapshot, tentatively applies student discount |
| `SubmitManualPaymentProofCommand` | Guest uploads GCash/Maya/Bank screenshot to Azure Blob. Sets `PaymentValidationStatus = PendingValidation` |
| `ConfirmDownpaymentCommand` | Called by Paymongo webhook or after FrontDesk validates manual proof. Sets `Confirmed` or `PendingStudentVerification` |
| `CancelBookingCommand` | Cancels a booking before check-in |

### Online Guest — Student Verification (`api/coworking`)

| Command / Query | Description |
|---|---|
| `VerifyStudentIdCommand` | FrontDesk approves or rejects uploaded student ID. Adjusts discount and sets final `Confirmed` status |

### Walk-In Flow (`api/coworking/frontdesk`)

| Command / Query | Description |
|---|---|
| `CheckWalkInAvailabilityQuery` | Real-time check for a specific resource right now |
| `ProcessWalkInTimeInCommand` | Creates booking with `CheckedIn`, sets `ActualStart = now`, freezes snapshot |
| `ProcessWalkInTimeOutCommand` | Sets `ActualEnd = now`, computes hours, calculates overstay, transitions to `CheckedOut` |
| `ExtendSessionCommand` | Checks availability, creates `BookingExtension`, pushes `ScheduledEnd` forward |
| `ProcessPaymentCommand` | Records any payment (downpayment, balance, overstay, extension). Updates `PaymentStatus` |
| `ValidateManualPaymentCommand` | FrontDesk validates an uploaded payment proof. Approved → triggers downpayment confirmation |
| `MarkNoShowCommand` | Manual FrontDesk override to mark a no-show at any time before check-in |

### FrontDesk Dashboard Queries (`api/coworking/frontdesk`)

| Query | Description |
|---|---|
| `GetActiveSessionsQuery` | All currently `CheckedIn` bookings (live floor view) |
| `GetBookingsByDateQuery` | Full schedule for a given date across all resources |
| `GetBookingByIdQuery` | Single booking detail including payment history and extensions |
| `GetUpcomingBookingsQuery` | All `Confirmed` bookings not yet checked in |
| `GetPendingPaymentValidationsQuery` | All payments in `PendingValidation` state awaiting FrontDesk review |
| `GetPendingStudentVerificationsQuery` | All bookings in `PendingStudentVerification` state |

### Background Job Commands (Internal — Not HTTP Endpoints)

| Command | Description |
|---|---|
| `SendNoShowReminderCommand` | Fired at T+10 min — emails guest and alerts FrontDesk UI |
| `AutoMarkNoShowCommand` | Fired at T+AutoForfeitMinutes — marks booking as `NoShow`, frees seat |
| `SendSessionEndingReminderCommand` | Fired 15 min before `ScheduledEnd` — alerts FrontDesk to ask guest about extension |

---

## 12. Controller Structure

| Controller | Base Route | Auth | Responsibility |
|---|---|---|---|
| `CoworkingController` | `api/coworking` | Public | Availability check, online guest booking, manual payment proof upload, Paymongo webhook |
| `CoworkingFrontDeskController` | `api/coworking/frontdesk` | Admin, Frontdesk | Walk-in time-in/out, extensions, payment processing, manual payment validation, student ID verification, no-show marking, dashboard queries |
| `CoworkingAdminController` | `api/coworking/admin` | Admin only | Service package CRUD, seat/room resource CRUD |

---

## 13. Walk-In Flow Step-by-Step

```
FrontDesk: enters guest name + selects specific seat/room + estimated hours
        │
        ▼
CheckWalkInAvailabilityQuery
  → If unavailable → return 409 with next available time for that resource
        │
        ▼
ProcessWalkInTimeInCommand
  → Create BookingGuest (FirstName, LastName, Phone)
  → If guest shows student ID → set IsStudentVerified = true immediately
  → Create Booking:
       ReferenceNumber = generate (e.g. BKG-1042)
       BookingType    = WalkIn
       BookingStatus  = CheckedIn
       PaymentStatus  = Unpaid
       ActualStart    = DateTime.UtcNow
       ScheduledStart = DateTime.UtcNow
       ScheduledEnd   = DateTime.UtcNow + EstimatedHours
  → Freeze price snapshot from ServicePackage
        │
        ▼
[Guest uses the space...]
        │
        ▼ ← Background job fires 15 min before ScheduledEnd
[FrontDesk alerted: session ending soon]
        │
        ├── Guest wants to extend?
        │     └── Yes → ExtendSessionCommand (see Section 9)
        │     └── No → proceed to Time Out
        │
        ▼
ProcessWalkInTimeOutCommand (FrontDesk triggers)
  → ActualEnd = DateTime.UtcNow
  → ActualHours = (ActualEnd - ActualStart).TotalHours
  → If ActualEnd > ScheduledEnd:
       OverstayHours  = ActualHours - ScheduledHours
       OverstayAmount = OverstayHours × SnapshotHourlyRate
       BookingStatus  = Overstayed
  → Else:
       BookingStatus  = CheckedOut
  → TotalAmount = BaseAmount + ExtensionAmounts + OverstayAmount - DiscountAmount
  → BalanceAmount = TotalAmount (walk-ins have no downpayment)
        │
        ▼
ProcessPaymentCommand (FrontDesk selects Cash / Card / GCash / Maya)
  → If GCash/Maya → manual, PaymentValidationStatus = PendingValidation (edge case for walk-ins)
  → If Cash/Card → PaymentValidationStatus = NotRequired, confirmed immediately
  → Create BookingPayment (PaymentType = Balance)
  → PaymentStatus = FullyPaid
```

---

## 14. Online Booking Flow Step-by-Step

```
Guest: visits booking page (no account required)
        │
        ▼
CheckAvailabilityQuery (date + time window)
  → Returns list of available seats / rooms with their names
        │
        ▼
Guest: selects resource, package, date/time, enters name/email/phone
       Declares IsStudent = true/false
       If IsStudent → upload StudentIdImageUrl
        │
        ▼
CreateGuestBookingCommand
  → Re-check availability inside transaction (prevents race condition)
  → Create BookingGuest with StudentIdImageUrl (if student)
  → Create Booking:
       ReferenceNumber = generate (e.g. BKG-1042)
       BookingType     = Online
       BookingStatus   = Pending
       PaymentStatus   = Unpaid
       IsStudent       = true/false
  → Freeze price snapshot
  → Compute:
       BaseAmount        = SnapshotPricePerUnit (flat) or (hours × SnapshotPricePerUnit)
       DiscountAmount    = IsStudent && IsStudentEligible ? Base × DiscountPercent : 0
       TotalAmount       = BaseAmount - DiscountAmount
       DownpaymentAmount = TotalAmount × DownpaymentPercent (50%)
       BalanceAmount     = TotalAmount - DownpaymentAmount
  → Return BookingId + DownpaymentAmount → frontend navigates to payment step
        │
        ▼
Guest selects payment method:
  ├── Paymongo (card/e-wallet):
  │     → Frontend initiates Paymongo checkout with DownpaymentAmount
  │     → Paymongo fires webhook → POST api/coworking/payments/webhook
  │     → ConfirmDownpaymentCommand triggered automatically
  │
  └── GCash / Maya / Bank Transfer (manual):
        → Guest uploads screenshot → SubmitManualPaymentProofCommand
        → PaymentValidationStatus = PendingValidation
        → FrontDesk is alerted on dashboard
        → ValidateManualPaymentCommand (FrontDesk)
             → If Validated → ConfirmDownpaymentCommand triggered
             → If Rejected → guest notified to resubmit
        │
        ▼
ConfirmDownpaymentCommand
  → Create BookingPayment (PaymentType = Downpayment)
  → DownpaymentPaidAt = DateTime.UtcNow
  → If IsStudent && NOT IsStudentVerified:
       BookingStatus = PendingStudentVerification
       → Alert FrontDesk: student ID needs review
  → Else:
       BookingStatus = Confirmed
  → Send confirmation email to guest (see Section 16)
        │
        ▼
[Background job monitors for no-show — see Section 8]
        │
        ▼
[Day of booking — Guest arrives at front desk]
        │
        ▼
ProcessWalkInTimeInCommand (used for both walk-ins AND online check-ins)
  → BookingStatus = CheckedIn
  → ActualStart   = DateTime.UtcNow
        │
        ▼
[Same Time Out + Balance Payment flow as Walk-In]
  → Balance collected at checkout
  → PaymentStatus = FullyPaid
```

---

## 15. Pricing Calculation Logic

All rates are read from the price snapshot on the `Booking`, not from the live `ServicePackage`.

### Coworking Seats

```
Hourly — Regular:
  BaseAmount     = ActualHours × ₱50
  DiscountAmount = 0
  TotalAmount    = BaseAmount

Hourly — Student:
  BaseAmount     = ActualHours × ₱40
  DiscountAmount = 0   ← Student rate is the base price, not a discount applied on top
  TotalAmount    = BaseAmount

Day Pass (₱600 / 10hrs cap):
  BaseAmount     = ₱600
  DiscountAmount = IsStudent && IsStudentVerified ? ₱600 × 10% = ₱60 : 0
  TotalAmount    = BaseAmount - DiscountAmount   → ₱540 (student) or ₱600 (regular)

Weekly Pass (₱2,700 / 5 days):
  BaseAmount     = ₱2,700
  DiscountAmount = IsStudent && IsStudentVerified ? ₱2,700 × 10% = ₱270 : 0
  TotalAmount    = ₱2,700 - DiscountAmount

Monthly Dedicated Desk (₱8,500/month):
  BaseAmount     = ₱8,500
  DiscountAmount = IsStudent && IsStudentVerified ? ₱8,500 × 10% = ₱850 : 0
  TotalAmount    = ₱8,500 - DiscountAmount
```

> Student discount is only applied after `IsStudentVerified = true`. If a student's ID is rejected, `DiscountAmount` is zeroed and `BalanceAmount` is recalculated.

### Meeting Rooms

```
Executive Room (Max 3 Pax) — No student discount:
  Hourly:    BaseAmount = ActualHours × ₱800
  Half-Day:  BaseAmount = ₱3,000 flat (5hrs cap — overstay billed at ₱800/hr beyond 5hrs)
  Full Day:  BaseAmount = ₱5,500 flat (8hrs cap — overstay billed at ₱800/hr beyond 8hrs)

Collaboration Room (Max 20 Pax) — No student discount:
  Hourly:    BaseAmount = ActualHours × ₱1,500
  Half-Day:  BaseAmount = ₱5,500 flat (4hrs cap — overstay billed at ₱1,500/hr beyond 4hrs)
  Full Day:  BaseAmount = ₱9,500 flat (8hrs cap — overstay billed at ₱1,500/hr beyond 8hrs)
```

### Downpayment Split (Online Only)

```
DownpaymentAmount = TotalAmount × DownpaymentPercent (default 50%, configurable per package)
BalanceAmount     = TotalAmount - DownpaymentAmount
```

### Overstay

```
OverstayHours  = max(0, ActualHours - ScheduledDurationHours)
OverstayAmount = OverstayHours × SnapshotHourlyRate
```

### Extension

```
Per extension:
  Amount = AddedHours × BookingExtension.SnapshotHourlyRate

Final TotalAmount = BaseAmount - DiscountAmount + OverstayAmount + SUM(Extension.Amount)
```

---

## 16. Confirmation Email Content

Sent automatically after `ConfirmDownpaymentCommand` succeeds (online bookings only). Uses the existing email service already integrated in the backend.

```
Subject: Your Booking is Confirmed — CommunSide (#BKG-1042)

─────────────────────────────────────────────────
  Booking Reference:   #BKG-1042
  Guest Name:          Juan Dela Cruz
  Resource:            Coworking - Seat 07
  Date:                October 24, 2026
  Time:                10:00 AM – 2:00 PM
─────────────────────────────────────────────────
  PAYMENT SUMMARY
  Package:             Day Pass (10hrs)
  Total Fee:           ₱540.00  (student rate applied)
  Downpayment Paid:    ₱270.00  ✓
  Balance Due:         ₱270.00  (payable at check-in)
─────────────────────────────────────────────────
  CHECK-IN INSTRUCTIONS
  • Please arrive on time. A 10-minute grace period applies.
  • Bring a valid Student ID for verification upon arrival.
  • No-show policy: downpayment is non-refundable if you fail
    to check in within 30 minutes of your booking start time.
─────────────────────────────────────────────────
  CONTACT US
  📞 +63 9XX XXX XXXX
  ✉  hello@communside.com
─────────────────────────────────────────────────
```

---

## 17. Deferred Features (Post-MVP)

These features are intentionally excluded from the initial implementation. The schema and command structure are designed to accommodate them without breaking changes.

| Feature | Why Deferred | What's Prepared |
|---|---|---|
| **VO Credits** | Virtual Office module not yet built | `CalculateFeeQuery` will accept an injectable discount step where credit deduction can be inserted |
| **Priority Booking for Premium VO** | Depends on VO module | Availability query is already parameterized; a "reserve window" flag can be added to `coworking_resources` |
| **Paymongo Full Integration** | May use manual reference for initial launch | `booking_payments` already has `PaymentMethod = Paymongo` and webhook endpoint is planned in the controller structure |

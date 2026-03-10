# Tutorial: Walk-In Booking Flow (Phases 3 → 4A → 4B → 5)

This tutorial walks through a complete Tuesday morning at CommunSide Coworking Space. You will check availability, check guests in, handle time-outs, collect payments (cash and image proof), validate uploaded proofs, and verify every state transition along the way.

**Scenario:** Three guests arrive at the front desk. Juan books a Day Pass with cash. Maria is a student on an hourly session and pays via GCash screenshot. Carlos arrives later, overstays his session, and pays the overstay fee in cash. A bonus section covers the corrected time-out flow for when FrontDesk forgot to process a checkout in real time.

---

## Before You Start

### 1 — Log In as FrontDesk (or Admin)

All FrontDesk endpoints require a valid JWT. Log in first.

**`POST /api/auth/login`**

```json
{
  "email": "frontdesk@virtualoffice.com",
  "password": "Frontdesk@123456"
}
```

**Response `200 OK`:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "expiration": "2026-03-11T10:00:00Z"
}
```

Add this header to every authenticated request in this tutorial:
```
Authorization: Bearer <accessToken>
```

> **No token needed** for the two availability endpoints — they are fully public.

---

### 2 — Get Resource and Package IDs

You need the IDs of specific resources and packages before checking guests in. Use the admin catalog endpoints (or the public ones added by your colleague).

**`GET /api/public/resources`** *(no auth)*

Pick two coworking seats to use for this tutorial. Note their `id` values:
- `SEAT_07_ID` — e.g., `"3fa85f64-5717-4562-b3fc-2c963f66afa6"`
- `SEAT_12_ID` — e.g., `"4fb96a75-6828-5673-c4gd-3d074g77bgb7"`

**`GET /api/public/packages`** *(no auth)*

Note the IDs for:
- `DAY_PASS_ID` — the `"Day Pass"` package (`packageType: "day_pass"`, ₱600.00)
- `HOURLY_REGULAR_ID` — the `"Hourly — Regular"` package (`packageType: "hourly"`, ₱50.00/hr)

> These IDs are fixed after seeding and never change unless an admin deletes the row.

---

## Phase 3 — Check Availability

Before guests arrive, FrontDesk checks what is available for the morning.

### Step 1 — Check Availability for a Time Window

**`GET /api/coworking/availability`** *(no auth)*

```
GET /api/coworking/availability?date=2026-03-11&startTime=09:00&endTime=18:00&resourceType=coworking_seats
```

**Response `200 OK`:**
```json
[
  { "resourceId": "...", "resourceName": "Seat 01", "resourceType": "coworking_seats", "capacity": 1, "isAvailable": true },
  { "resourceId": "...", "resourceName": "Seat 02", "resourceType": "coworking_seats", "capacity": 1, "isAvailable": true },
  ...
  { "resourceId": "<SEAT_07_ID>", "resourceName": "Seat 07", "resourceType": "coworking_seats", "capacity": 1, "isAvailable": true },
  ...
  { "resourceId": "<SEAT_12_ID>", "resourceName": "Seat 12", "resourceType": "coworking_seats", "capacity": 1, "isAvailable": true },
  ...
]
```

> All 20 seats return `"isAvailable": true` because no bookings exist yet. The response always includes all matching resources so the frontend can render a complete grid — both available and unavailable slots.

> **DB:** Runs the availability overlap query (`scheduled_start < requestedEnd AND scheduled_end > requestedStart`) against `bookings` for each resource. With zero rows in `bookings`, every check returns `false` for `isUnavailable`.

---

### Step 2 — Check a Specific Seat Right Now (Walk-In Check)

Before assigning Seat 07 to Juan, FrontDesk verifies it is free for the next 8 hours.

**`GET /api/coworking/availability/{resourceId}`** *(no auth)*

```
GET /api/coworking/availability/<SEAT_07_ID>?estimatedHours=8
```

**Response `200 OK`:**
```json
{
  "resourceId": "<SEAT_07_ID>",
  "resourceName": "Seat 07",
  "isAvailable": true,
  "nextAvailableAt": null
}
```

> `"nextAvailableAt": null` because the seat is free. When a seat is occupied, this field returns the `scheduledEnd` of its earliest active booking — so FrontDesk knows exactly when it opens up next.

---

## Phase 4A — Walk-In Time-In

### Step 3 — Check In Juan Dela Cruz (Regular, Day Pass)

Juan arrives at 9:00 AM and wants a Day Pass on Seat 07.

**`POST /api/coworking/frontdesk/time-in`**
```
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "resourceId": "<SEAT_07_ID>",
  "servicePackageId": "<DAY_PASS_ID>",
  "estimatedHours": 8,
  "guestFirstName": "Juan",
  "guestLastName": "Dela Cruz",
  "guestPhone": "09171234567",
  "guestEmail": "juan.dc@email.com",
  "isStudent": false
}
```

**Response `200 OK`:**
```json
{
  "bookingId": "<JUAN_BOOKING_ID>",
  "referenceNumber": "BKG-0001",
  "guestName": "Juan Dela Cruz",
  "resourceName": "Seat 07",
  "scheduledStart": "2026-03-11T01:00:00Z",
  "scheduledEnd": "2026-03-11T09:00:00Z",
  "snapshotPackageName": "Day Pass",
  "isStudent": false,
  "baseAmount": 600.00,
  "discountAmount": 0.00,
  "totalAmount": 600.00
}
```

> **Note on times:** All `DateTime` values are stored and returned in UTC. If your local timezone is UTC+8, `01:00 UTC` = `09:00 PHT`.

> **DB:** Inserts one row in `booking_guests` and one row in `bookings`. `booking_status = 2` (`checked_in`), `payment_status = 0` (`unpaid`). `snapshot_package_name = "Day Pass"`, `snapshot_price_per_unit = 600.00`, `base_amount = 600.00`, `total_amount = 600.00`. `reference_number = "BKG-0001"` (from `nextval('booking_ref_seq')`).

**Save `<JUAN_BOOKING_ID>` — you will need it throughout this tutorial.**

---

### Step 4 — Verify Seat 07 Is Now Unavailable

Immediately re-run the walk-in availability check on Seat 07.

**`GET /api/coworking/availability/<SEAT_07_ID>?estimatedHours=2`** *(no auth)*

**Response `200 OK`:**
```json
{
  "resourceId": "<SEAT_07_ID>",
  "resourceName": "Seat 07",
  "isAvailable": false,
  "nextAvailableAt": "2026-03-11T09:00:00Z"
}
```

> `"isAvailable": false` — Juan's booking overlaps with the requested 2-hour window. `"nextAvailableAt"` is Juan's `scheduledEnd`. This is the availability algorithm working correctly — the same check that prevented double-booking during time-in.

---

### Step 5 — Check In Maria Santos (Student, Hourly)

Maria arrives and shows her student ID to the FrontDesk. She wants Seat 12 for 3 hours.

**`POST /api/coworking/frontdesk/time-in`**

```json
{
  "resourceId": "<SEAT_12_ID>",
  "servicePackageId": "<HOURLY_REGULAR_ID>",
  "estimatedHours": 3,
  "guestFirstName": "Maria",
  "guestLastName": "Santos",
  "guestPhone": "09189876543",
  "guestEmail": null,
  "isStudent": true
}
```

**Response `200 OK`:**
```json
{
  "bookingId": "<MARIA_BOOKING_ID>",
  "referenceNumber": "BKG-0002",
  "guestName": "Maria Santos",
  "resourceName": "Seat 12",
  "scheduledStart": "2026-03-11T01:15:00Z",
  "scheduledEnd": "2026-03-11T04:15:00Z",
  "snapshotPackageName": "Hourly — Regular",
  "isStudent": true,
  "baseAmount": 150.00,
  "discountAmount": 30.00,
  "totalAmount": 120.00
}
```

> `baseAmount = 3 × ₱50 = ₱150`. `discountAmount = ₱30` (20% student discount, if configured on the package). `totalAmount = ₱120`.

> `guestEmail` was not collected — that's fine, it's optional. `isStudentVerified` is set to `true` immediately because the FrontDesk physically checked the ID at the counter.

> **DB:** `reference_number = "BKG-0002"` (sequence incremented). `is_student = true`, `discount_amount = 30.00`, `total_amount = 120.00`.

**Save `<MARIA_BOOKING_ID>`.**

---

## Phase 4B — Active Sessions, Time-Out, and Payment

### Step 6 — View All Active Sessions

FrontDesk opens the dashboard to see who is currently using the space.

**`GET /api/coworking/frontdesk/sessions/active`**
```
Authorization: Bearer <token>
```

**Response `200 OK`:**
```json
[
  {
    "bookingId": "<JUAN_BOOKING_ID>",
    "referenceNumber": "BKG-0001",
    "guestName": "Juan Dela Cruz",
    "resourceName": "Seat 07",
    "bookingStatus": "checked_in",
    "paymentStatus": "unpaid",
    "scheduledStart": "2026-03-11T01:00:00Z",
    "scheduledEnd": "2026-03-11T09:00:00Z",
    "actualStart": "2026-03-11T01:00:00Z",
    "snapshotPackageName": "Day Pass",
    "totalAmount": 600.00,
    "balanceAmount": 600.00
  },
  {
    "bookingId": "<MARIA_BOOKING_ID>",
    "referenceNumber": "BKG-0002",
    "guestName": "Maria Santos",
    "resourceName": "Seat 12",
    "bookingStatus": "checked_in",
    "paymentStatus": "unpaid",
    "scheduledStart": "2026-03-11T01:15:00Z",
    "scheduledEnd": "2026-03-11T04:15:00Z",
    "actualStart": "2026-03-11T01:15:00Z",
    "snapshotPackageName": "Hourly — Regular",
    "totalAmount": 120.00,
    "balanceAmount": 120.00
  }
]
```

> Results are ordered by `actualStart` ascending — earliest arrival shown first.

---

### Step 7 — Maria Checks Out (On Time, Cash Payment)

Maria finishes at 12:15 PM — exactly on time. FrontDesk processes her time-out.

**`POST /api/coworking/frontdesk/time-out/<MARIA_BOOKING_ID>`**
```
Authorization: Bearer <token>
```

> The request body is **optional**. Omit it entirely (or send an empty `{}`) to use the current server time (`DateTime.UtcNow`) as the checkout time. See the [Corrected Time-Out Scenario](#corrected-time-out-scenario--frontdesk-forgot) below for when you need to pass a body.

**Response `200 OK`:**
```json
{
  "bookingId": "<MARIA_BOOKING_ID>",
  "referenceNumber": "BKG-0002",
  "bookingType": "walk_in",
  "bookingStatus": "checked_out",
  "paymentStatus": "unpaid",
  "resourceName": "Seat 12",
  "guestName": "Maria Santos",
  "scheduledStart": "2026-03-11T01:15:00Z",
  "scheduledEnd": "2026-03-11T04:15:00Z",
  "actualStart": "2026-03-11T01:15:00Z",
  "actualEnd": "2026-03-11T04:15:00Z",
  "snapshotPackageName": "Hourly — Regular",
  "baseAmount": 150.00,
  "discountAmount": 30.00,
  "overstayAmount": 0.00,
  "totalAmount": 120.00,
  "balanceAmount": 120.00,
  "totalPaid": 0.00,
  "outstandingBalance": 120.00,
  ...
}
```

> `overstayAmount = 0.00` — she left exactly on time. `bookingStatus = "checked_out"`.

> **DB:** `actual_end` is set, `booking_status` transitions from `checked_in` → `checked_out`.

Now collect her cash payment:

**`POST /api/coworking/frontdesk/payment`**
```json
{
  "bookingId": "<MARIA_BOOKING_ID>",
  "amount": 120.00,
  "paymentMethod": "cash",
  "paymentType": "balance"
}
```

**Response `200 OK`** *(no body — returns `{}` or 200 status)*

Verify her booking is now fully paid:

**`GET /api/coworking/frontdesk/bookings/<MARIA_BOOKING_ID>`**

```json
{
  "bookingId": "<MARIA_BOOKING_ID>",
  "bookingStatus": "checked_out",
  "paymentStatus": "fully_paid",
  "totalPaid": 120.00,
  "outstandingBalance": 0.00,
  "payments": [
    {
      "paymentId": "...",
      "amount": 120.00,
      "paymentMethod": "cash",
      "paymentType": "balance",
      "paymentValidationStatus": "not_required",
      "paidAt": "2026-03-11T04:16:00Z"
    }
  ]
}
```

> `paymentValidationStatus = "not_required"` for cash — no proof upload is needed.

> Seat 12 is now free. The availability query will return `"isAvailable": true` for Seat 12.

---

### Step 8 — View Today's Bookings by Date

FrontDesk wants to review the full day's bookings so far.

**`GET /api/coworking/frontdesk/bookings?date=2026-03-11`**

**Response `200 OK`:**
```json
[
  {
    "bookingId": "<JUAN_BOOKING_ID>",
    "referenceNumber": "BKG-0001",
    "guestName": "Juan Dela Cruz",
    "resourceName": "Seat 07",
    "bookingStatus": "checked_in",
    "paymentStatus": "unpaid",
    ...
  },
  {
    "bookingId": "<MARIA_BOOKING_ID>",
    "referenceNumber": "BKG-0002",
    "guestName": "Maria Santos",
    "resourceName": "Seat 12",
    "bookingStatus": "checked_out",
    "paymentStatus": "fully_paid",
    ...
  }
]
```

> Results are ordered by `scheduledStart` ascending and include bookings in all statuses.

---

## Phase 4B + Phase 5 — Juan Checks Out and Pays via Image

### Step 9 — Juan Checks Out (On Time)

Juan finishes at 5:00 PM, exactly at his scheduled end. FrontDesk triggers his time-out.

**`POST /api/coworking/frontdesk/time-out/<JUAN_BOOKING_ID>`**

> No request body needed — omit it or send `{}` to use the current time.

**Response `200 OK`:**
```json
{
  "bookingId": "<JUAN_BOOKING_ID>",
  "bookingStatus": "checked_out",
  "paymentStatus": "unpaid",
  "overstayAmount": 0.00,
  "totalAmount": 600.00,
  "balanceAmount": 600.00,
  "outstandingBalance": 600.00,
  ...
}
```

> `bookingStatus = "checked_out"`, `overstayAmount = 0.00`. Payment is still outstanding.

---

### Step 10 — Juan Pays via GCash (Image Proof Upload)

Juan opens his GCash app and shows a screenshot of his payment. FrontDesk takes a photo and uploads it.

**`POST /api/coworking/frontdesk/payments/submit-proof`**
```
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

Form fields:
| Field | Value |
|---|---|
| `bookingId` | `<JUAN_BOOKING_ID>` |
| `paymentType` | `balance` |
| `amount` | `600.00` |
| `guestProvidedReference` | `GC-20260311-09876` *(GCash reference number, optional)* |
| `file` | *(attach the screenshot image — JPEG or PNG, max 5 MB)* |

**Response `200 OK`** *(empty body)*

> **DB:** Inserts a row in `booking_payments` with `payment_method = 7` (`image_attachment`), `payment_validation_status = 1` (`pending_validation`), `proof_of_payment_url = "https://<blob>.windows.net/..."`, `paid_at = NULL`. The booking's `payment_status` is **not yet updated** — that happens only after FrontDesk validation.

---

### Step 11 — View the Pending Validation Queue

The payment is now sitting in the validation queue. FrontDesk opens the pending list.

**`GET /api/coworking/frontdesk/payments/pending`**

**Response `200 OK`:**
```json
[
  {
    "paymentId": "<JUAN_PAYMENT_ID>",
    "bookingId": "<JUAN_BOOKING_ID>",
    "referenceNumber": "BKG-0001",
    "guestName": "Juan Dela Cruz",
    "resourceName": "Seat 07",
    "amount": 600.00,
    "paymentMethod": "image_attachment",
    "paymentType": "balance",
    "proofOfPaymentUrl": "https://<storage>.windows.net/payment-proofs/<JUAN_BOOKING_ID>/...",
    "createdAt": "2026-03-11T09:05:00Z"
  }
]
```

> Results are ordered by `createdAt` ascending — oldest unvalidated proof is shown first.

**Save `<JUAN_PAYMENT_ID>`.**

---

### Step 12 — Validate Juan's Payment (Approve)

FrontDesk opens the proof image, confirms the amount matches ₱600, and approves it.

**`POST /api/coworking/frontdesk/payments/<JUAN_PAYMENT_ID>/validate`**
```json
{
  "isApproved": true
}
```

**Response `200 OK`** *(empty body)*

Verify Juan's booking is now fully settled:

**`GET /api/coworking/frontdesk/bookings/<JUAN_BOOKING_ID>`**

```json
{
  "bookingId": "<JUAN_BOOKING_ID>",
  "bookingStatus": "checked_out",
  "paymentStatus": "fully_paid",
  "totalPaid": 600.00,
  "outstandingBalance": 0.00,
  "payments": [
    {
      "paymentId": "<JUAN_PAYMENT_ID>",
      "amount": 600.00,
      "paymentMethod": "image_attachment",
      "paymentType": "balance",
      "paymentValidationStatus": "validated",
      "proofOfPaymentUrl": "https://...",
      "paidAt": "2026-03-11T09:06:00Z"
    }
  ]
}
```

> `paymentValidationStatus = "validated"`, `paidAt` is now set. `paymentStatus = "fully_paid"`. Seat 07 has been `checked_out` since Step 9 — this payment step had no effect on booking status since there was no overstay.

> **DB:** Updates `payment_validation_status = 2` (`validated`), sets `paid_at = now()`, `validated_by_user_id`, and `validated_at`. Recalculates booking `payment_status` → `fully_paid`.

---

## Overstay Scenario — Carlos Reyes

### Step 13 — Carlos Checks In (Hourly, 2h Estimated)

Carlos arrives at 2:00 PM and books Seat 12 for 2 hours.

**`POST /api/coworking/frontdesk/time-in`**
```json
{
  "resourceId": "<SEAT_12_ID>",
  "servicePackageId": "<HOURLY_REGULAR_ID>",
  "estimatedHours": 2,
  "guestFirstName": "Carlos",
  "guestLastName": "Reyes",
  "guestPhone": "09201112222",
  "guestEmail": null,
  "isStudent": false
}
```

**Response `200 OK`:**
```json
{
  "bookingId": "<CARLOS_BOOKING_ID>",
  "referenceNumber": "BKG-0003",
  "guestName": "Carlos Reyes",
  "resourceName": "Seat 12",
  "scheduledStart": "2026-03-11T06:00:00Z",
  "scheduledEnd": "2026-03-11T08:00:00Z",
  "snapshotPackageName": "Hourly — Regular",
  "isStudent": false,
  "baseAmount": 100.00,
  "discountAmount": 0.00,
  "totalAmount": 100.00
}
```

---

### Step 14 — Carlos Checks Out Late (Overstay)

Carlos stayed an extra 45 minutes past his scheduled end. FrontDesk triggers his time-out.

**`POST /api/coworking/frontdesk/time-out/<CARLOS_BOOKING_ID>`**

> No request body needed — the actual end time is captured as `DateTime.UtcNow` (8:45 PM PHT).

**Response `200 OK`:**
```json
{
  "bookingId": "<CARLOS_BOOKING_ID>",
  "bookingStatus": "overstayed",
  "paymentStatus": "unpaid",
  "scheduledEnd": "2026-03-11T08:00:00Z",
  "actualEnd": "2026-03-11T08:45:00Z",
  "overstayAmount": 37.50,
  "baseAmount": 100.00,
  "discountAmount": 0.00,
  "totalAmount": 137.50,
  "balanceAmount": 137.50,
  "outstandingBalance": 137.50,
  ...
}
```

> `bookingStatus = "overstayed"` — because `actualEnd > scheduledEnd`. `overstayAmount = 0.75h × ₱50/hr = ₱37.50`. `totalAmount` was recalculated to `₱137.50`.

> **DB:** `booking_status` transitions to `overstayed` (not `checked_out`). The resource is **not blocked** for new bookings because the availability algorithm excludes `overstayed` — Carlos has physically left.

---

### Step 15 — Collect Carlos's Cash Payment (Full Amount)

**`POST /api/coworking/frontdesk/payment`**
```json
{
  "bookingId": "<CARLOS_BOOKING_ID>",
  "amount": 137.50,
  "paymentMethod": "cash",
  "paymentType": "balance"
}
```

**Response `200 OK`**

Verify final state:

**`GET /api/coworking/frontdesk/bookings/<CARLOS_BOOKING_ID>`**

```json
{
  "bookingStatus": "checked_out",
  "paymentStatus": "fully_paid",
  "overstayAmount": 37.50,
  "totalAmount": 137.50,
  "totalPaid": 137.50,
  "outstandingBalance": 0.00
}
```

> `bookingStatus` automatically transitioned from `"overstayed"` → `"checked_out"` the moment `paymentStatus` became `"fully_paid"`. This is the `RecalculatePaymentStatus` helper applying the Overstayed → CheckedOut rule.

---

---

## Corrected Time-Out Scenario — FrontDesk Forgot

This scenario demonstrates what to do when FrontDesk forgot to process a guest's time-out and the guest had already left earlier. The backend supports two optional fields on the time-out body to handle this gracefully:

| Field | Type | Purpose |
|---|---|---|
| `actualEndOverride` | `DateTime?` (ISO 8601 UTC) | Override the checkout time to the real departure time. Must be in the past and after `ActualStart`. |
| `timeOutNotes` | `string?` (max 500 chars) | Free-text note explaining the correction, appended to the booking's `Notes` field. |

---

### Corrected Time-Out Example

**Situation:** A guest was checked in from 10:00 AM PHT. At 6:00 PM, FrontDesk discovers they forgot to process the time-out — the guest actually left at 2:00 PM PHT (06:00 UTC). The current time is now 10:00 AM UTC the next day.

**`POST /api/coworking/frontdesk/time-out/<BOOKING_ID>`**
```
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "actualEndOverride": "2026-03-11T06:00:00Z",
  "timeOutNotes": "Guest left at 2:00 PM PHT. Time-out processed late due to staff oversight."
}
```

**Response `200 OK`:**
```json
{
  "bookingId": "<BOOKING_ID>",
  "bookingStatus": "checked_out",
  "actualStart": "2026-03-11T02:00:00Z",
  "actualEnd": "2026-03-11T06:00:00Z",
  "overstayAmount": 0.00,
  "notes": "Guest left at 2:00 PM PHT. Time-out processed late due to staff oversight.",
  ...
}
```

> `actualEnd` is set to the overridden time (`06:00 UTC = 2:00 PM PHT`), not the current server time. `timeOutNotes` is appended to `booking.Notes` so there is a permanent audit trail of the correction.

---

### Corrected Time-Out With Overstay

If the guest actually left after their scheduled end, the override time will trigger an overstay calculation just like a normal late time-out.

**Situation:** Guest was scheduled for 2 hours (10:00–12:00 PM PHT). They actually left at 1:00 PM PHT (05:00 UTC), but time-out was forgotten.

```json
{
  "actualEndOverride": "2026-03-11T05:00:00Z",
  "timeOutNotes": "Guest left at 1:00 PM PHT, 1h past scheduled end. Processed late — staff was on break."
}
```

**Response `200 OK`:**
```json
{
  "bookingStatus": "overstayed",
  "scheduledEnd": "2026-03-11T04:00:00Z",
  "actualEnd": "2026-03-11T05:00:00Z",
  "overstayAmount": 50.00,
  "totalAmount": 150.00,
  ...
}
```

> The overstay calculation uses `actualEndOverride` as the effective end — the system correctly computes 1 extra hour at the snapshot hourly rate.

---

### Corrected Time-Out — Validation Errors

**Future `actualEndOverride`** — sending a time that hasn't happened yet:

```json
{ "actualEndOverride": "2099-01-01T00:00:00Z" }
```
**Response `400 Bad Request`:**
```json
{ "detail": "Actual end time cannot be in the future." }
```

**`actualEndOverride` before `ActualStart`** — logically impossible checkout:

```json
{ "actualEndOverride": "2026-03-11T00:00:00Z" }
```
*(If ActualStart was 02:00 UTC)*

**Response `409 Conflict`:**
```json
{ "detail": "Actual end time must be after the booking's actual start time." }
```

**`timeOutNotes` too long** — exceeds 500 characters:

**Response `400 Bad Request`:**
```json
{ "detail": "Time-out notes must not exceed 500 characters." }
```

---

## Validation Errors to Test

### Wrong payment method via `POST /payment`

```json
{ "bookingId": "...", "amount": 100, "paymentMethod": "image_attachment", "paymentType": "balance" }
```
**Response `409 Conflict`:**
```json
{ "detail": "Use the proof upload endpoint (POST frontdesk/payments/submit-proof) for image-based payments." }
```

### Past date on availability check

```
GET /api/coworking/availability?date=2026-01-01&startTime=09:00&endTime=17:00
```
**Response `400 Bad Request`:**
```json
{ "detail": "Date must not be in the past." }
```

### Time window reversed

```
GET /api/coworking/availability?date=2026-03-15&startTime=17:00&endTime=09:00
```
**Response `400 Bad Request`:**
```json
{ "detail": "Start time must be before end time." }
```

### Rejecting a payment proof (wrong screenshot)

**`POST /api/coworking/frontdesk/payments/<PAYMENT_ID>/validate`**
```json
{
  "isApproved": false,
  "rejectionReason": "Screenshot amount does not match. Guest paid ₱500 but booking total is ₱600."
}
```
**Response `200 OK`**

> `paymentValidationStatus` is set to `"rejected"`. The booking's `paymentStatus` is **unchanged** — the guest must resubmit a new proof. A second call to `submit-proof` for the same booking creates another `booking_payments` row in `pending_validation`.

### Double-validating the same payment

**`POST /api/coworking/frontdesk/payments/<ALREADY_VALIDATED_PAYMENT_ID>/validate`**
```json
{ "isApproved": true }
```
**Response `409 Conflict`:**
```json
{ "detail": "This payment has already been validated." }
```

### Time-out on a non-checked-in booking

**`POST /api/coworking/frontdesk/time-out/<ALREADY_CHECKED_OUT_BOOKING_ID>`**

**Response `409 Conflict`:**
```json
{ "detail": "Booking is not currently checked in." }
```

---

## Summary

| Step | Action | Endpoint | Status After |
|---|---|---|---|
| 1 | Login | `POST /api/auth/login` | — |
| 2 | Seed IDs | `GET /api/public/resources`, `GET /api/public/packages` | — |
| 3 | Check time window availability | `GET /api/coworking/availability` | All seats available |
| 4 | Walk-in seat check | `GET /api/coworking/availability/{id}` | Available |
| 5 | Juan time-in (Day Pass) | `POST /frontdesk/time-in` | `checked_in / unpaid` |
| 6 | Confirm Seat 07 blocked | `GET /api/coworking/availability/{id}` | Unavailable |
| 7 | Maria time-in (Hourly, student) | `POST /frontdesk/time-in` | `checked_in / unpaid` |
| 8 | View active sessions | `GET /frontdesk/sessions/active` | 2 sessions shown |
| 9 | Maria time-out (on time) | `POST /frontdesk/time-out/{id}` | `checked_out / unpaid` |
| 10 | Maria pays cash | `POST /frontdesk/payment` | `checked_out / fully_paid` |
| 11 | View today's bookings | `GET /frontdesk/bookings?date=...` | Both bookings listed |
| 12 | Juan time-out (on time) | `POST /frontdesk/time-out/{id}` | `checked_out / unpaid` |
| 13 | Juan submits GCash proof | `POST /frontdesk/payments/submit-proof` | Payment `pending_validation` |
| 14 | View pending validations | `GET /frontdesk/payments/pending` | 1 pending |
| 15 | FrontDesk approves proof | `POST /frontdesk/payments/{id}/validate` | `checked_out / fully_paid` |
| 16 | Carlos time-in (Hourly 2h) | `POST /frontdesk/time-in` | `checked_in / unpaid` |
| 17 | Carlos time-out (overstay) | `POST /frontdesk/time-out/{id}` | `overstayed / unpaid` |
| 18 | Carlos pays full cash | `POST /frontdesk/payment` | `checked_out / fully_paid` |
| — | *Corrected time-out (optional body)* | `POST /frontdesk/time-out/{id}` | Uses `actualEndOverride` + `timeOutNotes` |

---

## Enum Reference

All enum fields are sent and received as snake_case strings.

**`bookingStatus`**
| Value | Meaning |
|---|---|
| `checked_in` | Session active — guest is using the space |
| `checked_out` | Session ended and payment settled |
| `overstayed` | Guest left after scheduled end — overstay fee applied; awaiting payment |
| `cancelled` | Booking cancelled before check-in |

**`paymentStatus`**
| Value | Meaning |
|---|---|
| `unpaid` | No confirmed payment received yet |
| `partially_paid` | Some amount received but balance remains |
| `fully_paid` | Entire balance settled |

**`paymentMethod`**
| Value | Meaning |
|---|---|
| `cash` | Cash at counter — no proof needed |
| `image_attachment` | Any e-wallet or bank transfer screenshot — requires FrontDesk validation |

**`paymentType`**
| Value | Meaning |
|---|---|
| `balance` | Standard session payment |
| `overstay_fee` | Additional fee for time beyond scheduled end |

**`paymentValidationStatus`**
| Value | Meaning |
|---|---|
| `not_required` | Cash payment — no proof |
| `pending_validation` | Proof uploaded, awaiting FrontDesk review |
| `validated` | FrontDesk confirmed the proof |
| `rejected` | FrontDesk rejected the proof — guest must resubmit |

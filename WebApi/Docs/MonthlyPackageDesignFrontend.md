# Monthly Package — Frontend Implementation Guide

## What Changed and Why

Three things changed on the backend:

1. **Packages now expose which payment schemes they allow** (`allowsUpfront`, `allowsPostpaid`, `allowsDaily`)
2. **Time-in now requires a `paymentScheme` choice** — FrontDesk picks one of the package's allowed schemes
3. **`estimatedHours` is optional** — only sent for Hourly packages; non-hourly packages derive their own end time server-side
4. **Time-in response includes `dailyRate`** — only when `paymentScheme == "daily"`

Nothing else changed. Checkout, payment recording, and booking detail are untouched.

---

## User Flows

### Flow A — Admin Creates a Monthly Package

1. Admin opens **Create Package** form
2. Selects **Package Type = Monthly Dedicated Desk**
3. Three payment scheme toggles appear *(only show these when PackageType = monthly_dedicated_desk — see rules below)*:
   - ☐ Allow Upfront payment
   - ☑ Allow Postpaid payment *(checked by default)*
   - ☐ Allow Daily payment
4. Admin enables the schemes the business wants to offer (e.g., all three for flexibility)
5. Submits — backend validates at least one is enabled

**UI rules for the three toggles:**
- Always show `allowsPostpaid` for all package types
- Show `allowsUpfront` for all package types **except Hourly** (disable + tooltip: "Upfront not available for hourly — total is unknown at check-in")
- Show `allowsDaily` **only** for Monthly Dedicated Desk; hide it for all other types
- Enforce: at least one toggle must be on before submitting

---

### Flow B — FrontDesk Checks In a Monthly Member

1. FrontDesk opens **Time-In** form
2. Selects resource and package
3. **If package type is NOT hourly:** hide the `estimatedHours` field entirely — do not send it
4. **Payment scheme selector appears** — built from the selected package's `allowsUpfront / allowsPostpaid / allowsDaily`:
   - If only one scheme is allowed → auto-select it, show as read-only label (no picker needed)
   - If multiple → show radio buttons or a dropdown with only the enabled options
5. FrontDesk fills in guest info and submits

**After submit — response handling by scheme:**

| `paymentScheme` in response | What to show FrontDesk |
|-----------------------------|------------------------|
| `upfront` | "Collect **₱[totalAmount]** now before seating the member." |
| `postpaid` | "Member will pay **₱[totalAmount]** at checkout." |
| `daily` | "Collect **₱[dailyRate]** today. Collect the same amount on each return visit." |

For `daily`: show `dailyRate` prominently in the confirmation screen/receipt. This is the amount the FrontDesk collects on day 1 and every subsequent visit via the normal payment endpoint.

Also note: `scheduledEnd` for a monthly booking will now be ~30 days from now (correct). No longer 8 hours.

---

### Flow C — Monthly Member Returns (Daily Payment)

No new endpoints. The FrontDesk:
1. Searches for the member's booking in the booking list
2. Opens the booking detail
3. Records payment using the existing **Record Payment** or **Submit Proof** flow
4. Amount to enter = `dailyRate` (which is stored in the booking detail or FrontDesk can look it up from the time-in confirmation)

**Consider:** in the booking detail for daily-scheme bookings, show a helper like:
> "Daily rate: ₱100.00 — Balance remaining: ₱2,900.00"

This uses existing fields: `balanceAmount` / `totalAmount` are already on the booking detail response. You don't need `dailyRate` from the detail endpoint — it's only in the time-in response. Compute it client-side if needed: `totalAmount / durationDays` (you have `snapshotPackageName` but not `durationDays` on the booking detail — so either store `dailyRate` from the time-in response in local state/session for the receipt screen, or just show the balance).

---

## API Changes

### GET /api/coworking/packages — package list now includes scheme flags

```json
{
  "id": "uuid",
  "name": "Monthly Dedicated Desk",
  "packageType": "monthly_dedicated_desk",
  "durationDays": 30,
  "allowsUpfront": true,
  "allowsPostpaid": true,
  "allowsDaily": true,
  ...
}
```

Fetch the selected package when building the time-in form to know which scheme options to show.

---

### POST /api/coworking/packages — create package

Add the three booleans to the create body:

```json
{
  "name": "Monthly Dedicated Desk",
  "resourceType": "coworking_space",
  "packageType": "monthly_dedicated_desk",
  "priceAmount": 3000,
  "durationDays": 30,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 50,
  "allowsUpfront": true,
  "allowsPostpaid": true,
  "allowsDaily": true
}
```

Same three fields on `PUT /api/coworking/packages/{id}` (all optional on update).

---

### POST /api/coworking/frontdesk/time-in — updated request

```json
// Monthly package — no estimatedHours, add paymentScheme
{
  "resourceId": "uuid",
  "servicePackageId": "uuid",
  "guestFirstName": "Raniel",
  "guestLastName": "Montebon",
  "guestPhone": "09916472453",
  "guestEmail": "raniel@email.com",
  "isStudent": false,
  "paymentScheme": "daily"
}

// Hourly package — estimatedHours still required
{
  "resourceId": "uuid",
  "servicePackageId": "uuid",
  "estimatedHours": 2,
  "guestFirstName": "Raniel",
  "guestLastName": "Montebon",
  "guestPhone": "09916472453",
  "guestEmail": "raniel@email.com",
  "isStudent": false,
  "paymentScheme": "postpaid"
}
```

`paymentScheme` accepted values: `upfront` | `postpaid` | `daily`

---

### POST /api/coworking/frontdesk/time-in — updated response

```json
{
  "bookingId": "uuid",
  "referenceNumber": "BKG-0005",
  "guestName": "Raniel Montebon",
  "resourceName": "Dedicated Desk 1",
  "scheduledStart": "2026-03-15T08:00:00Z",
  "scheduledEnd": "2026-04-14T08:00:00Z",
  "snapshotPackageName": "Monthly Dedicated Desk",
  "paymentScheme": "daily",
  "isStudent": false,
  "baseAmount": 3000.00,
  "discountAmount": 0.00,
  "totalAmount": 3000.00,
  "dailyRate": 100.00
}
```

- `paymentScheme` — always present
- `dailyRate` — present only when `paymentScheme == "daily"`, otherwise `null`
- `scheduledEnd` — for monthly, this is now 30 days from start (not 8 hours)

---

## Component Checklist

### Package Form (Admin)

- [ ] Add `allowsPostpaid` toggle (visible for all package types, default on)
- [ ] Add `allowsUpfront` toggle (hidden or disabled for Hourly, default off)
- [ ] Add `allowsDaily` toggle (visible only when `packageType == monthly_dedicated_desk`, default off)
- [ ] Client-side validation: at least one scheme must be on
- [ ] Include all three booleans in create/update request body

### Time-In Form (FrontDesk)

- [ ] Hide `estimatedHours` field when selected package type is not Hourly
- [ ] After package is selected, fetch package details to get allowed schemes
- [ ] Show payment scheme selector with only the allowed options
- [ ] Auto-select and hide selector if only one scheme is allowed
- [ ] Send `paymentScheme` in request body always
- [ ] Do not send `estimatedHours` for non-Hourly packages

### Time-In Confirmation / Receipt (FrontDesk)

- [ ] Show appropriate collection instruction based on `paymentScheme`
- [ ] If `dailyRate` is not null, display it prominently with label "Collect per visit"
- [ ] Show correct `scheduledEnd` (no longer same-day for monthly)

### Booking Detail (FrontDesk)

- [ ] No endpoint changes — existing fields cover everything
- [ ] Consider showing "Daily rate: ₱X" helper text for daily-scheme bookings
  - Derive from `totalAmount` / package `durationDays` if needed, or store from time-in response

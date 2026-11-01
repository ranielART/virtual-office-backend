# Admin Dashboard — Frontend Implementation Guide

## Endpoint

```
GET /api/coworking/admin/dashboard?period=today|this_week|this_month|this_year
Authorization: Bearer {token}   (Admin role only)
```

Default period when the param is omitted: `today`.

---

## Full Response Shape

```json
{
  "live": {
    "activeSessions": 3,
    "overstayedSessions": 1,
    "pendingPaymentValidations": 2,
    "unpaidActiveSessions": 2,
    "outstandingFromActive": 350.00
  },
  "period": {
    "periodLabel": "Today, March 18",
    "periodStart": "2026-03-18T00:00:00Z",
    "periodEnd":   "2026-03-19T00:00:00Z",
    "totalCheckIns": 12,
    "totalCheckOuts": 8,
    "walkInCount": 12,
    "studentCount": 3,
    "revenueCollected": 1800.00,
    "totalBilled": 2400.00,
    "totalOutstanding": 600.00,
    "unpaidCount": 4,
    "partiallyPaidCount": 2,
    "fullyPaidCount": 6
  },
  "byResourceType": [
    { "resourceType": "coworking_seats",    "checkInCount": 8, "totalBilled": 1200.00 },
    { "resourceType": "executive_room",     "checkInCount": 3, "totalBilled": 900.00  },
    { "resourceType": "collaboration_room", "checkInCount": 1, "totalBilled": 300.00  }
  ],
  "byPackageType": [
    { "packageType": "hourly",                 "checkInCount": 6, "totalBilled": 600.00  },
    { "packageType": "day_pass",               "checkInCount": 3, "totalBilled": 900.00  },
    { "packageType": "monthly_dedicated_desk", "checkInCount": 3, "totalBilled": 9000.00 }
  ],
  "overall": {
    "totalResources": 25,
    "activeResources": 24,
    "totalPackages": 14,
    "activePackages": 13,
    "totalBookings": 390,
    "totalRevenue": 253532.76
  },
  "recentBookings": [
    {
      "bookingId": "uuid",
      "referenceNumber": "BKG-0039",
      "guestName": "Raniel Art Montebon",
      "resourceName": "Seat 01",
      "bookingStatus": "checked_in",
      "paymentStatus": "unpaid",
      "scheduledStart": "2026-03-18T08:31:26Z",
      "scheduledEnd":   "2026-03-18T09:31:26Z",
      "actualStart":    "2026-03-18T08:31:26Z",
      "actualEnd": null,
      "snapshotPackageName": "Hourly — Regular",
      "totalAmount": 45.00,
      "balanceAmount": 45.00
    }
  ]
}
```

---

## What Each Section Means

### `live` — Always current, never changes with the period filter
Real-time snapshot of what is happening in the space right now.

| Field | Meaning |
|-------|---------|
| `activeSessions` | Guests currently checked in |
| `overstayedSessions` | Guests past their scheduled end time (urgent) |
| `pendingPaymentValidations` | Payment proofs uploaded by guests/staff awaiting admin review |
| `unpaidActiveSessions` | Currently active/overstayed sessions with an outstanding balance |
| `outstandingFromActive` | Total ₱ owed by all currently active/overstayed guests |

### `period` — Changes when the period filter changes
All stats here are scoped to bookings whose **scheduled start** falls within the selected period.

| Field | Meaning |
|-------|---------|
| `periodLabel` | Human-readable label the backend already computed — display this as-is (e.g. "Today, March 18", "Mar 10–16, 2026", "March 2026", "2026") |
| `periodStart` / `periodEnd` | UTC bounds of the period — use these only if you need to display date ranges yourself |
| `totalCheckIns` | Number of bookings that started in this period |
| `totalCheckOuts` | Number of bookings that were checked out within this period |
| `walkInCount` | How many of the period's check-ins were walk-ins |
| `studentCount` | How many of the period's check-ins were student bookings |
| `revenueCollected` | Total payments collected for period bookings (validated or no-validation-required) |
| `totalBilled` | Sum of the total amount owed across all period bookings |
| `totalOutstanding` | Sum of unpaid balance across period bookings that are not fully paid |
| `unpaidCount` | Period bookings with payment status = unpaid |
| `partiallyPaidCount` | Period bookings with payment status = partially paid |
| `fullyPaidCount` | Period bookings with payment status = fully paid |

### `byResourceType` — Changes with period
Per-resource-type breakdown for the selected period. Each entry has:
- `resourceType`: one of `coworking_seats`, `executive_room`, `collaboration_room`
- `checkInCount`: number of bookings in this period for that resource type
- `totalBilled`: total amount billed for that resource type in this period

Only resource types with at least one booking in the period are returned.

Display name mapping:
- `coworking_seats` → "Coworking Seats"
- `executive_room` → "Executive Room"
- `collaboration_room` → "Collaboration Room"

### `byPackageType` — Changes with period
Per-package-type breakdown for the selected period. Same shape as `byResourceType`.

Display name mapping:
- `hourly` → "Hourly"
- `half_day` → "Half Day"
- `full_day` → "Full Day"
- `day_pass` → "Day Pass"
- `weekly_pass` → "Weekly Pass"
- `monthly_dedicated_desk` → "Monthly Desk"

Only package types with at least one booking in the period are returned.

### `overall` — Never changes, always all-time totals
High-level system-wide numbers, completely independent of the period filter.

| Field | Meaning |
|-------|---------|
| `totalResources` | Total number of resources ever created |
| `activeResources` | Resources currently marked active |
| `totalPackages` | Total number of packages ever created |
| `activePackages` | Packages currently marked active |
| `totalBookings` | All-time total booking count |
| `totalRevenue` | All-time total revenue collected (validated payments only) |

### `recentBookings` — Last 5, never changes with period
The 5 most recent bookings by scheduled start, regardless of period. Each entry has:
- `bookingId`, `referenceNumber`, `guestName`, `resourceName`
- `bookingStatus`, `paymentStatus`
- `scheduledStart`, `scheduledEnd`, `actualStart`, `actualEnd`
- `snapshotPackageName`, `totalAmount`, `balanceAmount`

Tapping a row navigates to the booking detail page for that booking.

---

## Period Filter Behavior

The period filter controls `period`, `byResourceType`, and `byPackageType`. It does **not** affect `live` or `overall`.

Four options:

| Label | `period` param | What it covers |
|-------|---------------|----------------|
| Today | `today` | Current calendar day in UTC |
| This Week | `this_week` | Monday through Sunday of the current ISO week |
| This Month | `this_month` | First through last day of the current calendar month |
| This Year | `this_year` | January 1 through December 31 of the current year |

- Default to **Today** on page mount.
- When the user switches periods, re-fetch the dashboard with the new `period` param.
- Display `period.periodLabel` (returned by the backend) as a subtitle or secondary label below the filter tabs. Do not compute or format it yourself — use it verbatim.

---

## What to Display and Where

### Always-visible section — Live Status
Show `live` fields unconditionally, regardless of the selected period. These values never change based on the period tab.

Display:
- `live.activeSessions` — number of currently active sessions
- `live.overstayedSessions` — number of currently overstayed sessions; visually flag this as urgent (e.g. red) when the value is greater than 0
- `live.pendingPaymentValidations` — number of payment proofs waiting for admin review
- `live.unpaidActiveSessions` — number of active sessions with outstanding balance
- `live.outstandingFromActive` — total outstanding balance from active sessions, formatted as ₱X,XXX.XX

### Always-visible section — Overall Stats
Show `overall` fields unconditionally. These are all-time system-wide figures, never period-filtered.

Display:
- `overall.activeResources` out of `overall.totalResources` total resources
- `overall.activePackages` out of `overall.totalPackages` total packages
- `overall.totalBookings` — all-time booking count
- `overall.totalRevenue` — all-time revenue, formatted as ₱X,XXX.XX

### Period-scoped section — Activity Stats
All values here come from `period` and must update when the period tab changes.

Display:
- `period.totalCheckIns` — total check-ins in the period
- `period.totalCheckOuts` — total check-outs in the period
- `period.walkInCount` — walk-ins in the period
- `period.studentCount` — student bookings in the period

### Period-scoped section — Financial Stats
All values come from `period` and update with the period tab.

Display:
- `period.revenueCollected` as "Revenue Collected" — formatted as ₱X,XXX.XX
- `period.totalBilled` as "Total Billed" — formatted as ₱X,XXX.XX
- `period.totalOutstanding` as "Outstanding" — formatted as ₱X,XXX.XX; flag amber or red when greater than 0

### Period-scoped section — Payment Status Breakdown
Three counts from `period`, all update with the period tab:
- `period.unpaidCount` — unpaid bookings
- `period.partiallyPaidCount` — partially paid bookings
- `period.fullyPaidCount` — fully paid bookings

### Period-scoped section — Breakdown Tables
Two tables, both scoped to the period. Both update when the period tab changes.

**By Resource Type** — iterate `byResourceType`:
- Show resource type display name, `checkInCount`, and `totalBilled` formatted as ₱
- If `byResourceType` is empty, show a "No data for this period" placeholder

**By Package Type** — iterate `byPackageType`:
- Show package type display name, `checkInCount`, and `totalBilled` formatted as ₱
- If `byPackageType` is empty, show a "No data for this period" placeholder

### Always-visible section — Recent Bookings
Show the last 5 bookings from `recentBookings`. These do not change with the period.

For each booking, display:
- `referenceNumber` and `guestName`
- `resourceName` and `snapshotPackageName`
- `bookingStatus` and `paymentStatus` as status badges
- `scheduledStart` formatted as a readable date/time
- `totalAmount` and `balanceAmount` formatted as ₱

Tapping a row navigates to the booking detail for that `bookingId`.

---

## Empty States

- If `period.totalCheckIns === 0` for the selected period: show a message like "No bookings for this period" in the activity and financial sections. Still render the live and overall sections normally.
- If `byResourceType` or `byPackageType` are empty arrays: show "No data for this period" in those sections.
- If `recentBookings` is an empty array: show "No bookings yet."

---

## Data Fetching

- Fetch on page mount with `period=today`.
- Re-fetch whenever the period tab changes.
- Do not cache the response across period changes — always fetch fresh.
- The `live` and `overall` sections still refresh on every fetch even though they are not period-sensitive, because the user may expect to see real-time data when they switch tabs.

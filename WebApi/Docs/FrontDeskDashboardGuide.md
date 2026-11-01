# FrontDesk Dashboard — Frontend Implementation Guide

## Endpoint

```
GET /api/coworking/frontdesk/dashboard?period=today|this_week|this_month|this_year
Authorization: Bearer {token}   (Admin or Frontdesk role)
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
    "periodLabel": "Today, March 16",
    "periodStart": "2026-03-16T00:00:00Z",
    "periodEnd":   "2026-03-17T00:00:00Z",
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
    { "resourceType": "coworking_seats",     "checkInCount": 8, "totalBilled": 1200.00 },
    { "resourceType": "executive_room",      "checkInCount": 3, "totalBilled": 900.00  },
    { "resourceType": "collaboration_room",  "checkInCount": 1, "totalBilled": 300.00  }
  ],
  "byPackageType": [
    { "packageType": "hourly",                  "checkInCount": 6, "totalBilled": 600.00  },
    { "packageType": "day_pass",                "checkInCount": 3, "totalBilled": 900.00  },
    { "packageType": "monthly_dedicated_desk",  "checkInCount": 3, "totalBilled": 9000.00 }
  ],
  "recentCheckIns": [
    {
      "bookingId": "uuid",
      "referenceNumber": "BKG-0012",
      "guestName": "Raniel Montebon",
      "guestPhone": "09916472453",
      "resourceName": "Hot Desk A",
      "bookingStatus": "checked_in",
      "paymentStatus": "unpaid",
      "snapshotPaymentScheme": "postpaid",
      "scheduledStart": "2026-03-16T08:00:00Z",
      "scheduledEnd":   "2026-03-16T10:00:00Z",
      "actualStart":    "2026-03-16T08:03:00Z",
      "actualEnd": null,
      "snapshotPackageName": "Hourly",
      "totalAmount": 100.00,
      "balanceAmount": 100.00,
      "isStudent": false
    }
  ]
}
```

`periodLabel` for each period value:
| `period` param  | Example label                 |
|-----------------|-------------------------------|
| `today`         | `"Today, March 16"`           |
| `this_week`     | `"Mar 10–16, 2026"` or `"Mar 28 – Apr 3, 2026"` (cross-month) |
| `this_month`    | `"March 2026"`                |
| `this_year`     | `"2026"`                      |

Week boundaries are always **Monday 00:00 UTC → Sunday 23:59 UTC** (ISO week).

---

## Layout & Components

### Period Filter (tab or segmented control)

Always at the top of the dashboard. Four options: **Today · This Week · This Month · This Year**.
- On mount, default to **Today**.
- On tab change, re-fetch the dashboard with the new `period` param.
- Show `period.periodLabel` as a subtitle below the tab bar (e.g. `"Mar 10–16, 2026"`).

---

### Section 1 — Live Status (no period, always current)

Display these as a row of stat cards. These are real-time and do not change when the period tab changes.

| Card | Value | Color / Urgency |
|------|-------|-----------------|
| Active Sessions | `live.activeSessions` | Green (normal) |
| Overstayed | `live.overstayedSessions` | Red (urgent) — highlight prominently when > 0 |
| Pending Validations | `live.pendingPaymentValidations` | Yellow/Amber — tap to navigate to pending validations screen |
| Unpaid Active | `live.unpaidActiveSessions` | Amber |
| Outstanding Balance | `₱ live.outstandingFromActive` | Amber |

> **Tap behavior:** tapping "Overstayed" or "Active Sessions" should navigate to the booking list pre-filtered to those statuses. Tapping "Pending Validations" navigates to the pending validations screen.

---

### Section 2 — Period KPIs

Display as a grid of stat cards. These all change when the period tab changes.

**Row 1 — Activity**

| Card | Value |
|------|-------|
| Check-ins | `period.totalCheckIns` |
| Check-outs | `period.totalCheckOuts` |
| Walk-ins | `period.walkInCount` |
| Students | `period.studentCount` |

**Row 2 — Financials**

| Card | Label | Value |
|------|-------|-------|
| Revenue Collected | "Cash In" | `₱ period.revenueCollected` — green |
| Total Billed | "Total Billed" | `₱ period.totalBilled` |
| Outstanding | "Uncollected" | `₱ period.totalOutstanding` — amber when > 0 |

**Row 3 — Payment Status Breakdown** (small pill/badge row or mini bar)

Show three inline counters side-by-side:
- 🔴 `period.unpaidCount` Unpaid
- 🟡 `period.partiallyPaidCount` Partial
- 🟢 `period.fullyPaidCount` Paid

---

### Section 3 — Breakdown Charts / Lists

Two compact tables or horizontal bar charts side-by-side (or stacked on mobile):

**By Resource Type**

| Resource | Check-ins | Billed |
|----------|-----------|--------|
| Coworking Seats | `checkInCount` | `₱ totalBilled` |
| Executive Room | ... | ... |
| Collaboration Room | ... | ... |

Derived from `byResourceType`. Map `resourceType` enum to a display name:
- `coworking_seats` → "Coworking Seats"
- `executive_room` → "Executive Room"
- `collaboration_room` → "Collaboration Room"

**By Package Type**

| Package | Check-ins | Billed |
|---------|-----------|--------|
| Hourly | `checkInCount` | `₱ totalBilled` |
| Day Pass | ... | ... |
| ... | ... | ... |

Derived from `byPackageType`. Map `packageType` enum to a display name:
- `hourly` → "Hourly"
- `half_day` → "Half Day"
- `full_day` → "Full Day"
- `day_pass` → "Day Pass"
- `weekly_pass` → "Weekly Pass"
- `monthly_dedicated_desk` → "Monthly Desk"

Only render rows where `checkInCount > 0` — skip empty package types.

---

### Section 4 — Recent Check-ins

Show the last 5 check-ins as a compact list. These do NOT change when the period tab changes — they are always the most recent 5 regardless of period.

Each row shows:
- Reference number + guest name
- Resource name + package name
- `bookingStatus` badge + `paymentStatus` badge
- `totalAmount` and `balanceAmount`
- `scheduledStart` (formatted as time, or date + time if not today)
- Tap → navigate to booking detail screen

Use the same status badge colors used in the booking list screen.

---

## Data Fetching

```ts
// Example using React Query / SWR pattern
const fetchDashboard = (period: 'today' | 'this_week' | 'this_month' | 'this_year') =>
  api.get(`/api/coworking/frontdesk/dashboard?period=${period}`)

// Refetch when period tab changes
// Do NOT cache across period changes — always re-fetch fresh
```

Recommended: add a **manual refresh** button (pull-to-refresh on mobile, a refresh icon on web) since the Live section reflects real-time state. Auto-refresh every 60 seconds is optional but useful for the Live section.

---

## Empty States

- If `period.totalCheckIns === 0` for a period: show "No check-ins for this period" in the Period KPI section. Still show the Live section normally.
- If `byResourceType` or `byPackageType` are empty arrays: show a "—" placeholder row.
- If `recentCheckIns` is empty: show "No bookings yet" in the recent check-ins list.

---

## Navigation Shortcuts (nice to have)

These are quick-action shortcuts from the dashboard to other screens:

| From | Navigates to |
|------|-------------|
| "Overstayed" card | Booking list filtered to `bookingStatus=overstayed` |
| "Active Sessions" card | Booking list filtered to `bookingStatus=checked_in` |
| "Pending Validations" card | Pending validations screen |
| "Unpaid Active" card | Booking list filtered to `bookingStatus=checked_in,overstayed&paymentStatus=unpaid,partially_paid` |
| Any row in Recent Check-ins | Booking detail screen for that booking |

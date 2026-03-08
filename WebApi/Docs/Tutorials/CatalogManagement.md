# Tutorial: Resource & Package Catalog Management

This tutorial walks you through managing the coworking catalog — the spaces available for booking and the pricing options attached to them. By the end, you will know how to add a new room, configure its packages, make corrections, and control visibility.

**Scenario:** A new meeting room is being added to the coworking space. You need to register it in the system and set up its pricing before guests can book it.

---

## What Is the Catalog?

The catalog has two parts:

| Concept | What it represents | DB Table |
|--------|-------------------|----------|
| **Resource** | A physical bookable space (a seat, a room) | `coworking_resources` |
| **Service Package** | A pricing option for a resource type (hourly, half day, full day) | `service_packages` |

Resources and packages are independent — packages belong to a **resource type** (e.g., `"executive_room"`), not to a specific room. This means one package applies to all rooms of the same type.

---

## Before You Start

All catalog endpoints require an Admin token. Log in first.

**`POST /api/auth/login`**

```json
{
  "email": "admin@virtualoffice.com",
  "password": "Admin@123456"
}
```

**Response `200 OK`:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "...",
  "expiration": "2026-03-07T10:00:00Z"
}
```

Add this header to every request below:
```
Authorization: Bearer <token>
```

---

## Step 1 — See What Already Exists

Before adding anything, get a picture of the current catalog.

**`GET /api/coworking/admin/resources`**

> No body needed.

**Response `200 OK`:**
```json
[
  { "id": "...", "name": "Seat 01", "resourceType": "coworking_seats", "capacity": 1, "isActive": true },
  { "id": "...", "name": "Seat 02", "resourceType": "coworking_seats", "capacity": 1, "isActive": true },
  // ... seats 03–20 ...
  { "id": "...", "name": "Collaboration Room", "resourceType": "collaboration_room", "capacity": 20, "isActive": true },
  { "id": "...", "name": "Executive Room",     "resourceType": "executive_room",     "capacity": 3,  "isActive": true }
]
```

> Resources are ordered alphabetically by name.

**`GET /api/coworking/admin/packages`**

**Response `200 OK`:**
```json
[
  { "name": "Day Pass",               "resourceType": "coworking_seats", "packageType": "day_pass",              "priceAmount": 600.00  },
  { "name": "Hourly — Regular",       "resourceType": "coworking_seats", "packageType": "hourly",                "priceAmount": 50.00   },
  { "name": "Hourly — Student",       "resourceType": "coworking_seats", "packageType": "hourly",                "priceAmount": 40.00   },
  { "name": "Monthly Dedicated Desk", "resourceType": "coworking_seats", "packageType": "monthly_dedicated_desk","priceAmount": 8500.00 },
  { "name": "Weekly Pass",            "resourceType": "coworking_seats", "packageType": "weekly_pass",           "priceAmount": 2700.00 },
  { "name": "Executive Room — Full Day",  "resourceType": "executive_room", "packageType": "full_day",  "priceAmount": 5500.00 },
  { "name": "Executive Room — Half Day",  "resourceType": "executive_room", "packageType": "half_day",  "priceAmount": 3000.00 },
  { "name": "Executive Room — Hourly",    "resourceType": "executive_room", "packageType": "hourly",    "priceAmount": 800.00  },
  { "name": "Collaboration Room — Full Day",  "resourceType": "collaboration_room", "packageType": "full_day",  "priceAmount": 9500.00 },
  { "name": "Collaboration Room — Half Day",  "resourceType": "collaboration_room", "packageType": "half_day",  "priceAmount": 5500.00 },
  { "name": "Collaboration Room — Hourly",    "resourceType": "collaboration_room", "packageType": "hourly",    "priceAmount": 1500.00 }
]
```

> **DB:** These are the rows seeded into `coworking_resources` and `service_packages` on first startup.

---

## Step 2 — Add the New Room

Register the new meeting room as a resource.

**`POST /api/coworking/admin/resources`**

```json
{
  "name": "Boardroom",
  "resourceType": "executive_room",
  "capacity": 8,
  "description": "Premium boardroom on the third floor. Projector and whiteboard included."
}
```

> `"resourceType": "executive_room"` tells the system which package pool this room draws from.

**Response `200 OK`:**
```json
{
  "id": "f3a1b2c4-d5e6-7890-ab12-cd34ef567890",
  "name": "Boardroom",
  "resourceType": "executive_room",
  "capacity": 8,
  "description": "Premium boardroom on the third floor. Projector and whiteboard included.",
  "isActive": true,
  "createdAt": "2026-03-07T08:00:00Z",
  "updatedAt": "2026-03-07T08:00:00Z"
}
```

> **DB:** A new row is inserted into `coworking_resources`. `is_active` is `true` by default.

**Save the returned `id` — you'll need it in later steps.**

---

## Step 3 — Confirm the Room Was Created

Fetch the room directly by its ID to confirm everything looks correct.

**`GET /api/coworking/admin/resources/{id}`**

```
GET /api/coworking/admin/resources/f3a1b2c4-d5e6-7890-ab12-cd34ef567890
```

**Response `200 OK`:**
```json
{
  "id": "f3a1b2c4-d5e6-7890-ab12-cd34ef567890",
  "name": "Boardroom",
  "resourceType": "executive_room",
  "capacity": 8,
  "description": "Premium boardroom on the third floor. Projector and whiteboard included.",
  "isActive": true,
  "createdAt": "2026-03-07T08:00:00Z",
  "updatedAt": "2026-03-07T08:00:00Z"
}
```

> **DB:** Reads from `coworking_resources WHERE id = '...'`.

---

## Step 4 — Add Packages for the New Room

The Boardroom is `"executive_room"`, so it shares packages with the existing Executive Room. The existing three packages (Hourly, Half Day, Full Day) already apply to it automatically.

However, this Boardroom is premium and needs a higher rate. Add dedicated packages for it.

> **Note:** Package names must be unique *per resource type*. Since "Executive Room — Hourly" already exists for `"executive_room"`, you need a different name.

**`POST /api/coworking/admin/packages`**

**Package 1 — Hourly (premium rate):**
```json
{
  "name": "Boardroom — Hourly",
  "resourceType": "executive_room",
  "packageType": "hourly",
  "priceAmount": 1200.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": 8,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 50
}
```

> `"durationHours": null` means this is metered — the guest pays per hour used. `"maxCapacity": 8` caps this package at 8 pax.

**Response `200 OK`:**
```json
{
  "id": "a1b2c3d4-e5f6-7890-ab12-cd34ef567891",
  "name": "Boardroom — Hourly",
  "resourceType": "executive_room",
  "packageType": "hourly",
  "priceAmount": 1200.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": 8,
  "isStudentEligible": false,
  "studentDiscountPercent": 0.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-07T08:05:00Z",
  "updatedAt": "2026-03-07T08:05:00Z"
}
```

Now add a full-day package with a fixed block duration:

**Package 2 — Full Day:**
```json
{
  "name": "Boardroom — Full Day",
  "resourceType": "executive_room",
  "packageType": "full_day",
  "priceAmount": 8000.00,
  "durationHours": 8,
  "durationDays": null,
  "maxCapacity": 8,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 50
}
```

> `"durationHours": 8` makes this a flat 8-hour block at a fixed price. Unlike hourly, the guest pays ₱8,000 regardless of whether they use 6 or 8 hours.

**Response `200 OK`:**
```json
{
  "id": "b2c3d4e5-f6a7-8901-bc23-de45fa678902",
  "name": "Boardroom — Full Day",
  "resourceType": "executive_room",
  "packageType": "full_day",
  "priceAmount": 8000.00,
  "durationHours": 8,
  "durationDays": null,
  "maxCapacity": 8,
  "isStudentEligible": false,
  "studentDiscountPercent": 0.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-07T08:07:00Z",
  "updatedAt": "2026-03-07T08:07:00Z"
}
```

> **DB:** Each package creates a row in `service_packages`. Both rows have `resource_type = 2` stored as an integer in the database; the API translates this to `"executive_room"` on the wire.

---

## Step 5 — Fix a Mistake on the Resource

You realize the capacity should be 10, not 8 — the room fits a larger table. Update only the capacity. All other fields stay the same.

**`PUT /api/coworking/admin/resources/{id}`**

```
PUT /api/coworking/admin/resources/f3a1b2c4-d5e6-7890-ab12-cd34ef567890
```

```json
{
  "capacity": 10
}
```

> Only send the fields you want to change. Fields omitted from the request are left untouched.

**Response `200 OK`:**
```json
{
  "id": "f3a1b2c4-d5e6-7890-ab12-cd34ef567890",
  "name": "Boardroom",
  "resourceType": "executive_room",
  "capacity": 10,
  "description": "Premium boardroom on the third floor. Projector and whiteboard included.",
  "isActive": true,
  "createdAt": "2026-03-07T08:00:00Z",
  "updatedAt": "2026-03-07T08:15:00Z"
}
```

> **DB:** Updates `capacity = 10` and `updated_at = now()` in `coworking_resources`. `created_at` is unchanged.

---

## Step 6 — Adjust a Package Price

After reviewing competitor pricing, the full-day package should be ₱7,500. Also update the `maxCapacity` to match the corrected room capacity.

**`PUT /api/coworking/admin/packages/{id}`**

```
PUT /api/coworking/admin/packages/b2c3d4e5-f6a7-8901-bc23-de45fa678902
```

```json
{
  "priceAmount": 7500.00,
  "maxCapacity": 10
}
```

**Response `200 OK`:**
```json
{
  "id": "b2c3d4e5-f6a7-8901-bc23-de45fa678902",
  "name": "Boardroom — Full Day",
  "resourceType": "executive_room",
  "packageType": "full_day",
  "priceAmount": 7500.00,
  "durationHours": 8,
  "durationDays": null,
  "maxCapacity": 10,
  "isStudentEligible": false,
  "studentDiscountPercent": 0.0,
  "downpaymentPercent": 50.0,
  "isActive": true,
  "createdAt": "2026-03-07T08:07:00Z",
  "updatedAt": "2026-03-07T08:20:00Z"
}
```

> **DB:** Updates `price_amount` and `max_capacity` in `service_packages`. **This price change does not affect any existing bookings** — prices are snapshotted at booking creation time.

---

## Step 7 — Take the Room Offline Temporarily

The room needs maintenance for two days. Disable it so it cannot be booked.

**`PATCH /api/coworking/admin/resources/{id}/toggle`**

```
PATCH /api/coworking/admin/resources/f3a1b2c4-d5e6-7890-ab12-cd34ef567890/toggle
```

> No request body needed.

**Response `200 OK`:**
```json
{
  "id": "f3a1b2c4-d5e6-7890-ab12-cd34ef567890",
  "name": "Boardroom",
  "isActive": false,
  "message": "'Boardroom' has been disabled."
}
```

> **DB:** Sets `is_active = false` and `updated_at = now()` in `coworking_resources`.

Verify it no longer appears in active-resource queries:

**`GET /api/coworking/admin/resources?isActive=true`**

The Boardroom will not be in this list. To confirm it still exists, query inactive resources:

**`GET /api/coworking/admin/resources?isActive=false`**

```json
[
  {
    "id": "f3a1b2c4-d5e6-7890-ab12-cd34ef567890",
    "name": "Boardroom",
    "resourceType": "executive_room",
    "capacity": 10,
    "isActive": false,
    ...
  }
]
```

---

## Step 8 — Bring the Room Back Online

Maintenance is done. Re-enable the room with the same toggle endpoint — it flips the current state.

**`PATCH /api/coworking/admin/resources/{id}/toggle`**

```
PATCH /api/coworking/admin/resources/f3a1b2c4-d5e6-7890-ab12-cd34ef567890/toggle
```

**Response `200 OK`:**
```json
{
  "id": "f3a1b2c4-d5e6-7890-ab12-cd34ef567890",
  "name": "Boardroom",
  "isActive": true,
  "message": "'Boardroom' has been enabled."
}
```

> **DB:** Sets `is_active = true` in `coworking_resources`. The room is bookable again.

---

## Step 9 — Retire a Package

The "Boardroom — Hourly" package is being discontinued in favour of block bookings. Deactivate it without deleting it (historical bookings that used it remain intact).

**`PATCH /api/coworking/admin/packages/{id}/toggle`**

```
PATCH /api/coworking/admin/packages/a1b2c3d4-e5f6-7890-ab12-cd34ef567891/toggle
```

**Response `200 OK`:**
```json
{
  "id": "a1b2c3d4-e5f6-7890-ab12-cd34ef567891",
  "name": "Boardroom — Hourly",
  "isActive": false,
  "message": "'Boardroom — Hourly' has been deactivated."
}
```

> **DB:** Sets `is_active = false` in `service_packages`. Existing bookings that used this package are unaffected — the price was already snapshotted on those bookings at creation time.

---

## Step 10 — Filter the Catalog

With multiple resources and packages in the system, use query filters to quickly find what you need.

**Filter resources by type — show only Executive Rooms:**
```
GET /api/coworking/admin/resources?resourceType=executive_room
```

**Filter packages by type and active status:**
```
GET /api/coworking/admin/packages?resourceType=executive_room&isActive=true
```

**Response `200 OK` (active Executive Room packages only):**
```json
[
  { "name": "Boardroom — Full Day",      "resourceType": "executive_room", "packageType": "full_day", "priceAmount": 7500.00, "isActive": true },
  { "name": "Executive Room — Full Day", "resourceType": "executive_room", "packageType": "full_day", "priceAmount": 5500.00, "isActive": true },
  { "name": "Executive Room — Half Day", "resourceType": "executive_room", "packageType": "half_day", "priceAmount": 3000.00, "isActive": true },
  { "name": "Executive Room — Hourly",   "resourceType": "executive_room", "packageType": "hourly",   "priceAmount": 800.00,  "isActive": true }
]
```

> Results are always ordered by `resourceType ASC` → `packageType ASC` → `name ASC`.

---

## Summary

Here is the complete sequence of what was done and the tables affected:

| Step | Action | API | DB Table |
|------|--------|-----|----------|
| 1 | View existing catalog | `GET /resources`, `GET /packages` | `coworking_resources`, `service_packages` |
| 2 | Register new room | `POST /resources` | INSERT `coworking_resources` |
| 3 | Confirm room | `GET /resources/{id}` | SELECT `coworking_resources` |
| 4 | Add 2 packages | `POST /packages` × 2 | INSERT `service_packages` × 2 |
| 5 | Fix room capacity | `PUT /resources/{id}` | UPDATE `coworking_resources` |
| 6 | Adjust package price | `PUT /packages/{id}` | UPDATE `service_packages` |
| 7 | Take room offline | `PATCH /resources/{id}/toggle` | UPDATE `is_active = false` |
| 8 | Bring room back | `PATCH /resources/{id}/toggle` | UPDATE `is_active = true` |
| 9 | Retire a package | `PATCH /packages/{id}/toggle` | UPDATE `is_active = false` |
| 10 | Filter catalog | `GET /resources?...`, `GET /packages?...` | SELECT with WHERE |

---

## Enum Reference

Enum fields are sent and received as readable string labels, not numbers. The database stores integers internally — the API handles the translation automatically.

**`resourceType`**
| Label | Meaning |
|-------|---------|
| `"coworking_seats"` | Individual open-plan desks |
| `"executive_room"` | Small private meeting room |
| `"collaboration_room"` | Large meeting / event room |

**`packageType`**
| Label | Meaning |
|-------|---------|
| `"hourly"` | Metered pay-per-hour (`durationHours` is `null`) |
| `"day_pass"` | Full-day block (typically 10 hours) |
| `"half_day"` | Half-day block (4–5 hours depending on room) |
| `"full_day"` | Full-day block (8 hours) |
| `"weekly_pass"` | 5 consecutive business days |
| `"monthly_dedicated_desk"` | 30-day dedicated seat |

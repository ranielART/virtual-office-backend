# Booking Query — Design Guide

A transferable reference for building paginated, filterable, sortable list endpoints
in any backend stack. The patterns here are stack-agnostic; C# / EF Core specifics
are called out explicitly so they can be adapted.

---

## 1. The Problem with Returning Everything

When a table can grow unboundedly (bookings, orders, transactions), returning all rows
in a single response is a reliability hazard:

- **Memory** — the server loads thousands of objects into RAM for every request.
- **Network** — the client receives a large payload it can only partially display.
- **Database** — a full-table scan with multiple JOINs on every page load.

The solution is **server-side pagination**: the database returns only the rows the
client needs right now, with metadata to navigate to more.

---

## 2. Architecture Overview

```
GET /api/coworking/frontdesk/bookings?page=1&pageSize=20&search=raniel&...
        │
        ▼
  BookingFilterRequest        ← validated DTO holding all filter/sort/page params
        │
        ▼
  GetBookingsQuery (MediatR)  ← passes filter into handler
        │
        ▼
  GetBookingsQueryHandler
    │  ├─ Build IQueryable     ← start with table reference, nothing hits DB yet
    │  ├─ ApplySearch()        ← appends WHERE clause
    │  ├─ ApplyStatusFilter()  ← appends WHERE clause
    │  ├─ ApplyDateFilter()    ← appends WHERE clause
    │  ├─ CountAsync()         ← ONE DB round-trip: SELECT COUNT(*)
    │  ├─ ApplySort()          ← appends ORDER BY
    │  ├─ Skip().Take()        ← appends OFFSET/LIMIT
    │  └─ Select().ToList()    ← ONE DB round-trip: SELECT projected columns
        │
        ▼
  PagedResult<BookingListItemResponse>
    { items, totalCount, page, pageSize, totalPages, hasNextPage, hasPreviousPage }
```

Two DB round-trips total, regardless of how many filters are applied.

---

## 3. Core Pattern: IQueryable Composition

The central idea: an `IQueryable` (or equivalent lazy query object) is a **description
of a query** — it does not execute until you call a materializing method like `.ToList()`.

This means you can conditionally add WHERE, ORDER BY, OFFSET, and LIMIT clauses in
separate methods, and the final SQL is composed from all of them before being sent
to the database.

```csharp
// Nothing touches the DB yet. This is just a query plan.
IQueryable<Booking> query = _dbContext.Bookings.AsNoTracking();

// Each of these appends a WHERE clause to the same query plan.
if (!string.IsNullOrWhiteSpace(search))
    query = query.Where(b => b.ReferenceNumber.Contains(search));

if (resourceId.HasValue)
    query = query.Where(b => b.ResourceId == resourceId);

// NOW the DB is hit — a single SELECT with all WHERE clauses combined.
var results = await query.ToListAsync();
```

**In other stacks:**
- Python / SQLAlchemy: `query = session.query(Booking).filter(...)`
- Node.js / TypeORM: `qb = repo.createQueryBuilder('b').where(...).andWhere(...)`
- Java / JPA Criteria API: `CriteriaQuery` + `Predicate` list

---

## 4. Projection — Select Only What You Need

Loading full entity objects with all columns and all related entities wastes memory
and bandwidth. Use projection to select only the fields the response needs:

```csharp
// EF Core generates: SELECT b.id, b.reference_number, g.first_name, r.name, ...
// It does NOT load Payments, Extensions, or any other navigation.
var items = await query
    .Select(b => new BookingListItemResponse
    {
        BookingId    = b.Id,
        GuestName    = b.Guest.FirstName + " " + b.Guest.LastName,
        ResourceName = b.Resource.Name,
        // ...
    })
    .ToListAsync();
```

EF Core resolves navigation properties inside `.Select()` as SQL JOINs automatically
— no explicit `.Include()` needed. This keeps the query minimal.

**Rule of thumb:** `.Include()` for when you need the full entity loaded in memory;
`.Select()` for read-only list/detail endpoints.

---

## 5. Count Before Pagination

To tell the client the total number of matching records (needed for pagination UI),
run `COUNT(*)` on the filtered query **before** applying OFFSET/LIMIT:

```csharp
// Same filters as the data query, but no ordering or pagination.
// EF Core generates: SELECT COUNT(*) FROM bookings WHERE ...
var totalCount = await query.CountAsync(cancellationToken);

// Apply sort + pagination to the same IQueryable (without re-filtering).
var items = await query
    .OrderBy(...)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(...)
    .ToListAsync(cancellationToken);
```

This is two DB round-trips. The alternative (load all rows and count in memory) is
always worse at scale. The trade-off is acceptable.

---

## 6. Smart Default Sort

When no explicit sort is requested, the default order should surface the most
actionable records first. For a coworking FrontDesk view:

```
Priority 1: Payment urgency  → unpaid / partially-paid bookings first
Priority 2: Session state    → active (checked-in, overstayed) before closed
Priority 3: Time urgency     → soonest scheduled end within each group
Priority 4: Creation date    → stable tiebreaker (most recent first)
```

In EF Core, ternary expressions in `OrderBy` translate to `CASE WHEN` in SQL:

```csharp
query
  .OrderBy(b => b.PaymentStatus == PaymentStatus.Unpaid ||
                b.PaymentStatus == PaymentStatus.PartiallyPaid ? 0 : 1)
  .ThenBy(b => b.BookingStatus == BookingStatus.CheckedIn ||
               b.BookingStatus == BookingStatus.Overstayed ? 0 : 1)
  .ThenBy(b => b.ScheduledEnd)
  .ThenByDescending(b => b.CreatedAt)
```

Generated SQL (conceptually):
```sql
ORDER BY
  CASE WHEN payment_status IN (1, 3) THEN 0 ELSE 1 END,
  CASE WHEN booking_status IN (4, 8) THEN 0 ELSE 1 END,
  scheduled_end ASC,
  created_at DESC
```

**Adapt this pattern** to any domain by identifying the "urgency dimensions":
e.g. for support tickets: open → in-progress → resolved, then by priority level,
then by created date.

---

## 7. Date Range Resolution

Date range presets ("today", "this week", etc.) are more useful than raw date inputs
for operational dashboards. The server converts them to UTC bounds:

```
dateRange=today       → [start of today in UTC, end of today in UTC]
dateRange=this_week   → [Monday 00:00 UTC, Sunday 23:59 UTC]
dateRange=this_month  → [1st of month 00:00 UTC, last day 23:59 UTC]
dateRange=this_year   → [Jan 1 00:00 UTC, Dec 31 23:59 UTC]
dateRange=custom      → [dateFrom 00:00 UTC, dateTo 23:59 UTC]
                        (dateTo defaults to dateFrom for single-day custom ranges)
```

Key implementation note for PostgreSQL: `DateOnly.ToDateTime(TimeOnly)` produces a
`DateTime` with `Kind = Unspecified`. Always call `.ToUniversalTime()` immediately
after to get the correct UTC value.

The date range always filters on `scheduled_start` — the column that answers the
natural user question "when was this booking scheduled?" Filtering on `actual_start`
or `actual_end` belongs to a reporting/analytics screen, not an operational list.

---

## 8. Enum Parsing from Query Strings

JSON body deserialization uses a `JsonStringEnumConverter` that handles snake_case
automatically. Query string binding does NOT use this converter — it uses `Enum.TryParse`,
which matches by name (e.g. `CheckedIn`) or integer value.

To accept snake_case values from query strings (`checked_in`, `partially_paid`):
strip underscores before parsing. `Enum.TryParse` with `ignoreCase: true` then
matches `checkedin` → `CheckedIn`, `partiallypaid` → `PartiallyPaid`.

```csharp
var normalized = rawValue.Replace("_", "");
Enum.TryParse<BookingStatus>(normalized, ignoreCase: true, out var result);
```

Multiple values are accepted as comma-separated: `?bookingStatus=checked_in,overstayed`

---

## 9. PageSize Safety Valve

Always clamp the client-provided `pageSize` to a maximum. Without this, a client can
request `pageSize=999999` and force a full-table load per request:

```csharp
private const int MaxPageSize = 100;
set => _pageSize = value < 1 ? 1 : value > MaxPageSize ? MaxPageSize : value;
```

---

## 10. API Reference

### Endpoint

```
GET /api/coworking/frontdesk/bookings
Authorization: Bearer {token}   (Admin or Frontdesk role)
```

### Query Parameters

| Parameter        | Type          | Default          | Description |
|------------------|---------------|------------------|-------------|
| `page`           | int           | `1`              | Page number (1-based) |
| `pageSize`       | int           | `20` (max `100`) | Records per page |
| `search`         | string        | —                | Searches reference number, guest full name, guest phone |
| `bookingStatus`  | string        | —                | Comma-separated snake_case values |
| `paymentStatus`  | string        | —                | Comma-separated snake_case values |
| `resourceId`     | UUID          | —                | Exact resource filter |
| `servicePackageId` | UUID        | —                | Exact package filter |
| `dateRange`      | string        | —                | `today` \| `this_week` \| `this_month` \| `this_year` \| `custom` |
| `dateFrom`       | `YYYY-MM-DD`  | —                | Required when `dateRange=custom` |
| `dateTo`         | `YYYY-MM-DD`  | —                | Optional; defaults to `dateFrom` |
| `sortBy`         | string        | smart default    | `scheduled_start` \| `scheduled_end` \| `actual_start` \| `actual_end` \| `created_at` |
| `sortDir`        | string        | `asc`            | `asc` \| `desc` |

**`bookingStatus` values:** `pending` \| `confirmed` \| `checked_in` \| `checked_out` \|
`cancelled` \| `no_show` \| `overstayed` \| `pending_student_verification`

**`paymentStatus` values:** `unpaid` \| `downpayment_paid` \| `partially_paid` \| `fully_paid`

### Response Shape

```json
{
  "items": [
    {
      "bookingId": "uuid",
      "referenceNumber": "BKG-0001",
      "guestName": "Raniel Montebon",
      "guestPhone": "09916472453",
      "resourceName": "Hot Desk A",
      "bookingStatus": "checked_in",
      "paymentStatus": "unpaid",
      "scheduledStart": "2026-03-15T08:00:00Z",
      "scheduledEnd": "2026-03-15T10:00:00Z",
      "actualStart": "2026-03-15T08:03:00Z",
      "actualEnd": null,
      "snapshotPackageName": "Hourly",
      "totalAmount": 50.00,
      "balanceAmount": 50.00,
      "isStudent": false
    }
  ],
  "totalCount": 142,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### Example Requests

```
# Default view — unpaid + active sessions first
GET /api/coworking/frontdesk/bookings

# Today's bookings, most recent first
GET /api/coworking/frontdesk/bookings?dateRange=today&sortBy=scheduled_start&sortDir=desc

# Active sessions with outstanding balance
GET /api/coworking/frontdesk/bookings?bookingStatus=checked_in,overstayed&paymentStatus=unpaid,partially_paid

# Search by guest name
GET /api/coworking/frontdesk/bookings?search=raniel

# This month, filtered to a specific resource, page 2
GET /api/coworking/frontdesk/bookings?dateRange=this_month&resourceId=c9af7e7b-...&page=2

# Custom date range
GET /api/coworking/frontdesk/bookings?dateRange=custom&dateFrom=2026-03-01&dateTo=2026-03-15
```

---

## 11. Files

| File | Role |
|------|------|
| `Application/Common/Models/PagedResult.cs` | Generic pagination envelope |
| `Application/Services/Coworking/Request/BookingFilterRequest.cs` | Query param DTO |
| `Application/Services/Coworking/Response/BookingListItemResponse.cs` | List-row projection |
| `Application/Services/Coworking/Query/GetBookingsQuery.cs` | Query + handler |
| `WebApi/Controllers/CoworkingFrontDeskController.cs` | `GET /bookings` endpoint |

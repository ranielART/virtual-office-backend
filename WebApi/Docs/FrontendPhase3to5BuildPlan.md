# Frontend Build Plan — Phase 3–5 (FrontDesk Walk-In Module)

> **Backend reference:** `WebApi/Docs/CoworkingPhase3to5Guide.md`
> **Testing reference:** `WebApi/Docs/Tutorials/WalkInBookingFlow.md`
> **Existing rules (apply here too):** `WebApi/Docs/FrontendCurrentBuildPlan.md`
> **Rule:** Each frontend phase must compile and run without errors before starting the next.

---

## Read Before Building

These rules prevent the most common misalignments with the backend.

### DateTime
All `DateTime` values from the API are **UTC**. Display them in the user's local timezone. For the Philippines (UTC+8), `2026-03-11T01:00:00Z` displays as `2026-03-11 09:00 PHT`. Use a shared `formatLocalTime(utcString)` utility — do not inline `new Date()` calls on every component.

### Enum values
All enum fields travel as **snake_case strings**. Examples: `"checked_in"`, `"image_attachment"`, `"pending_validation"`. Add the new enum groups below to `src/shared/constants/enums.ts` before building any component that uses them.

New entries to add:
```typescript
export const BookingType = {
  WALK_IN: 'walk_in',
  ONLINE:  'online',
} as const

export const BookingStatus = {
  CHECKED_IN:  'checked_in',
  CHECKED_OUT: 'checked_out',
  OVERSTAYED:  'overstayed',
  CANCELLED:   'cancelled',
  NO_SHOW:     'no_show',
  PENDING:     'pending',
  CONFIRMED:   'confirmed',
} as const

export const PaymentStatus = {
  UNPAID:          'unpaid',
  PARTIALLY_PAID:  'partially_paid',
  FULLY_PAID:      'fully_paid',
} as const

export const PaymentMethod = {
  CASH:             'cash',
  IMAGE_ATTACHMENT: 'image_attachment',
  // Reserved for future phases — do not render in walk-in UI:
  // GCASH, MAYA, BANK_TRANSFER, CARD, PAYMONGO
} as const

export const PaymentType = {
  BALANCE:      'balance',
  OVERSTAY_FEE: 'overstay_fee',
  DOWNPAYMENT:  'downpayment',
} as const

export const PaymentValidationStatus = {
  NOT_REQUIRED:       'not_required',
  PENDING_VALIDATION: 'pending_validation',
  VALIDATED:          'validated',
  REJECTED:           'rejected',
} as const
```

### Multipart/form-data (proof upload)
The `POST /api/coworking/frontdesk/payments/submit-proof` endpoint expects `multipart/form-data`. Use the browser's native `FormData` object — **do not set `Content-Type` manually**. Axios will set it automatically with the correct boundary when `FormData` is the request body.

```typescript
const form = new FormData()
form.append('bookingId', bookingId)
form.append('paymentType', 'balance')
form.append('amount', String(amount))
form.append('file', file) // File object from <input type="file" />
// guestProvidedReference is optional — append only if truthy
await axios.post('/api/coworking/frontdesk/payments/submit-proof', form)
```

### Time-out body is optional
`POST /api/coworking/frontdesk/time-out/{bookingId}` accepts an **optional JSON body**. For normal checkout, send no body at all (or omit the Content-Type header). Only send a body when processing a corrected/late time-out with `actualEndOverride`.

### Payment method for proof upload
`PaymentMethod` is **never selectable by the user** on the proof upload form. The backend always stores `image_attachment`. The form only asks for `paymentType` (balance or overstay_fee), `amount`, and an optional `guestProvidedReference` (the reference number on the screenshot).

### Query key additions to `queryKeys.ts`

```typescript
frontdesk: {
  sessions: {
    active: ['frontdesk', 'sessions', 'active'] as const,
  },
  bookings: {
    byDate: (date: string) => ['frontdesk', 'bookings', 'date', date] as const,
    detail:  (id: string)  => ['frontdesk', 'bookings', 'detail', id]  as const,
  },
  payments: {
    pending: ['frontdesk', 'payments', 'pending'] as const,
  },
},
coworking: {
  availability: (params: object) => ['coworking', 'availability', params] as const,
  walkInAvailability: (resourceId: string, hours: number) =>
    ['coworking', 'availability', resourceId, hours] as const,
},
```

---

## Frontend Phase F — FrontDesk Dashboard (Active Sessions)

**Delivers:** FrontDesk dashboard is no longer a placeholder. It shows a live list of all currently checked-in sessions, auto-refreshed every 30 seconds.

### New files

- `src/features/frontdesk/sessions/types/session.types.ts`
- `src/features/frontdesk/sessions/api/sessionsApi.ts`
- `src/features/frontdesk/sessions/hooks/useActiveSessions.ts`
- `src/features/frontdesk/sessions/components/ActiveSessionsTable.tsx`
- `src/features/frontdesk/sessions/components/BookingStatusBadge.tsx`
- `src/features/frontdesk/sessions/components/PaymentStatusBadge.tsx`
- `src/shared/lib/dateUtils.ts` — `formatLocalTime(utcString)`, `formatLocalDate(utcString)`, `formatDuration(startUtc, endUtc?)`

### Update files

- `src/app/(frontdesk)/frontdesk/dashboard/page.tsx` — render `ActiveSessionsTable`
- `src/shared/constants/enums.ts` — add all new enum groups listed above
- `src/shared/constants/queryKeys.ts` — add `frontdesk.*` and `coworking.*` keys

### Endpoint

#### GET /api/coworking/frontdesk/sessions/active

No query params. Requires `Authorization: Bearer <token>`.

Response:
```json
[
  {
    "bookingId": "uuid",
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
  }
]
```

`actualStart` is always set (non-null) on `checked_in` sessions. `scheduledStart` and `scheduledEnd` are the booked window in UTC.

**Hook:**
```typescript
export function useActiveSessions() {
  return useQuery({
    queryKey: queryKeys.frontdesk.sessions.active,
    queryFn: () => sessionsApi.getActive(),
    refetchInterval: 30_000, // auto-refresh every 30 seconds
  })
}
```

### Types

```typescript
// session.types.ts
export interface BookingSession {
  bookingId:           string
  referenceNumber:     string
  guestName:           string
  resourceName:        string
  bookingStatus:       BookingStatusValue
  paymentStatus:       PaymentStatusValue
  scheduledStart:      string  // UTC ISO string
  scheduledEnd:        string  // UTC ISO string
  actualStart:         string | null
  snapshotPackageName: string
  totalAmount:         number
  balanceAmount:       number
}
```

### Status badge colours

| `bookingStatus` | Colour |
|---|---|
| `checked_in` | green |
| `checked_out` | grey |
| `overstayed` | orange / amber |
| `cancelled` | red |
| `no_show` | red |

| `paymentStatus` | Colour |
|---|---|
| `unpaid` | red |
| `partially_paid` | yellow |
| `fully_paid` | green |

Both `BookingStatusBadge` and `PaymentStatusBadge` take the raw snake_case string and render the correct label + colour. Define them once here — they are reused in every phase.

### Verified when

- Dashboard shows 0 rows with no active bookings
- After a time-in (Phase G), the new session appears within 30 seconds
- After a time-out (Phase H), the session disappears within 30 seconds
- All DateTime values display in local time (09:00 PHT, not 01:00 UTC)

---

## Frontend Phase G — Walk-In Check-In

**Delivers:** FrontDesk can check in a walk-in guest through a two-step form. Step 1 collects the booking details (seat, package, hours). Step 2 collects guest identity. Submission calls the time-in endpoint and shows a receipt on success.

### New files

- `src/features/frontdesk/walkin/types/walkin.types.ts`
- `src/features/frontdesk/walkin/api/walkinApi.ts`
- `src/features/frontdesk/walkin/schemas/walkInSchema.ts` — zod schema for the full form
- `src/features/frontdesk/walkin/hooks/useWalkInAvailability.ts`
- `src/features/frontdesk/walkin/hooks/useTimeIn.ts`
- `src/features/frontdesk/walkin/components/WalkInForm.tsx` — step-1 + step-2 form
- `src/features/frontdesk/walkin/components/WalkInReceipt.tsx` — shown after successful time-in

### Update files

- `src/app/(frontdesk)/frontdesk/walk-in/page.tsx` — render `WalkInForm`

### Endpoints

#### GET /api/coworking/availability/{resourceId}?estimatedHours={n}

No auth required. Used to show a live availability badge when the FrontDesk selects a seat and enters hours. Call this whenever `resourceId` or `estimatedHours` changes (debounced — 500 ms).

Query param: `estimatedHours` — decimal number (e.g., `3`, `1.5`).

Response:
```json
{
  "resourceId": "uuid",
  "resourceName": "Seat 07",
  "isAvailable": true,
  "nextAvailableAt": null
}
```

When `isAvailable = false`, `nextAvailableAt` is a UTC ISO string. Display it in local time: "Next available at 3:00 PM".

#### GET /api/public/resources?isActive=true

Already implemented in Phase D. Call it (no auth needed) to populate the resource dropdown/grid. Filter by `resourceType` on the client side to show only `coworking_seats` initially, or allow switching type.

#### GET /api/public/packages?isActive=true

Already implemented in Phase E. Call it to populate the package dropdown. Filter on the client by `resourceType` matching the selected resource's type.

#### POST /api/coworking/frontdesk/time-in

Requires `Authorization: Bearer <token>`. Body:
```json
{
  "resourceId":       "uuid",
  "servicePackageId": "uuid",
  "estimatedHours":   3,
  "guestFirstName":   "Maria",
  "guestLastName":    "Santos",
  "guestPhone":       "09189876543",
  "guestEmail":       null,
  "isStudent":        true
}
```

Field notes:
- `estimatedHours` — decimal, must be > 0 and ≤ 24. For Day Pass, send 8 (or the package's `durationHours`).
- `guestEmail` — nullable string. Omit the field or send `null` if not collected. **Do not send an empty string** — the backend validates format when the value is present.
- `isStudent` — boolean. FrontDesk checks physical ID at the counter. Set `true` only after physical verification.

Response `200 OK`:
```json
{
  "bookingId":          "uuid",
  "referenceNumber":    "BKG-0001",
  "guestName":          "Maria Santos",
  "resourceName":       "Seat 12",
  "scheduledStart":     "2026-03-11T01:15:00Z",
  "scheduledEnd":       "2026-03-11T04:15:00Z",
  "snapshotPackageName":"Hourly — Regular",
  "isStudent":          true,
  "baseAmount":         150.00,
  "discountAmount":     30.00,
  "totalAmount":        120.00
}
```

`WalkInReceipt` displays this response. The receipt is the final step — show it after a successful mutation and include a "Done" button that resets the form. Note: `bookingId` from this response must be navigable to the booking detail page (Phase H).

### Form schema (zod)

```typescript
const walkInSchema = z.object({
  resourceId:       z.string().uuid(),
  servicePackageId: z.string().uuid(),
  estimatedHours:   z.number().gt(0).lte(24),
  guestFirstName:   z.string().min(1).max(100),
  guestLastName:    z.string().min(1).max(100),
  guestPhone:       z.string().regex(/^\d{10,11}$/, 'Must be 10 or 11 digits'),
  guestEmail:       z.string().email().nullable().optional(),
  isStudent:        z.boolean(),
})
```

### Error cases to handle in the UI

| HTTP | Condition | User-facing message |
|---|---|---|
| `409` | Resource just became occupied | "This seat was just taken. Please select another." |
| `400` | Validation error | Show field-level errors from `errors` array |
| `404` | Resource or package inactive/missing | "Selected resource or package is no longer available." |

### Cache invalidation on success

```
invalidate: queryKeys.frontdesk.sessions.active
invalidate: queryKeys.frontdesk.bookings.byDate(today)
```

### Verified when

- Selecting a resource + hours → availability badge updates (debounced)
- `isStudent = true` → receipt shows `discountAmount > 0` if package has student discount
- `guestEmail` left blank → form submits without error, `guestEmail` sent as `null`
- `guestPhone` with letters → validation error before submit
- After receipt shown → active sessions dashboard (Phase F) includes the new session

---

## Frontend Phase H — Bookings List, Session Detail, Time-Out, and Cash Payment

**Delivers:** FrontDesk can browse bookings by date, open a full booking detail view, process a normal time-out, process a corrected time-out with an override time, and record a cash payment.

### New files

- `src/features/frontdesk/bookings/types/booking.types.ts`
- `src/features/frontdesk/bookings/api/bookingsApi.ts`
- `src/features/frontdesk/bookings/hooks/useBookingsByDate.ts`
- `src/features/frontdesk/bookings/hooks/useBookingDetail.ts`
- `src/features/frontdesk/bookings/hooks/useTimeOut.ts`
- `src/features/frontdesk/bookings/hooks/useCashPayment.ts`
- `src/features/frontdesk/bookings/components/BookingsByDateTable.tsx`
- `src/features/frontdesk/bookings/components/BookingDetailCard.tsx`
- `src/features/frontdesk/bookings/components/TimeOutButton.tsx`
- `src/features/frontdesk/bookings/components/CorrectedTimeOutModal.tsx`
- `src/features/frontdesk/bookings/components/CashPaymentForm.tsx`
- `src/features/frontdesk/bookings/schemas/cashPaymentSchema.ts`
- `src/features/frontdesk/bookings/schemas/correctedTimeOutSchema.ts`

### Update files

- `src/app/(frontdesk)/frontdesk/bookings/page.tsx` — render `BookingsByDateTable` with date picker
- `src/app/(frontdesk)/frontdesk/bookings/[id]/page.tsx` — render `BookingDetailCard` + action buttons

### Endpoints

#### GET /api/coworking/frontdesk/bookings?date=YYYY-MM-DD

Query param: `date` in `YYYY-MM-DD` format. Returns all bookings for that date (all statuses).

Response: same `BookingSession[]` shape as the active sessions endpoint, but includes all statuses.

#### GET /api/coworking/frontdesk/bookings/{id}

Full booking detail including payment history.

Response:
```json
{
  "bookingId":          "uuid",
  "referenceNumber":    "BKG-0001",
  "bookingType":        "walk_in",
  "bookingStatus":      "checked_in",
  "paymentStatus":      "unpaid",
  "resourceName":       "Seat 07",
  "guestName":          "Juan Dela Cruz",
  "guestPhone":         "09171234567",
  "guestEmail":         "juan.dc@email.com",
  "isStudent":          false,
  "isStudentVerified":  false,
  "scheduledStart":     "2026-03-11T01:00:00Z",
  "scheduledEnd":       "2026-03-11T09:00:00Z",
  "actualStart":        "2026-03-11T01:00:00Z",
  "actualEnd":          null,
  "snapshotPackageName":"Day Pass",
  "baseAmount":         600.00,
  "discountAmount":     0.00,
  "overstayAmount":     0.00,
  "totalAmount":        600.00,
  "downpaymentAmount":  0.00,
  "balanceAmount":      600.00,
  "totalPaid":          0.00,
  "outstandingBalance": 600.00,
  "notes":              null,
  "payments": []
}
```

Key field notes:
- `actualEnd` is `null` while checked in; set after time-out.
- `overstayAmount` is `0.00` until time-out occurs past `scheduledEnd`.
- `outstandingBalance` = `totalAmount - totalPaid`. Render this as the amount still owed.
- `payments` is a list of `BookingPayment` objects (see type below).

#### POST /api/coworking/frontdesk/time-out/{bookingId}

**Normal checkout (no body):**
```typescript
await axios.post(`/api/coworking/frontdesk/time-out/${bookingId}`)
// No body, no Content-Type header needed
```

**Corrected checkout (with body):**
```typescript
await axios.post(`/api/coworking/frontdesk/time-out/${bookingId}`, {
  actualEndOverride: '2026-03-11T06:00:00Z', // must be UTC ISO 8601
  timeOutNotes: 'Guest left at 2:00 PM PHT. Processed late due to staff oversight.',
})
```

`actualEndOverride` rules (enforce in zod before submitting):
- Must be in the past (`new Date(value) <= new Date()`)
- Must be after the booking's `actualStart` (available from booking detail)
- When the FrontDesk inputs a local time in the corrected time-out form, convert it to UTC before submitting: `new Date(localInputValue).toISOString()`

Response (same for both normal and corrected): full `BookingDetail` object with updated `actualEnd`, `overstayAmount`, `totalAmount`, `balanceAmount`, `bookingStatus`.

#### POST /api/coworking/frontdesk/payment

Body:
```json
{
  "bookingId":     "uuid",
  "amount":        600.00,
  "paymentMethod": "cash",
  "paymentType":   "balance",
  "notes":         null
}
```

`paymentMethod` must be the string `"cash"` — send the constant from `PaymentMethod.CASH`. Response is empty `{}` on success. On success, refetch the booking detail.

### Types

```typescript
// booking.types.ts
export interface BookingDetail {
  bookingId:          string
  referenceNumber:    string
  bookingType:        BookingTypeValue
  bookingStatus:      BookingStatusValue
  paymentStatus:      PaymentStatusValue
  resourceName:       string
  guestName:          string
  guestPhone:         string
  guestEmail:         string | null
  isStudent:          boolean
  isStudentVerified:  boolean
  scheduledStart:     string   // UTC
  scheduledEnd:       string   // UTC
  actualStart:        string | null
  actualEnd:          string | null
  snapshotPackageName:string
  baseAmount:         number
  discountAmount:     number
  overstayAmount:     number
  totalAmount:        number
  downpaymentAmount:  number
  balanceAmount:      number
  totalPaid:          number
  outstandingBalance: number
  notes:              string | null
  payments:           BookingPayment[]
}

export interface BookingPayment {
  paymentId:               string
  amount:                  number
  paymentMethod:           PaymentMethodValue
  paymentType:             PaymentTypeValue
  paymentValidationStatus: PaymentValidationStatusValue
  proofOfPaymentUrl:       string | null
  paidAt:                  string | null   // UTC — null until validated (for image) or immediate (for cash)
  createdAt:               string          // UTC
}
```

### UI logic on booking detail

Show actions based on `bookingStatus`:

| `bookingStatus` | Available actions |
|---|---|
| `checked_in` | "Time Out" button (normal), "Corrected Time-Out" (collapse/modal) |
| `checked_out` | "Record Cash Payment" (if `outstandingBalance > 0`), "Submit Proof" link (Phase I) |
| `overstayed` | "Record Cash Payment" (if `outstandingBalance > 0`), "Submit Proof" link (Phase I) |
| `cancelled` / `no_show` | No actions — show status only |

**CorrectedTimeOutModal** fields:
- `actualEndOverride` — datetime-local input. Display in local time for FrontDesk input; convert to UTC on submit.
- `timeOutNotes` — textarea, max 500 chars, required when modal is submitted.

### Corrected time-out schema (zod)

```typescript
const correctedTimeOutSchema = z.object({
  actualEndOverride: z.string()
    .refine(v => !isNaN(Date.parse(v)), 'Invalid date')
    .refine(v => new Date(v) <= new Date(), 'Cannot be in the future'),
  timeOutNotes: z.string().min(1).max(500),
})
```

Note: `actualStart` context for the "must be after ActualStart" rule is available in the parent component from `BookingDetail`. Pass it as a `.refine()` at the schema level or validate in `onSubmit` before calling the mutation.

### Cache invalidation

| Action | Invalidate |
|---|---|
| Time-out | `bookings.detail(id)`, `sessions.active`, `bookings.byDate(today)` |
| Cash payment | `bookings.detail(id)` |

### Verified when

- Bookings page: date picker defaults to today; changing date reloads list
- Booking detail shows correct UTC→local timestamps
- Normal time-out: `bookingStatus` → `checked_out`, `overstayAmount = 0`
- Late time-out (overstay): `bookingStatus` → `overstayed`, `overstayAmount > 0`, `totalAmount` updated
- Corrected time-out modal: submitting a future time → validation error, does not call API
- Cash payment: after submit, `paymentStatus` → `fully_paid` (if `amount >= outstandingBalance`)
- `overstayed` booking paid in full → `bookingStatus` → `checked_out` (server auto-transitions)

---

## Frontend Phase I — Payment Proof Upload and Validation Queue

**Delivers:** FrontDesk can upload a payment screenshot from the booking detail page. The payments page shows a queue of all pending proofs. FrontDesk can open each proof image and approve or reject it.

### New files

- `src/features/frontdesk/payments/types/payment.types.ts`
- `src/features/frontdesk/payments/api/paymentsApi.ts`
- `src/features/frontdesk/payments/hooks/usePendingPayments.ts`
- `src/features/frontdesk/payments/hooks/useSubmitProof.ts`
- `src/features/frontdesk/payments/hooks/useValidatePayment.ts`
- `src/features/frontdesk/payments/components/PendingPaymentsTable.tsx`
- `src/features/frontdesk/payments/components/ProofUploadForm.tsx`
- `src/features/frontdesk/payments/components/ValidatePaymentModal.tsx`
- `src/features/frontdesk/payments/schemas/proofUploadSchema.ts`
- `src/features/frontdesk/payments/schemas/validatePaymentSchema.ts`

### Update files

- `src/app/(frontdesk)/frontdesk/payments/page.tsx` — render `PendingPaymentsTable`
- `src/app/(frontdesk)/frontdesk/bookings/[id]/page.tsx` — add `ProofUploadForm` for `checked_out` / `overstayed` bookings with `outstandingBalance > 0`

### Endpoints

#### POST /api/coworking/frontdesk/payments/submit-proof

`multipart/form-data`. Requires `Authorization: Bearer <token>`.

Form fields:
| Field | Type | Notes |
|---|---|---|
| `bookingId` | string (UUID) | |
| `paymentType` | string | `"balance"` or `"overstay_fee"` |
| `amount` | string (numeric) | `FormData` values are strings — convert `number` to `String(amount)` |
| `guestProvidedReference` | string | Optional. The reference number printed on the screenshot. |
| `file` | `File` | JPEG or PNG only, max 5 MB |

```typescript
// correct construction — do NOT set Content-Type header
const form = new FormData()
form.append('bookingId', bookingId)
form.append('paymentType', paymentType)
form.append('amount', String(amount))
if (guestProvidedReference) form.append('guestProvidedReference', guestProvidedReference)
form.append('file', file)

await axios.post('/api/coworking/frontdesk/payments/submit-proof', form)
```

Response: `200 OK` with empty body on success.

#### GET /api/coworking/frontdesk/payments/pending

No query params. Returns all payments with `paymentValidationStatus = "pending_validation"`, ordered by `createdAt` ascending (oldest first).

Response:
```json
[
  {
    "paymentId":         "uuid",
    "bookingId":         "uuid",
    "referenceNumber":   "BKG-0001",
    "guestName":         "Juan Dela Cruz",
    "resourceName":      "Seat 07",
    "amount":            600.00,
    "paymentMethod":     "image_attachment",
    "paymentType":       "balance",
    "proofOfPaymentUrl": "https://<storage>.windows.net/payment-proofs/...",
    "createdAt":         "2026-03-11T09:05:00Z"
  }
]
```

`proofOfPaymentUrl` is a direct Azure Blob URL. Open it in a new tab or render it in a lightbox so FrontDesk can inspect the screenshot.

#### POST /api/coworking/frontdesk/payments/{paymentId}/validate

Body:
```json
{ "isApproved": true }
```

or

```json
{ "isApproved": false, "rejectionReason": "Amount on screenshot does not match." }
```

`rejectionReason` is **required** when `isApproved = false`. Max 500 characters. Response: `200 OK` with empty body.

### Types

```typescript
// payment.types.ts
export interface PendingPayment {
  paymentId:         string
  bookingId:         string
  referenceNumber:   string
  guestName:         string
  resourceName:      string
  amount:            number
  paymentMethod:     PaymentMethodValue
  paymentType:       PaymentTypeValue
  proofOfPaymentUrl: string
  createdAt:         string  // UTC
}
```

### Form schemas (zod)

```typescript
// proofUploadSchema.ts
const proofUploadSchema = z.object({
  paymentType:             z.enum(['balance', 'overstay_fee']),
  amount:                  z.number().gt(0),
  guestProvidedReference:  z.string().max(100).optional(),
  file: z
    .instanceof(File, { message: 'A file is required' })
    .refine(f => ['image/jpeg', 'image/png'].includes(f.type), 'Only JPEG or PNG allowed')
    .refine(f => f.size <= 5 * 1024 * 1024, 'File must not exceed 5 MB'),
})

// validatePaymentSchema.ts
const validatePaymentSchema = z.discriminatedUnion('isApproved', [
  z.object({ isApproved: z.literal(true) }),
  z.object({
    isApproved:      z.literal(false),
    rejectionReason: z.string().min(1).max(500),
  }),
])
```

### ValidatePaymentModal behaviour

- Opens when FrontDesk clicks "Approve" or "Reject" from the pending table row.
- Shows the proof image (`proofOfPaymentUrl`) at the top so FrontDesk can see it while deciding.
- "Approve" → submits `{ isApproved: true }` — no extra fields needed.
- "Reject" → reveals a `rejectionReason` textarea (required). FrontDesk must enter a reason before submitting.
- On success: row disappears from the pending table, `paymentValidationStatus` is updated on the booking detail.

### Cache invalidation

| Action | Invalidate |
|---|---|
| Submit proof | `bookings.detail(bookingId)`, `payments.pending` |
| Validate (approve or reject) | `payments.pending`, `bookings.detail` (if ID known) |

> The pending table does not expose `bookingId` for the invalidation key directly in the UI — you can pass it from the modal's data or do a broad invalidation: `invalidate(['frontdesk', 'bookings'])`.

### Error cases

| HTTP | Condition | User-facing message |
|---|---|---|
| `400` | Non-image file type | "Only JPEG and PNG files are accepted." |
| `400` | File larger than 5 MB | "File must not exceed 5 MB." |
| `409` | Payment already validated | "This payment was already reviewed." |
| `409` | Booking cancelled / no-show | "Cannot add payment to a cancelled booking." |

### Verified when

- Booking detail for a `checked_out` booking with balance > 0 shows the `ProofUploadForm`
- Submitting a valid PNG → success toast; booking detail `payments` list gains a new row with `paymentValidationStatus = "pending_validation"` and a non-null `proofOfPaymentUrl`
- Submitting a PDF or a file > 5 MB → client-side validation error, no API call made
- Payments page shows the newly uploaded proof in the queue
- Approving → proof disappears from queue; booking detail shows `paymentStatus = "fully_paid"` and `paidAt` is now set on the payment row
- Rejecting without a reason → zod error, form does not submit
- Rejecting with a reason → proof disappears from queue; booking `paymentStatus` unchanged; guest must resubmit

---

## Standing rules (inherited from Phase 1–2 plan)

- All enum values sent to the API use constants from `src/shared/constants/enums.ts` — never raw strings
- All forms use `react-hook-form` + `zod`; schemas live in a `schemas/` folder inside the feature
- Error toasts use `sonner`
- Route path: `(frontdesk)/frontdesk/bookings/[id]` renders the booking detail for both Phases H and I — do not create separate pages for time-out and proof upload; add them as conditional sections on the same detail page

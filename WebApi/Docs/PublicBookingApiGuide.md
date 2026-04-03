# Public Booking API — Frontend Integration Guide

Base URL: `https://{host}/api/bookings`

Both endpoints use `multipart/form-data` — **NOT** `application/json`. Do not set `Content-Type` manually; let the browser/fetch set it with the correct boundary.

Rate limit: **3 requests per hour per IP**. Exceeding returns `429 Too Many Requests`.

---

## 1. Coworking Space Booking

**`POST /api/bookings/coworking`**

### Form Fields

| Field | Type | Required | Constraints | Example |
|---|---|---|---|---|
| `ServicePackageId` | string (UUID) | ✅ | Must be an active package ID from `GET /api/public/packages` | `"a1b2c3d4-..."` |
| `Date` | string | ✅ | Format: `YYYY-MM-DD`. Must not be in the past (UTC) | `"2026-05-10"` |
| `StartTime` | string | ✅ | Format: `HH:mm`, 24-hour, UTC | `"09:00"` |
| `EndTime` | string | ⚠️ | Format: `HH:mm`, 24-hour, UTC. **Required only if the selected package is Hourly**. Ignored for fixed-duration packages | `"11:00"` |
| `FirstName` | string | ✅ | Max 100 chars | `"Juan"` |
| `LastName` | string | ✅ | Max 100 chars | `"dela Cruz"` |
| `Email` | string | ✅ | Valid email | `"juan@email.com"` |
| `PhoneCountryCode` | string | ✅ | Format: `+` followed by 1–4 digits | `"+63"` |
| `PhoneNumber` | string | ✅ | Digits only, 7–12 digits | `"9123456789"` |
| `IsStudent` | string | ✅ | `"true"` or `"false"` | `"false"` |
| `payment_screenshots` | file[] | ✅ | Max **2 files**. Accepted: `image/jpeg`, `image/png`, `image/webp`. Max **5 MB each** | — |

> **Date/Time are UTC.** The frontend must convert the user's local time to UTC before sending. Example: if the user picks 9:00 AM Manila time (UTC+8), send `StartTime = "01:00"`.

> **How to know if a package is Hourly:** The `GET /api/public/packages` response includes `packageType`. If `packageType === "hourly"`, `EndTime` is required. For all other types (`day_pass`, `half_day`, `full_day`, `weekly_pass`, `monthly_dedicated_desk`), omit `EndTime` — the end time is computed server-side from the package duration.

### Success Response `200 OK`

```json
{
  "bookingId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "referenceNumber": "BKG-0042",
  "guestName": "Juan dela Cruz",
  "packageName": "Day Pass",
  "scheduledStart": "2026-05-10T01:00:00Z",
  "scheduledEnd": "2026-05-10T09:00:00Z",
  "totalAmount": 600.00,
  "message": "Your booking has been received. We will confirm it once payment has been verified."
}
```

### What happens on submit
- Booking is created with status `pending` and payment status `unpaid`.
- Admin(s) receive an email with all booking details and the payment screenshots as **attachments**.
- Customer receives a confirmation email (no attachments).
- Admin validates the payment in the dashboard → booking status moves to `confirmed`.

---

## 2. Virtual Office Application

**`POST /api/bookings/virtual-office`**

### Form Fields

| Field | Type | Required | Constraints | Example |
|---|---|---|---|---|
| `FirstName` | string | ✅ | Max 100 chars | `"Juan"` |
| `LastName` | string | ✅ | Max 100 chars | `"dela Cruz"` |
| `Email` | string | ✅ | Valid email | `"juan@email.com"` |
| `PhoneCountryCode` | string | ✅ | Format: `+` followed by 1–4 digits | `"+63"` |
| `PhoneNumber` | string | ✅ | Digits only, 7–12 digits | `"9123456789"` |
| `City` | string | ✅ | Max 100 chars | `"Davao City"` |
| `Country` | string | ✅ | Max 100 chars | `"Philippines"` |
| `BusinessType` | number | ✅ | `1` = Sole Proprietorship, `2` = Partnership, `3` = Corporation | `1` |
| `NeedsBusinessAddress` | string | ✅ | `"true"` or `"false"` | `"true"` |
| `NeedsOccasionalSpace` | string | ✅ | `"true"` or `"false"` | `"false"` |
| `DurationMonths` | number | ✅ | Integer, minimum `1` | `3` |
| `business_documents` | file[] | ✅ | Max **5 files**. Accepted: `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp`, `.heic`, `.pdf`, `.doc`, `.docx`. Max **10 MB each** | — |
| `down_payment_screenshots` | file[] | ✅ | Max **2 files**. Accepted: `image/jpeg`, `image/png`, `image/webp`. Max **5 MB each** | — |

> **BusinessType must be sent as an integer** (`1`, `2`, or `3`), not a string label.

> **business_documents security:** The server validates extension + MIME type + file magic bytes. PDFs are scanned for embedded JavaScript/macros and rejected if found. Do not attempt to send executables or disguised files.

### Success Response `200 OK`

```json
{
  "applicationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "referenceNumber": "VO-20260510-A1B2C3D4",
  "applicantName": "Juan dela Cruz",
  "message": "Your virtual office application has been received. Our team will review it and respond within 24 hours."
}
```

### What happens on submit
- Application is created with status `pending`.
- Admin(s) receive an email with full applicant details and **all uploaded files as attachments** (business documents + payment proof).
- Customer receives a "under review" confirmation email stating to expect a response within 24 hours and that dashboard credentials will be sent upon approval.

---

## Error Responses

All errors follow the same ProblemDetails shape:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "ValidationException",
  "detail": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Request.Email": ["A valid email address is required."],
    "PaymentScreenshots": ["At least one payment screenshot is required."]
  }
}
```

| Status | Title | When |
|---|---|---|
| `400` | `ValidationException` | Missing/invalid fields or file constraint violations |
| `409` | `ConflictException` | No available resource for the selected package + time slot |
| `404` | `NotFoundException` | `ServicePackageId` does not exist or is inactive |
| `429` | — | Rate limit exceeded (3 per hour per IP) |
| `500` | `InternalServerError` | Unexpected server error |

---

## JavaScript Fetch Example

### Coworking Booking

```js
const form = new FormData();
form.append("ServicePackageId", "a1b2c3d4-0000-0000-0000-000000000000");
form.append("Date", "2026-05-10");
form.append("StartTime", "01:00");   // UTC
form.append("EndTime", "03:00");     // UTC — only needed for Hourly packages
form.append("FirstName", "Juan");
form.append("LastName", "dela Cruz");
form.append("Email", "juan@email.com");
form.append("PhoneCountryCode", "+63");
form.append("PhoneNumber", "9123456789");
form.append("IsStudent", "false");

// Attach each screenshot under the same field name
screenshotFiles.forEach(file => form.append("payment_screenshots", file));

const res = await fetch("/api/bookings/coworking", {
  method: "POST",
  body: form,
  // DO NOT set Content-Type — browser sets it with the boundary automatically
});

if (!res.ok) {
  const err = await res.json(); // ProblemDetails
  // err.errors contains per-field messages
}

const data = await res.json();
console.log(data.referenceNumber); // "BKG-0042"
```

### Virtual Office Application

```js
const form = new FormData();
form.append("FirstName", "Juan");
form.append("LastName", "dela Cruz");
form.append("Email", "juan@email.com");
form.append("PhoneCountryCode", "+63");
form.append("PhoneNumber", "9123456789");
form.append("City", "Davao City");
form.append("Country", "Philippines");
form.append("BusinessType", "1");           // 1 = Sole Proprietorship
form.append("NeedsBusinessAddress", "true");
form.append("NeedsOccasionalSpace", "false");
form.append("DurationMonths", "3");

businessDocFiles.forEach(file => form.append("business_documents", file));
proofFiles.forEach(file => form.append("down_payment_screenshots", file));

const res = await fetch("/api/bookings/virtual-office", {
  method: "POST",
  body: form,
});

const data = await res.json();
console.log(data.referenceNumber); // "VO-20260510-A1B2C3D4"
```

---

## Common Mistakes

| Mistake | Fix |
|---|---|
| Setting `Content-Type: multipart/form-data` manually | Remove it — let the browser set it with the correct boundary |
| Sending `IsStudent: true` (boolean) instead of `"true"` (string) | FormData always sends strings; use `form.append("IsStudent", "true")` |
| Sending `BusinessType: "SoleProprietorship"` | Send the integer: `"1"` |
| Sending times in local time | Convert to UTC before sending |
| Using field name `paymentScreenshots` for files | Must be exactly `payment_screenshots` (snake_case) |
| Using field name `businessDocuments` for files | Must be exactly `business_documents` (snake_case) |
| Using field name `downPaymentScreenshots` for files | Must be exactly `down_payment_screenshots` (snake_case) |
| Omitting `EndTime` for an Hourly package | Required for Hourly — server returns 409 ConflictException if missing |

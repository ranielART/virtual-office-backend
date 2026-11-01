# Frontend Build Plan — Phase 1 & 2 Backend Scope

> **Full reference:** `WebApi/Docs/NextJsFrontendGuide.md` — all code implementations, architecture decisions, and detailed explanations live there.
> **Goal:** Build a fully functional admin frontend for what the backend currently supports: auth, coworking resources, and service packages.
> **Rule:** Each frontend phase must compile and run without errors before starting the next.

---

## Frontend Phase A — Foundation
**Delivers:** App compiles. All routes resolve. Providers mounted. No runtime errors.

### Create
- `src/app/layout.tsx` — root layout, mounts `AppProviders`
- `src/shared/providers/AppProviders.tsx` — `ReduxProvider → QueryClientProvider → AuthSessionProvider → ThemeProvider`
- `src/shared/store/redux/store.ts` + `sidebarSlice.ts` + `notificationSlice.ts` + `appSettingsSlice.ts`
- `src/shared/store/auth/authStore.ts`
- `src/shared/lib/queryClient.ts`
- `src/shared/lib/axios.ts` — base config + request interceptor only (no 401 handler yet)
- `src/shared/constants/roles.ts` + `routes.ts` + `queryKeys.ts` + `enums.ts`
- `src/middleware.ts`
- `.env.local` — `NEXT_PUBLIC_API_URL=http://localhost:5000`

### Placeholder layouts (render children only)
- `src/app/(auth)/layout.tsx`
- `src/app/(admin)/layout.tsx`
- `src/app/(frontdesk)/layout.tsx`

### Placeholder pages (a div with the page name — nothing else)
- `(auth)/login/page.tsx`
- `(admin)/admin/dashboard/page.tsx`
- `(admin)/admin/resources/page.tsx` + `[id]/page.tsx`
- `(admin)/admin/packages/page.tsx` + `[id]/page.tsx`
- `(admin)/admin/settings/page.tsx`
- `(frontdesk)/frontdesk/dashboard/page.tsx`
- `(frontdesk)/frontdesk/settings/page.tsx`
- `app/unauthorized/page.tsx`

### Verified when
- `npm run dev` — no errors
- All routes return 200
- Redux DevTools shows `sidebar`, `notifications`, `appSettings`
- TanStack Query DevTools visible

---

## Frontend Phase B — Auth
**Delivers:** Full login → dashboard redirect → silent refresh on reload → logout.

### Create
- `src/app/api/auth/login/route.ts`
- `src/app/api/auth/refresh/route.ts`
- `src/app/api/auth/logout/route.ts`
- `src/features/auth/schemas/loginSchema.ts`
- `src/features/auth/types/auth.types.ts`
- `src/features/auth/components/LoginForm.tsx`
- `src/features/auth/components/AuthSessionProvider.tsx`
- `src/features/auth/hooks/useLogin.ts`
- `src/features/auth/hooks/useLogout.ts`
- `src/shared/hooks/useAuth.ts`
- `src/shared/hooks/useRole.ts`

### Update
- `src/shared/lib/axios.ts` — add 401 response interceptor with refresh queue
- `src/shared/providers/AppProviders.tsx` — add `AuthSessionProvider`
- `src/app/(auth)/layout.tsx` — centered card layout
- `src/app/(auth)/login/page.tsx` — render `LoginForm`

### Endpoints

#### POST /api/auth/login

.NET request body:
```json
{
  "email": "admin@virtualoffice.com",
  "password": "Admin@123456"
}
```

.NET response body:
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "dGhp..."
}
```

.NET also sets cookie on the response: `refreshToken` — httpOnly, Secure, SameSite=None, expires 7 days.

**What the Next.js proxy does:**
- Calls .NET login with the credentials
- Receives `{ accessToken, refreshToken }` from .NET
- Decodes the JWT to extract user info (id, email, role) — the .NET does not return user info in the body
- Sets its own `refreshToken` cookie (httpOnly, Secure, SameSite=Strict, Path=/api/auth) using the value from .NET response
- Sets `has_session=true` and `user_role={role}` cookies (non-httpOnly)
- Returns to the browser:
```json
{
  "accessToken": "eyJ...",
  "user": {
    "id": "...",
    "email": "admin@virtualoffice.com",
    "role": "Admin",
    "firstName": "...",
    "lastName": "..."
  }
}
```

> User info (firstName, lastName) must be decoded from the JWT claims, since .NET does not return them in the login response body. Ensure the token service includes these claims when generating the access token, or omit them from the proxy response and fetch them separately if needed.

---

#### POST /api/auth/refresh

The .NET refresh endpoint requires BOTH the expired access token AND the refresh token in the body. It uses the expired access token to extract the JWT ID (jti) for token rotation validation.

**How the proxy handles this:**
- Browser calls `POST /api/auth/refresh` — Axios interceptor sends the expired access token in the body
- Proxy reads `refreshToken` from its own httpOnly cookie
- Proxy calls .NET with:
```json
{
  "accessToken": "eyJ...(expired)",
  "refreshToken": "dGhp..."
}
```

.NET response body:
```json
{
  "accessToken": "eyJ...(new)",
  "refreshToken": "dGhp...(new)"
}
```

.NET also sets a new `refreshToken` cookie.

**What the proxy returns to the browser:**
```json
{
  "accessToken": "eyJ...(new)"
}
```

Proxy also updates its own `refreshToken` httpOnly cookie with the new value from the .NET response.

> The Axios 401 interceptor must include the current (expired) access token when calling the proxy refresh route. The proxy cannot call .NET without it.

---

#### POST /api/auth/logout

The .NET logout reads `refreshToken` directly from its own cookie (`Request.Cookies["refreshToken"]`). Since the proxy makes a server-to-server call to .NET, the cookie will not be forwarded automatically. The proxy must pass the token manually.

**What the proxy does:**
- Reads `refreshToken` from its own httpOnly cookie
- Calls .NET `POST /api/auth/logout` and manually sets the `refreshToken` cookie header on the request to .NET
- Clears its own `refreshToken`, `has_session`, and `user_role` cookies
- Returns to browser:
```json
{
  "message": "Logged out successfully"
}
```

---

### Verified when
- Login with valid credentials → redirects to `/admin/dashboard` (Admin) or `/frontdesk/dashboard` (FrontDesk)
- Page refresh → session silently restored, still authenticated
- Logout → `/login`, all cookies cleared
- Unauthenticated access to `/admin/**` → redirect to `/login?returnTo=...`
- FrontDesk accessing `/admin/**` → redirect to `/unauthorized`
- Admin accessing `/frontdesk/**` → allowed

---

## Frontend Phase C — Shell & Settings
**Delivers:** Full sidebar layouts for both roles. Role guards. Settings page functional.

### Create
- `src/shared/components/layout/AdminSidebar.tsx`
- `src/shared/components/layout/FrontDeskSidebar.tsx`
- `src/shared/components/layout/Topbar.tsx`
- `src/shared/components/layout/SidebarItem.tsx`
- `src/shared/components/guards/RoleGuard.tsx`
- `src/shared/components/guards/AuthGuard.tsx`
- `src/shared/components/settings/SettingsPanel.tsx`
- `src/shared/hooks/useSidebar.ts`
- `src/shared/hooks/useAppSettings.ts`
- `src/shared/providers/ThemeProvider.tsx`

### Update
- `src/app/(admin)/layout.tsx` — full shell: `AdminSidebar` + `Topbar` + `{children}`
- `src/app/(frontdesk)/layout.tsx` — full shell: `FrontDeskSidebar` + `Topbar` + `{children}`
- `src/app/(admin)/admin/settings/page.tsx` — render `SettingsPanel`
- `src/app/(frontdesk)/frontdesk/settings/page.tsx` — render `SettingsPanel`
- `tailwind.config.ts` — `darkMode: 'class'`
- `src/app/globals.css` — `html { font-size: var(--base-font-size, 16px); }` + reduce-motion CSS
- `src/shared/providers/AppProviders.tsx` — add `ThemeProvider`

### Sidebar nav
**Admin (top):** Dashboard · Resources · Packages
**Admin (bottom):** Settings · User info + Logout

**FrontDesk (top):** Dashboard · Sessions · Walk-in · Bookings · Payments
**FrontDesk (bottom):** Settings · User info + Logout

> FrontDesk pages other than Dashboard and Settings remain as placeholders from Phase A.

### Settings inventory
| Setting | Options | Persisted in |
|---------|---------|-------------|
| Theme | light / dark / system | `appSettingsSlice` → localStorage |
| Font Size | sm (14px) / md (16px) / lg (18px) | `appSettingsSlice` → localStorage |
| Compact Sidebar | on / off | `appSettingsSlice` → localStorage |
| Enable Animations | on / off | `appSettingsSlice` → localStorage |

### Verified when
- Both sidebars render with correct nav items per role
- Settings: toggle dark mode → page goes dark immediately and survives refresh
- Settings: change font size → text size changes immediately and survives refresh
- Reset to defaults → reverts to system theme, medium font

---

## Frontend Phase D — Admin Resources
**Delivers:** Full CRUD for coworking resources wired to the live backend.

### Create
- `src/features/admin/resources/types/resource.types.ts`
- `src/features/admin/resources/api/resourcesApi.ts`
- `src/features/admin/resources/hooks/useResources.ts` — exports `useResources(filters?)` and `useResourceById(id)`
- `src/features/admin/resources/hooks/useCreateResource.ts`
- `src/features/admin/resources/hooks/useUpdateResource.ts`
- `src/features/admin/resources/hooks/useToggleResource.ts`
- `src/features/admin/resources/components/ResourceTable.tsx`
- `src/features/admin/resources/components/ResourceForm.tsx`
- `src/features/admin/resources/components/ResourceToggleButton.tsx`

### Update
- `src/app/(admin)/admin/resources/page.tsx` — `ResourceTable` + create button/modal
- `src/app/(admin)/admin/resources/[id]/page.tsx` — `ResourceForm` (edit) + `ResourceToggleButton`

### Endpoints

#### GET /api/coworking/admin/resources
Query params (all optional): `?resourceType=coworking_seats` · `?isActive=true`

Response:
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Seat 01",
    "resourceType": "coworking_seats",
    "capacity": 1,
    "description": "Individual coworking seat #01",
    "isActive": true,
    "createdAt": "2026-03-07T08:00:00Z",
    "updatedAt": "2026-03-07T08:00:00Z"
  }
]
```

#### GET /api/coworking/admin/resources/{id}
Response: same shape as a single item above.
Error: `404` if not found.

#### POST /api/coworking/admin/resources
Request:
```json
{
  "name": "Seat 23",
  "resourceType": "coworking_seats",
  "capacity": 1,
  "description": "Optional."
}
```

Response: the created resource object (same shape as GET single).
Errors: `400` validation failed · `409` name already exists.

#### PUT /api/coworking/admin/resources/{id}
Only `name`, `capacity`, and `description` are updatable. `resourceType` cannot be changed after creation.
All three fields are optional — send only what changed.

Request:
```json
{
  "name": "Window Seat",
  "capacity": 2,
  "description": "Updated description."
}
```

Response: the updated resource object (same shape as GET single).
Errors: `400` · `404` · `409`.

#### PATCH /api/coworking/admin/resources/{id}/toggle
Request body: none.

Response:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Seat 01",
  "isActive": false,
  "message": "'Seat 01' has been disabled."
}
```

When re-enabling: `"message": "'Seat 01' has been enabled."`
Errors: `404` · `409` cannot disable while a guest is currently checked in.

### Cache invalidation
| Action | Invalidate |
|--------|-----------|
| Create | `queryKeys.resources.all` |
| Update | `queryKeys.resources.detail(id)` + `queryKeys.resources.all` |
| Toggle | `queryKeys.resources.all` + `queryKeys.resources.detail(id)` |

### Valid resourceType values
`coworking_seats` · `executive_room` · `collaboration_room`

### Verified when
- List shows all 22 seeded resources
- Filter by `resourceType` → list updates
- Create a resource → appears in list
- Edit a resource → changes reflected
- Toggle active → badge flips, message reflects direction (enabled/disabled)
- Duplicate name → conflict error shown

---

## Frontend Phase E — Admin Packages
**Delivers:** Full CRUD for service packages wired to the live backend.

### Create
- `src/features/admin/packages/types/package.types.ts`
- `src/features/admin/packages/api/packagesApi.ts`
- `src/features/admin/packages/hooks/usePackages.ts` — exports `usePackages(filters?)` and `usePackageById(id)`
- `src/features/admin/packages/hooks/useCreatePackage.ts`
- `src/features/admin/packages/hooks/useUpdatePackage.ts`
- `src/features/admin/packages/hooks/useTogglePackage.ts`
- `src/features/admin/packages/components/PackageTable.tsx`
- `src/features/admin/packages/components/PackageForm.tsx`
- `src/features/admin/packages/components/PackageToggleButton.tsx`

### Update
- `src/app/(admin)/admin/packages/page.tsx` — `PackageTable` + create button/modal
- `src/app/(admin)/admin/packages/[id]/page.tsx` — `PackageForm` (edit) + `PackageToggleButton`

### Endpoints

#### GET /api/coworking/admin/packages
Query params (all optional): `?resourceType=coworking_seats` · `?isActive=true`

Response:
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Coworking Seats — Hourly",
    "resourceType": "coworking_seats",
    "resourceTypeName": "CoworkingSeats",
    "packageType": "hourly",
    "packageTypeName": "Hourly",
    "priceAmount": 50.00,
    "durationHours": null,
    "durationDays": null,
    "maxCapacity": null,
    "isStudentEligible": true,
    "studentDiscountPercent": 10.00,
    "downpaymentPercent": 50.00,
    "isActive": true,
    "createdAt": "2026-03-07T08:00:00Z",
    "updatedAt": "2026-03-07T08:00:00Z"
  }
]
```

> `resourceTypeName` and `packageTypeName` are computed properties on the response — they are the raw enum `.ToString()` value (PascalCase), not the snake_case API string. Use `resourceType` and `packageType` for all comparisons and display logic.

#### GET /api/coworking/admin/packages/{id}
Response: same shape as a single item above.
Error: `404` if not found.

#### POST /api/coworking/admin/packages
Request:
```json
{
  "name": "Coworking Seats — Day Pass",
  "resourceType": "coworking_seats",
  "packageType": "day_pass",
  "priceAmount": 600.00,
  "durationHours": 10,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": true,
  "studentDiscountPercent": 10.00,
  "downpaymentPercent": 50.00
}
```

Response: the created package object (same shape as GET single).
Errors: `400` validation failed · `409` name already exists for this resourceType.

#### PUT /api/coworking/admin/packages/{id}
All fields are optional — send only what changed. Unlike resources, `resourceType` and `packageType` can also be updated.

Request:
```json
{
  "name": "Coworking Seats — Day Pass (Updated)",
  "resourceType": "coworking_seats",
  "packageType": "day_pass",
  "priceAmount": 650.00,
  "durationHours": 10,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": true,
  "studentDiscountPercent": 15.00,
  "downpaymentPercent": 50.00
}
```

Response: the updated package object (same shape as GET single).
Errors: `400` · `404` · `409`.

#### PATCH /api/coworking/admin/packages/{id}/toggle
Request body: none.

Response:
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Coworking Seats — Day Pass",
  "isActive": false,
  "message": "'Coworking Seats — Day Pass' has been deactivated."
}
```

When re-enabling: `"message": "'Coworking Seats — Day Pass' has been activated."`
Error: `404` if not found.

### Cache invalidation
| Action | Invalidate |
|--------|-----------|
| Create | `queryKeys.packages.all` |
| Update | `queryKeys.packages.detail(id)` + `queryKeys.packages.all` |
| Toggle | `queryKeys.packages.all` + `queryKeys.packages.detail(id)` |

### Valid enum values
**packageType:** `hourly` · `day_pass` · `half_day` · `full_day` · `weekly_pass` · `monthly_dedicated_desk`

### Form conditional fields
| Condition | Show field |
|-----------|-----------|
| `packageType` is `hourly` | `durationHours` |
| `packageType` is `day_pass` / `weekly_pass` / `monthly_dedicated_desk` | `durationDays` |
| `isStudentEligible` is true | `studentDiscountPercent` |

### Verified when
- List shows all 11 seeded packages
- Filter by `resourceType` or `isActive` → list updates
- Create, edit, toggle — same verification pattern as Phase D
- Conditional form fields show/hide correctly per `packageType`

---

## Standing rules across all phases
- All enum values sent to the API use snake_case strings from `src/shared/constants/enums.ts` — never raw strings
- All forms use `react-hook-form` + `zod`. Schemas live in a `schemas/` folder inside the feature
- Error toasts use `sonner`
- Dashboard pages remain as placeholders after Phase E — real widgets come with Phase 4 backend
- Do not build any FrontDesk operational pages (sessions, walk-in, bookings, payments) — backend not ready

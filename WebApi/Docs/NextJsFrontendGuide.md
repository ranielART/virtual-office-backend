# Next.js Frontend — Authentication & Architecture Guide

**Project:** CommunSide — Virtual Office Booking System
**Frontend Stack:** Next.js 14+ (App Router) · TypeScript · Tailwind CSS · Zustand · Redux Toolkit · TanStack Query · Axios
**Backend:** .NET 9 REST API with JWT (access token + refresh token)

> This document is the authoritative implementation guide for the Next.js frontend. It covers project structure, authentication architecture, token management, route protection, state management, and sidebar layout for the Admin and FrontDesk roles. The landing page folder is allocated but not implemented here.

---

## ⚡ CURRENT BUILD SCOPE — Build These Now

> **This section defines what to build in the current sprint.** The rest of this document covers the full architecture including future phases — do not wire up anything marked `PHASE N` until the backend for that phase is complete. Everything below is backed by a finished, testable backend endpoint.

### What the backend has finished (Phase 1 & 2)

| # | Area | Backend status | Frontend target |
|---|------|---------------|-----------------|
| 1 | **Authentication** — login, logout, token refresh | ✅ Done | `/login` page · auth proxy routes · Zustand store · silent refresh |
| 2 | **Admin — Coworking Resources** — list, get by ID, create, update, toggle active | ✅ Done | `/admin/resources` · `/admin/resources/[id]` |
| 3 | **Admin — Service Packages** — list, get by ID, create, update, toggle active | ✅ Done | `/admin/packages` · `/admin/packages/[id]` |
| 4 | **Settings** — theme, font size, compact sidebar, animations | ✅ Client-side only | `/admin/settings` · `/frontdesk/settings` |

### What to skip for now (backend not yet built)

| Area | Reason |
|------|--------|
| Admin — Users page | No user management endpoints yet |
| Admin — Bookings page | Booking CRUD is Phase 4 |
| FrontDesk — all pages | Sessions, walk-in, payments are Phase 4–5 |
| Availability endpoints | Phase 3 |
| FrontDesk dashboard | Depends on Phase 4+ data |

### Pages to build now

```
/login                   ← Auth entry point
/admin/dashboard         ← Placeholder only (no real data yet)
/admin/resources         ← Full CRUD — backend ready
/admin/resources/[id]    ← Full CRUD — backend ready
/admin/packages          ← Full CRUD — backend ready
/admin/packages/[id]     ← Full CRUD — backend ready
/admin/settings          ← Client-side only — no backend needed
/frontdesk/dashboard     ← Placeholder only
/frontdesk/settings      ← Client-side only — no backend needed
/unauthorized            ← Static page
```

> All other routes defined in Section 3 (folder structure) should exist as **placeholder pages** — a layout shell with a "Coming soon" notice. This keeps the routing and middleware intact without wiring up missing APIs.

### Implementation order for the current scope

Follow this sequence. Each step is independently testable.

| Step | Task |
|------|------|
| 1 | Project init, folder structure, placeholder pages |
| 2 | `AppProviders` — Zustand, Redux, TanStack Query, ThemeProvider |
| 3 | Axios instance with request interceptor |
| 4 | Next.js API proxy routes: `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout` |
| 5 | Middleware — route and role protection |
| 6 | `authStore` (Zustand) + `AuthSessionProvider` — silent refresh on page load |
| 7 | Axios response interceptor — 401 → refresh → retry |
| 8 | Login page + `LoginForm` + `useLogin` mutation |
| 9 | `useLogout` + logout button wired into sidebar |
| 10 | Admin sidebar layout + FrontDesk sidebar layout (both with Settings link) |
| 11 | `appSettingsSlice` + `ThemeProvider` + Settings pages |
| 12 | `RoleGuard` + `AuthGuard` |
| 13 | Admin Resources pages — `useResources`, `useCreateResource`, `useUpdateResource`, `useToggleResource` |
| 14 | Admin Packages pages — `usePackages`, `useCreatePackage`, `useUpdatePackage`, `useTogglePackage` |
| 15 | Admin dashboard placeholder, FrontDesk dashboard placeholder |

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Tech Stack Decisions](#2-tech-stack-decisions)
3. [Folder Structure](#3-folder-structure)
4. [Route Groups & Page Map](#4-route-groups--page-map)
5. [Token Storage Strategy](#5-token-storage-strategy)
6. [Next.js API Routes — Auth Proxy](#6-nextjs-api-routes--auth-proxy)
7. [Middleware — Route & Role Protection](#7-middleware--route--role-protection)
8. [Zustand — Auth Store](#8-zustand--auth-store)
9. [Redux Toolkit — Global App Store](#9-redux-toolkit--global-app-store)
10. [Axios Instance & Interceptors](#10-axios-instance--interceptors)
11. [TanStack Query Setup](#11-tanstack-query-setup)
12. [Login Flow](#12-login-flow)
13. [Silent Refresh Flow](#13-silent-refresh-flow)
14. [Logout Flow](#14-logout-flow)
15. [Sidebar Layout](#15-sidebar-layout)
16. [Role-Based Component Guards](#16-role-based-component-guards)
17. [Security Hardening](#17-security-hardening)
18. [Implementation Order](#18-implementation-order)
19. [API Reference — All Endpoints](#19-api-reference--all-endpoints)
20. [Settings — Appearance & Preferences](#20-settings--appearance--preferences)

---

## 1. Architecture Overview

```
Browser
  │
  ├── Landing Page  (/)              → public, no auth
  ├── Login         (/login)         → public, redirects if already logged in
  ├── Admin         (/admin/*)       → requires Admin role
  └── FrontDesk     (/frontdesk/*)   → requires Admin or FrontDesk role

Next.js Middleware (edge, runs before every request)
  → reads non-sensitive session cookies
  → redirects unauthenticated or wrong-role users before any page renders

Next.js API Routes (/api/auth/*)
  → proxy between browser and .NET backend
  → sets and clears httpOnly cookies (refresh token, session indicator)
  → browser never directly calls the .NET auth endpoints

Axios (client)
  → attaches access token from Zustand (in-memory)
  → on 401: triggers silent refresh via /api/auth/refresh
  → queues concurrent requests during refresh to prevent race conditions

State layers:
  Zustand        → access token (in-memory), user object, auth status
  Redux Toolkit  → sidebar state, global notifications, admin UI state
  TanStack Query → all server data (resources, packages, bookings, sessions)
```

### Why proxy through Next.js API routes?

The `.NET` backend sets tokens in the response body. The Next.js API routes intercept the login response and translate the refresh token into an `httpOnly` cookie — a cookie that JavaScript in the browser cannot read. This eliminates the ability of any XSS attack to steal the refresh token because no client-side code ever sees it. The access token lives only in Zustand memory and is lost on page refresh, which is intentional — recovery happens transparently via the silent refresh flow.

---

## 2. Tech Stack Decisions

| Concern | Tool | Why |
|---------|------|-----|
| Framework | Next.js 14+ App Router | Server components, middleware, API routes, file-based routing |
| Language | TypeScript (strict mode) | Type safety, better DX, fewer runtime bugs |
| Styling | Tailwind CSS | Utility-first, no CSS files, consistent design system |
| Auth state | Zustand | Lightweight, synchronous in-memory store for access token |
| Global UI state | Redux Toolkit | Sidebar, notifications, complex cross-feature state with devtools |
| Server state | TanStack Query v5 | Caching, deduplication, background refetch, optimistic updates |
| HTTP client | Axios | Interceptors for token attachment and 401 handling |
| Forms | react-hook-form + Zod | Schema-first validation, minimal re-renders |
| Icons | lucide-react | Clean, consistent icon set |
| Component primitives | shadcn/ui | Accessible, Tailwind-native, unstyled base components |

---

## 3. Folder Structure

```
src/
├── app/
│   ├── (marketing)/                  # Landing page route group — no auth required
│   │   ├── _layout.tsx               # Marketing layout (navbar, footer)
│   │   ├── page.tsx                  # / → Landing page (placeholder)
│   │   ├── about/
│   │   │   └── page.tsx              # /about (placeholder)
│   │   └── contact/
│   │       └── page.tsx              # /contact (placeholder)
│   │
│   ├── (auth)/                       # Auth pages — redirects logged-in users
│   │   ├── _layout.tsx               # Centered card layout
│   │   └── login/
│   │       └── page.tsx              # /login
│   │
│   ├── (admin)/                      # Admin-only routes
│   │   ├── _layout.tsx               # Admin shell: sidebar + topbar
│   │   └── admin/
│   │       ├── dashboard/
│   │       │   └── page.tsx          # /admin/dashboard
│   │       ├── resources/
│   │       │   ├── page.tsx          # /admin/resources — list
│   │       │   └── [id]/
│   │       │       └── page.tsx      # /admin/resources/:id — detail
│   │       ├── packages/
│   │       │   ├── page.tsx          # /admin/packages — list
│   │       │   └── [id]/
│   │       │       └── page.tsx      # /admin/packages/:id — detail
│   │       ├── bookings/
│   │       │   ├── page.tsx          # /admin/bookings
│   │       │   └── [id]/
│   │       │       └── page.tsx      # /admin/bookings/:id
│   │       ├── users/
│   │       │   └── page.tsx          # /admin/users — user management
│   │       └── settings/
│   │           └── page.tsx          # /admin/settings — appearance & preferences
│   │
│   ├── (frontdesk)/                  # FrontDesk routes (Admin can also access)
│   │   ├── _layout.tsx               # FrontDesk shell: sidebar + topbar
│   │   └── frontdesk/
│   │       ├── dashboard/
│   │       │   └── page.tsx          # /frontdesk/dashboard
│   │       ├── sessions/
│   │       │   ├── page.tsx          # /frontdesk/sessions — active sessions
│   │       │   └── [id]/
│   │       │       └── page.tsx      # /frontdesk/sessions/:id
│   │       ├── time-in/
│   │       │   └── page.tsx          # /frontdesk/time-in — walk-in check-in
│   │       ├── bookings/
│   │       │   ├── page.tsx          # /frontdesk/bookings — by date
│   │       │   └── [id]/
│   │       │       └── page.tsx      # /frontdesk/bookings/:id — detail
│   │       ├── payments/
│   │       │   ├── page.tsx          # /frontdesk/payments/pending
│   │       │   └── [id]/
│   │       │       └── page.tsx      # /frontdesk/payments/:id — validate
│   │       └── settings/
│   │           └── page.tsx          # /frontdesk/settings — appearance & preferences
│   │
│   ├── unauthorized/
│   │   └── page.tsx                  # /unauthorized — wrong role page
│   │
│   └── api/
│       └── auth/
│           ├── login/
│           │   └── route.ts          # POST /api/auth/login
│           ├── refresh/
│           │   └── route.ts          # POST /api/auth/refresh
│           └── logout/
│               └── route.ts          # POST /api/auth/logout
│
├── features/
│   ├── auth/
│   │   ├── api/
│   │   │   └── authApi.ts            # login(), logout(), refreshToken()
│   │   ├── components/
│   │   │   ├── LoginForm.tsx
│   │   │   └── AuthSessionProvider.tsx  # Hydrates Zustand on app load
│   │   ├── hooks/
│   │   │   ├── useLogin.ts           # useMutation wrapping authApi.login()
│   │   │   └── useLogout.ts
│   │   ├── schemas/
│   │   │   └── loginSchema.ts        # Zod schema for login form
│   │   └── types/
│   │       └── auth.types.ts
│   │
│   ├── admin/
│   │   ├── resources/
│   │   │   ├── api/
│   │   │   │   └── resourcesApi.ts
│   │   │   ├── components/
│   │   │   │   ├── ResourceTable.tsx
│   │   │   │   ├── ResourceForm.tsx
│   │   │   │   └── ResourceToggleButton.tsx
│   │   │   ├── hooks/
│   │   │   │   ├── useResources.ts
│   │   │   │   ├── useCreateResource.ts
│   │   │   │   ├── useUpdateResource.ts
│   │   │   │   └── useToggleResource.ts
│   │   │   └── types/
│   │   │       └── resource.types.ts
│   │   ├── packages/
│   │   │   ├── api/
│   │   │   │   └── packagesApi.ts
│   │   │   ├── components/
│   │   │   │   ├── PackageTable.tsx
│   │   │   │   ├── PackageForm.tsx
│   │   │   │   └── PackageToggleButton.tsx
│   │   │   ├── hooks/
│   │   │   │   ├── usePackages.ts
│   │   │   │   └── useCreatePackage.ts
│   │   │   └── types/
│   │   │       └── package.types.ts
│   │   └── users/
│   │       ├── api/
│   │       │   └── usersApi.ts
│   │       ├── components/
│   │       │   └── UserTable.tsx
│   │       └── hooks/
│   │           └── useUsers.ts
│   │
│   └── frontdesk/
│       ├── sessions/
│       │   ├── api/
│       │   │   └── sessionsApi.ts
│       │   ├── components/
│       │   │   ├── ActiveSessionsTable.tsx
│       │   │   └── SessionDetailCard.tsx
│       │   └── hooks/
│       │       ├── useActiveSessions.ts
│       │       └── useTimeOut.ts
│       ├── time-in/
│       │   ├── api/
│       │   │   └── timeInApi.ts
│       │   ├── components/
│       │   │   └── WalkInForm.tsx
│       │   └── hooks/
│       │       └── useWalkIn.ts
│       ├── bookings/
│       │   ├── api/
│       │   │   └── bookingsApi.ts
│       │   ├── components/
│       │   │   ├── BookingsByDateTable.tsx
│       │   │   └── BookingDetailPanel.tsx
│       │   └── hooks/
│       │       └── useBookings.ts
│       └── payments/
│           ├── api/
│           │   └── paymentsApi.ts
│           ├── components/
│           │   ├── PendingPaymentsTable.tsx
│           │   └── PaymentValidationForm.tsx
│           └── hooks/
│               └── usePendingPayments.ts
│
├── shared/
│   ├── components/
│   │   ├── layout/
│   │   │   ├── AdminSidebar.tsx
│   │   │   ├── FrontDeskSidebar.tsx
│   │   │   ├── Topbar.tsx
│   │   │   └── SidebarItem.tsx
│   │   ├── ui/
│   │   │   ├── Button.tsx
│   │   │   ├── Input.tsx
│   │   │   ├── Badge.tsx
│   │   │   ├── Card.tsx
│   │   │   ├── Modal.tsx
│   │   │   ├── Table.tsx
│   │   │   └── Spinner.tsx
│   │   └── guards/
│   │       ├── RoleGuard.tsx         # Client-side role check component
│   │       └── AuthGuard.tsx         # Wraps pages that need auth (belt-and-suspenders)
│   ├── hooks/
│   │   ├── useAuth.ts                # Reads from Zustand auth store
│   │   ├── useRole.ts                # Returns current role, hasRole(), canAccess()
│   │   └── useSidebar.ts             # Reads/toggles sidebar from Redux
│   ├── lib/
│   │   ├── axios.ts                  # Axios instance + interceptors
│   │   ├── queryClient.ts            # TanStack Query client config
│   │   └── cookies.ts                # Cookie read helpers (client-side non-sensitive only)
│   ├── store/
│   │   ├── auth/
│   │   │   └── authStore.ts          # Zustand auth slice
│   │   └── redux/
│   │       ├── store.ts              # Redux store configuration
│   │       ├── sidebarSlice.ts       # Sidebar open/collapsed
│   │       ├── notificationSlice.ts  # Toast notifications queue
│   │       └── appSettingsSlice.ts   # Theme, font size, and other UI preferences
│   ├── providers/
│   │   └── AppProviders.tsx          # Mounts all providers in correct order
│   ├── constants/
│   │   ├── roles.ts                  # Role string constants
│   │   ├── routes.ts                 # All route path constants
│   │   └── queryKeys.ts              # TanStack Query key factory
│   └── types/
│       ├── api.types.ts              # Shared API response shapes
│       └── user.types.ts             # User and role types
│
└── middleware.ts                     # Next.js edge middleware — route protection
```

---

## 4. Route Groups & Page Map

Next.js App Router route groups (folders wrapped in parentheses) allow sharing layouts without affecting the URL. Each group gets its own layout — no admin chrome leaks into the landing page, and no marketing nav appears in the webapp.

| URL Pattern | Route Group | Layout | Access |
|-------------|-------------|--------|--------|
| `/` | `(marketing)` | Marketing layout | Public |
| `/about`, `/contact` | `(marketing)` | Marketing layout | Public |
| `/login` | `(auth)` | Centered card | Public (redirects if logged in) |
| `/admin/**` | `(admin)` | Admin sidebar shell | Admin role only |
| `/admin/settings` | `(admin)` | Admin sidebar shell | Admin role only |
| `/frontdesk/**` | `(frontdesk)` | FrontDesk sidebar shell | Admin + FrontDesk roles |
| `/frontdesk/settings` | `(frontdesk)` | FrontDesk sidebar shell | Admin + FrontDesk roles |
| `/unauthorized` | root | Minimal | Public |

### Role-to-route mapping

The backend returns roles as strings in the JWT. The roles used in this system are:

```typescript
// shared/constants/roles.ts
export const ROLES = {
  ADMIN: 'Admin',
  FRONTDESK: 'FrontDesk',
} as const

export type Role = typeof ROLES[keyof typeof ROLES]
```

| Role | Can access |
|------|-----------|
| `Admin` | `/admin/**` and `/frontdesk/**` |
| `FrontDesk` | `/frontdesk/**` only |

After login, the redirect destination is determined by role:
- `Admin` → `/admin/dashboard`
- `FrontDesk` → `/frontdesk/dashboard`

---

## 5. Token Storage Strategy

This is the most critical architectural decision. Getting this wrong opens the door to XSS and CSRF attacks.

```
┌─────────────────────────────────────────────────────────────────┐
│  Token           │  Storage            │  Why                   │
├─────────────────────────────────────────────────────────────────┤
│  Access Token    │  Zustand (memory)   │  Never touches disk.   │
│                  │                     │  Lost on refresh —     │
│                  │                     │  recovered silently.   │
│                  │                     │  XSS cannot steal it.  │
├─────────────────────────────────────────────────────────────────┤
│  Refresh Token   │  httpOnly cookie    │  JavaScript cannot     │
│                  │  (set by Next.js    │  read it. Only the     │
│                  │  API route)         │  Next.js proxy reads   │
│                  │                     │  it server-side.       │
├─────────────────────────────────────────────────────────────────┤
│  has_session     │  Non-httpOnly       │  Allows middleware to  │
│  (boolean flag)  │  cookie             │  know a session exists │
│                  │                     │  without exposing the  │
│                  │                     │  token.                │
├─────────────────────────────────────────────────────────────────┤
│  user_role       │  Non-httpOnly       │  Allows middleware to  │
│  (role string)   │  cookie             │  enforce role-based    │
│                  │                     │  routing.              │
└─────────────────────────────────────────────────────────────────┘
```

**Never use:**
- `localStorage` — persists across sessions, readable by any JS, vulnerable to XSS
- `sessionStorage` — readable by JS, cleared on tab close only
- A regular (non-httpOnly) cookie for refresh tokens — readable by JS

**Cookie attributes required:**

| Cookie | httpOnly | Secure | SameSite | Path |
|--------|----------|--------|----------|------|
| `refresh_token` | Yes | Yes (HTTPS only) | `Strict` | `/api/auth` |
| `has_session` | No | Yes | `Strict` | `/` |
| `user_role` | No | Yes | `Strict` | `/` |

`refresh_token` is scoped to `/api/auth` only — the browser sends it exclusively to the Next.js auth proxy routes, not to any other endpoint.

---

## 6. Next.js API Routes — Auth Proxy

These routes live in `src/app/api/auth/` and act as a secure intermediary between the browser and the .NET backend. The browser never directly calls the .NET auth endpoints.

### POST `/api/auth/login`

```
Browser  →  POST /api/auth/login  { email, password }
              ↓
Next.js API Route
  → forwards to .NET: POST /api/auth/login
  → .NET returns: { token, refreshToken, expiration }
              ↓
Next.js API Route:
  → sets cookie: refresh_token (httpOnly, Secure, SameSite=Strict, Path=/api/auth)
  → sets cookie: has_session=true (Secure, SameSite=Strict)
  → sets cookie: user_role={role} (Secure, SameSite=Strict)
  → returns to browser: { accessToken, user: { id, email, role }, expiresAt }
              ↓
Browser:
  → stores accessToken in Zustand (memory only)
  → stores user object in Zustand
  → redirects based on role
```

### POST `/api/auth/refresh`

```
Browser  →  POST /api/auth/refresh  (no body — cookie sent automatically)
              ↓
Next.js API Route:
  → reads refresh_token from incoming cookie (httpOnly — browser didn't touch it)
  → forwards to .NET: POST /api/auth/refresh  { refreshToken }
  → .NET returns: { token, refreshToken, expiration }
              ↓
Next.js API Route:
  → updates refresh_token cookie with new value
  → returns to browser: { accessToken, expiresAt }
              ↓
Browser:
  → updates accessToken in Zustand
  → retries all queued API calls
```

### POST `/api/auth/logout`

```
Browser  →  POST /api/auth/logout
              ↓
Next.js API Route:
  → reads refresh_token from cookie
  → forwards to .NET: POST /api/auth/logout  { refreshToken }
  → clears all auth cookies (refresh_token, has_session, user_role)
              ↓
Browser:
  → clears Zustand auth store
  → clears TanStack Query cache
  → redirects to /login
```

---

## 7. Middleware — Route & Role Protection

The middleware file runs at the **edge** before any page renders. It is the first line of defense for route protection. It reads the non-httpOnly cookies (`has_session`, `user_role`) to make routing decisions.

**File:** `src/middleware.ts`

```typescript
import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'
import { ROLES } from '@/shared/constants/roles'

// Routes the middleware applies to
export const config = {
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico|api/).*)',
  ],
}

const PUBLIC_ROUTES = ['/', '/about', '/contact']
const AUTH_ROUTES = ['/login']
const ADMIN_PREFIX = '/admin'
const FRONTDESK_PREFIX = '/frontdesk'

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl

  const hasSession = request.cookies.get('has_session')?.value === 'true'
  const userRole = request.cookies.get('user_role')?.value

  // Public marketing routes — always accessible
  if (PUBLIC_ROUTES.includes(pathname)) {
    return NextResponse.next()
  }

  // Auth routes — redirect to dashboard if already logged in
  if (AUTH_ROUTES.includes(pathname)) {
    if (hasSession && userRole) {
      const destination = userRole === ROLES.ADMIN
        ? '/admin/dashboard'
        : '/frontdesk/dashboard'
      return NextResponse.redirect(new URL(destination, request.url))
    }
    return NextResponse.next()
  }

  // All protected routes below — must have a session
  if (!hasSession) {
    const loginUrl = new URL('/login', request.url)
    loginUrl.searchParams.set('returnTo', pathname)
    return NextResponse.redirect(loginUrl)
  }

  // Admin routes — Admin role only
  if (pathname.startsWith(ADMIN_PREFIX)) {
    if (userRole !== ROLES.ADMIN) {
      return NextResponse.redirect(new URL('/unauthorized', request.url))
    }
    return NextResponse.next()
  }

  // FrontDesk routes — Admin and FrontDesk
  if (pathname.startsWith(FRONTDESK_PREFIX)) {
    if (userRole !== ROLES.ADMIN && userRole !== ROLES.FRONTDESK) {
      return NextResponse.redirect(new URL('/unauthorized', request.url))
    }
    return NextResponse.next()
  }

  return NextResponse.next()
}
```

**Important:** The middleware only checks cookie presence for routing decisions. The actual token validity is enforced by the .NET backend on every API call. If someone manually sets `has_session=true` without a valid refresh token, they will get past the middleware but every API call will return 401 and the silent refresh will fail, triggering a forced logout.

---

## 8. Zustand — Auth Store

Zustand holds the access token in memory and the current user's information. It is the source of truth that Axios reads when attaching the `Authorization` header.

**File:** `src/shared/store/auth/authStore.ts`

```typescript
import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import type { Role } from '@/shared/constants/roles'

interface AuthUser {
  id: string
  email: string
  role: Role
  firstName?: string
  lastName?: string
}

interface AuthState {
  accessToken: string | null
  user: AuthUser | null
  isAuthenticated: boolean
  isHydrating: boolean  // true while silent refresh is in progress on page load
}

interface AuthActions {
  setAuth: (accessToken: string, user: AuthUser) => void
  clearAuth: () => void
  setAccessToken: (token: string) => void
  setHydrating: (value: boolean) => void
}

const initialState: AuthState = {
  accessToken: null,
  user: null,
  isAuthenticated: false,
  isHydrating: true,
}

export const useAuthStore = create<AuthState & AuthActions>()(
  immer((set) => ({
    ...initialState,

    setAuth: (accessToken, user) =>
      set((state) => {
        state.accessToken = accessToken
        state.user = user
        state.isAuthenticated = true
        state.isHydrating = false
      }),

    clearAuth: () =>
      set((state) => {
        state.accessToken = null
        state.user = null
        state.isAuthenticated = false
        state.isHydrating = false
      }),

    setAccessToken: (token) =>
      set((state) => {
        state.accessToken = token
      }),

    setHydrating: (value) =>
      set((state) => {
        state.isHydrating = value
      }),
  }))
)
```

**File:** `src/shared/hooks/useAuth.ts`

```typescript
import { useAuthStore } from '@/shared/store/auth/authStore'

export function useAuth() {
  const { accessToken, user, isAuthenticated, isHydrating } = useAuthStore()
  return { accessToken, user, isAuthenticated, isHydrating }
}
```

**File:** `src/shared/hooks/useRole.ts`

```typescript
import { useAuthStore } from '@/shared/store/auth/authStore'
import { ROLES, type Role } from '@/shared/constants/roles'

export function useRole() {
  const user = useAuthStore((s) => s.user)

  return {
    role: user?.role ?? null,
    isAdmin: user?.role === ROLES.ADMIN,
    isFrontDesk: user?.role === ROLES.FRONTDESK,
    hasRole: (role: Role) => user?.role === role,
    canAccess: (allowedRoles: Role[]) =>
      user ? allowedRoles.includes(user.role) : false,
  }
}
```

---

## 9. Redux Toolkit — Global App Store

Redux Toolkit handles state that needs the Redux DevTools (sidebar, notifications, complex UI flows). It is not used for auth — Zustand handles that.

**File:** `src/shared/store/redux/store.ts`

```typescript
import { configureStore } from '@reduxjs/toolkit'
import sidebarReducer from './sidebarSlice'
import notificationReducer from './notificationSlice'
import appSettingsReducer from './appSettingsSlice'

export const store = configureStore({
  reducer: {
    sidebar: sidebarReducer,
    notifications: notificationReducer,
    appSettings: appSettingsReducer,
  },
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
```

**File:** `src/shared/store/redux/sidebarSlice.ts`

```typescript
import { createSlice } from '@reduxjs/toolkit'
import type { RootState } from './store'

interface SidebarState {
  isCollapsed: boolean
  isMobileOpen: boolean
}

const initialState: SidebarState = {
  isCollapsed: false,
  isMobileOpen: false,
}

const sidebarSlice = createSlice({
  name: 'sidebar',
  initialState,
  reducers: {
    toggleCollapsed: (state) => { state.isCollapsed = !state.isCollapsed },
    setCollapsed: (state, action) => { state.isCollapsed = action.payload },
    toggleMobile: (state) => { state.isMobileOpen = !state.isMobileOpen },
    closeMobile: (state) => { state.isMobileOpen = false },
  },
})

export const { toggleCollapsed, setCollapsed, toggleMobile, closeMobile } = sidebarSlice.actions
export const selectSidebar = (state: RootState) => state.sidebar
export default sidebarSlice.reducer
```

**File:** `src/shared/store/redux/notificationSlice.ts`

```typescript
import { createSlice, nanoid } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'
import type { RootState } from './store'

type NotificationType = 'success' | 'error' | 'warning' | 'info'

interface Notification {
  id: string
  type: NotificationType
  message: string
  duration?: number
}

interface NotificationState {
  queue: Notification[]
}

const notificationSlice = createSlice({
  name: 'notifications',
  initialState: { queue: [] } as NotificationState,
  reducers: {
    addNotification: {
      reducer: (state, action: PayloadAction<Notification>) => {
        state.queue.push(action.payload)
      },
      prepare: (payload: Omit<Notification, 'id'>) => ({
        payload: { ...payload, id: nanoid() },
      }),
    },
    removeNotification: (state, action: PayloadAction<string>) => {
      state.queue = state.queue.filter((n) => n.id !== action.payload)
    },
  },
})

export const { addNotification, removeNotification } = notificationSlice.actions
export const selectNotifications = (state: RootState) => state.notifications.queue
export default notificationSlice.reducer
```

**File:** `src/shared/store/redux/appSettingsSlice.ts`

Stores UI preferences that persist across page reloads via `localStorage`. The slice hydrates from `localStorage` on store init and writes back on every change.

```typescript
import { createSlice, type PayloadAction } from '@reduxjs/toolkit'
import type { RootState } from './store'

type Theme = 'light' | 'dark' | 'system'
type FontSize = 'sm' | 'md' | 'lg'

interface AppSettingsState {
  theme: Theme
  fontSize: FontSize
  sidebarCompact: boolean       // icon-only sidebar mode (independent of collapse state)
  enableAnimations: boolean
}

function loadFromStorage(): AppSettingsState {
  if (typeof window === 'undefined') {
    return defaults
  }
  try {
    const raw = localStorage.getItem('app_settings')
    return raw ? { ...defaults, ...JSON.parse(raw) } : defaults
  } catch {
    return defaults
  }
}

const defaults: AppSettingsState = {
  theme: 'system',
  fontSize: 'md',
  sidebarCompact: false,
  enableAnimations: true,
}

const appSettingsSlice = createSlice({
  name: 'appSettings',
  initialState: loadFromStorage,
  reducers: {
    setTheme: (state, action: PayloadAction<Theme>) => {
      state.theme = action.payload
      persist(state)
    },
    setFontSize: (state, action: PayloadAction<FontSize>) => {
      state.fontSize = action.payload
      persist(state)
    },
    toggleSidebarCompact: (state) => {
      state.sidebarCompact = !state.sidebarCompact
      persist(state)
    },
    toggleAnimations: (state) => {
      state.enableAnimations = !state.enableAnimations
      persist(state)
    },
    resetSettings: () => {
      localStorage.removeItem('app_settings')
      return defaults
    },
  },
})

function persist(state: AppSettingsState) {
  try {
    localStorage.setItem('app_settings', JSON.stringify(state))
  } catch {
    // storage quota exceeded or private browsing — fail silently
  }
}

export const {
  setTheme,
  setFontSize,
  toggleSidebarCompact,
  toggleAnimations,
  resetSettings,
} = appSettingsSlice.actions

export const selectAppSettings = (state: RootState) => state.appSettings
export default appSettingsSlice.reducer
```

> **Why `localStorage` for settings (but not tokens)?**
> Settings are not sensitive — there is no security risk in exposing them to JS. Unlike refresh tokens, leaking your dark mode preference has no consequences. `localStorage` is appropriate here because these preferences need to survive page refreshes and browser restarts. Tokens must never go here.

**Applying theme and font size to the DOM:**

The settings must be reflected on the `<html>` element so Tailwind dark mode and CSS custom properties work globally. Do this in a `ThemeProvider` component mounted in the root layout:

**File:** `src/shared/providers/ThemeProvider.tsx`

```typescript
'use client'

import { useEffect } from 'react'
import { useSelector } from 'react-redux'
import { selectAppSettings } from '@/shared/store/redux/appSettingsSlice'

const fontSizeMap = {
  sm: '14px',
  md: '16px',
  lg: '18px',
}

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const { theme, fontSize, enableAnimations } = useSelector(selectAppSettings)

  useEffect(() => {
    const root = document.documentElement

    // Dark mode — Tailwind 'class' strategy
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
    const isDark = theme === 'dark' || (theme === 'system' && prefersDark)
    root.classList.toggle('dark', isDark)

    // Font size — CSS custom property consumed by Tailwind base
    root.style.setProperty('--base-font-size', fontSizeMap[fontSize])

    // Animations
    root.classList.toggle('reduce-motion', !enableAnimations)
  }, [theme, fontSize, enableAnimations])

  return <>{children}</>
}
```

Add `ThemeProvider` inside `AppProviders`, wrapping the children:

```tsx
// Inside AppProviders.tsx — add ThemeProvider around children
<AuthSessionProvider>
  <ThemeProvider>
    {children}
  </ThemeProvider>
</AuthSessionProvider>
```

For the `--base-font-size` to take effect, add this to `globals.css`:

```css
html {
  font-size: var(--base-font-size, 16px);
}
```

**File:** `src/shared/hooks/useSidebar.ts`

```typescript
import { useDispatch, useSelector } from 'react-redux'
import { selectSidebar, toggleCollapsed, toggleMobile, closeMobile } from '@/shared/store/redux/sidebarSlice'

export function useSidebar() {
  const dispatch = useDispatch()
  const sidebar = useSelector(selectSidebar)

  return {
    ...sidebar,
    toggleCollapsed: () => dispatch(toggleCollapsed()),
    toggleMobile: () => dispatch(toggleMobile()),
    closeMobile: () => dispatch(closeMobile()),
  }
}
```

---

## 10. Axios Instance & Interceptors

A single Axios instance is shared across all feature API files. It reads the access token from Zustand and handles the 401 → silent refresh → retry cycle. A queue prevents multiple simultaneous refresh calls.

**File:** `src/shared/lib/axios.ts`

```typescript
import axios from 'axios'
import { useAuthStore } from '@/shared/store/auth/authStore'

const BASE_URL = process.env.NEXT_PUBLIC_API_URL

export const apiClient = axios.create({
  baseURL: BASE_URL,
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
})

// --- Token refresh queue ---
// Prevents multiple simultaneous refresh calls when multiple requests all get 401
let isRefreshing = false
let failedQueue: Array<{
  resolve: (token: string) => void
  reject: (error: unknown) => void
}> = []

function processQueue(error: unknown, token: string | null = null) {
  failedQueue.forEach(({ resolve, reject }) => {
    if (error) reject(error)
    else resolve(token!)
  })
  failedQueue = []
}

// --- Request interceptor: attach access token ---
apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// --- Response interceptor: handle 401 ---
apiClient.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    // Only handle 401 errors that haven't already been retried
    if (error.response?.status !== 401 || originalRequest._retry) {
      return Promise.reject(error)
    }

    // Mark this request as retried to prevent infinite loops
    originalRequest._retry = true

    if (isRefreshing) {
      // Another refresh is already in progress — queue this request
      return new Promise((resolve, reject) => {
        failedQueue.push({ resolve, reject })
      }).then((token) => {
        originalRequest.headers.Authorization = `Bearer ${token}`
        return apiClient(originalRequest)
      })
    }

    isRefreshing = true

    try {
      // Call Next.js proxy — it reads the httpOnly refresh token cookie
      const response = await fetch('/api/auth/refresh', { method: 'POST' })

      if (!response.ok) throw new Error('Refresh failed')

      const { accessToken } = await response.json()

      // Update Zustand with new access token
      useAuthStore.getState().setAccessToken(accessToken)

      // Retry all queued requests
      processQueue(null, accessToken)

      // Retry the original request
      originalRequest.headers.Authorization = `Bearer ${accessToken}`
      return apiClient(originalRequest)
    } catch (refreshError) {
      // Refresh failed — force logout
      processQueue(refreshError, null)
      useAuthStore.getState().clearAuth()
      window.location.href = '/login'
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  }
)
```

---

## 11. TanStack Query Setup

**File:** `src/shared/lib/queryClient.ts`

```typescript
import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 2,        // 2 minutes — data considered fresh
      gcTime: 1000 * 60 * 10,           // 10 minutes — cache kept in memory
      retry: (failureCount, error: any) => {
        // Do not retry on 401 or 403 — auth errors should fail immediately
        if (error?.response?.status === 401) return false
        if (error?.response?.status === 403) return false
        if (error?.response?.status === 404) return false
        return failureCount < 2
      },
      refetchOnWindowFocus: true,
    },
    mutations: {
      retry: 0,
    },
  },
})
```

**File:** `src/shared/constants/queryKeys.ts`

Query key factory pattern — typed, consistent, easy to invalidate.

```typescript
export const queryKeys = {
  // Resources
  resources: {
    all: ['resources'] as const,
    list: (filters?: Record<string, unknown>) =>
      ['resources', 'list', filters] as const,
    detail: (id: string) => ['resources', 'detail', id] as const,
  },

  // Packages
  packages: {
    all: ['packages'] as const,
    list: (filters?: Record<string, unknown>) =>
      ['packages', 'list', filters] as const,
    detail: (id: string) => ['packages', 'detail', id] as const,
  },

  // Bookings
  bookings: {
    all: ['bookings'] as const,
    byDate: (date: string) => ['bookings', 'date', date] as const,
    detail: (id: string) => ['bookings', 'detail', id] as const,
    upcoming: ['bookings', 'upcoming'] as const,
  },

  // Sessions
  sessions: {
    active: ['sessions', 'active'] as const,
  },

  // Payments
  payments: {
    pending: ['payments', 'pending'] as const,
    detail: (id: string) => ['payments', 'detail', id] as const,
  },
} as const
```

---

## 12. Login Flow

### AuthSessionProvider

This component runs on every app load and attempts to silently restore the session. It is mounted inside the root layout, above all page content.

**File:** `src/features/auth/components/AuthSessionProvider.tsx`

```typescript
'use client'

import { useEffect } from 'react'
import { useAuthStore } from '@/shared/store/auth/authStore'

/**
 * On every page load, check if the user has a session (via has_session cookie)
 * and silently restore the access token via the refresh endpoint.
 * This runs once on mount — all other pages can then rely on Zustand.
 */
export function AuthSessionProvider({ children }: { children: React.ReactNode }) {
  const { setAuth, clearAuth, setHydrating } = useAuthStore()

  useEffect(() => {
    const restore = async () => {
      // has_session cookie is readable by JS (not httpOnly)
      const hasSession = document.cookie.includes('has_session=true')

      if (!hasSession) {
        clearAuth()
        return
      }

      try {
        const response = await fetch('/api/auth/refresh', { method: 'POST' })
        if (!response.ok) throw new Error('Session expired')

        const { accessToken, user } = await response.json()
        setAuth(accessToken, user)
      } catch {
        clearAuth()
      }
    }

    restore()
  }, []) // runs once on mount

  return <>{children}</>
}
```

### Login mutation

**File:** `src/features/auth/hooks/useLogin.ts`

```typescript
import { useMutation } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { useAuthStore } from '@/shared/store/auth/authStore'
import { ROLES } from '@/shared/constants/roles'

interface LoginPayload {
  email: string
  password: string
}

export function useLogin() {
  const { setAuth } = useAuthStore()
  const router = useRouter()

  return useMutation({
    mutationFn: async (payload: LoginPayload) => {
      const res = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })

      if (!res.ok) {
        const error = await res.json()
        throw new Error(error.message ?? 'Login failed')
      }

      return res.json()
    },

    onSuccess: (data) => {
      setAuth(data.accessToken, data.user)

      const destination = data.user.role === ROLES.ADMIN
        ? '/admin/dashboard'
        : '/frontdesk/dashboard'

      router.replace(destination)
    },
  })
}
```

---

## 13. Silent Refresh Flow

```
1. User opens the app (or refreshes the page)
   → Zustand is empty (in-memory, lost on refresh)

2. AuthSessionProvider mounts
   → checks: does `has_session=true` cookie exist?
   → Yes: calls POST /api/auth/refresh (browser sends refresh_token cookie automatically)
   → Next.js API route reads httpOnly cookie, forwards to .NET
   → .NET validates and issues new tokens
   → Next.js route updates the refresh_token cookie, returns { accessToken, user }
   → AuthSessionProvider calls setAuth() — Zustand is populated

3. The rest of the app renders normally
   → All Axios calls attach the access token from Zustand

4. Access token expires (15 min)
   → Next .NET API call returns 401
   → Axios response interceptor catches 401
   → Fires POST /api/auth/refresh
   → New access token stored in Zustand
   → Original request retried with new token
   → User notices nothing
```

### When the session is genuinely expired

```
5. Refresh token has expired
   → POST /api/auth/refresh returns 401
   → Axios interceptor calls clearAuth()
   → window.location.href = '/login'
   → Next.js API route clears all cookies on logout
   → User sees login page
```

---

## 14. Logout Flow

**File:** `src/features/auth/hooks/useLogout.ts`

```typescript
import { useMutation } from '@tanstack/react-query'
import { useRouter } from 'next/navigation'
import { useAuthStore } from '@/shared/store/auth/authStore'
import { queryClient } from '@/shared/lib/queryClient'

export function useLogout() {
  const { clearAuth } = useAuthStore()
  const router = useRouter()

  return useMutation({
    mutationFn: async () => {
      await fetch('/api/auth/logout', { method: 'POST' })
    },

    onSettled: () => {
      // Always clear local state, even if the API call fails
      clearAuth()
      queryClient.clear()
      router.replace('/login')
    },
  })
}
```

Steps executed on logout:
1. `POST /api/auth/logout` → Next.js proxy forwards to .NET, then clears all auth cookies
2. `clearAuth()` → wipes Zustand (access token gone from memory)
3. `queryClient.clear()` → removes all cached server data
4. `router.replace('/login')` → navigates to login, removes protected route from back stack

---

## 15. Sidebar Layout

Each role gets its own sidebar with relevant navigation items. Both sidebars share the same `SidebarItem` component but receive different nav configs.

### Admin Sidebar

```typescript
// src/shared/components/layout/AdminSidebar.tsx
// Navigation items for Admin role

const adminNav = [
  { label: 'Dashboard',  href: '/admin/dashboard',  icon: LayoutDashboard },
  { label: 'Resources',  href: '/admin/resources',   icon: Building2 },
  { label: 'Packages',   href: '/admin/packages',    icon: Package },
  { label: 'Bookings',   href: '/admin/bookings',    icon: CalendarDays },
  { label: 'Users',      href: '/admin/users',       icon: Users },
]

// Bottom section — always rendered at the bottom of the sidebar, separated by a divider
const adminNavBottom = [
  { label: 'Settings',   href: '/admin/settings',    icon: Settings },
]
```

### FrontDesk Sidebar

```typescript
// src/shared/components/layout/FrontDeskSidebar.tsx
// Navigation items for FrontDesk role

const frontdeskNav = [
  { label: 'Dashboard',       href: '/frontdesk/dashboard',  icon: LayoutDashboard },
  { label: 'Active Sessions', href: '/frontdesk/sessions',   icon: Activity },
  { label: 'Walk-in',         href: '/frontdesk/time-in',    icon: LogIn },
  { label: 'Bookings',        href: '/frontdesk/bookings',   icon: CalendarDays },
  { label: 'Payments',        href: '/frontdesk/payments',   icon: CreditCard },
]

// Bottom section — always rendered at the bottom of the sidebar, separated by a divider
const frontdeskNavBottom = [
  { label: 'Settings',        href: '/frontdesk/settings',   icon: Settings },
]
```

### Shell Layout Pattern

Each route group layout renders:

```
┌─────────────────────────────────────────┐
│ Sidebar (fixed, role-specific)          │
│  ├── Logo                               │
│  ├── Nav items (top group)              │
│  │    Dashboard, Resources, ...         │
│  ├── [divider]                          │
│  ├── Settings (bottom group)            │
│  └── User info + Logout button          │
│                                         │
│  ┌─────────────────────────────────────┐│
│  │ Topbar (sticky)                     ││
│  │  ├── Page title                     ││
│  │  └── Sidebar toggle (mobile)        ││
│  ├─────────────────────────────────────┤│
│  │ Page content (scrollable)           ││
│  │  {children}                         ││
│  └─────────────────────────────────────┘│
└─────────────────────────────────────────┘
```

The sidebar renders two nav groups: the main nav items at the top, and the settings link pinned to the bottom above the user info block. Use a flex column layout with `justify-between` to push the bottom group down:

```tsx
<aside className="flex flex-col h-full">
  {/* Top: Logo + main nav */}
  <div className="flex-1 overflow-y-auto">
    <Logo />
    <nav>{mainNavItems.map(renderItem)}</nav>
  </div>

  {/* Bottom: Settings + divider + user info */}
  <div>
    <Separator />
    <nav>{bottomNavItems.map(renderItem)}</nav>
    <Separator />
    <UserInfoBlock />
  </div>
</aside>
```

**Admin layout** (`src/app/(admin)/_layout.tsx`):

```typescript
import { AdminSidebar } from '@/shared/components/layout/AdminSidebar'
import { Topbar } from '@/shared/components/layout/Topbar'

export default function AdminLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-screen overflow-hidden bg-gray-50">
      <AdminSidebar />
      <div className="flex flex-col flex-1 overflow-hidden">
        <Topbar />
        <main className="flex-1 overflow-y-auto p-6">
          {children}
        </main>
      </div>
    </div>
  )
}
```

---

## 16. Role-Based Component Guards

Middleware protects entire routes, but sometimes a component on a shared page should be visible only to a specific role. Use `RoleGuard` for this.

**File:** `src/shared/components/guards/RoleGuard.tsx`

```typescript
'use client'

import { useRole } from '@/shared/hooks/useRole'
import type { Role } from '@/shared/constants/roles'

interface RoleGuardProps {
  allowedRoles: Role[]
  fallback?: React.ReactNode
  children: React.ReactNode
}

export function RoleGuard({ allowedRoles, fallback = null, children }: RoleGuardProps) {
  const { canAccess } = useRole()

  if (!canAccess(allowedRoles)) return <>{fallback}</>
  return <>{children}</>
}
```

Usage:

```tsx
<RoleGuard allowedRoles={[ROLES.ADMIN]}>
  <DeleteResourceButton />
</RoleGuard>
```

**File:** `src/shared/components/guards/AuthGuard.tsx`

Belt-and-suspenders client-side guard. The middleware already protects routes, but this ensures that if somehow Zustand is empty (e.g., hydration race), the page doesn't render sensitive content.

```typescript
'use client'

import { useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { useAuth } from '@/shared/hooks/useAuth'

export function AuthGuard({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isHydrating } = useAuth()
  const router = useRouter()

  useEffect(() => {
    if (!isHydrating && !isAuthenticated) {
      router.replace('/login')
    }
  }, [isAuthenticated, isHydrating])

  if (isHydrating) return <div className="flex h-screen items-center justify-center">
    <span className="text-gray-400 text-sm">Loading...</span>
  </div>

  if (!isAuthenticated) return null

  return <>{children}</>
}
```

---

## 17. Security Hardening

### Content Security Policy

Add to `next.config.ts`:

```typescript
const securityHeaders = [
  { key: 'X-Frame-Options', value: 'DENY' },
  { key: 'X-Content-Type-Options', value: 'nosniff' },
  { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
  {
    key: 'Content-Security-Policy',
    value: [
      "default-src 'self'",
      "script-src 'self' 'unsafe-eval' 'unsafe-inline'",  // tighten in prod
      "style-src 'self' 'unsafe-inline'",
      `connect-src 'self' ${process.env.NEXT_PUBLIC_API_URL}`,
      "img-src 'self' blob: data: https:",
      "font-src 'self'",
      "frame-ancestors 'none'",
    ].join('; '),
  },
]
```

### What this prevents

| Attack | Prevention |
|--------|-----------|
| XSS stealing tokens | Access token in Zustand memory only — no DOM, no storage |
| XSS stealing refresh token | Refresh token in httpOnly cookie — JS cannot read it |
| CSRF | `SameSite=Strict` on all cookies — not sent on cross-origin requests |
| Clickjacking | `X-Frame-Options: DENY` + CSP `frame-ancestors 'none'` |
| Route bypass | Middleware runs at edge on every request — cannot be skipped |
| Role escalation | Backend enforces roles on every request independently |
| Token replay | Short-lived access tokens (15 min), rotating refresh tokens |
| Open redirect | `returnTo` parameter validated against allowlist before use |
| Concurrent refresh race | Queue in Axios interceptor — only one refresh call at a time |

### Input validation

All forms use Zod schemas. Never pass raw form values directly to API calls.

```typescript
// features/auth/schemas/loginSchema.ts
import { z } from 'zod'

export const loginSchema = z.object({
  email: z.string().email('Enter a valid email address'),
  password: z.string().min(1, 'Password is required'),
})

export type LoginInput = z.infer<typeof loginSchema>
```

---

## 18. Implementation Order

Follow this sequence. Each step produces a working, testable state.

| Step | What to build | Verifiable outcome |
|------|--------------|-------------------|
| 1 | Project init: `create-next-app`, TypeScript strict, Tailwind, ESLint | App compiles and renders |
| 2 | Folder structure: create all directories and placeholder `page.tsx` files | All routes resolve without errors |
| 3 | Environment config: `.env.local`, `NEXT_PUBLIC_API_URL`, typed env wrapper | `process.env` access is typed |
| 4 | Providers: Zustand store, Redux store, TanStack QueryClientProvider, Redux Provider — all mounted in `AppProviders` and `root layout.tsx` | DevTools visible |
| 5 | Axios instance: base URL, timeout, request interceptor (token attach) | Axios instance exported |
| 6 | Next.js API routes: `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout` | Proxy correctly sets cookies |
| 7 | Middleware: route protection + role enforcement | Wrong-role redirect works |
| 8 | Zustand auth store + `AuthSessionProvider` | Silent refresh restores session on reload |
| 9 | Axios response interceptor: 401 → silent refresh → retry queue | Expired token transparently refreshed |
| 10 | Login page + form + `useLogin` mutation | Full login → dashboard flow works |
| 11 | `useLogout` hook + logout button in sidebar | Full logout clears everything |
| 12 | Admin sidebar layout + FrontDesk sidebar layout | Both shells render with correct nav |
| 13 | `RoleGuard` + `AuthGuard` components | Component-level role hiding works |
| 14 | Admin: Resources pages + `useResources` / `useCreateResource` hooks | CRUD works end-to-end |
| 15 | Admin: Packages pages | Same pattern as resources |
| 16 | FrontDesk: Sessions + Walk-in pages | FrontDesk flow works |
| 17 | FrontDesk: Bookings + Payments pages | Full FrontDesk daily flow works |
| 18 | Settings pages: `appSettingsSlice`, `ThemeProvider`, settings UI for both roles | Dark mode, font size, and preferences persist across reloads |
| 19 | Security pass: CSP headers, cookie attributes, returnTo validation | Security checklist clean |

---

## AppProviders — Mount Order

Mount order matters. QueryClientProvider must wrap everything that uses TanStack. Redux Provider must wrap everything that uses `useSelector`. AuthSessionProvider must come after both.

**File:** `src/shared/providers/AppProviders.tsx`

```typescript
'use client'

import { QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { Provider as ReduxProvider } from 'react-redux'
import { queryClient } from '@/shared/lib/queryClient'
import { store } from '@/shared/store/redux/store'
import { AuthSessionProvider } from '@/features/auth/components/AuthSessionProvider'

export function AppProviders({ children }: { children: React.ReactNode }) {
  return (
    <ReduxProvider store={store}>
      <QueryClientProvider client={queryClient}>
        <AuthSessionProvider>
          {children}
        </AuthSessionProvider>
        {process.env.NODE_ENV === 'development' && (
          <ReactQueryDevtools initialIsOpen={false} />
        )}
      </QueryClientProvider>
    </ReduxProvider>
  )
}
```

**Root layout** (`src/app/layout.tsx`):

```typescript
import type { Metadata } from 'next'
import { AppProviders } from '@/shared/providers/AppProviders'
import '@/app/globals.css'

export const metadata: Metadata = {
  title: 'CommunSide',
  description: 'Virtual Office Booking System',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>
        <AppProviders>
          {children}
        </AppProviders>
      </body>
    </html>
  )
}
```

---

## 19. API Reference — All Endpoints

This section maps every backend endpoint to its corresponding frontend file, hook, and query key. Endpoints are grouped by feature and tagged with their implementation phase so it is clear which ones can be wired up immediately and which ones require the backend to be built first.

**Status legend:**
- `AVAILABLE` — backend is implemented, can be wired up now
- `PHASE N` — backend not yet implemented, build the frontend file but leave the hook returning placeholder data until the phase is ready

---

### Auth

These endpoints are called through the **Next.js API proxy routes** (`src/app/api/auth/`), never directly from Axios. They handle cookies server-side.

---

#### `POST /api/auth/login`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Auth** | None |
| **Proxy route** | `src/app/api/auth/login/route.ts` |
| **Hook** | `src/features/auth/hooks/useLogin.ts` |
| **Called from** | `src/features/auth/components/LoginForm.tsx` |

**Request body:**
```json
{
  "email": "admin@virtualoffice.com",
  "password": "Admin@123456"
}
```

**Backend response (consumed by proxy, not exposed to browser):**
```json
{
  "token": "eyJ...",
  "refreshToken": "dGhp...",
  "expiration": "2026-03-07T10:00:00Z"
}
```

**What the proxy returns to the browser:**
```json
{
  "accessToken": "eyJ...",
  "expiresAt": "2026-03-07T10:00:00Z",
  "user": {
    "id": "...",
    "email": "admin@virtualoffice.com",
    "role": "Admin",
    "firstName": "...",
    "lastName": "..."
  }
}
```

**What the proxy sets as cookies:**
- `refresh_token` — httpOnly, Secure, SameSite=Strict, Path=/api/auth
- `has_session=true` — Secure, SameSite=Strict
- `user_role=Admin` — Secure, SameSite=Strict

---

#### `POST /api/auth/refresh`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Auth** | `refresh_token` httpOnly cookie (sent automatically by browser) |
| **Proxy route** | `src/app/api/auth/refresh/route.ts` |
| **Called from** | `AuthSessionProvider` (on page load) · Axios interceptor (on 401) |

**Request body:** None — the proxy reads the httpOnly cookie.

**Backend request (sent by proxy):**
```json
{ "refreshToken": "<value from httpOnly cookie>" }
```

**Returns to browser:**
```json
{
  "accessToken": "eyJ...",
  "expiresAt": "2026-03-07T10:15:00Z",
  "user": {
    "id": "...",
    "email": "admin@virtualoffice.com",
    "role": "Admin"
  }
}
```

---

#### `POST /api/auth/logout`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Auth** | `refresh_token` cookie |
| **Proxy route** | `src/app/api/auth/logout/route.ts` |
| **Hook** | `src/features/auth/hooks/useLogout.ts` |
| **Called from** | Sidebar logout button |

**Request body:** None.

**Proxy behavior:** Forwards refresh token to `.NET`, then clears `refresh_token`, `has_session`, and `user_role` cookies regardless of backend response.

**Returns to browser:** `200 OK` with no body.

---

### Admin — Resources

**API file:** `src/features/admin/resources/api/resourcesApi.ts`
**Auth required:** Admin role JWT

---

#### `GET /api/coworking/admin/resources`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useResources(filters?)` |
| **Hook file** | `src/features/admin/resources/hooks/useResources.ts` |
| **Query key** | `queryKeys.resources.list(filters)` |
| **Page** | `/admin/resources` |

**Query params (all optional):**
```
?resourceType=coworking_seats   (or executive_room, collaboration_room)
?isActive=true                  (or false)
```

**Response:**
```json
[
  {
    "id": "...",
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

---

#### `GET /api/coworking/admin/resources/{id}`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useResourceById(id)` |
| **Hook file** | `src/features/admin/resources/hooks/useResources.ts` |
| **Query key** | `queryKeys.resources.detail(id)` |
| **Page** | `/admin/resources/[id]` |

**Response:** Same shape as a single item from the list above.

**Error:** `404` if ID does not exist.

---

#### `POST /api/coworking/admin/resources`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useCreateResource()` |
| **Hook file** | `src/features/admin/resources/hooks/useCreateResource.ts` |
| **On success** | Invalidates `queryKeys.resources.all` |
| **Page** | `/admin/resources` (modal or inline form) |

**Request body:**
```json
{
  "name": "Seat 21",
  "resourceType": "coworking_seats",
  "capacity": 1,
  "description": "Optional notes about this seat."
}
```

**Response:** The created resource object.

**Errors:**
- `400` — validation failed (name empty, capacity < 1, invalid resourceType string)
- `409` — a resource with this name already exists

---

#### `PUT /api/coworking/admin/resources/{id}`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useUpdateResource()` |
| **Hook file** | `src/features/admin/resources/hooks/useUpdateResource.ts` |
| **On success** | Invalidates `queryKeys.resources.detail(id)` and `queryKeys.resources.all` |
| **Page** | `/admin/resources/[id]` |

**Request body (all fields optional — send only what changed):**
```json
{
  "name": "Window Seat",
  "capacity": 2,
  "description": "Updated description."
}
```

**Response:** The updated resource object.

**Errors:**
- `400` — validation failed
- `404` — resource not found
- `409` — name conflict with another resource

---

#### `PATCH /api/coworking/admin/resources/{id}/toggle`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useToggleResource()` |
| **Hook file** | `src/features/admin/resources/hooks/useToggleResource.ts` |
| **On success** | Invalidates `queryKeys.resources.all` and `queryKeys.resources.detail(id)` |
| **Page** | `/admin/resources` (toggle button in table row) |

**Request body:** None.

**Response:**
```json
{
  "id": "...",
  "name": "Seat 01",
  "isActive": false,
  "message": "'Seat 01' has been disabled."
}
```

**Errors:**
- `404` — resource not found
- `409` — cannot disable while a guest is currently checked in

---

### Admin — Packages

**API file:** `src/features/admin/packages/api/packagesApi.ts`
**Auth required:** Admin role JWT

---

#### `GET /api/coworking/admin/packages`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `usePackages(filters?)` |
| **Hook file** | `src/features/admin/packages/hooks/usePackages.ts` |
| **Query key** | `queryKeys.packages.list(filters)` |
| **Page** | `/admin/packages` |

**Query params (all optional):**
```
?resourceType=coworking_seats
?isActive=true
```

**Response:**
```json
[
  {
    "id": "...",
    "name": "Day Pass",
    "resourceType": "coworking_seats",
    "packageType": "day_pass",
    "priceAmount": 600.00,
    "durationHours": 10,
    "durationDays": null,
    "maxCapacity": null,
    "isStudentEligible": true,
    "studentDiscountPercent": 10.0,
    "downpaymentPercent": 50.0,
    "isActive": true,
    "createdAt": "2026-03-07T08:00:00Z",
    "updatedAt": "2026-03-07T08:00:00Z"
  }
]
```

---

#### `GET /api/coworking/admin/packages/{id}`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `usePackageById(id)` |
| **Hook file** | `src/features/admin/packages/hooks/usePackages.ts` |
| **Query key** | `queryKeys.packages.detail(id)` |
| **Page** | `/admin/packages/[id]` |

**Response:** Same shape as a single item from the list above.

---

#### `POST /api/coworking/admin/packages`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useCreatePackage()` |
| **Hook file** | `src/features/admin/packages/hooks/useCreatePackage.ts` |
| **On success** | Invalidates `queryKeys.packages.all` |
| **Page** | `/admin/packages` |

**Request body:**
```json
{
  "name": "Evening Hourly",
  "resourceType": "coworking_seats",
  "packageType": "hourly",
  "priceAmount": 45.00,
  "durationHours": null,
  "durationDays": null,
  "maxCapacity": null,
  "isStudentEligible": false,
  "studentDiscountPercent": 0,
  "downpaymentPercent": 50
}
```

**Response:** The created package object.

**Errors:**
- `400` — validation failed (price ≤ 0, discount out of range, invalid enum string)
- `409` — package with this name already exists for this resourceType

---

#### `PUT /api/coworking/admin/packages/{id}`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useUpdatePackage()` |
| **Hook file** | `src/features/admin/packages/hooks/useUpdatePackage.ts` |
| **On success** | Invalidates `queryKeys.packages.detail(id)` and `queryKeys.packages.all` |
| **Page** | `/admin/packages/[id]` |

**Request body (all fields optional):**
```json
{
  "priceAmount": 55.00,
  "isStudentEligible": true,
  "studentDiscountPercent": 20
}
```

**Response:** The updated package object.

**Errors:**
- `400` — validation failed
- `404` — package not found
- `409` — name conflict within the same resourceType

---

#### `PATCH /api/coworking/admin/packages/{id}/toggle`

| | |
|---|---|
| **Status** | AVAILABLE |
| **Hook** | `useTogglePackage()` |
| **Hook file** | `src/features/admin/packages/hooks/useTogglePackage.ts` |
| **On success** | Invalidates `queryKeys.packages.all` and `queryKeys.packages.detail(id)` |
| **Page** | `/admin/packages` |

**Request body:** None.

**Response:**
```json
{
  "id": "...",
  "name": "Day Pass",
  "isActive": false,
  "message": "'Day Pass' has been deactivated."
}
```

---

### Public — Availability

**API file:** `src/features/frontdesk/sessions/api/availabilityApi.ts`
**Auth required:** None — public endpoints

---

#### `GET /api/coworking/availability`

| | |
|---|---|
| **Status** | PHASE 3 |
| **Hook** | `useAvailability(params)` |
| **Hook file** | `src/features/frontdesk/sessions/hooks/useAvailability.ts` |
| **Query key** | `queryKeys.availability.byWindow(date, startTime, endTime, resourceType?)` |
| **Page** | `/frontdesk/time-in` (resource picker step) |

**Query params:**
```
?date=2026-03-07
?startTime=09:00
?endTime=17:00
?resourceType=coworking_seats    (optional)
```

**Response:**
```json
[
  {
    "resourceId": "...",
    "resourceName": "Seat 07",
    "resourceType": "coworking_seats",
    "capacity": 1,
    "isAvailable": true
  }
]
```

---

#### `GET /api/coworking/availability/{resourceId}`

| | |
|---|---|
| **Status** | PHASE 3 |
| **Hook** | `useWalkInAvailability(resourceId, estimatedHours)` |
| **Hook file** | `src/features/frontdesk/sessions/hooks/useAvailability.ts` |
| **Query key** | `queryKeys.availability.walkIn(resourceId, estimatedHours)` |
| **Page** | `/frontdesk/time-in` |

**Query params:**
```
?estimatedHours=4
```

**Response:**
```json
{
  "resourceId": "...",
  "resourceName": "Seat 07",
  "isAvailable": true,
  "nextAvailableAt": null
}
```

---

### FrontDesk — Walk-in & Sessions

**API file:** `src/features/frontdesk/time-in/api/timeInApi.ts`
**Auth required:** Admin or FrontDesk role

---

#### `POST /api/coworking/frontdesk/time-in`

| | |
|---|---|
| **Status** | PHASE 4 |
| **Hook** | `useWalkIn()` |
| **Hook file** | `src/features/frontdesk/time-in/hooks/useWalkIn.ts` |
| **On success** | Invalidates `queryKeys.sessions.active` |
| **Page** | `/frontdesk/time-in` |

**Request body:**
```json
{
  "resourceId": "...",
  "servicePackageId": "...",
  "estimatedHours": 4,
  "guestFirstName": "Juan",
  "guestLastName": "Dela Cruz",
  "guestPhone": "9171234567",
  "isStudent": false
}
```

**Response:**
```json
{
  "bookingId": "...",
  "referenceNumber": "BKG-0001",
  "resourceName": "Seat 07",
  "scheduledStart": "2026-03-07T08:00:00Z",
  "scheduledEnd": "2026-03-07T12:00:00Z",
  "snapshotPackageName": "Hourly",
  "baseAmount": 200.00,
  "totalAmount": 200.00
}
```

**Errors:**
- `404` — resource or package not found or inactive
- `409` — resource is currently occupied

---

#### `POST /api/coworking/frontdesk/time-out/{bookingId}`

| | |
|---|---|
| **Status** | PHASE 4 |
| **Hook** | `useTimeOut()` |
| **Hook file** | `src/features/frontdesk/sessions/hooks/useTimeOut.ts` |
| **On success** | Invalidates `queryKeys.sessions.active` and `queryKeys.bookings.detail(id)` |
| **Page** | `/frontdesk/sessions/[id]` |

**Request body:** None.

**Response:** Full booking detail with computed `totalAmount` and `overstayAmount`.

**Errors:**
- `404` — booking not found
- `409` — booking is not in `CheckedIn` status

---

#### `POST /api/coworking/frontdesk/payment`

| | |
|---|---|
| **Status** | PHASE 4 |
| **Hook** | `useProcessPayment()` |
| **Hook file** | `src/features/frontdesk/payments/hooks/usePendingPayments.ts` |
| **On success** | Invalidates `queryKeys.bookings.detail(id)` |
| **Page** | `/frontdesk/bookings/[id]` |

**Request body:**
```json
{
  "bookingId": "...",
  "amount": 200.00,
  "paymentMethod": "cash",
  "paymentType": "balance",
  "notes": null
}
```

**Enum string values for `paymentMethod`:** `"cash"` · `"card"`
**Enum string values for `paymentType`:** `"downpayment"` · `"balance"` · `"overstay_fee"` · `"extension_fee"` · `"refund"`

**Response:** Updated booking with new `paymentStatus`.

---

#### `GET /api/coworking/frontdesk/sessions/active`

| | |
|---|---|
| **Status** | PHASE 4 |
| **Hook** | `useActiveSessions()` |
| **Hook file** | `src/features/frontdesk/sessions/hooks/useActiveSessions.ts` |
| **Query key** | `queryKeys.sessions.active` |
| **Page** | `/frontdesk/sessions` |
| **Recommended staleTime** | 30 seconds (live operational data) |

**Response:**
```json
[
  {
    "bookingId": "...",
    "referenceNumber": "BKG-0001",
    "guestName": "Juan Dela Cruz",
    "resourceName": "Seat 07",
    "actualStart": "2026-03-07T08:00:00Z",
    "scheduledEnd": "2026-03-07T12:00:00Z",
    "snapshotPackageName": "Hourly",
    "totalAmount": 200.00
  }
]
```

---

#### `GET /api/coworking/frontdesk/bookings`

| | |
|---|---|
| **Status** | PHASE 4 |
| **Hook** | `useBookingsByDate(date)` |
| **Hook file** | `src/features/frontdesk/bookings/hooks/useBookings.ts` |
| **Query key** | `queryKeys.bookings.byDate(date)` |
| **Page** | `/frontdesk/bookings` |

**Query params:**
```
?date=2026-03-07
```

**Response:** Array of booking summaries including `bookingStatus` and `paymentStatus`.

---

#### `GET /api/coworking/frontdesk/bookings/{id}`

| | |
|---|---|
| **Status** | PHASE 4 |
| **Hook** | `useBookingDetail(id)` |
| **Hook file** | `src/features/frontdesk/bookings/hooks/useBookings.ts` |
| **Query key** | `queryKeys.bookings.detail(id)` |
| **Page** | `/frontdesk/bookings/[id]` |

**Response:**
```json
{
  "bookingId": "...",
  "referenceNumber": "BKG-0001",
  "bookingType": "walk_in",
  "bookingStatus": "checked_out",
  "paymentStatus": "fully_paid",
  "resourceName": "Seat 07",
  "guestName": "Juan Dela Cruz",
  "scheduledStart": "2026-03-07T08:00:00Z",
  "scheduledEnd": "2026-03-07T12:00:00Z",
  "actualStart": "2026-03-07T08:03:00Z",
  "actualEnd": "2026-03-07T12:47:00Z",
  "snapshotPackageName": "Hourly",
  "baseAmount": 200.00,
  "discountAmount": 0.00,
  "overstayAmount": 37.50,
  "totalAmount": 237.50,
  "downpaymentAmount": 0.00,
  "balanceAmount": 237.50,
  "totalPaid": 237.50,
  "outstandingBalance": 0.00,
  "payments": [],
  "extensions": []
}
```

---

### FrontDesk — Manual Payment Proofs

**API file:** `src/features/frontdesk/payments/api/paymentsApi.ts`
**Auth required:** Admin or FrontDesk role

---

#### `POST /api/coworking/frontdesk/payments/submit-proof`

| | |
|---|---|
| **Status** | PHASE 5 |
| **Hook** | `useSubmitPaymentProof()` |
| **Hook file** | `src/features/frontdesk/payments/hooks/usePendingPayments.ts` |
| **On success** | Invalidates `queryKeys.payments.pending` |
| **Content-Type** | `multipart/form-data` |
| **Page** | `/frontdesk/bookings/[id]` |

**Form fields:**
```
bookingId         string (Guid)
paymentMethod     string  ("gcash" | "maya" | "bank_transfer")
paymentType       string  ("downpayment" | "balance")
amount            number
referenceNumber   string  (guest-provided transaction ref, optional)
file              File    (JPEG or PNG, max 5 MB)
```

**Response:** The created `BookingPayment` record with `paymentValidationStatus: "pending_validation"`.

---

#### `POST /api/coworking/frontdesk/payments/{paymentId}/validate`

| | |
|---|---|
| **Status** | PHASE 5 |
| **Hook** | `useValidatePayment()` |
| **Hook file** | `src/features/frontdesk/payments/hooks/usePendingPayments.ts` |
| **On success** | Invalidates `queryKeys.payments.pending` and `queryKeys.bookings.detail(bookingId)` |
| **Page** | `/frontdesk/payments/[id]` |

**Request body:**
```json
{
  "isApproved": true,
  "rejectionReason": null
}
```

**Response:** Updated payment record with new `paymentValidationStatus`.

**Errors:**
- `404` — payment not found
- `409` — payment is already validated or rejected

---

#### `GET /api/coworking/frontdesk/payments/pending`

| | |
|---|---|
| **Status** | PHASE 5 |
| **Hook** | `usePendingPayments()` |
| **Hook file** | `src/features/frontdesk/payments/hooks/usePendingPayments.ts` |
| **Query key** | `queryKeys.payments.pending` |
| **Page** | `/frontdesk/payments` |
| **Recommended staleTime** | 30 seconds |

**Response:**
```json
[
  {
    "paymentId": "...",
    "bookingId": "...",
    "referenceNumber": "BKG-0042",
    "guestName": "Maria Santos",
    "resourceName": "Seat 03",
    "amount": 300.00,
    "paymentMethod": "gcash",
    "paymentType": "downpayment",
    "proofOfPaymentUrl": "https://...",
    "createdAt": "2026-03-07T09:00:00Z"
  }
]
```

---

### FrontDesk — Upcoming Bookings

#### `GET /api/coworking/frontdesk/bookings/upcoming`

| | |
|---|---|
| **Status** | PHASE 7 |
| **Hook** | `useUpcomingBookings()` |
| **Hook file** | `src/features/frontdesk/bookings/hooks/useBookings.ts` |
| **Query key** | `queryKeys.bookings.upcoming` |
| **Page** | `/frontdesk/dashboard` |

**Response:** Array of confirmed and `pending_student_verification` bookings with `scheduledStart` in the future.

---

### Enum String Reference

All enum values are sent and received as snake_case strings. Use these constants in the frontend to avoid magic strings.

**File:** `src/shared/constants/enums.ts`

```typescript
export const ResourceType = {
  COWORKING_SEATS: 'coworking_seats',
  EXECUTIVE_ROOM: 'executive_room',
  COLLABORATION_ROOM: 'collaboration_room',
} as const

export const PackageType = {
  HOURLY: 'hourly',
  DAY_PASS: 'day_pass',
  HALF_DAY: 'half_day',
  FULL_DAY: 'full_day',
  WEEKLY_PASS: 'weekly_pass',
  MONTHLY_DEDICATED_DESK: 'monthly_dedicated_desk',
} as const

export const BookingStatus = {
  PENDING: 'pending',
  PENDING_STUDENT_VERIFICATION: 'pending_student_verification',
  CONFIRMED: 'confirmed',
  CHECKED_IN: 'checked_in',
  CHECKED_OUT: 'checked_out',
  CANCELLED: 'cancelled',
  NO_SHOW: 'no_show',
  OVERSTAYED: 'overstayed',
} as const

export const PaymentStatus = {
  UNPAID: 'unpaid',
  DOWNPAYMENT_PAID: 'downpayment_paid',
  PARTIALLY_PAID: 'partially_paid',
  FULLY_PAID: 'fully_paid',
} as const

export const PaymentMethod = {
  CASH: 'cash',
  CARD: 'card',
  GCASH: 'gcash',
  MAYA: 'maya',
  BANK_TRANSFER: 'bank_transfer',
  PAYMONGO: 'paymongo',
} as const

export const PaymentType = {
  DOWNPAYMENT: 'downpayment',
  BALANCE: 'balance',
  OVERSTAY_FEE: 'overstay_fee',
  EXTENSION_FEE: 'extension_fee',
  REFUND: 'refund',
} as const
```

---

### Endpoint Build Status Summary

| Endpoint | Method | Status | Frontend page |
|----------|--------|--------|---------------|
| `/api/auth/login` | POST | AVAILABLE | `/login` |
| `/api/auth/refresh` | POST | AVAILABLE | (background) |
| `/api/auth/logout` | POST | AVAILABLE | (sidebar) |
| `/api/coworking/admin/resources` | GET | AVAILABLE | `/admin/resources` |
| `/api/coworking/admin/resources` | POST | AVAILABLE | `/admin/resources` |
| `/api/coworking/admin/resources/{id}` | GET | AVAILABLE | `/admin/resources/[id]` |
| `/api/coworking/admin/resources/{id}` | PUT | AVAILABLE | `/admin/resources/[id]` |
| `/api/coworking/admin/resources/{id}/toggle` | PATCH | AVAILABLE | `/admin/resources` |
| `/api/coworking/admin/packages` | GET | AVAILABLE | `/admin/packages` |
| `/api/coworking/admin/packages` | POST | AVAILABLE | `/admin/packages` |
| `/api/coworking/admin/packages/{id}` | GET | AVAILABLE | `/admin/packages/[id]` |
| `/api/coworking/admin/packages/{id}` | PUT | AVAILABLE | `/admin/packages/[id]` |
| `/api/coworking/admin/packages/{id}/toggle` | PATCH | AVAILABLE | `/admin/packages` |
| `/api/coworking/availability` | GET | PHASE 3 | `/frontdesk/time-in` |
| `/api/coworking/availability/{id}` | GET | PHASE 3 | `/frontdesk/time-in` |
| `/api/coworking/frontdesk/time-in` | POST | PHASE 4 | `/frontdesk/time-in` |
| `/api/coworking/frontdesk/time-out/{id}` | POST | PHASE 4 | `/frontdesk/sessions/[id]` |
| `/api/coworking/frontdesk/payment` | POST | PHASE 4 | `/frontdesk/bookings/[id]` |
| `/api/coworking/frontdesk/sessions/active` | GET | PHASE 4 | `/frontdesk/sessions` |
| `/api/coworking/frontdesk/bookings` | GET | PHASE 4 | `/frontdesk/bookings` |
| `/api/coworking/frontdesk/bookings/{id}` | GET | PHASE 4 | `/frontdesk/bookings/[id]` |
| `/api/coworking/frontdesk/payments/submit-proof` | POST | PHASE 5 | `/frontdesk/bookings/[id]` |
| `/api/coworking/frontdesk/payments/{id}/validate` | POST | PHASE 5 | `/frontdesk/payments/[id]` |
| `/api/coworking/frontdesk/payments/pending` | GET | PHASE 5 | `/frontdesk/payments` |
| `/api/coworking/frontdesk/bookings/upcoming` | GET | PHASE 7 | `/frontdesk/dashboard` |

---

## 20. Settings — Appearance & Preferences

Both roles have a Settings page at their respective route (`/admin/settings`, `/frontdesk/settings`). Settings are **purely client-side** — no backend endpoint is involved. All preferences are stored in `localStorage` via `appSettingsSlice` and applied globally by `ThemeProvider`.

---

### Settings page structure

Both settings pages render the same `SettingsPanel` component. The page itself just provides the shell:

```
/admin/settings    → AdminLayout + SettingsPanel
/frontdesk/settings → FrontDeskLayout + SettingsPanel
```

**File:** `src/shared/components/settings/SettingsPanel.tsx`

The panel is divided into logical sections using Cards:

```
┌───────────────────────────────────────────┐
│ Appearance                                │
│  Theme         ○ Light  ○ Dark  ○ System  │
│  Font Size     ○ Small  ○ Medium ○ Large  │
├───────────────────────────────────────────┤
│ Interface                                 │
│  Compact Sidebar    [toggle]              │
│  Enable Animations  [toggle]              │
├───────────────────────────────────────────┤
│ Account                                   │
│  Change Password    [button → modal]      │
├───────────────────────────────────────────┤
│                          [Reset to defaults] │
└───────────────────────────────────────────┘
```

---

### Settings inventory

| Setting | Type | Options | Stored in | Applied by |
|---------|------|---------|-----------|-----------|
| Theme | Radio | `light` / `dark` / `system` | `appSettingsSlice` → `localStorage` | `ThemeProvider` adds/removes `dark` class on `<html>` |
| Font Size | Radio | `sm` (14px) / `md` (16px) / `lg` (18px) | `appSettingsSlice` → `localStorage` | `ThemeProvider` sets `--base-font-size` CSS var on `<html>` |
| Compact Sidebar | Toggle | on / off | `appSettingsSlice` → `localStorage` | Sidebar component reads `sidebarCompact` from Redux |
| Enable Animations | Toggle | on / off | `appSettingsSlice` → `localStorage` | `ThemeProvider` adds/removes `reduce-motion` class on `<html>` |
| Change Password | Action | — | N/A — calls `PUT /api/account/change-password` (Phase N) | Modal form with current + new password |

---

### useAppSettings hook

**File:** `src/shared/hooks/useAppSettings.ts`

```typescript
import { useDispatch, useSelector } from 'react-redux'
import {
  selectAppSettings,
  setTheme,
  setFontSize,
  toggleSidebarCompact,
  toggleAnimations,
  resetSettings,
} from '@/shared/store/redux/appSettingsSlice'

export function useAppSettings() {
  const dispatch = useDispatch()
  const settings = useSelector(selectAppSettings)

  return {
    ...settings,
    setTheme: (theme: 'light' | 'dark' | 'system') => dispatch(setTheme(theme)),
    setFontSize: (size: 'sm' | 'md' | 'lg') => dispatch(setFontSize(size)),
    toggleSidebarCompact: () => dispatch(toggleSidebarCompact()),
    toggleAnimations: () => dispatch(toggleAnimations()),
    resetSettings: () => dispatch(resetSettings()),
  }
}
```

---

### Tailwind dark mode configuration

Ensure `tailwind.config.ts` uses class-based dark mode so `ThemeProvider` controls it:

```typescript
// tailwind.config.ts
export default {
  darkMode: 'class',   // ← required — uses .dark class on <html>
  // ...rest of config
}
```

With this in place, all standard Tailwind dark variants (`dark:bg-gray-900`, `dark:text-white`, etc.) work automatically whenever `ThemeProvider` toggles the `.dark` class.

---

### Reduce-motion support

When animations are disabled, add CSS that respects the `reduce-motion` class set by `ThemeProvider`:

```css
/* globals.css */
.reduce-motion *,
.reduce-motion *::before,
.reduce-motion *::after {
  animation-duration: 0.01ms !important;
  animation-iteration-count: 1 !important;
  transition-duration: 0.01ms !important;
}
```

This covers all CSS animations and transitions across the entire app, including shadcn/ui component animations.

---

### Change Password (Account section)

The Change Password button opens a modal with a short form. Since this hits a backend endpoint that does not exist yet, the form can be built now with a placeholder mutation:

**Zod schema:**
```typescript
export const changePasswordSchema = z.object({
  currentPassword: z.string().min(1, 'Required'),
  newPassword: z.string().min(10, 'At least 10 characters')
    .regex(/[A-Z]/, 'Must contain uppercase')
    .regex(/[a-z]/, 'Must contain lowercase')
    .regex(/[0-9]/, 'Must contain a number'),
  confirmNewPassword: z.string(),
}).refine((d) => d.newPassword === d.confirmNewPassword, {
  message: 'Passwords do not match',
  path: ['confirmNewPassword'],
})
```

**Planned endpoint (Phase N):**
```
PUT /api/account/change-password
Body: { currentPassword, newPassword }
Auth: Bearer token (any authenticated user)
```

Until the endpoint exists, the mutation can throw a `501 Not Implemented` response and the UI can display a "Coming soon" notice.

---

### Implementation notes

- Settings are loaded from `localStorage` synchronously on store init (`loadFromStorage` in `appSettingsSlice`). There is no loading state — the correct theme is applied before the first render.
- `ThemeProvider` runs `useEffect` on mount and whenever `theme`, `fontSize`, or `enableAnimations` change. There is no flash of unstyled content because the effect runs before paint only on client — for SSR, the `<html>` element will initially have no dark class, which matches the `light` default. Accept this trade-off; a full SSR theme solution requires cookie-based theme detection which adds unnecessary complexity for an admin tool.
- The `sidebarCompact` setting collapses the sidebar to icon-only mode (no labels). This is independent of the mobile sidebar toggle managed by `sidebarSlice`. Both can be true simultaneously — on mobile, the sidebar drawer opens; on desktop, compact mode shows only icons.

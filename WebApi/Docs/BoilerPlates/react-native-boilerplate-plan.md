# React Native Boilerplate — Comprehensive Implementation Plan

---

## Table of Contents

1. [Foundation & Tooling](#1-foundation--tooling)
2. [Architecture: Vertical Slice](#2-architecture-vertical-slice)
3. [Navigation — Expo Router with Deep Linking](#3-navigation--expo-router-with-deep-linking)
4. [Authentication & Session Architecture](#4-authentication--session-architecture)
5. [State Management Architecture](#5-state-management-architecture)
6. [Multi-Step Registration Flow](#6-multi-step-registration-flow)
7. [Platform Compatibility Layer](#7-platform-compatibility-layer)
8. [Shared Component Library](#8-shared-component-library)
9. [API Layer — Axios Architecture](#9-api-layer--axios-architecture)
10. [Security — OWASP Mobile Top 10 Compliance](#10-security--owasp-mobile-top-10-compliance)
11. [Legal & Compliance](#11-legal--compliance)
12. [Performance](#12-performance)
13. [Environment Configuration](#13-environment-configuration)
14. [CI/CD Pipeline](#14-cicd-pipeline)
15. [OTA Updates Strategy](#15-ota-updates-strategy)
16. [Pages Breakdown](#16-pages-breakdown)
17. [Implementation Order](#17-implementation-order)

---

## 1. Foundation & Tooling

### Framework Choice: Expo (EAS) with Bare Workflow

The bare workflow gives you full access to native modules (like Keychain, MMKV) while keeping Expo's excellent DX, EAS Build, and OTA updates. This is the sweet spot between raw React Native CLI and managed Expo.

**Initial Setup Stack:**

| Tool | Purpose |
|------|---------|
| Expo SDK (latest) — Bare Workflow | App foundation with native module access |
| TypeScript (`strict: true`, `noImplicitAny`, `strictNullChecks`) | Type safety and runtime error reduction |
| ESLint (`@typescript-eslint`, `eslint-plugin-react-native`, `eslint-plugin-security`) | Static code analysis |
| Prettier | Consistent code formatting |
| Husky + lint-staged | Pre-commit hooks blocking bad commits |
| Hermes Engine | Enabled by default on both Android and iOS |

---

## 2. Architecture: Vertical Slice

Each feature is a self-contained "slice" folder. Nothing leaks across slices except through shared utilities.

```
src/
├── app/                        # Root navigation, providers, app entry
├── features/
│   ├── auth/                   # Login, registration, token logic
│   │   ├── api/                # Axios calls
│   │   ├── components/         # Feature-specific UI
│   │   ├── hooks/              # useLogin, useRegister
│   │   ├── screens/            # LoginScreen, RegisterScreen
│   │   ├── store/              # Zustand slice or Redux slice
│   │   ├── types/              # TypeScript interfaces
│   │   └── utils/              # Validators, formatters
│   ├── admin/
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── screens/
│   │   ├── store/
│   │   ├── types/
│   │   └── utils/
│   ├── user/
│   │   ├── api/
│   │   ├── components/
│   │   ├── hooks/
│   │   ├── screens/
│   │   ├── store/
│   │   ├── types/
│   │   └── utils/
│   └── profile/
│       ├── api/
│       ├── components/
│       ├── hooks/
│       ├── screens/
│       ├── store/
│       ├── types/
│       └── utils/
├── shared/
│   ├── components/             # Button, Input, Card, etc.
│   ├── hooks/                  # useAppTheme, useSafeArea, etc.
│   ├── config/                 # Platform configs, env vars
│   ├── constants/
│   ├── navigation/             # Expo Router setup, deep linking
│   ├── services/               # Axios instance, interceptors
│   ├── store/                  # Root store setup
│   └── utils/
└── assets/
    ├── fonts/
    ├── images/
    └── icons/
```

---

## 3. Navigation — Expo Router with Deep Linking

Expo Router uses file-based routing (similar to Next.js) and supports deep linking natively.

### Route Structure

```
app/
├── (auth)/
│   ├── _layout.tsx             # Auth layout (redirect if already logged in)
│   ├── login.tsx               # Login / Sign-in screen
│   └── register/
│       ├── _layout.tsx         # Registration layout with progress stepper
│       ├── step-1.tsx          # Basic info (name, email, password)
│       ├── step-2.tsx          # Profile details (avatar, DOB, phone)
│       └── step-3.tsx          # Verification (OTP / email confirm)
├── (protected)/
│   ├── _layout.tsx             # Auth guard — validates JWT, refreshes, or redirects
│   ├── user/
│   │   ├── index.tsx           # User dashboard
│   │   └── profile.tsx         # User info / edit profile page
│   └── admin/
│       └── index.tsx           # Admin dashboard (role-gated)
└── _layout.tsx                 # Root layout — mounts all providers
```

### Deep Linking Config

Deep linking is configured in `app.json` and the root `_layout.tsx`. URL schemes map directly to Expo Router file paths:

```
myapp://auth/login            → app/(auth)/login.tsx
myapp://user/profile          → app/(protected)/user/profile.tsx
myapp://admin                 → app/(protected)/admin/index.tsx
```

### Auth Guard Logic (`(protected)/_layout.tsx`)

1. Read token presence from Zustand auth store
2. If no token → redirect to `/(auth)/login`
3. If token exists but expired → attempt silent refresh via Keychain refresh token
4. If refresh succeeds → allow navigation, update Zustand with new token
5. If refresh fails → clear all tokens, redirect to login

---

## 4. Authentication & Session Architecture

This is the most critical piece — it must be airtight.

### Token Storage Strategy

| Token | Storage Location | Reasoning |
|-------|-----------------|-----------|
| Access Token | Zustand in-memory only | Never written to disk; cleared on app close |
| Refresh Token | React Native Keychain | iOS Keychain / Android Keystore; AES-encrypted; hardware-backed on modern devices |
| Non-sensitive session data | MMKV | Fast synchronous reads; encrypted at rest |

> **Never** use AsyncStorage for any token or sensitive data — it stores data as plaintext.

### JWT Flow

```
User submits login credentials
        ↓
POST /auth/login → { accessToken, refreshToken }
        ↓
┌────────────────────────────────────────┐
│  Store refreshToken → Keychain         │
│  Store accessToken  → Zustand memory   │
└────────────────────────────────────────┘
        ↓
Every outgoing API request:
  Axios interceptor attaches → Authorization: Bearer <accessToken>
        ↓
Server returns 401 (token expired)
        ↓
┌─────────────────────────────────────────────┐
│  Interceptor queues all pending requests     │
│  Reads refreshToken from Keychain            │
│  POST /auth/refresh → { newAccessToken,      │
│                          newRefreshToken }   │
└─────────────────────────────────────────────┘
        ↓
Success → Update Zustand + Keychain → Retry all queued requests
Failure → Clear Zustand + Keychain  → Redirect to /auth/login
```

### Refresh Token Request Queue

A request queue is implemented inside the Axios response interceptor to prevent race conditions. If 3 concurrent requests all receive 401 responses simultaneously, only **one** refresh call is made. All 3 requests are queued and replayed once the new token is received.

```
isRefreshing flag  → prevents duplicate refresh calls
failedQueue array  → holds resolve/reject callbacks for queued requests
On refresh success → resolve all queued requests with new token
On refresh failure → reject all queued requests → force logout
```

### OAuth (Google, Apple, etc.)

- **Expo AuthSession** manages the OAuth redirect flow
- **PKCE (Proof Key for Code Exchange)** is enforced on every OAuth request — mandatory for mobile OAuth security
- **State parameter** is randomly generated per session to prevent CSRF attacks
- **Apple Sign-In** is required by App Store guidelines if any social login is offered on iOS

---

## 5. State Management Architecture

### The Core Rule

> If data comes from a server → **TanStack Query** owns it.  
> If it is purely UI state or local app state → **Zustand** owns it.

### Zustand (Client / UI State)

- Auth state (user object, token presence, user role)
- UI state (theme preference, modal visibility, loading flags)
- Registration wizard state (accumulated multi-step form data)
- Per-feature slices composed into a root store via Zustand's `create` with `immer` middleware

### TanStack Query (Server State)

- All API data fetching, caching, and background refetching
- Request deduplication — identical queries fired simultaneously share one network request
- Stale-while-revalidate pattern for snappy UIs
- Optimistic updates on mutations with automatic rollback on error
- Query keys organized as typed constants inside each feature's `api/` folder

### Redux Toolkit (Optional, Pre-configured)

For complex cross-feature state scenarios (e.g., real-time notifications updating multiple slices, complex undo/redo flows), RTK is included pre-configured but dormant. Teams opt in per-slice without removing Zustand.

### State Layers Summary

```
┌──────────────────────────────────────────────────────┐
│                   TanStack Query                      │
│         Server data · Caching · Mutations             │
├──────────────────────────────────────────────────────┤
│                     Zustand                           │
│      Auth · UI · Registration wizard · Theme          │
├──────────────────────────────────────────────────────┤
│               Redux Toolkit (optional)                │
│        Complex cross-feature global state             │
├──────────────────────────────────────────────────────┤
│                      MMKV                             │
│     Fast local persistence for non-sensitive data     │
├──────────────────────────────────────────────────────┤
│                React Native Keychain                  │
│       Encrypted storage for tokens and secrets        │
└──────────────────────────────────────────────────────┘
```

---

## 6. Multi-Step Registration Flow

### Step Architecture

Each step lives in its own Expo Router screen. A dedicated `registrationSlice` in Zustand accumulates data across steps.

```
Step 1 — Basic Info
  Fields: Full name, email address, password, confirm password
  Validation: Zod schema (email format, password strength min requirements)
  On Next: Save to registrationSlice, navigate to step-2

Step 2 — Profile Details
  Fields: Date of birth, phone number, avatar upload
  Validation: Zod schema (age gate if required, phone format)
  On Next: Merge into registrationSlice, navigate to step-3

Step 3 — Verification
  Fields: OTP input or email verification link
  On Confirm: Fire final POST /auth/register with full accumulated payload
  On Success: Clear registrationSlice, navigate to /(protected)/user

```

### Key Features

- **Zod schemas** defined per step — strict, reusable, composable
- **react-hook-form** drives all form logic — minimal re-renders, clean error handling
- **Back navigation** restores previously entered data from the Zustand slice
- **Progress indicator** (`ProgressStepper` shared component) reused across all steps
- **Partial persistence** — non-sensitive fields (name, step number) survive app close via MMKV; passwords are never persisted
- **Privacy consent** — checkbox linking to Privacy Policy and Terms of Service on step 1 (required for GDPR/CCPA compliance)

---

## 7. Platform Compatibility Layer

Pre-made, centralized configs so platform-specific quirks are handled once and never repeated.

### `shared/config/platform.ts`

```
PlatformConfig {
  shadow:            iOS shadow props  |  Android elevation value
  fontWeight:        typed union avoiding Android font rendering bugs
  hitSlop:           larger touch targets on iOS, tighter on Android
  keyboardBehavior:  'padding' on iOS  |  'height' on Android
  statusBarStyle:    per-platform safe area handling
  hapticFeedback:    Expo Haptics wrapper with silent no-op fallback on unsupported devices
  inputReturnKey:    platform-appropriate return key labels
  datePickerMode:    native picker style per platform
}
```

### `react-native-size-matters` Integration

`scale()`, `verticalScale()`, and `moderateScale()` are wrapped in a custom `useResponsive()` hook. Designers work in a single base resolution (375px width). All spacing, font sizes, and dimensions adapt across all device sizes automatically.

### Shared Component Contract

Every shared component accepts a `style` prop for overrides but resolves all platform differences internally. Consumers never write `Platform.OS === 'ios'` in feature code.

```
<AppButton />   → shadow, haptic, press state handled internally per platform
<AppInput />    → keyboard type, return key, cursor color normalized
<AppText />     → font weight, line height, scaling normalized
<AppCard />     → shadow/elevation normalized
```

---

## 8. Shared Component Library

Every component is: fully typed, accessible (WCAG 2.1 AA), platform-safe, and follows a consistent prop API.

| Component | Key Features |
|-----------|-------------|
| `AppButton` | `variant` prop (primary / ghost / danger / link), loading spinner, disabled state, haptic feedback |
| `AppInput` | Secure text entry, error message display, label, floating label animation, platform keyboard handling |
| `AppText` | Typed `variant` system (heading1–4, body, caption, label), auto-scaling, accessible |
| `AppCard` | Platform-normalized shadow/elevation, pressable variant |
| `AppModal` | Bottom sheet or centered modal, animated via Reanimated, backdrop dismiss |
| `AppSplashScreen` | React Native BootSplash integration, smooth JS transition |
| `ProgressStepper` | Animated step indicator, reused across multi-step registration |
| `ProtectedRoute` | HOC wrapping auth-gated screens with role support |
| `FlashListWrapper` | Typed, pre-configured Shopify FlashList with empty/loading states |
| `AppAvatar` | Image with initials fallback, upload trigger |
| `AppToast` | Cross-platform notification toasts via Reanimated |
| `OTPInput` | Segmented OTP input, auto-advance, paste support |

---

## 9. API Layer — Axios Architecture

### Single Axios Instance (`shared/services/apiClient.ts`)

- Base URL sourced from React Native Config (switches per environment automatically)
- Default timeout: 10 seconds (configurable per request)
- **Request interceptor**: Reads access token from Zustand, attaches `Authorization: Bearer <token>`
- **Response interceptor**: Catches 401 → triggers refresh queue → retries original request
- Request and response logging active in `development` environment only — completely stripped in `production` via environment flag
- All errors normalized into a typed `ApiError` interface before reaching feature code

### Per-Feature API Files

```
features/auth/api/
  authApi.ts         → login(), register(), refreshToken(), logout()

features/user/api/
  userApi.ts         → getProfile(), updateProfile(), deleteAccount()

features/admin/api/
  adminApi.ts        → getUsers(), updateUserRole(), getDashboardStats()
```

### TanStack Query Hook Pattern (per feature)

```
features/auth/hooks/
  useLogin.ts        → useMutation wrapping authApi.login()
  useRegister.ts     → useMutation wrapping authApi.register()

features/user/hooks/
  useProfile.ts      → useQuery wrapping userApi.getProfile()
  useUpdateProfile.ts → useMutation with optimistic update
```

---

## 10. Security — OWASP Mobile Top 10 Compliance

| OWASP Risk | Threat | Implementation |
|-----------|--------|---------------|
| M1 — Improper Credential Usage | Tokens stored insecurely | Keychain/SecureStore only; zero AsyncStorage for secrets |
| M2 — Inadequate Supply Chain | Malicious dependencies | `npm audit` in CI pipeline; pinned dependency versions in lockfile |
| M3 — Insecure Authentication | Weak auth flows | PKCE OAuth; short-lived JWTs; rotating refresh tokens |
| M4 — Insufficient Input/Output Validation | Injection, XSS | Zod schemas on all inputs; API response validation |
| M5 — Insecure Communication | MITM attacks | Certificate pinning via `react-native-ssl-pinning` |
| M6 — Inadequate Privacy Controls | PII leakage | No PII in logs; no PII in MMKV or AsyncStorage |
| M7 — Insufficient Binary Protections | Reverse engineering | ProGuard/R8 enabled on Android; Hermes bytecode compilation |
| M8 — Security Misconfiguration | Exposed secrets | All secrets via RN Config env vars; nothing hardcoded in source |
| M9 — Insecure Data Storage | Plaintext sensitive data | MMKV (encrypted) for non-sensitive; Keychain for sensitive |
| M10 — Insufficient Cryptography | Weak encryption | Delegated to OS Keychain — hardware-backed AES on modern devices |

### Additional Security Measures

- **Root/Jailbreak detection** via `react-native-device-info` — warn or restrict on compromised devices
- **Screenshot prevention** on sensitive screens (token entry, profile) on Android via `FLAG_SECURE`
- **Biometric authentication** gate for app re-entry via Expo LocalAuthentication
- **Session timeout** — auto-logout after configurable inactivity period via background timer in Zustand

---

## 11. Legal & Compliance

### GDPR / CCPA

- Consent screen on first launch — user must explicitly accept before any data collection
- Granular consent options (analytics, marketing) stored in MMKV
- **Data deletion flow** available in User Info page — sends `DELETE /users/me` and clears all local storage
- Privacy Policy and Terms of Service linked during registration Step 1 — checkbox required to proceed

### Apple App Store Guidelines

- Apple Sign-In **required** if any other social login (Google, Facebook) is offered on iOS
- No misleading authentication flows
- In-app purchases must use StoreKit if digital goods are sold

### Google Play Guidelines

- Target API level must meet current Play Store requirements (updated annually)
- Sensitive permissions (camera, contacts) must have clear user-facing justification

### Accessibility (WCAG 2.1 AA)

- All interactive components have `accessibilityLabel` and `accessibilityRole`
- Minimum color contrast ratios enforced via design tokens
- `accessibilityHint` on non-obvious interactive elements
- VoiceOver (iOS) and TalkBack (Android) manually tested

### COPPA

- Age gate implemented during registration if the app may serve users under 13
- Under-13 users blocked from account creation

---

## 12. Performance

### Hermes JavaScript Engine

- Enabled by default in both Android (`android/app/build.gradle`) and iOS (`Podfile`)
- Compiles JavaScript to bytecode at build time — faster startup, lower memory usage, smaller bundle size

### Shopify FlashList

- Replaces `FlatList` and `ScrollView` for all lists
- Estimated item size provided per list type to prevent layout recalculation
- `recycleItems` enabled for maximum memory efficiency

### React Native Reanimated 3

- All animations run on the **UI thread** via worklets — zero JavaScript thread involvement
- Used for: screen transitions, modal animations, skeleton loaders, gesture-driven interactions
- No `Animated` API from core React Native used anywhere

### TanStack Query Caching

- `staleTime` configured per query type (e.g., user profile: 5 min; admin dashboard: 30 sec)
- `gcTime` controls how long inactive cache data is kept in memory
- Background refetch on window focus and network reconnect

### MMKV Persistence

- Synchronous reads eliminate async waterfall on app startup
- Auth state hydration happens before the first render — no loading flash
- Encrypted storage instance configured once at app startup

### Bundle Optimization

- Expo Router lazy-loads each route bundle — only the current screen's code is loaded
- Tree-shaking via Metro bundler with `sideEffects: false` in packages where applicable
- All `console.log` statements stripped in production build via Babel plugin

---

## 13. Environment Configuration

### Three Environments

| Environment | API | Logging | OTA | Sentry |
|-------------|-----|---------|-----|--------|
| `development` | Local / ngrok | Verbose | Disabled | Off |
| `staging` | Staging API | Errors only | Enabled | Staging DSN |
| `production` | Production API | Off | Enabled | Production DSN |

### Environment Files

```
.env.development
.env.staging
.env.production
```

### React Native Config Wrapper (`shared/config/env.ts`)

All environment variables are accessed through a single typed config object — never referenced directly from `process.env` in feature code. This provides autocomplete, type safety, and a single place to validate required env vars at startup.

### Secrets Management

- API keys and secrets are **never** committed to source control
- CI/CD secrets stored in GitHub Actions Secrets or EAS Secrets
- `.env.*` files added to `.gitignore`

---

## 14. CI/CD Pipeline

### Pipeline Flow

```
Developer runs git commit
        ↓
Husky pre-commit hook
  ├── ESLint (auto-fix where possible, fail on errors)
  ├── Prettier (format check)
  └── TypeScript (tsc --noEmit)
        ↓
Push to GitHub
        ↓
GitHub Actions — PR Check
  ├── Install dependencies (cached)
  ├── ESLint + TypeScript full check
  ├── Jest unit tests + coverage threshold
  └── Bundle size check
        ↓
Merge to main / develop
        ↓
EAS Build triggered
  ├── Android build
  │   ├── Internal: .apk (direct install, no Play Store needed)
  │   └── Production: .aab (Play Store submission)
  └── iOS build
      ├── Internal: .ipa (TestFlight / Ad Hoc)
      └── Production: .ipa (App Store submission)
        ↓
EAS Submit (optional — automates store submission)
        ↓
Expo Updates — OTA push for JS-only patches
```

### APK for Direct Testing

EAS Build configuration for direct `.apk` output (no Play Store required):

```json
{
  "build": {
    "preview": {
      "android": {
        "buildType": "apk",
        "distribution": "internal"
      }
    }
  }
}
```

### Fastlane Integration

- **Android**: Manages signing keystores, uploads to Play Store internal track
- **iOS**: Manages provisioning profiles via `match`, uploads to TestFlight
- **Screenshots**: Automated screenshot generation for store listings
- Triggered by GitHub Actions on tagged releases

---

## 15. OTA Updates Strategy

### Expo Updates Configuration

```
Channel: development  → local testing builds
Channel: staging      → QA and internal testing
Channel: production   → live user builds
```

### Update Policy

| Change Type | Deployment Method |
|-------------|------------------|
| JS/TS logic fix, UI tweak, copy change | OTA via Expo Updates (no store review) |
| New screen or feature | OTA if no native changes |
| New native module, permission change | Full store release required |
| Security patch (critical) | OTA immediately, then store release |

### Rollback

EAS channel pinning allows immediate rollback to any previous OTA bundle if a bad update is deployed. Rollback takes effect on next app foreground.

---

## 16. Pages Breakdown

### Login / Sign-in Screen

- Email + password form with Zod validation
- OAuth buttons (Google, Apple) — PKCE enforced
- "Remember me" toggle — extends refresh token TTL
- Forgot password link
- Redirect to registration if no account

### Registration / Sign-up (Multi-Step)

- **Step 1**: Full name, email, password, confirm password, Privacy Policy + ToS checkbox
- **Step 2**: Date of birth, phone number, optional avatar upload
- **Step 3**: OTP entry or email verification link
- Progress stepper visible across all steps
- Back button restores previous step data from Zustand slice
- Final submit fires `POST /auth/register` with full accumulated payload

### User Dashboard Page

- Welcome header with user display name
- Primary content via FlashList (posts, activity, feed — depends on app domain)
- Reanimated entrance animations
- Pull-to-refresh via TanStack Query `refetch`
- Navigation to User Info Page

### User Info / Profile Page

- Display and edit: name, email, phone, avatar
- Avatar upload with image picker
- Change password flow
- GDPR data deletion CTA with confirmation modal
- Logout button (also accessible globally)

### Admin Dashboard Page

- Role-gated at both navigation level (`(protected)/_layout.tsx`) and API level (server enforces admin role on all admin endpoints)
- User list via FlashList with search and filter
- User role management (promote/demote)
- Dashboard stats via TanStack Query
- Graceful 403 handling if non-admin somehow reaches this route

### Logout

Logout is not a page — it is a shared utility action:

1. Call `POST /auth/logout` (server invalidates refresh token)
2. Clear Zustand auth store
3. Delete refresh token from Keychain
4. Invalidate and clear all TanStack Query cache
5. Clear relevant MMKV keys
6. Navigate to `/(auth)/login` with `router.replace` (no back navigation to protected screens)

---

## 17. Implementation Order

Follow this sequence to build on a stable foundation at each phase before adding complexity.

| Phase | Steps |
|-------|-------|
| **Phase 1 — Foundation** | Expo bare workflow init · TypeScript strict config · ESLint + Prettier + Husky · Git repository setup |
| **Phase 2 — Navigation** | Expo Router file structure · Root layout with providers · Deep linking config · Auth/protected route groups |
| **Phase 3 — API Layer** | Axios instance · Request/response interceptors · Token refresh queue · Typed ApiError |
| **Phase 4 — Secure Storage** | React Native Keychain integration · MMKV integration · Zustand auth slice with hydration |
| **Phase 5 — State Management** | TanStack Query client config · Zustand root store · Query key conventions |
| **Phase 6 — Shared Components** | AppButton · AppInput · AppText · AppCard · AppModal · ProgressStepper · FlashListWrapper |
| **Phase 7 — Platform Layer** | platform.ts config · useResponsive hook · size-matters integration · Component platform testing |
| **Phase 8 — Auth Feature Slice** | Login screen · Multi-step registration · OAuth (Google + Apple) · Logout utility |
| **Phase 9 — Protected Routes** | Auth guard implementation · Role-based access · 403 and 401 handling |
| **Phase 10 — Feature Slices** | User dashboard · User info/profile · Admin dashboard |
| **Phase 11 — Performance** | Reanimated 3 animations · FlashList optimization · MMKV startup hydration · Bundle audit |
| **Phase 12 — Environments** | React Native Config setup · .env files · Per-environment Axios base URLs |
| **Phase 13 — Splash + Boot** | React Native BootSplash · Smooth native-to-JS transition · App icon + splash assets |
| **Phase 14 — Security Audit** | OWASP checklist pass · SSL pinning · Root detection · Screenshot prevention |
| **Phase 15 — EAS Build** | EAS project setup · Build profiles (dev/staging/prod) · APK output config · iOS Ad Hoc config |
| **Phase 16 — CI/CD** | GitHub Actions workflows · EAS Build trigger · Jest coverage enforcement |
| **Phase 17 — OTA + Fastlane** | Expo Updates channel config · Fastlane lanes for Android + iOS · Rollback procedure documentation |

---

## Technology Reference

| Category | Library | Version Strategy |
|----------|---------|-----------------|
| Framework | Expo (Bare) | Latest stable SDK |
| Language | TypeScript | Strict mode |
| Navigation | Expo Router | File-based, deep linking |
| Client State | Zustand + Immer | Latest stable |
| Server State | TanStack Query v5 | Latest stable |
| Optional State | Redux Toolkit | Pre-configured, opt-in |
| HTTP Client | Axios | Latest stable |
| Forms | react-hook-form + Zod | Latest stable |
| Secure Storage | React Native Keychain | Latest stable |
| Fast Storage | React Native MMKV | Latest stable |
| Animations | React Native Reanimated 3 | Latest stable |
| Lists | Shopify FlashList | Latest stable |
| Responsive | react-native-size-matters | Latest stable |
| Environment | React Native Config | Latest stable |
| OTA Updates | Expo Updates | Bundled with SDK |
| Splash Screen | React Native BootSplash | Latest stable |
| JS Engine | Hermes | Bundled with React Native |
| CI/CD | EAS Build + GitHub Actions | — |
| Deployment | Fastlane | Latest stable |
| Linting | ESLint + Prettier | Latest stable |
| Git Hooks | Husky + lint-staged | Latest stable |

---

*This plan covers all requirements. Implementation proceeds phase by phase — each phase produces a stable, testable state before the next begins.*

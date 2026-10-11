# MobileLoginAuth — Plan

A mobile login app, to be published on the Apple App Store, that notifies the user by **email** and **real-time push (SignalR)** whenever they log in successfully, fail a login attempt, or log out.

| Layer | Technology |
|---|---|
| Mobile UI | React Native (Expo, TypeScript) |
| Backend API | C# ASP.NET Core Web API (.NET 8) |
| Authentication | JWT (access token + refresh token) |
| Database | SQLite via Entity Framework Core |
| Message broker | RabbitMQ (via MassTransit) |
| Real-time notifications | SignalR |
| Email | SMTP via MailKit (MailHog / smtp4dev for local dev) |
| Unit tests | xUnit + Moq + FluentAssertions (backend), Jest + React Native Testing Library (mobile) |

---

## 1. Plan

### 1.1 Architecture

```
┌──────────────────────┐   HTTPS (REST + JWT)   ┌──────────────────────────────┐
│  React Native App    │ ─────────────────────► │  MobileLoginAuth.Api         │
│  - Login screen      │                        │  - AuthController            │
│  - Home screen       │ ◄───── SignalR ─────── │  - NotificationHub (SignalR) │
│  - Notifications     │   (WebSocket, JWT)     │  - AuthService / TokenService│
└──────────────────────┘                        └──────────────┬───────────────┘
                                                               │ EF Core
                                                               ▼
                                                        ┌─────────────┐
                                                        │   SQLite    │
                                                        └─────────────┘
                                                               │
                              publish AuthEvent                │
       MobileLoginAuth.Api ───────────────────────► ┌──────────────────┐
                                                    │    RabbitMQ      │
                                                    │ exchange:        │
                                                    │  auth-events     │
                                                    └───────┬──────────┘
                                         ┌──────────────────┴─────────────────┐
                                         ▼                                    ▼
                              ┌─────────────────────┐            ┌─────────────────────────┐
                              │ EmailConsumer       │            │ SignalRConsumer         │
                              │ → SMTP (MailKit)    │            │ → IHubContext.SendAsync │
                              └─────────────────────┘            └─────────────────────────┘
```

**Flow:** The API never sends email or SignalR messages inside the HTTP request. It records the event in SQLite and publishes an `AuthEvent` message to RabbitMQ. Two consumers pick it up independently: one sends the email, the other pushes the SignalR notification. This keeps login fast and means a slow or failing mail server never blocks a login.

### 1.2 Solution structure

```
MobileLoginAuth/
├── Plan.md
├── README.md
├── docker-compose.yml               # RabbitMQ + MailHog (+ API)
├── backend/
│   ├── MobileLoginAuth.sln
│   ├── src/
│   │   ├── MobileLoginAuth.Api/            # Controllers, Hubs, Program.cs, DI
│   │   ├── MobileLoginAuth.Application/    # Services, interfaces, DTOs, validators
│   │   ├── MobileLoginAuth.Domain/         # Entities, enums, events
│   │   └── MobileLoginAuth.Infrastructure/ # EF Core/SQLite, RabbitMQ, Email, JWT
│   └── tests/
│       ├── MobileLoginAuth.UnitTests/
│       └── MobileLoginAuth.IntegrationTests/   # optional, WebApplicationFactory
└── mobile/
    ├── App.tsx
    ├── src/
    │   ├── api/            # axios client, auth endpoints
    │   ├── auth/           # AuthContext, secure token storage
    │   ├── notifications/  # SignalR connection hook
    │   ├── screens/        # LoginScreen, RegisterScreen, HomeScreen
    │   └── components/
    └── __tests__/
```

### 1.3 Milestones

| # | Milestone | Outcome |
|---|---|---|
| M1 | Repo + scaffolding | Solution, projects, Expo app, docker-compose, CI skeleton |
| M2 | Data + auth core | SQLite schema, register/login/logout/refresh with JWT |
| M3 | Messaging | RabbitMQ publisher + consumers wired with MassTransit |
| M4 | Notifications | Email consumer (templates) + SignalR hub/consumer |
| M5 | Mobile app | Login/Register/Home screens, token storage, SignalR client, toasts |
| M6 | Unit tests | Backend + mobile test suites passing, coverage report |
| M7 | Docs + polish | README, run instructions, GitHub Actions CI green |
| M8 | iOS release | Backend deployed to a public HTTPS host, app built with EAS and published on the Apple App Store |

---

## 2. Requirements

### 2.1 Functional requirements

| ID | Requirement |
|---|---|
| FR-01 | A user can register with email, password and display name. |
| FR-02 | A user can log in with email + password and receives a JWT access token and a refresh token. |
| FR-03 | On **successful login**, the user receives an email ("New sign-in to your account") with time, device/platform and IP. |
| FR-04 | On **invalid login** (wrong password for an existing account), the account owner receives an email ("Failed sign-in attempt") with time, device and IP. Unknown emails are logged but no email is sent. |
| FR-05 | On **logout**, the user receives an email ("You signed out") and the refresh token is revoked. |
| FR-06 | For each of the three events, a real-time SignalR notification is pushed to the user's other connected sessions (and the current one for login/logout). |
| FR-07 | The mobile app shows an in-app toast/banner when a SignalR notification arrives. |
| FR-08 | An access token can be renewed with a valid refresh token; refresh tokens rotate on use. |
| FR-09 | After 5 consecutive failed attempts, the account is locked for 15 minutes (configurable). |
| FR-10 | Every auth event (success, failure, logout) is persisted in a `LoginAudit` table. |
| FR-11 | A protected endpoint `GET /api/users/me` returns the current user's profile (proves JWT works). |
| FR-12 | A user can delete their account from the app (Apple App Store requirement). |

### 2.2 Non-functional requirements

| ID | Requirement |
|---|---|
| NFR-01 | Passwords are hashed with ASP.NET Core `PasswordHasher` (PBKDF2) — never stored in plain text. |
| NFR-02 | JWT: HMAC-SHA256, access token lifetime 15 min, refresh token 7 days; secret comes from configuration / user-secrets, not source code. |
| NFR-03 | Login response time is not affected by email delivery (async via RabbitMQ). |
| NFR-04 | Messages are durable; failed email sends are retried (MassTransit retry: 3 attempts, exponential backoff), then moved to an error queue. |
| NFR-05 | Login error responses do not reveal whether the email exists ("Invalid email or password"). |
| NFR-06 | Tokens are stored on the device using `expo-secure-store` (Keychain / Keystore). |
| NFR-07 | Structured logging (Serilog) with correlation id per request. |
| NFR-08 | Backend unit test coverage ≥ 80% on Application layer. |
| NFR-09 | Everything runs locally with `docker compose up` + `dotnet run` + `npx expo start`. |

### 2.3 API endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | — | Create account |
| POST | `/api/auth/login` | — | Login, returns `{ accessToken, refreshToken, expiresAt, user }` |
| POST | `/api/auth/refresh` | — | Rotate refresh token, new access token |
| POST | `/api/auth/logout` | JWT | Revoke refresh token, publish logout event |
| GET | `/api/users/me` | JWT | Current user profile |
| GET | `/api/users/me/login-history` | JWT | Recent `LoginAudit` entries |
| DELETE | `/api/users/me` | JWT | Delete account (App Store requirement) |
| WS | `/hubs/notifications` | JWT (query `access_token`) | SignalR hub |

### 2.4 Data model (SQLite)

**Users**
| Column | Type | Notes |
|---|---|---|
| Id | GUID (TEXT) | PK |
| Email | TEXT | unique, normalized lowercase |
| DisplayName | TEXT | |
| PasswordHash | TEXT | |
| FailedLoginCount | INTEGER | reset on success |
| LockoutEndUtc | TEXT (DateTime) | nullable |
| CreatedAtUtc | TEXT (DateTime) | |

**RefreshTokens**
| Column | Type | Notes |
|---|---|---|
| Id | GUID | PK |
| UserId | GUID | FK → Users |
| TokenHash | TEXT | SHA-256 of token, never raw |
| ExpiresAtUtc | DateTime | |
| RevokedAtUtc | DateTime | nullable |
| ReplacedByTokenHash | TEXT | nullable (rotation chain) |
| DeviceInfo | TEXT | |

**LoginAudits**
| Column | Type | Notes |
|---|---|---|
| Id | GUID | PK |
| UserId | GUID | nullable (unknown email) |
| Email | TEXT | as entered |
| EventType | INTEGER | `LoginSucceeded`, `LoginFailed`, `Logout` |
| IpAddress | TEXT | |
| DeviceInfo | TEXT | |
| OccurredAtUtc | DateTime | |

### 2.5 Message contract (RabbitMQ)

```csharp
public record AuthEvent(
    Guid EventId,
    AuthEventType Type,      // LoginSucceeded | LoginFailed | Logout
    Guid UserId,
    string Email,
    string DisplayName,
    string? IpAddress,
    string? DeviceInfo,
    DateTime OccurredAtUtc);
```

- Exchange: `auth-events` (fanout, durable), managed by MassTransit.
- Queues: `auth-events-email`, `auth-events-signalr`.
- Consumers are idempotent on `EventId`.

### 2.6 SignalR contract

- Hub: `NotificationHub` at `/hubs/notifications`, `[Authorize]`.
- Users are addressed with `Clients.User(userId)` (user id from JWT `sub` claim via `IUserIdProvider`).
- Client method: `ReceiveNotification(NotificationDto { type, title, message, occurredAtUtc })`.

### 2.7 Prerequisites

- .NET 8 SDK
- Node.js 20 LTS, Expo CLI, Android emulator / iOS simulator or Expo Go
- Docker Desktop (RabbitMQ `rabbitmq:3-management`, MailHog `mailhog/mailhog`)

---

## 3. Execution

### Phase 1 — Scaffolding (M1)
1. Create GitHub repo `MobileLoginAuth`, add `.gitignore` (VisualStudio + Node), `README.md`, this `Plan.md`.
2. `dotnet new sln` + four `classlib`/`webapi` projects + `xunit` test project; set project references (Api → Application/Infrastructure → Domain).
3. `npx create-expo-app mobile --template expo-template-blank-typescript`.
4. `docker-compose.yml` with RabbitMQ (5672, 15672 UI) and MailHog (1025 SMTP, 8025 UI).
5. GitHub Actions workflow: `dotnet test` and `npm test` on push/PR.

### Phase 2 — Data + Auth core (M2)
1. Domain entities `User`, `RefreshToken`, `LoginAudit`, enum `AuthEventType`.
2. `AppDbContext` (EF Core SQLite), initial migration, auto-migrate on startup in Development.
3. `IPasswordHasher<User>` wrapper, `ITokenService` (create access JWT, create/hash refresh token).
4. `IAuthService` with `RegisterAsync`, `LoginAsync`, `RefreshAsync`, `LogoutAsync` — includes lockout logic and audit writes.
5. `AuthController` + `UsersController`; FluentValidation for request DTOs.
6. JWT bearer configuration in `Program.cs`, including reading `access_token` from query string for `/hubs/*`.

### Phase 3 — Messaging (M3)
1. Add MassTransit.RabbitMQ; configure host from `appsettings` (`RabbitMq:Host/User/Password`).
2. `IAuthEventPublisher` abstraction (wraps `IPublishEndpoint`) so `AuthService` stays testable.
3. `AuthService` publishes `AuthEvent` after a login success, a failed login for an existing user, and logout.
4. Retry policy + error queue configuration.

### Phase 4 — Notifications (M4)
1. `IEmailSender` (MailKit) + `IEmailTemplateRenderer` with three templates (success, failed, logout).
2. `EmailNotificationConsumer : IConsumer<AuthEvent>` → render template → send.
3. `NotificationHub`, `SubClaimUserIdProvider`, `SignalRNotificationConsumer : IConsumer<AuthEvent>` → `IHubContext<NotificationHub>.Clients.User(...).SendAsync("ReceiveNotification", dto)`.
4. Manual check: login from Swagger → email visible in MailHog UI, message visible in RabbitMQ UI.

### Phase 5 — Mobile app (M5)
1. Navigation (`@react-navigation/native-stack`): `LoginScreen`, `RegisterScreen`, `HomeScreen`.
2. `api/client.ts` (axios) with interceptor that attaches the access token and refreshes on 401.
3. `AuthContext` (login, logout, register, restore session) storing tokens in `expo-secure-store`.
4. `useNotifications` hook using `@microsoft/signalr`: connect after login with `accessTokenFactory`, auto-reconnect, disconnect on logout; show toast (`react-native-toast-message`).
5. Home screen: profile + recent login history + logout button.
6. Configurable API base URL (`app.config.ts` / `.env`) — `10.0.2.2` for Android emulator.

### Phase 6 — Unit tests (M6)
See section 4. Run on every push via GitHub Actions.

### Phase 7 — Docs + polish (M7)
README with: architecture diagram, setup steps, how to see emails (MailHog `http://localhost:8025`), RabbitMQ UI (`http://localhost:15672`), how to run tests.

### Phase 8 — Publish to the Apple App Store (M8)
1. **Deploy the backend** to a public HTTPS host (e.g. Azure App Service or a VPS with Docker). Production config: managed RabbitMQ (e.g. CloudAMQP), real SMTP provider, JWT secret from environment/Key Vault, SQLite file on a persistent volume (or migrate to PostgreSQL/SQL Server if scale requires). Apple requires HTTPS (App Transport Security).
2. **[Jay]** **Apple Developer Program** membership (USD 99/year) and an App Store Connect app record with bundle id, e.g. `com.csharpibocbina.mobileloginauth`.
3. **App config:** `app.config.ts` with `ios.bundleIdentifier`, version/build number, app icon (1024×1024), splash screen, production API URL.
4. **Build:** `eas build --platform ios --profile production` (EAS handles certificates and provisioning profiles).
5. **[Jay signs in / approves]** **TestFlight:** `eas submit --platform ios`, test on real devices with internal testers.
6. **App Store review requirements:**
   - Privacy policy URL and App Privacy "nutrition label" (email address, device info, IP collected for security notifications).
   - **In-app account deletion** (required by Apple for apps that support account creation) → add `DELETE /api/users/me` and a "Delete account" button.
   - Demo account credentials for the reviewer in App Review notes.
   - Screenshots for required iPhone sizes, description, keywords, support URL.
7. **[Jay]** **Submit for review** in App Store Connect → release.

Steps marked **[Jay]** need your own Apple Developer account; everything else Claude can prepare. Jay will use their own Apple ID and sign in directly (`eas login` / `eas build` prompts, App Store Connect in the browser) on their own machine. The Apple ID password and 2FA codes are never pasted into chat or committed; CI uses an App Store Connect API key stored as a GitHub secret.

### How to run (target)
```bash
docker compose up -d                       # RabbitMQ + MailHog
cd backend && dotnet run --project src/MobileLoginAuth.Api
cd mobile && npm install && npx expo start
```

---

## 4. Unit Test

### 4.1 Tooling
- **Backend:** xUnit, Moq, FluentAssertions, `Microsoft.EntityFrameworkCore.Sqlite` in-memory (`DataSource=:memory:`) for repository-level tests, MassTransit `InMemoryTestHarness` for consumers, Coverlet for coverage.
- **Mobile:** Jest (jest-expo preset), React Native Testing Library, `jest.mock` for axios, secure-store and SignalR.

### 4.2 Backend test cases

**AuthServiceTests**
| ID | Test | Expected |
|---|---|---|
| UT-01 | `Register_WithNewEmail_CreatesUserWithHashedPassword` | User saved; hash ≠ plain password |
| UT-02 | `Register_WithExistingEmail_ReturnsConflict` | Failure result, no user added |
| UT-03 | `Login_WithValidCredentials_ReturnsTokens` | Access + refresh token returned |
| UT-04 | `Login_WithValidCredentials_PublishesLoginSucceededEvent` | Publisher called once with `LoginSucceeded` |
| UT-05 | `Login_WithValidCredentials_ResetsFailedLoginCount` | `FailedLoginCount == 0` |
| UT-06 | `Login_WithWrongPassword_ReturnsUnauthorized` | Failure, generic message |
| UT-07 | `Login_WithWrongPassword_PublishesLoginFailedEvent` | Publisher called with `LoginFailed` |
| UT-08 | `Login_WithWrongPassword_IncrementsFailedLoginCount` | Count + 1 |
| UT-09 | `Login_WithUnknownEmail_DoesNotPublishEvent` | Publisher never called; audit row written |
| UT-10 | `Login_AfterFiveFailures_LocksAccount` | `LockoutEndUtc` set; 6th attempt rejected even with correct password |
| UT-11 | `Login_WritesLoginAudit_ForEveryAttempt` | Audit row with correct `EventType` |
| UT-12 | `Logout_RevokesRefreshToken` | `RevokedAtUtc` set |
| UT-13 | `Logout_PublishesLogoutEvent` | Publisher called with `Logout` |
| UT-14 | `Refresh_WithValidToken_RotatesToken` | Old token revoked, new token issued |
| UT-15 | `Refresh_WithRevokedToken_ReturnsUnauthorized` | Failure |
| UT-16 | `Refresh_WithExpiredToken_ReturnsUnauthorized` | Failure |

**TokenServiceTests**
| ID | Test | Expected |
|---|---|---|
| UT-20 | `CreateAccessToken_ContainsSubAndEmailClaims` | Claims present |
| UT-21 | `CreateAccessToken_ExpiresAfterConfiguredMinutes` | `exp` ≈ now + 15 min |
| UT-22 | `CreateAccessToken_IsValidWithConfiguredKey` | Validates with signing key; fails with a different key |
| UT-23 | `CreateRefreshToken_IsRandomAndHashed` | Two tokens differ; stored hash ≠ raw |

**Consumer tests (MassTransit test harness)**
| ID | Test | Expected |
|---|---|---|
| UT-30 | `EmailConsumer_LoginSucceeded_SendsSuccessEmail` | `IEmailSender.SendAsync` called with success subject + user email |
| UT-31 | `EmailConsumer_LoginFailed_SendsFailedAttemptEmail` | Failed-attempt subject |
| UT-32 | `EmailConsumer_Logout_SendsLogoutEmail` | Logout subject |
| UT-33 | `EmailConsumer_WhenSmtpThrows_MessageIsRetried` | Consumer faulted → retry observed |
| UT-34 | `SignalRConsumer_SendsToUserGroup` | `Clients.User(userId).SendCoreAsync("ReceiveNotification", …)` |
| UT-35 | `Consumers_IgnoreDuplicateEventId` | Second identical message not re-sent |

**Controller / validation tests**
| ID | Test | Expected |
|---|---|---|
| UT-40 | `LoginRequestValidator_RejectsEmptyOrInvalidEmail` | Validation error |
| UT-41 | `RegisterRequestValidator_RejectsWeakPassword` | Min 8 chars, upper, lower, digit |
| UT-42 | `AuthController_Login_ReturnsOk_OnSuccess` | 200 with tokens |
| UT-43 | `AuthController_Login_Returns401_OnFailure` | 401 |
| UT-44 | `AuthController_Logout_RequiresAuthorization` | `[Authorize]` attribute present |

**Email template tests**
| ID | Test | Expected |
|---|---|---|
| UT-50 | `Renderer_IncludesDisplayNameTimeAndDevice` | Placeholders replaced for all three templates |

### 4.3 Mobile test cases

| ID | Test | Expected |
|---|---|---|
| MT-01 | LoginScreen renders email, password inputs and Login button | Elements found |
| MT-02 | LoginScreen shows validation error for empty fields | Error text shown, API not called |
| MT-03 | Successful login stores tokens and navigates to Home | secure-store `setItemAsync` called; Home rendered |
| MT-04 | Failed login shows "Invalid email or password" | Error message shown |
| MT-05 | Logout clears tokens, calls `/auth/logout`, returns to Login | secure-store `deleteItemAsync` called |
| MT-06 | API client attaches `Authorization: Bearer` header | Header present |
| MT-07 | API client refreshes token on 401 and retries once | Refresh called, original request retried |
| MT-08 | `useNotifications` starts SignalR connection after login and stops on logout | `start`/`stop` called |
| MT-09 | Incoming `ReceiveNotification` shows a toast | Toast shown with title/message |
| MT-10 | RegisterScreen validates password rules | Error shown for weak password |

### 4.4 Optional integration tests
- `WebApplicationFactory<Program>` with SQLite in-memory and MassTransit test harness: register → login → `/users/me` → logout, asserting the three `AuthEvent`s were published.

### 4.5 Commands and acceptance
```bash
cd backend && dotnet test --collect:"XPlat Code Coverage"
cd mobile && npm test -- --coverage
```
- All tests pass in GitHub Actions.
- Backend Application layer coverage ≥ 80%.

---

## 5. Open decisions (confirm before coding)

1. **Email provider:** SMTP via MailKit with MailHog for local dev (default). Alternative: SendGrid API.
2. **Mobile toolchain:** Expo managed workflow (default) vs bare React Native CLI.
3. **.NET version:** .NET 8 LTS (default).
4. **Failed login for unknown email:** log only, no email (default, avoids spamming non-users).
5. **Message bus library:** MassTransit over raw `RabbitMQ.Client` (default, gives retries/error queues for free).
6. **Production hosting for the iOS release:** Azure App Service + CloudAMQP (default) vs. a single VPS running docker-compose.
7. **Apple Developer account:** needed under your name/company before M8; Claude can prepare the build and submission config, but signing in to App Store Connect and pressing Submit is done by you.

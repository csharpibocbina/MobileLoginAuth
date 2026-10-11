# MobileLoginAuth

A React Native (Expo) login app with a C# ASP.NET Core Web API backend. Every **successful login**, **failed login** and **logout** sends the user an **email** and a **real-time SignalR notification**, delivered through **RabbitMQ**.

See [Plan.md](Plan.md) for the full plan, requirements and test list.

| Layer | Tech |
|---|---|
| Mobile | React Native, Expo SDK 57, Expo Router, TypeScript |
| API | ASP.NET Core 8 Web API |
| Auth | JWT access token (15 min) + rotating refresh token (7 days) |
| Database | SQLite via EF Core (migrations included) |
| Broker | RabbitMQ via MassTransit (`auth-events` fanout exchange) |
| Real-time | SignalR hub at `/hubs/notifications` |
| Email | MailKit SMTP (MailHog locally) |
| Tests | xUnit + Moq + FluentAssertions + MassTransit test harness; Jest + React Native Testing Library |

## How it works

```
Mobile app ──REST + JWT──► API ──► SQLite (users, refresh tokens, login audit)
    ▲                       │
    │                       └─ publish AuthEvent ─► RabbitMQ "auth-events"
    │                                                 ├─► auth-events-email   → EmailNotificationConsumer → SMTP
    └──────── SignalR ◄───────────────────────────────└─► auth-events-signalr → SignalRNotificationConsumer → hub
```

The API never sends email inside the login request: it publishes an `AuthEvent` and returns. Consumers retry failures (email: 3 retries, exponential backoff) and are idempotent on `EventId`.

## Run it locally

Prerequisites: .NET 8 SDK, Node 20+, Docker, and Xcode (iOS simulator) or Android Studio, or the Expo Go app on a phone.

```bash
# 1. RabbitMQ + MailHog
docker compose up -d

# 2. API on http://localhost:5080 (Swagger at /swagger)
cd backend
dotnet run --project src/MobileLoginAuth.Api

# 3. Mobile app
cd mobile
npm install
npx expo start          # press i for iOS simulator, a for Android emulator
```

- Emails appear in MailHog at http://localhost:8025.
- Queues and messages are visible at http://localhost:15672 (guest / guest).
- On a **physical phone**, copy `mobile/.env.example` to `mobile/.env` and set `EXPO_PUBLIC_API_URL` to your computer's LAN IP, e.g. `http://192.168.1.20:5080`.
- No Docker? Run the API with `RabbitMq__Transport=InMemory` to use an in-process bus (emails still need an SMTP server).

Or run everything including the API in Docker: `docker compose --profile api up --build`.

## API

| Method | Route | Auth | |
|---|---|---|---|
| POST | `/api/auth/register` | – | `{ email, password, displayName }` |
| POST | `/api/auth/login` | – | `{ email, password }` → `{ accessToken, refreshToken, expiresAtUtc, user }` |
| POST | `/api/auth/refresh` | – | `{ refreshToken }` → new token pair (old one revoked) |
| POST | `/api/auth/logout` | JWT | `{ refreshToken }` (omit to sign out all devices) |
| GET | `/api/users/me` | JWT | Profile |
| GET | `/api/users/me/login-history` | JWT | Recent sign-in activity |
| DELETE | `/api/users/me` | JWT | Delete account (App Store requirement) |
| WS | `/hubs/notifications` | JWT (`?access_token=`) | Client method `ReceiveNotification` |

Send an `X-Device-Info` header (the app sends e.g. `iPhone 15 (iOS 18.1)`) so emails say which device was used. After 5 wrong passwords the account is locked for 15 minutes (`Lockout` settings).

## Configuration

`backend/src/MobileLoginAuth.Api/appsettings.json` holds every setting. **`Jwt:SigningKey` is empty on purpose**; the development key is in `appsettings.Development.json`. In production set it from the environment or a secret store:

```bash
export Jwt__SigningKey="$(openssl rand -base64 48)"
export Smtp__Host=smtp.yourprovider.com Smtp__Port=587 Smtp__UseSsl=true Smtp__Username=... Smtp__Password=...
export RabbitMq__Host=... RabbitMq__Username=... RabbitMq__Password=...
```

## Tests

```bash
cd backend && dotnet test        # 62 tests: services, tokens, consumers, validators, controllers, HTTP flow
cd mobile && npm test            # 33 tests: screens, auth context, API client refresh, SignalR hook
```

CI runs both on every push (`.github/workflows/ci.yml`).

## Publishing to the App Store

Steps are in Plan.md, Phase 8. In short:

1. Deploy the API somewhere with **HTTPS** and set `EXPO_PUBLIC_API_URL` in `mobile/eas.json`.
2. `cd mobile && npx eas-cli@latest login` (your Expo account), then `npx eas-cli@latest build --platform ios --profile production` and sign in with **your own Apple ID** when prompted.
3. `npx eas-cli@latest submit --platform ios` → TestFlight → submit for review in App Store Connect.

Bundle identifier: `com.csharpibocbina.mobileloginauth` (change in `mobile/app.json` if you prefer).

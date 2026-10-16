# How to run MobileLoginAuth, step by step

This guide shows how to run the app on an iPhone against the live server, how to watch the login emails, and how to run everything on your own PC instead. Commands are for Windows Command Prompt unless a step says "on the server".

**What you'll end up with:** the app on your iPhone. Every successful login, failed login and logout sends an email (you can read them in a test inbox) and shows a pop-up notification in the app.

---

## Part 1. One-time setup

### 1.1 On your PC

1. Install **Node.js LTS** from https://nodejs.org (accept the defaults).
2. Install **Git** from https://git-scm.com (accept the defaults).
3. Open a new **Command Prompt** and check both installs:
   ```
   node -v
   git --version
   ```
4. Download the code:
   ```
   cd %USERPROFILE%
   git clone https://github.com/csharpibocbina/MobileLoginAuth.git
   ```
   The repo is private, so Git opens a browser window. Sign in to GitHub there.

### 1.2 On your iPhone

1. Install **Expo Go** from the App Store.
2. Connect the iPhone to the **same Wi-Fi** as your PC.

### 1.3 The server (already done)

The backend already runs on the VPS at **https://api.178-128-56-88.sslip.io**. It was set up with `deploy/setup-server.sh`, and [deploy/DEPLOY.md](../deploy/DEPLOY.md) has the details. To check that it's up, open https://api.178-128-56-88.sslip.io/health in any browser. It should say **Healthy**.

---

## Part 2. Run the app on your iPhone

1. Open Command Prompt and go to the app folder:
   ```
   cd %USERPROFILE%\MobileLoginAuth\mobile
   ```
2. Point the app at the live server. You only need to do this once:
   ```
   echo EXPO_PUBLIC_API_URL=https://api.178-128-56-88.sslip.io> .env
   ```
3. Install the app's packages. Do this the first time, and again after every `git pull`:
   ```
   npm install
   ```
   You'll see yellow "deprecated" warnings and an "N vulnerabilities" line. They come from the test tools, so ignore them. **Do not run `npm audit fix --force`**, because it breaks the project.
4. Start the app:
   ```
   npx expo start
   ```
5. A **QR code** appears. Open the iPhone **Camera**, point it at the QR code and tap the banner. The app opens in Expo Go.
6. Keep the Command Prompt window open while you use the app. Press `Ctrl+C` to stop it.

**If the iPhone can't connect** (for example, the PC and phone are on different networks, or a firewall blocks it), use a tunnel instead:
```
npm install --no-save @expo/ngrok@^4.1.0
npx expo start --tunnel
```

---

## Part 3. See the login emails (test inbox)

Until a real email provider is configured, the server keeps every email in a test inbox called **MailHog**.

### 3.1 Turn the test inbox on (once, on the server)

Log in to the server with `ssh root@178.128.56.88`, then run:
```bash
cd /opt/MobileLoginAuth && git pull && cd deploy
sed -i 's/^SMTP_HOST=.*/SMTP_HOST=mailhog/; s/^SMTP_PORT=.*/SMTP_PORT=1025/; s/^SMTP_USE_SSL=.*/SMTP_USE_SSL=false/' .env
docker compose -f docker-compose.prod.yml --env-file .env --profile testmail up -d
```

### 3.2 Open the inbox (each time you want to look)

1. In a **new** Command Prompt, run:
   ```
   ssh -L 8025:localhost:8025 root@178.128.56.88
   ```
   Type the server password right away, because the prompt times out after about 2 minutes. Nothing appears on screen while you type.
2. Leave that window open and browse to **http://localhost:8025**.

---

## Part 4. Try it

| Do this in the app | You should get |
|---|---|
| Register with any email and a password like `Test1234` (8+ characters, with upper case, lower case and a digit) | You're signed in, and a **"New sign-in to your account"** email arrives |
| Log out | A **"You signed out"** email and a notification |
| Log in with a wrong password | A **"Failed sign-in attempt"** email |
| Log in with the right password | A **"New sign-in"** email and an in-app notification |
| Enter a wrong password 5 times | The account locks for 15 minutes |

Refresh MailHog to see each email.

**Quick test without the app.** On the server, run:
```bash
API=https://api.178-128-56-88.sslip.io
curl -s -X POST $API/api/auth/register -H "Content-Type: application/json" -d '{"email":"test@example.com","password":"Test1234","displayName":"Tester"}'; echo
curl -s -X POST $API/api/auth/login -H "Content-Type: application/json" -d '{"email":"test@example.com","password":"WrongPass1"}'; echo
```

---

## Part 5. Run the unit tests (optional)

Mobile tests run on your PC:
```
cd %USERPROFILE%\MobileLoginAuth\mobile
npm test
```

Backend tests need the **.NET 8 SDK** (https://dotnet.microsoft.com/download/dotnet/8.0):
```
cd %USERPROFILE%\MobileLoginAuth\backend
dotnet test
```

Both test suites also run automatically on GitHub for every change.

---

## Part 6. Get the newest version

On your PC:
```
cd %USERPROFILE%\MobileLoginAuth
git pull
cd mobile
npm install
```

On the server:
```bash
cd /opt/MobileLoginAuth && git pull
cd deploy && docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```
Add `--profile testmail` to the last command while you're using the test inbox.

---

## Part 7. Run everything on your PC instead (no server)

This needs **Docker Desktop** and the **.NET 8 SDK**.

1. Start RabbitMQ and MailHog:
   ```
   cd %USERPROFILE%\MobileLoginAuth
   docker compose up -d
   ```
2. Start the API. It runs on port 5080:
   ```
   cd backend
   dotnet run --project src/MobileLoginAuth.Api
   ```
3. Find your PC's Wi-Fi IP with `ipconfig` (look for "IPv4 Address", e.g. `192.168.1.20`). Then, in `mobile`:
   ```
   echo EXPO_PUBLIC_API_URL=http://192.168.1.20:5080> .env
   npx expo start
   ```
4. Emails show up at http://localhost:8025, and the RabbitMQ dashboard is at http://localhost:15672 (guest / guest).

To switch back to the live server, write the `https://api.178-128-56-88.sslip.io` line into `.env` again.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `Connection closed by 178.128.56.88 port 22` | The password prompt timed out. Run the command again and type the password straight away. |
| `CommandError: Install @expo/ngrok@^4.1.0 and try again` | Run `npm install --no-save @expo/ngrok@^4.1.0` in `mobile`, or skip the tunnel and use the same Wi-Fi. |
| The app says "Network Error" | Check https://api.178-128-56-88.sslip.io/health in the iPhone's Safari, and check that `mobile\.env` has the right URL. Restart with `npx expo start -c` after changing `.env`. |
| No emails in MailHog | Run Part 3.1 again. On the server, `docker compose -f docker-compose.prod.yml logs --tail 100 api` shows email errors. |
| Expo Go says the project needs a newer SDK | Update Expo Go from the App Store. |
| You ran `npm audit fix --force` by mistake | In `mobile`, run `git checkout package.json package-lock.json` and then `npm install`. |

---

## What's next: the App Store

Expo Go is for testing. To publish, you need an **Apple Developer Program** membership (USD 99/year). After that, the steps are `eas build --platform ios --profile production` and `eas submit --platform ios`, which you sign in to yourself. See Phase 8 in [Plan.md](../Plan.md).

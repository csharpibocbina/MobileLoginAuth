# Deploying the API to a VPS

This puts the backend on one small Linux server with automatic HTTPS, which the App Store build needs.

```
Internet ──443──► Caddy (Let's Encrypt HTTPS) ──► API container ──► RabbitMQ container
                                                      └──► SQLite file on a Docker volume
```

## What you need

| Item | Notes | Rough cost |
|---|---|---|
| A VPS | Ubuntu 24.04, 1 vCPU, **2 GB RAM** (RabbitMQ needs headroom). E.g. Hetzner CX22, DigitalOcean Basic, Vultr. | $5–12 / month |
| A domain name | Any registrar (Namecheap, Cloudflare, Porkbun). You'll use a subdomain like `api.yourdomain.com`. | ~$10 / year |
| An SMTP provider | Sends the login emails. Brevo, Mailgun or Amazon SES all have free tiers. | Free to start |

## 1. Create the server

1. Create an Ubuntu 24.04 VPS and add your SSH key during setup.
2. Note its public IPv4 address.

## 2. Point your domain at it

At your DNS provider add an **A record**: `api` → `<server IP>`.
Check it with `nslookup api.yourdomain.com` before continuing; Caddy can't get a certificate until DNS resolves.

## 3. Prepare the server (once)

```bash
ssh root@<server IP>

# Updates, firewall (only SSH + web open), Docker
apt update && apt upgrade -y
ufw allow OpenSSH && ufw allow 80 && ufw allow 443 && ufw --force enable
curl -fsSL https://get.docker.com | sh
```

## 4. Get the code and configure it

```bash
git clone https://github.com/csharpibocbina/MobileLoginAuth.git /opt/MobileLoginAuth
cd /opt/MobileLoginAuth/deploy
cp .env.example .env
openssl rand -base64 48      # paste the output as JWT_SIGNING_KEY
nano .env                    # fill in API_DOMAIN, JWT_SIGNING_KEY, RABBITMQ_PASSWORD, SMTP_*
chmod 600 .env
```

The repo is private, so `git clone` asks for your GitHub username and a [personal access token](https://github.com/settings/tokens) (read-only "Contents" access to this repo is enough) as the password.

**SMTP tip:** the `SMTP_FROM_ADDRESS` domain must be verified with your provider (they'll give you SPF/DKIM DNS records to add), otherwise emails land in spam.

## 5. Start it

```bash
docker compose -f docker-compose.prod.yml --env-file .env up -d --build
docker compose -f docker-compose.prod.yml logs -f api      # Ctrl+C to stop following
```

Check it from your laptop:

```bash
curl https://api.yourdomain.com/health          # → Healthy
```

## 6. Point the app at it

In `mobile/eas.json`, replace `https://REPLACE-WITH-YOUR-API-HOST` with `https://api.yourdomain.com` (both `preview` and `production`). For local testing against the server, put the same URL in `mobile/.env` as `EXPO_PUBLIC_API_URL`.

## Updating to a new version

```bash
cd /opt/MobileLoginAuth && git pull
cd deploy && docker compose -f docker-compose.prod.yml --env-file .env up -d --build
```

Database migrations run automatically when the API starts.

## Backups

```bash
./backup-db.sh                     # writes deploy/backups/mobileloginauth-<timestamp>.db, keeps the newest 14
crontab -e                         # daily at 03:00:
0 3 * * * /opt/MobileLoginAuth/deploy/backup-db.sh >> /var/log/mla-backup.log 2>&1
```

Copy backups off the server now and then (e.g. `scp root@<server IP>:/opt/MobileLoginAuth/deploy/backups/*.db .`), or turn on your VPS provider's snapshot backups.

## Looking inside

- **RabbitMQ UI** (not exposed publicly): `ssh -L 15672:localhost:15672 root@<server IP>`, then open http://localhost:15672 and log in with `RABBITMQ_USER` / `RABBITMQ_PASSWORD`.
- **Logs:** `docker compose -f docker-compose.prod.yml logs --tail 200 api`
- **Failed emails** end up in the `auth-events-email_error` queue after 3 retries; the API log shows why.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Caddy log says it can't obtain a certificate | DNS A record not pointing at the server yet, or ports 80/443 blocked |
| API exits at start with "Jwt:SigningKey must be at least 32 bytes" | `JWT_SIGNING_KEY` empty or too short in `.env` |
| Logins work but no emails arrive | Wrong `SMTP_*` values; check `logs api` for MailKit errors and the provider's dashboard |
| App says "Cannot reach the server" | `EXPO_PUBLIC_API_URL` / `eas.json` URL wrong, or not `https://` |

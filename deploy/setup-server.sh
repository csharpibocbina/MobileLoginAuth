#!/usr/bin/env bash
# One-command production setup for a fresh Ubuntu 24.04 VPS.
#
#   sudo bash deploy/setup-server.sh
#
# It installs Docker, turns on the firewall (SSH, 80, 443 only), creates deploy/.env with
# generated secrets, asks for your domain and SMTP details, then starts Caddy + API + RabbitMQ.
# Safe to re-run: it keeps an existing deploy/.env and just rebuilds and restarts.
set -euo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$DEPLOY_DIR/.env"
COMPOSE=(docker compose -f "$DEPLOY_DIR/docker-compose.prod.yml" --env-file "$ENV_FILE")

say() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31mError: %s\033[0m\n' "$*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "run it with sudo:  sudo bash deploy/setup-server.sh"

# --- 1. System packages, firewall, Docker -------------------------------------------------
say "Installing updates and Docker (this can take a few minutes)"
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq ca-certificates curl openssl ufw dnsutils >/dev/null
if ! command -v docker >/dev/null 2>&1; then
  curl -fsSL https://get.docker.com | sh >/dev/null
fi
systemctl enable --now docker >/dev/null

say "Configuring firewall: allow SSH, HTTP, HTTPS"
ufw allow OpenSSH >/dev/null
ufw allow 80/tcp >/dev/null
ufw allow 443/tcp >/dev/null
ufw --force enable >/dev/null

# --- 2. Settings (.env) --------------------------------------------------------------------
server_ip=$(curl -fsS4 https://api.ipify.org 2>/dev/null || hostname -I | awk '{print $1}')
default_domain="api.${server_ip//./-}.sslip.io"

if [[ -f "$ENV_FILE" ]]; then
  say "Keeping existing $ENV_FILE (delete it to start over)"
else
  say "Creating $ENV_FILE"
  read -rp "API hostname [$default_domain]: " api_domain
  api_domain=${api_domain:-$default_domain}

  echo
  echo "SMTP settings for the login emails (Brevo, Mailgun, SES, Gmail app password...)."
  echo "Press Enter to skip for now: the app works, emails are retried until you add these."
  read -rp "SMTP host: " smtp_host
  if [[ -n "$smtp_host" ]]; then
    read -rp "SMTP port [587]: " smtp_port
    read -rp "SMTP username: " smtp_user
    read -rsp "SMTP password (hidden): " smtp_pass; echo
    read -rp "From address (e.g. no-reply@yourdomain.com): " smtp_from
  fi

  umask 077
  cat > "$ENV_FILE" <<ENV
API_DOMAIN=$api_domain
JWT_SIGNING_KEY=$(openssl rand -base64 48 | tr -d '\n')
RABBITMQ_USER=mla
RABBITMQ_PASSWORD=$(openssl rand -hex 24)
SMTP_HOST=${smtp_host:-localhost}
SMTP_PORT=${smtp_port:-587}
SMTP_USE_SSL=true
SMTP_USERNAME=${smtp_user:-}
SMTP_PASSWORD=${smtp_pass:-}
SMTP_FROM_ADDRESS=${smtp_from:-no-reply@$api_domain}
SMTP_FROM_NAME=MobileLoginAuth
ENV
  chmod 600 "$ENV_FILE"
fi

api_domain=$(grep -E '^API_DOMAIN=' "$ENV_FILE" | cut -d= -f2-)

# --- 3. DNS check (Caddy needs it to get the HTTPS certificate) -----------------------------
resolved=$(dig +short "$api_domain" A | tail -n1 || true)
if [[ "$resolved" != "$server_ip" ]]; then
  echo
  echo "Warning: $api_domain resolves to '${resolved:-nothing}', but this server is $server_ip."
  echo "HTTPS will not work until the DNS A record points here. Continuing anyway."
fi

# --- 4. Build and start --------------------------------------------------------------------
say "Building and starting the containers"
"${COMPOSE[@]}" up -d --build

say "Waiting for https://$api_domain/health (certificate issuance can take a minute)"
for _ in $(seq 1 60); do
  if curl -fsS "https://$api_domain/health" 2>/dev/null | grep -q Healthy; then
    printf '\n\033[1;32mDone! The API is live at https://%s\033[0m\n' "$api_domain"
    echo "Logs:     docker compose -f deploy/docker-compose.prod.yml logs -f api"
    echo "Settings: $ENV_FILE"
    exit 0
  fi
  sleep 5
done

echo
echo "The containers are running but https://$api_domain/health did not answer yet."
echo "Check the logs and send them over:"
echo "  docker compose -f deploy/docker-compose.prod.yml --env-file deploy/.env logs --tail 50 caddy api"
exit 1

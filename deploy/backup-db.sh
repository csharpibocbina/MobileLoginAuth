#!/usr/bin/env bash
# Takes a consistent SQLite backup (sqlite3 .backup) while the API keeps running.
# Keeps the 14 newest backups in deploy/backups/. Schedule daily with cron, e.g.:
#   0 3 * * * /opt/MobileLoginAuth/deploy/backup-db.sh >> /var/log/mla-backup.log 2>&1
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p backups
stamp=$(date +%Y%m%d-%H%M%S)
api_container=$(docker compose -f docker-compose.prod.yml ps -q api)
docker run --rm --volumes-from "$api_container" -v "$PWD/backups:/backups" alpine:3 \
  sh -c "apk add --no-cache -q sqlite && sqlite3 /data/mobileloginauth.db \".backup /backups/mobileloginauth-$stamp.db\""
ls -1t backups/*.db | tail -n +15 | xargs -r rm --
echo "Saved backups/mobileloginauth-$stamp.db"

#!/usr/bin/env bash
# Daily logical backup of the production DB, run by cron on the VPS. See README.md ("백업").
# Dumps live on the same disk as the DB: they cover mistakes and corruption, not losing the
# instance (that's what Lightsail automatic snapshots are for).
set -euo pipefail
umask 077   # dumps hold account data
cd "$(dirname "$0")"

backup_dir="${BACKUP_DIR:-$HOME/backups}"
keep_days="${KEEP_DAYS:-14}"
mkdir -p "$backup_dir"

file="$backup_dir/dg_village-$(date -u +%Y%m%dT%H%MZ).dump"
trap 'rm -f "$file.tmp"' EXIT
# custom format (-Fc): compressed, restore with pg_restore (whole DB or single tables)
sudo docker compose exec -T postgres pg_dump -U dg_village -Fc dg_village > "$file.tmp"
mv "$file.tmp" "$file"

find "$backup_dir" -name 'dg_village-*.dump' -mtime +"$keep_days" -delete
echo "$(date -u +%FT%TZ) backup $file ($(du -h "$file" | cut -f1))"

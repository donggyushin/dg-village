#!/usr/bin/env bash
# Runs on the VPS: pull main and rebuild the stack. See README.md ("자동 배포").
# GitHub Actions calls it through a deploy-only SSH key whose authorized_keys entry is
#   restrict,command="/home/ubuntu/dg-village/deploy/deploy.sh" ssh-ed25519 ...
# so that key can run this script and nothing else.
#
# The whole body is one { } block ending in exit: bash parses it completely before running,
# so `git pull` rewriting this file mid-run is safe.
{
set -euo pipefail
cd "$(dirname "$0")"

git pull --ff-only
# --wait: return only once every service is running/healthy, fail otherwise (fails the job)
sudo docker compose up -d --build --wait
# old server images pile up on every build; the disk is small
sudo docker image prune -f

echo "deployed $(git rev-parse --short HEAD)"
exit
}

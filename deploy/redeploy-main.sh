#!/bin/sh
# Forced command of restricted SSH keys (see deploy/setup-redeploy.sh). Takes no input: whatever
# the caller asks for, it deploys the current main branch from GitHub.
# Installed as a root-owned copy, so a change merged into the repository does not change what the key can do
# until the owner installs it again.
set -eu

APP_DIR=/opt/desertod

exec 9>"$APP_DIR/.redeploy.lock"
if ! flock -n 9; then
    echo "Another redeploy is running. Try again when it finishes."
    exit 1
fi

cd "$APP_DIR"
git fetch --quiet origin main
git reset --quiet --hard origin/main
echo "Deploying $(git log -1 --format='%h %s (%an, %cr)')"

cd deploy
docker compose up -d --build
docker image prune -f > /dev/null
docker compose ps

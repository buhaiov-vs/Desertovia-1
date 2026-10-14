#!/bin/sh
# One-time server setup for redeploys from main. Run as the deploy user, with redeploy-main.sh in the same folder:
#   sh setup-redeploy.sh [restricted-public-key-file]
# Safe to run again: it reinstalls the redeploy script and skips steps that are already done.
#
# 1. Installs redeploy-main.sh as the root-owned /usr/local/bin/desertod-redeploy.
# 2. Gives the server a read-only GitHub deploy key and turns /opt/desertod into a checkout of main.
#    The first time, it saves the old folder as ~/desertod-before-git-<time>.tar.gz; deploy/.env and
#    deploy/backups are ignored by git and stay as they are.
# 3. Adds the given public key to ~/.ssh/authorized_keys, locked to running desertod-redeploy only.
set -eu

APP_DIR=/opt/desertod
REPOSITORY=git@github.com:buhaiov-vs/Desertovia.git
GITHUB_HOST_KEY="github.com ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIOMqqnkVzrm0SdG6UOoqKLsabgH5C9okWi0dh2l9GKJl"
GITHUB_KEY="$HOME/.ssh/github_desertod"
REDEPLOY=/usr/local/bin/desertod-redeploy
SCRIPT_DIR=$(cd "$(dirname "$0")" && pwd)

command -v git > /dev/null || sudo apt-get install -y -q git

sudo install -o root -g root -m 0755 "$SCRIPT_DIR/redeploy-main.sh" "$REDEPLOY"
echo "Installed $REDEPLOY"

mkdir -p "$HOME/.ssh"
chmod 700 "$HOME/.ssh"
if [ ! -f "$GITHUB_KEY" ]; then
    ssh-keygen -q -t ed25519 -N "" -C "desertod-server-read" -f "$GITHUB_KEY"
fi
touch "$HOME/.ssh/known_hosts" "$HOME/.ssh/config"
grep -qF "$GITHUB_HOST_KEY" "$HOME/.ssh/known_hosts" || echo "$GITHUB_HOST_KEY" >> "$HOME/.ssh/known_hosts"
if ! grep -q "github_desertod" "$HOME/.ssh/config"; then
    printf 'Host github.com\n    IdentityFile %s\n    IdentitiesOnly yes\n' "$GITHUB_KEY" >> "$HOME/.ssh/config"
fi
chmod 600 "$HOME/.ssh/config"

cd "$APP_DIR"
if [ ! -d .git ]; then
    git init -q -b main
    git remote add origin "$REPOSITORY"
fi
if ! git ls-remote --exit-code origin main > /dev/null 2>&1; then
    echo
    echo "The server cannot read the repository yet. Add this key on GitHub:"
    echo "  https://github.com/buhaiov-vs/Desertovia/settings/keys/new"
    echo "  Title: desertod-server, leave 'Allow write access' unchecked"
    echo
    cat "$GITHUB_KEY.pub"
    echo
    echo "Then run this script again."
    exit 1
fi
if ! git rev-parse --verify -q HEAD > /dev/null; then
    BACKUP="$HOME/desertod-before-git-$(date -u +%Y%m%d-%H%M%S).tar.gz"
    tar --exclude=./deploy/backups --exclude=./web/node_modules --exclude=./.git -czf "$BACKUP" -C "$APP_DIR" .
    chmod 600 "$BACKUP"
    echo "Saved the old folder as $BACKUP"
    git fetch --quiet origin main
    git reset --quiet --hard origin/main
    echo "$APP_DIR now follows main: $(git log -1 --format='%h %s')"
fi

if [ $# -ge 1 ]; then
    KEY=$(grep -v '^#' "$1" | tr -d '\r' | head -n 1)
    case "$KEY" in
        ssh-ed25519\ *|ssh-rsa\ *|ecdsa-sha2-*) ;;
        *) echo "$1 does not look like an SSH public key."; exit 1 ;;
    esac
    KEY_BODY=$(echo "$KEY" | cut -d ' ' -f 2)
    touch "$HOME/.ssh/authorized_keys"
    chmod 600 "$HOME/.ssh/authorized_keys"
    if grep -F "$KEY_BODY" "$HOME/.ssh/authorized_keys" | grep -qv "^restrict,command=\"$REDEPLOY\" "; then
        echo "That key is already in authorized_keys without the redeploy lock. Remove its line by hand first."
        exit 1
    elif grep -qF "$KEY_BODY" "$HOME/.ssh/authorized_keys"; then
        echo "That key is already in authorized_keys, locked to $REDEPLOY."
    else
        echo "restrict,command=\"$REDEPLOY\" $KEY" >> "$HOME/.ssh/authorized_keys"
        echo "Added the key, locked to $REDEPLOY"
    fi
fi

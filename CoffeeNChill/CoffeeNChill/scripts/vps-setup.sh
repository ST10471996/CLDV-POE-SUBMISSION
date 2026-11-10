#!/usr/bin/env bash
# One-time setup on a fresh Ubuntu 22.04/24.04 VPS
set -euo pipefail
sudo apt-get update
sudo apt-get install -y ca-certificates curl ufw
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
sudo ufw allow OpenSSH && sudo ufw allow 5000/tcp && sudo ufw --force enable
mkdir -p ~/coffeenchill
echo "Copy docker-compose.yml and .env to ~/coffeenchill, then: cd ~/coffeenchill && docker compose up -d"

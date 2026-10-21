#!/usr/bin/env bash
# Ball Battle PvP server: one-shot setup on an Oracle Cloud Always Free Ubuntu VM (24.04, ARM Ampere A1 or AMD micro).
# Run by deploy.ps1 as:  sudo bash ~/bbpvp-upload/setup.sh [domain]
# Safe to run again: it updates the binaries and config and keeps the player data in /var/lib/ballbattle-pvp.
set -euo pipefail

UPLOAD="$(cd "$(dirname "$0")" && pwd)"
APP=/opt/ballbattle-pvp
DATA=/var/lib/ballbattle-pvp
SVC=ballbattle-pvp
PORT=5088

[ "$(id -u)" -eq 0 ] || { echo "Run with sudo." >&2; exit 1; }
[ -f "$UPLOAD/app/PvpServer" ] || { echo "Missing $UPLOAD/app/PvpServer (run deploy.ps1)." >&2; exit 1; }

DOMAIN="${1:-}"
if [ -z "$DOMAIN" ]; then
  IP="$(curl -fsS --max-time 10 https://api.ipify.org || curl -fsS --max-time 10 https://ifconfig.me)"
  [[ "$IP" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Could not read the public IP; pass a domain." >&2; exit 1; }
  DOMAIN="${IP//./-}.sslip.io"   # free wildcard DNS: 1-2-3-4.sslip.io -> 1.2.3.4, good enough for an HTTPS certificate
fi
echo "== Domain: $DOMAIN"

echo "== Packages"
export DEBIAN_FRONTEND=noninteractive
apt-get update -q
apt-get install -y -q curl gnupg debian-keyring debian-archive-keyring apt-transport-https iptables-persistent >/dev/null
if ! command -v caddy >/dev/null; then
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/gpg.key | gpg --dearmor --yes -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update -q
  apt-get install -y -q caddy >/dev/null
fi

echo "== Firewall (Oracle Ubuntu images reject everything but SSH in iptables)"
for p in 80 443; do
  iptables -C INPUT -p tcp --dport "$p" -m state --state NEW -j ACCEPT 2>/dev/null ||
    iptables -I INPUT 1 -p tcp --dport "$p" -m state --state NEW -j ACCEPT
done
netfilter-persistent save >/dev/null

echo "== App"
id -u bbpvp >/dev/null 2>&1 || useradd --system --home "$DATA" --shell /usr/sbin/nologin bbpvp
mkdir -p "$APP" "$DATA/backups"
systemctl stop "$SVC" 2>/dev/null || true
rm -rf "$APP.new" && cp -r "$UPLOAD/app" "$APP.new" && rm -rf "$APP" && mv "$APP.new" "$APP"
chmod +x "$APP/PvpServer"
chown -R root:root "$APP"
chown -R bbpvp:bbpvp "$DATA"

cat > /etc/systemd/system/$SVC.service <<UNIT
[Unit]
Description=Ball Battle PvP server
After=network-online.target
Wants=network-online.target

[Service]
User=bbpvp
WorkingDirectory=$APP
ExecStart=$APP/PvpServer --urls http://127.0.0.1:$PORT
Environment=PVP_DATA_PATH=$DATA/pvp-data.json
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_gcServer=0
Restart=always
RestartSec=3
NoNewPrivileges=true
ProtectSystem=strict
ProtectHome=true
PrivateTmp=true
ReadWritePaths=$DATA

[Install]
WantedBy=multi-user.target
UNIT

echo "== Daily backup (keeps 14)"
cat > /usr/local/bin/ballbattle-pvp-backup <<'BK'
#!/bin/sh
f=/var/lib/ballbattle-pvp/pvp-data.json
[ -f "$f" ] || exit 0
cp "$f" "/var/lib/ballbattle-pvp/backups/pvp-data-$(date +%Y%m%d).json"
ls -1t /var/lib/ballbattle-pvp/backups/pvp-data-*.json | tail -n +15 | xargs -r rm -f
BK
chmod +x /usr/local/bin/ballbattle-pvp-backup
echo "17 3 * * * bbpvp /usr/local/bin/ballbattle-pvp-backup" > /etc/cron.d/ballbattle-pvp-backup

echo "== HTTPS (Caddy gets and renews the certificate itself)"
cat > /etc/caddy/Caddyfile <<CADDY
$DOMAIN {
	encode gzip
	reverse_proxy 127.0.0.1:$PORT
}
CADDY

systemctl daemon-reload
systemctl enable --now "$SVC" >/dev/null
systemctl enable caddy >/dev/null
systemctl reload caddy 2>/dev/null || systemctl restart caddy

for _ in $(seq 1 30); do curl -fsS "http://127.0.0.1:$PORT/health" >/dev/null 2>&1 && break; sleep 1; done
curl -fsS "http://127.0.0.1:$PORT/health" >/dev/null || { journalctl -u "$SVC" -n 30 --no-pager; exit 1; }
echo "== Service up on 127.0.0.1:$PORT"
echo "PVP_URL=https://$DOMAIN"

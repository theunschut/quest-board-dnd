# Self-Hosted Server Setup

Deploys the Quest Board app to Proxmox using LXC containers. The GitHub Actions runner lives on the App CT itself — no separate deploy CT needed.

## Architecture

```
Internet ──80/443──► [Traefik]  already running, handles SSL
                          │
                     :5000│
                     [App CT]  .NET 10 + systemd + GitHub Actions runner
                          │
                    :1433 │
                    [SQL Server CT]  already exists

GitHub ──HTTPS──► [App CT runner]  outbound only, no inbound ports needed
```

---

## Prerequisites

- Domain name with an A record pointing to your public IP
- Traefik already running and handling SSL via Let's Encrypt
- SQL Server CT already running and accessible on the internal network
- All CTs on the same Proxmox internal bridge (e.g. `vmbr0`)

---

## 1. App CT

Create an Ubuntu 24.04 LXC (unprivileged, nesting off). 1–2 CPU, 512MB RAM, 8GB disk.

### Install .NET 10 runtime

```bash
apt update && apt install -y wget unzip
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
apt update && apt install -y aspnetcore-runtime-10.0
```

### Create the app user and directories

```bash
useradd -m -s /bin/bash questboard
mkdir -p /opt/questboard
chown questboard:questboard /opt/questboard
mkdir -p /etc/questboard
```

### Create the environment file

This file holds all secrets. It is never committed to git.

```bash
cat > /etc/questboard/env <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
ConnectionStrings__DefaultConnection=Server=<SQL_SERVER_CT_IP>;Database=QuestBoard;User Id=sa;Password=<SA_PASSWORD>;TrustServerCertificate=true;
EmailSettings__SmtpUsername=<GMAIL_ADDRESS>
EmailSettings__SmtpPassword=<GMAIL_APP_PASSWORD>
EmailSettings__FromEmail=<FROM_EMAIL>
ReverseProxy__KnownProxies__0=<TRAEFIK_CT_IP>
EOF

chmod 600 /etc/questboard/env
chown questboard:questboard /etc/questboard/env
```

### Create the deploy script

```bash
cat > /home/questboard/deploy.sh <<'EOF'
#!/bin/bash
set -e

TAG=$1
REPO="theunschut/dnd-quest-board"   # update if repo name differs

if [ -z "$TAG" ]; then
  echo "Usage: deploy.sh <tag>"
  exit 1
fi

echo "Deploying $TAG..."
wget -q -O /tmp/questboard.zip "https://github.com/$REPO/releases/download/$TAG/questboard-$TAG.zip"

sudo systemctl stop questboard
rm -rf /opt/questboard/*
unzip -q /tmp/questboard.zip -d /opt/questboard/
rm /tmp/questboard.zip
sudo systemctl start questboard
echo "Done: $TAG deployed."
EOF

chmod +x /home/questboard/deploy.sh
chown questboard:questboard /home/questboard/deploy.sh
```

### Allow questboard to restart the service

```bash
echo "questboard ALL=(ALL) NOPASSWD: /usr/bin/systemctl stop questboard, /usr/bin/systemctl start questboard" \
  > /etc/sudoers.d/questboard
chmod 440 /etc/sudoers.d/questboard
```

### Create the systemd service

```bash
cat > /etc/systemd/system/questboard.service <<EOF
[Unit]
Description=D&D Quest Board
After=network.target

[Service]
User=questboard
WorkingDirectory=/opt/questboard
ExecStart=/usr/bin/dotnet /opt/questboard/QuestBoard.Service.dll
Restart=always
RestartSec=10
EnvironmentFile=/etc/questboard/env

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable questboard
```

### Install the GitHub Actions runner

Go to your GitHub repository → **Settings → Actions → Runners → New self-hosted runner**.
Select **Linux / x64** and follow the shown commands. Install it under `/home/questboard/actions-runner/` as the `questboard` user.

After configuration, install and start it as a service:

```bash
cd /home/questboard/actions-runner
sudo ./svc.sh install questboard
sudo ./svc.sh start
```

Verify the runner appears as **Online** in GitHub → Settings → Actions → Runners.

---

## 2. SQL Server CT

Ensure SQL Server accepts remote connections from the App CT.

### Verify SQL Server listens on all interfaces

```bash
ss -tlnp | grep 1433
```

If it only shows `127.0.0.1:1433`, configure it to listen on all interfaces via SQL Server Configuration Manager or `mssql-conf`, then restart:

```bash
systemctl restart mssql-server
```

### Restrict firewall access

Allow connections only from the App CT:

```bash
ufw allow from <APP_CT_IP> to any port 1433
ufw deny 1433
```

### Test from the App CT

```bash
apt install -y mssql-tools18 unixodbc-dev
/opt/mssql-tools18/bin/sqlcmd -S <SQL_SERVER_CT_IP> -U sa -P '<SA_PASSWORD>' -C -Q "SELECT 1"
```

---

## 3. Traefik — Add Route for Quest Board

Add a dynamic config file to your existing Traefik file provider directory (check your `traefik.yml` for the `directory` path under `providers.file`):

```bash
cat > /etc/traefik/dynamic/questboard.yml <<EOF
http:
  routers:
    questboard:
      rule: "Host(\`yourdomain.com\`)"
      entryPoints:
        - websecure
      service: questboard
      tls:
        certResolver: letsencrypt

  services:
    questboard:
      loadBalancer:
        servers:
          - url: "http://<APP_CT_IP>:5000"
EOF
```

Traefik picks up file changes automatically — no restart needed. Verify the route appears in the Traefik dashboard.

> **Note:** `entryPoints` and `certResolver` names must match what's defined in your `traefik.yml`. Common names are `websecure` and `letsencrypt` but adjust if yours differ.

> **Note:** the App CT trusts `X-Forwarded-For` only from IPs listed in `ReverseProxy__KnownProxies__0` (see the environment file above). Without this set to the Traefik CT's IP, the app sees every request as coming from Traefik itself — this breaks per-client rate limiting (e.g. the Forgot Password rate limiter) by collapsing all visitors into one shared bucket.

---

## 4. DNS & Router

| What | Value |
|---|---|
| DNS A record | `yourdomain.com` → your public IP |
| Router port forward 80 | → Traefik host IP |
| Router port forward 443 | → Traefik host IP |

Do **not** expose port 5000 (app), 1433 (SQL Server), or 22 (SSH) to the internet.

---

## 5. Deploying

### First deploy (manual)

After the CT is set up, trigger the first deploy from GitHub:

**Actions → Binary Release → Run workflow** → enter the latest tag (e.g. `v1.0.0`)

Or run it directly on the App CT:

```bash
sudo -u questboard /opt/questboard/deploy.sh v1.0.0
```

### Subsequent deploys

Push a semver tag — the workflow builds, releases, and deploys automatically:

```bash
git tag v1.2.3
git push origin v1.2.3
```

To redeploy an existing release without a new tag: **Actions → Binary Release → Run workflow** → enter the tag.

---

## Checking logs

```bash
# App logs
journalctl -u questboard -f

# Runner logs
journalctl -u actions.runner.* -f

# Traefik logs (on Traefik host)
journalctl -u traefik -f
```

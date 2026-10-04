# Self-Hosted Server Setup

Deploys the Quest Board app to Proxmox using LXC containers. The App CT pulls attested releases from GitHub on a timer, verifies them and installs them itself. GitHub has no runner or credential on the App CT, so no separate deploy CT is needed.

## Architecture

```
Internet ──80/443──► [Traefik]  already running, handles SSL
                          │
                     :5000│
                     [App CT]  .NET 10 + systemd + release poll timer
                          │
                    :1433 │
                    [SQL Server CT]  already exists

[App CT] ──HTTPS──► GitHub  the App CT polls outbound; nothing on GitHub connects to it
```

Cutting a release is described in [releasing.md](releasing.md). Operating the installer on the
server (outcomes, rollback, restoring a backup, moving an older install over) is described in
[deploy.md](deploy.md).

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
chown root:root /opt/questboard
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

This is the base unit. The deploy tooling's `setup` command adds a drop-in
(`/etc/systemd/system/questboard.service.d/10-release-layout.conf`) that points `WorkingDirectory`
and `ExecStart` at `/opt/questboard/current/app`, so the base unit does not need to change when a
release is installed. On a fresh CT, do not start the service yet: there is no release installed
until the first install has run.

### Install the deploy tooling

The App CT installs releases with `questboard-deploy`, a root-owned installer that a systemd timer
runs every few minutes. It is delivered inside each release and put in place once by its `setup`
command, which also installs the GitHub CLI it needs, the poll units and the `deploy.conf`
settings file, and arranges `/opt/questboard/releases` and the `current` link.

Follow [deploy.md](deploy.md) for the steps, which depend on whether this is a fresh CT or an
existing one that is already running an older install. Before the first release is cut, the
one-time GitHub settings in [releasing.md](releasing.md) need to exist.

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

Releases are cut from GitHub and installed by the App CT itself. Cutting one (tag, notes,
approval) is in [releasing.md](releasing.md). What the server does with it, and what to do when an
install fails or has to be put back, is in [deploy.md](deploy.md).

The two everyday commands, both run as root on the App CT:

```bash
# Install or redeploy one release by hand (also overrides a release the poll is skipping)
questboard-deploy install v1.2.3

# Switch back to a release that is still on disk (version without the v)
questboard-deploy rollback 1.2.2
```

Normally neither is needed: the timer installs a newly published release within about five
minutes.

---

## Checking logs

```bash
# App logs
journalctl -u questboard -f

# Release poll and install logs
journalctl -u questboard-deploy-poll.service -f

# Traefik logs (on Traefik host)
journalctl -u traefik -f
```

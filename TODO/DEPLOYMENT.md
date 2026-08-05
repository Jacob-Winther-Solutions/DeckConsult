# Deployment Guide — DeckConsult (deckconsult.winther-solutions.dk)

All deployment files are already committed to the repo. This guide covers everything you
need to do manually — one-time VPS setup, DNS, and GitHub configuration — before the
automated CI/CD pipeline takes over.

---

## Architecture

- **Host:** Hetzner CX22 VPS (~€4.51/month, EU data center)
- **App:** Blazor Server in Docker, listening on HTTP port 8080 inside the container
- **TLS:** Caddy reverse proxy handles HTTPS and fetches a Let's Encrypt certificate automatically
- **CI/CD:** GitHub Actions — on every push to `main`: run tests → build Docker image →
  push to GitHub Container Registry (`ghcr.io`) → copy compose files to VPS → restart containers
- **Total monthly cost:** ~€4.51 + your domain registration fee. No other recurring charges.

### Persistent volumes (survive container restarts and redeployments)

| Volume | Container path | Contents |
|---|---|---|
| `edh_appdata` | `/root/.local/share/EdhDeckBuilder` | Scryfall card cache, EDHREC cache, classification cache |
| `edh_keys` | `/root/.aspnet/DataProtection-Keys` | ASP.NET Core Data Protection keys (encrypts session cookies) |
| `caddy_data` | `/data` (Caddy) | TLS certificates from Let's Encrypt |
| `caddy_config` | `/config` (Caddy) | Caddy runtime config |

---

## Files already in the repo

These were created as part of this work and require no further editing (except the GitHub
username placeholder noted below):

| File | Purpose |
|---|---|
| `Dockerfile` | Multi-stage build: SDK image compiles + publishes, ASP.NET runtime image serves |
| `.dockerignore` | Keeps build context lean — excludes `bin/`, `obj/`, `.vs/`, `TODO/`, etc. |
| `docker-compose.yml` | Defines `app` + `caddy` services, volumes, and internal network |
| `Caddyfile` | Reverse proxy config — one block for `deckconsult.winther-solutions.dk` |
| `.env.example` | Template for the `.env` file you create on the VPS |
| `.github/workflows/deploy.yml` | Full CI/CD pipeline |

---

## Step 1 — Provision the Hetzner VPS

> **Status:** Hetzner account created and credit card added. Waiting for identity
> verification (passport upload) before a project can be created.

Once your account is verified:

1. Log in at https://console.hetzner.cloud
2. Click **+ New project**, name it `deckconsult`, click **Add project**

### 1a — Generate an SSH key (on your Windows machine)

Open PowerShell:

```powershell
ssh-keygen -t ed25519 -C "deckconsult-hetzner"
```

- **Enter file in which to save the key** — press Enter (saves to `C:\Users\jawi01\.ssh\id_ed25519`)
- **Enter passphrase** — press Enter twice to skip

If those files already exist (you generated a key before), skip this — just use the existing key.

Print the public key so you can copy it:

```powershell
Get-Content C:\Users\jawi01\.ssh\id_ed25519.pub
```

Copy the entire line — it starts with `ssh-ed25519 AAAA...`.

### 1b — Add the SSH key to Hetzner

1. In the Hetzner project, click **Security** → **SSH Keys** tab
2. Click **Add SSH key**
3. Paste the public key, name it `deckconsult`, click **Add SSH key**

### 1c — Create the server

Click **Servers** → **Add server** and fill in:

| Field | Value |
|---|---|
| **Location** | Any EU location (Frankfurt or Helsinki) |
| **Image** | Ubuntu 24.04 |
| **Type** | Shared vCPU → x86 → **CX22** (2 vCPU, 4 GB RAM, 40 GB SSD) |
| **Networking** | Leave defaults (public IPv4 + IPv6) |
| **SSH keys** | Check the `deckconsult` key you added |
| **Name** | `deckconsult` |

Click **Create & Buy Now**. The server is ready in ~30 seconds.

**Note the IP address** — you will need it for DNS and GitHub Actions secrets.

### 1d — Verify you can connect

From PowerShell:

```powershell
ssh root@<YOUR_VPS_IP>
```

Type `yes` when asked to confirm the fingerprint. You should land at a `root@deckconsult:~#`
prompt. Type `exit` to disconnect.

---

## Step 2 — Install Docker on the VPS

SSH in and run:

```bash
ssh root@<YOUR_VPS_IP>
curl -fsSL https://get.docker.com | sh
```

Docker and Docker Compose (as a plugin) are both installed by that script. Verify:

```bash
docker --version
docker compose version
```

---

## Step 3 — Create the deployment directory and `.env` file

Still on the VPS:

```bash
mkdir -p /opt/deckconsult
cd /opt/deckconsult
```

Create the `.env` file that tells Docker Compose which image to pull. Replace
`YOUR_GITHUB_USERNAME` with your actual GitHub username (lowercase):

```bash
cat > .env << 'EOF'
DOCKER_IMAGE=ghcr.io/YOUR_GITHUB_USERNAME/edh-deck-builder:latest
EOF
```

The `.env` file stays on the VPS only — it is not committed to the repo and is not copied
by the CI pipeline. You only need to create it once.

---

## Step 4 — Authenticate Docker to GitHub Container Registry

The Docker image is pushed to `ghcr.io` (GitHub Container Registry) by CI. The VPS
needs permission to pull it.

1. In GitHub, go to **Settings** → **Developer settings** → **Personal access tokens** →
   **Tokens (classic)** → **Generate new token (classic)**
2. Give it a name like `deckconsult-vps-pull`, set expiration to **No expiration** (or
   1 year), and tick only the **`read:packages`** scope
3. Copy the token

On the VPS, log in to ghcr.io:

```bash
echo YOUR_PERSONAL_ACCESS_TOKEN | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

You should see `Login Succeeded`. This auth is saved to `/root/.docker/config.json` and
persists across reboots.

---

## Step 5 — Add DNS records

In your domain registrar's DNS settings for `winther-solutions.dk`, add:

| Type | Name | Value | TTL |
|---|---|---|---|
| A | `deckconsult` | `<YOUR_VPS_IP>` | 3600 |

This makes `deckconsult.winther-solutions.dk` resolve to your VPS.

DNS propagation usually takes a few minutes, but can take up to an hour. You can check
it from your machine with:

```powershell
nslookup deckconsult.winther-solutions.dk
```

When it returns your VPS IP, DNS is ready.

---

## Step 6 — Add GitHub Actions secrets

In your GitHub repo, go to **Settings** → **Secrets and variables** → **Actions** →
**New repository secret** and add these two:

| Secret name | Value |
|---|---|
| `HETZNER_HOST` | Your VPS IP address (e.g. `95.216.12.34`) |
| `HETZNER_SSH_KEY` | The full contents of `C:\Users\jawi01\.ssh\id_ed25519` (the **private** key, not `.pub`) |

To copy the private key contents from PowerShell:

```powershell
Get-Content C:\Users\jawi01\.ssh\id_ed25519
```

Paste the entire output (including `-----BEGIN OPENSSH PRIVATE KEY-----` and the
closing `-----END OPENSSH PRIVATE KEY-----` line) as the secret value.

---

## Step 7 — Push to `main` to trigger the first deployment

Push any commit to `main`. The GitHub Actions workflow will:

1. Run all 407 tests
2. Build the Docker image and push it to `ghcr.io/YOUR_GITHUB_USERNAME/edh-deck-builder:latest`
3. Copy `docker-compose.yml` and `Caddyfile` to `/opt/deckconsult` on the VPS
4. Pull the new image and restart the containers

You can watch progress under the **Actions** tab in your GitHub repo.

### First-request TLS

On the very first HTTP request to `deckconsult.winther-solutions.dk`, Caddy contacts
Let's Encrypt and issues a certificate. This takes a few seconds — the browser may show
a brief error before the cert is ready. Subsequent requests are instant.

---

## Verifying the deployment

Once the Actions workflow completes, open https://deckconsult.winther-solutions.dk in
a browser. You should see the DeckConsult home page over HTTPS.

To check container status on the VPS:

```bash
ssh root@<YOUR_VPS_IP>
cd /opt/deckconsult
docker compose ps
docker compose logs app --tail 50
```

---

## Ongoing operations

| Task | How |
|---|---|
| **Deploy a new version** | Push to `main` — CI handles everything |
| **View live logs** | `ssh root@VPS` → `docker compose logs app -f` |
| **Restart manually** | `ssh root@VPS` → `cd /opt/deckconsult && docker compose restart app` |
| **Update Caddyfile or docker-compose.yml** | Edit in repo and push to `main` — CI copies the files and restarts |
| **Scale up** (if traffic outgrows CX22) | Resize the VPS in Hetzner Console (vertical scaling, no config changes needed) |

---

## Resumption checklist

- [ ] Hetzner identity verification approved
- [ ] Step 1 — VPS created, IP noted
- [ ] Step 2 — Docker installed on VPS
- [ ] Step 3 — `/opt/deckconsult/.env` created with correct GitHub username
- [ ] Step 4 — `docker login ghcr.io` on VPS with PAT
- [ ] Step 5 — DNS A record added, propagation confirmed
- [ ] Step 6 — `HETZNER_HOST` and `HETZNER_SSH_KEY` secrets added to GitHub repo
- [ ] Step 7 — Pushed to `main`, workflow passed, site live at https://deckconsult.winther-solutions.dk

# Deployment Guide — DeckConsult (deckconsult.winther-solutions.dk)

All deployment files are already committed to the repo. This guide covers everything you
need to do manually — one-time VPS setup, DNS, and GitHub configuration — before the
automated CI/CD pipeline takes over.

---

## Architecture

- **Host:** Hetzner CX23 VPS (~€6.80/month, EU data center, Cost-Optimized x86 tier)
- **App:** Blazor Server in Docker, listening on HTTP port 8080 inside the container
- **TLS:** Caddy reverse proxy handles HTTPS and fetches a Let's Encrypt certificate automatically
- **CI/CD:** GitHub Actions — on every push to `main`: run tests → build Docker image →
  push to GitHub Container Registry (`ghcr.io`) → copy compose files to VPS → restart containers
- **Live URL:** https://deckconsult.winther-solutions.dk
- **Total monthly cost:** ~€6.80 + your domain registration fee. No other recurring charges.

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
| **Type** | Shared vCPU → **Cost-Optimized** → x86 → **CX23** (2 vCPU, 4 GB RAM, 40 GB SSD, ~€6.80/month) |
| **Networking** | Enable **Public IPv4** (required — IPv6-only servers are not reachable via SSH by default) |
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

1. Run all tests
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

- [x] Hetzner identity verification approved
- [x] Step 1 — VPS created (CX23, IP: 91.98.114.42)
- [x] Step 2 — Docker installed on VPS
- [x] Step 3 — `/opt/deckconsult/.env` created (`DOCKER_IMAGE=ghcr.io/jacob-winther-solutions/edh-deck-builder:latest`)
- [x] Step 4 — `docker login ghcr.io` on VPS with PAT (`read:packages` scope)
- [x] Step 5 — DNS A record `deckconsult` → `91.98.114.42` added at Simply.com
- [x] Step 6 — `HETZNER_HOST` and `HETZNER_SSH_KEY` secrets added to GitHub repo
- [x] Step 7 — Site live at https://deckconsult.winther-solutions.dk

---

## Troubleshooting — issues encountered during initial setup

### Image tag must be lowercase

`github.repository_owner` preserves the original casing of the GitHub username. Docker
requires all image references to be lowercase. Fix: hardcode the image name in the
workflow instead of deriving it from `github.repository_owner`:

```yaml
env:
  IMAGE: ghcr.io/jacob-winther-solutions/edh-deck-builder
```

### Bootstrap CSS returns 404

The default `.gitignore` template for .NET excludes `**/wwwroot/lib/`, which strips the
LibMan-managed Bootstrap files from the repository. The Docker image therefore never
contains them. Fix: remove `**/wwwroot/lib/` from `.gitignore` and commit the Bootstrap
files with:

```powershell
git add -f EdhDeckBuilder.Web/wwwroot/lib
```

### `blazor.web.js` returns 404 (SDK version mismatch)

The stable .NET SDK (`10.0.3xx`) does not publish `blazor.web.js` as a physical file in
the publish output. The preview SDK (`10.0.4xx-preview`) does. Because the Docker build
image uses the stable SDK, the file is absent from the container.

**Fix applied** (two parts):

1. The file is committed at `docker/framework/blazor.web.js` (sourced from a local
   `dotnet publish` with the preview SDK). The Dockerfile copies it into the publish
   output after `dotnet publish` runs:

   ```dockerfile
   # Stable SDK (10.0.3xx) doesn't publish blazor.web.js; copy it from the repo
   RUN mkdir -p /app/publish/wwwroot/_framework && \
       cp /src/docker/framework/blazor.web.js /app/publish/wwwroot/_framework/blazor.web.js
   ```

2. `app.UseStaticFiles()` is added before `app.MapStaticAssets()` in `Program.cs` so
   that physical files in `wwwroot/` are served even when they have no entry in the
   static asset manifest.

The file is **not** placed in `EdhDeckBuilder.Web/wwwroot/_framework/` because the
preview SDK already generates it as a framework asset — placing it in `wwwroot/` creates
a duplicate-key build error on the preview SDK.

### Retrieving your local .NET API key secrets

API keys for local development are stored as .NET user secrets. To list them:

```powershell
dotnet user-secrets list --project EdhDeckBuilder.Web
```

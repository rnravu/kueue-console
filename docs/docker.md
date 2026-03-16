# Running locally with Docker Desktop

This project can run in a container for local testing (Docker Desktop). The app will attempt to use in-cluster credentials when available, and will fall back to your kubeconfig file for local development.

Quick steps (PowerShell)

1. Ensure you have Docker Desktop running.
2. Make sure your kubeconfig exists (default: `%USERPROFILE%\.kube\config`).
3. Set the `KUBECONFIG` environment variable so docker-compose can mount it into the container:

```powershell
$env:KUBECONFIG = "$env:USERPROFILE\.kube\config"
docker compose up --build
```

Alternatively run with `docker run`:

```powershell
docker build -t kueue-console .
docker run --rm -p 5000:80 -e KUBECONFIG=/kube/config -v "%USERPROFILE%\.kube\config:/kube/config:ro" kueue-console
```

Notes
- If you don't provide a kubeconfig the app will still start, but Kubernetes calls will fail and lists will be empty (errors are logged).
- For Docker Desktop on Windows, ensure file sharing / volume mount permissions allow the container to read your kubeconfig.
- This setup is intended for local development. For production, run the app in Kubernetes with a least-privilege ServiceAccount and use a proper auth provider (OIDC / Azure AD).

# KueueConsole.Web

> Web UI and demo app for Kueue (KueueConsole) — shows workloads, queues, and basic provisioning helpers.

## Overview

This is a small ASP.NET Core web application used as a demo / UI for Kueue workloads and queues.

## Prerequisites

- .NET 8 SDK (https://dotnet.microsoft.com)
- (Optional) Kubernetes cluster and a valid `kubeconfig` if you want the watcher services to connect to a cluster

## Build & Run

Build the solution:

```bash
dotnet build KueueConsole.Web.sln -c Release
```

Run the app:

```bash
dotnet run --project KueueConsole.Web.csproj
```

By default the app will be available on `http://localhost:5000` (Kestrel default). Open your browser and navigate to the root to view the UI.

## Demo data

The `kueue-demo/` folder contains sample manifests and helper scripts for running demo jobs and queues. See `kueue-demo/README` and scripts for more details.

## Important notes (security & publishing)

- This repository intentionally does **not** include authentication or authorization; no auth middleware or demo user store is registered. All API endpoints are unprotected.
  - Before deploying or exposing this app, add a proper authentication and authorization solution (OpenID Connect / OAuth2, ASP.NET Core Identity, or a reverse-proxy auth layer).

- Before publishing to a public GitHub repo, verify there are no committed secrets (API keys, passwords, kubeconfigs). If secrets were committed, rotate them immediately and remove them from the git history.

## License

This repository is released under the MIT License. See the `LICENSE` file for details.

## GitHub: prepare & push

If you already have a git repo locally, ensure build artifacts are ignored and then commit:

```bash
# add common ignores
echo ".gitignore" > .gitignore
git add .gitignore README.md
git commit -m "chore(docs): add README and .gitignore"
```

To create a new GitHub repo and push (using GitHub CLI `gh`):

```bash
gh repo create <OWNER>/<REPO> --public --source=. --remote=origin --push
```

Or manually:

```bash
git remote add origin https://github.com/<USERNAME>/<REPO>.git
git branch -M main
git push -u origin main
```

If you previously committed build artifacts, remove them from the index before pushing:

```bash
git rm -r --cached bin obj
git commit -m "chore: remove build artifacts from repo"
```

## Contributing

Contributions welcome. Open an issue or submit a pull request.

## License

No license file is included. Add a `LICENSE` (for example, the MIT license) before publishing if you want to grant public reuse rights.

# Kueue Console

Public report and reference implementation for a .NET 8 web UI that observes and manages Kueue queues, workloads, and jobs in Kubernetes.

## What This Repository Contains

- KueueConsole.Web: ASP.NET Core web app (controllers, services, static UI).
- KueueConsole.Web.Tests: tests for API and unit-level behavior.
- KueueConsole.Web/kueue-demo: Kueue demo manifests and helper scripts.
- KueueConsole.Web/docs: supporting project documentation.

## Screenshot Gallery

Store screenshots under KueueConsole.Web/docs/screenshots and use the filenames below.

### Product UI

![Home dashboard](KueueConsole.Web/docs/screenshots/01-home-dashboard.png)
![Cluster queues](KueueConsole.Web/docs/screenshots/02-cluster-queues.png)
![Local queues](KueueConsole.Web/docs/screenshots/03-local-queues.png)
![Workloads](KueueConsole.Web/docs/screenshots/04-workloads.png)
![Job details](KueueConsole.Web/docs/screenshots/05-job-details.png)
![Events stream](KueueConsole.Web/docs/screenshots/06-events-stream.png)

### Setup and Validation Evidence

![Kueue controller running](KueueConsole.Web/docs/screenshots/07-kueue-controller-running.png)
![Kueue CRDs](KueueConsole.Web/docs/screenshots/08-kueue-crds.png)
![Workload admitted](KueueConsole.Web/docs/screenshots/09-workload-admitted.png)

If an image does not render, add the matching file into KueueConsole.Web/docs/screenshots.

## Prerequisites

- Docker Desktop
- kubectl
- kind
- .NET SDK 8.0+
- Internet access to pull Kueue manifests and container images

## Docker Desktop Setup (Windows, macOS, Linux)

### Windows

1. Install Docker Desktop:

```powershell
winget install -e --id Docker.DockerDesktop
```

2. Start Docker Desktop from Start menu.
3. In Docker Desktop settings, enable Kubernetes only if you plan to use Docker Desktop Kubernetes. This guide uses kind.
4. Verify install:

```powershell
docker version
docker info
```

### macOS

1. Install Docker Desktop:

```bash
brew install --cask docker
```

2. Open Docker Desktop from Applications and allow requested permissions.
3. Verify install:

```bash
docker version
docker info
```

### Linux (Docker Desktop for Linux)

Example for Ubuntu/Debian package flow:

1. Download the Docker Desktop .deb package from official Docker docs.
2. Install package:

```bash
sudo apt-get update
sudo apt-get install -y ./docker-desktop-<version>-<arch>.deb
```

3. Start Docker Desktop and verify:

```bash
systemctl --user start docker-desktop
docker version
docker info
```

For Fedora, Arch, and other distributions, use the package and instructions from the official Docker Desktop Linux page.

## Install kubectl and kind

### Windows

```powershell
winget install -e --id Kubernetes.kubectl
winget install -e --id Kubernetes.kind
kubectl version --client
kind version
```

### macOS

```bash
brew install kubectl kind
kubectl version --client
kind version
```

### Linux

Follow your distro package manager or official binaries for kubectl and kind, then verify:

```bash
kubectl version --client
kind version
```

## Full Kueue Cluster Setup Guide

This section provides a full local setup using kind and Kueue.

### 1. Create a local cluster

```bash
kind create cluster --name kueue-dev
kubectl get nodes
```

Expected: control-plane node reaches Ready.

### 2. Install Kueue

```bash
kubectl apply --server-side -f https://github.com/kubernetes-sigs/kueue/releases/download/v0.16.2/manifests.yaml
kubectl wait deploy/kueue-controller-manager -n kueue-system --for=condition=available --timeout=5m
```

### 3. Verify Kueue installation

```bash
kubectl get pods -n kueue-system
kubectl get crd | grep -i kueue
```

PowerShell alternative:

```powershell
kubectl get crd | Select-String -Pattern kueue
```

### 4. Apply demo queue resources from this repository

Run from repository root:

```bash
kubectl apply -f KueueConsole.Web/kueue-demo/flavor.yaml
kubectl apply -f KueueConsole.Web/kueue-demo/clusterqueue.yaml
kubectl apply -f KueueConsole.Web/kueue-demo/localqueue.yaml
kubectl apply -f KueueConsole.Web/kueue-demo/job.yaml
```

### 5. Validate admission and job lifecycle

```bash
kubectl get resourceflavors
kubectl get clusterqueues
kubectl get localqueues -A
kubectl get workloads -A
kubectl get jobs -A
kubectl get events -A --sort-by='.metadata.creationTimestamp'
```

### 6. Optional: run expanded demo scripts

From KueueConsole.Web/kueue-demo:

Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\setup.ps1
```

Cleanup:

```powershell
powershell -ExecutionPolicy Bypass -File .\cleanup.ps1
```

### 7. Start Kueue Console Web app

From KueueConsole.Web:

```bash
dotnet restore
dotnet build KueueConsole.Web.sln -c Debug
dotnet run --project KueueConsole.Web.csproj
```

Open browser at http://localhost:5000 or the URL printed by Kestrel.

## Optional: Run the Web App with Docker

From KueueConsole.Web:

```bash
docker compose up --build
```

The compose setup mounts kubeconfig for local development access.

## Test Commands

From repository root:

```bash
dotnet test KueueConsole.Web.Tests/KueueConsole.Web.Tests.csproj
```

## Public Report Checklist

Before publishing this repository/report:

1. Confirm all screenshot links render on GitHub.
2. Remove or redact sensitive values from screenshots.
3. Confirm no secrets are committed (tokens, kubeconfig, credentials).
4. Add architecture and workflow screenshots that show admission flow.
5. Include test execution evidence and environment details.

## Additional Documentation

- KueueConsole.Web/README.md
- KueueConsole.Web/docs/docker.md
- KueueConsole.Web/kueue-demo/docs/kueue-local-setup-guide.md
- KueueConsole.Web/kueue-demo/docs/kueue_setup_reference.md

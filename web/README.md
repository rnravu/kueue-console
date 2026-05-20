# Kueue Console Web

> A .NET 8 ASP.NET Core web dashboard for [Kueue](https://kueue.sigs.k8s.io/) — observe and manage workload queues, cluster queues, local queues, and batch jobs running on Kubernetes.

## About

Kueue Console Web is a reference implementation and public report demonstrating platform engineering practices with Kubernetes and .NET. It provides a real-time web UI that connects to a Kubernetes cluster, watches Kueue custom resources, and surfaces queue state, workload admission status, and job lifecycle in one place.

Built to explore the intersection of .NET backend development and Kubernetes platform engineering — covering in-cluster deployment, background watch services, Kubernetes client SDK usage, and production container patterns.

**Tech stack:** ASP.NET Core 8 · Kubernetes Client SDK · Kueue CRDs · Docker · kind · GitHub Actions CI

## What This Repository Contains

- ASP.NET Core web app for Kueue dashboard and operations.
- Kubernetes/Kueue demo manifests and setup scripts under `kueue-demo/`.
- Project documentation in `docs/`.

## Screenshot Gallery

Place screenshots in `docs/screenshots/`.

![Dashboard](docs/screenshots/dashboard.png)
![Queues](docs/screenshots/queues.png)
![Jobs](docs/screenshots/jobs.png)
![Troubleshooting](docs/screenshots/troubleshoot.png)

## Architecture Diagram

See the full system diagram and data-flow notes in `docs/ARCHITECTURE.md`.

## Prerequisites

- Docker Desktop
- `kubectl`
- `kind`
- .NET SDK 8.0+

## Docker Desktop Setup (Windows, macOS, Linux)

### Windows

```powershell
winget install -e --id Docker.DockerDesktop
docker version
docker info
```

### macOS

```bash
brew install --cask docker
docker version
docker info
```

### Linux (Docker Desktop)

Install Docker Desktop from official packages for your distribution, then verify:

```bash
docker version
docker info
```

## Install `kubectl` and `kind`

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

Install via your distro package manager or official binaries, then verify:

```bash
kubectl version --client
kind version
```

## Full Kueue Cluster Setup Guide

### 1. Create a local cluster

```bash
kind create cluster --name kueue-dev
kubectl get nodes
```

### 2. Install Kueue

```bash
kubectl apply --server-side -f https://github.com/kubernetes-sigs/kueue/releases/download/v0.16.2/manifests.yaml
kubectl wait deploy/kueue-controller-manager -n kueue-system --for=condition=available --timeout=5m
```

### 3. Verify installation

```bash
kubectl get pods -n kueue-system
kubectl get crd | grep -i kueue
```

PowerShell alternative:

```powershell
kubectl get crd | Select-String -Pattern kueue
```

### 4. Apply demo resources from this repository

```bash
kubectl apply -f kueue-demo/flavor.yaml
kubectl apply -f kueue-demo/clusterqueue.yaml
kubectl apply -f kueue-demo/localqueue.yaml
kubectl apply -f kueue-demo/job.yaml
```

### 5. Validate queue/job/workload lifecycle

```bash
kubectl get resourceflavors
kubectl get clusterqueues
kubectl get localqueues -A
kubectl get workloads -A
kubectl get jobs -A
kubectl get events -A --sort-by='.metadata.creationTimestamp'
```

### 6. Optional scripted demo

PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\kueue-demo\setup.ps1
```

Cleanup:

```powershell
powershell -ExecutionPolicy Bypass -File .\kueue-demo\cleanup.ps1
```

### 7. Run the web app

```bash
dotnet restore
dotnet build KueueConsole.Web.sln -c Debug
dotnet run --project KueueConsole.Web.csproj
```

Open `http://localhost:5000`.

## Run with Docker (Local)

```bash
docker compose up --build
```

## Security Note

This demo app does not include authentication/authorization middleware by default. Add auth before exposing it publicly.

## License

MIT. See `LICENSE`.

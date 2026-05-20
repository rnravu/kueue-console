# Kueue Console – System Architecture

This document describes how Kueue Console Web interacts with Kubernetes and Kueue to show queue and workload state.

## 1. High-Level Architecture

<div class="mermaid">
flowchart LR
	U[User Browser]

	subgraph APP[Kueue Console Web (.NET 8)]
		API[REST Controllers]
		AGG[Dashboard Aggregator Service]
		SVC[Domain Services\nClusterQueue / LocalQueue / Workload / Jobs]
		W1[Workload Watch Service]
		W2[ClusterQueue Watch Service]
		W3[LocalQueue Watch Service]
		STATE[In-Memory State Stores]
		EVT[Cluster Event Service]
	end

	subgraph K8S[Kubernetes Cluster]
		KAPI[Kubernetes API Server]
		subgraph KUEUE[Kueue CRDs]
			WL[Workloads]
			LQ[LocalQueues]
			CQ[ClusterQueues]
			RF[ResourceFlavors]
		end
		CTRL[Kueue Controller Manager]
		JOBS[Kubernetes Jobs/Pods]
	end

	U -->|HTTP JSON + Static UI| API
	API --> AGG
	AGG --> STATE
	API --> SVC
	SVC -->|List/Get/Create/Delete| KAPI

	W1 -->|Watch Workloads| KAPI
	W2 -->|Watch ClusterQueues| KAPI
	W3 -->|Watch LocalQueues| KAPI

	W1 --> STATE
	W2 --> STATE
	W3 --> STATE
	W1 --> EVT
	W2 --> EVT
	W3 --> EVT

	KAPI <--> WL
	KAPI <--> LQ
	KAPI <--> CQ
	KAPI <--> RF

	CTRL -->|Admission decisions| WL
	WL -->|Admitted workloads| JOBS
</div>

<script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>
<script>mermaid.initialize({startOnLoad:true});</script>

## 2. Runtime Flow

1. The UI calls API controllers for dashboard and resource operations.
2. Controllers use domain services to query Kubernetes or apply manifests.
3. Background watch services subscribe to Kueue resources and keep in-memory state current.
4. The dashboard aggregator composes summary data from these in-memory stores.
5. Kueue controller admission decisions update Workload status, which appears in the UI.

## 3. Authentication Mode

The app attempts in-cluster Kubernetes authentication first. If unavailable (local development), it falls back to kubeconfig.

## 4. Core Kueue Objects

- Workload: pending/admitted/finished state for schedulable units.
- LocalQueue: namespace-level queue used by job submitters.
- ClusterQueue: cluster-scoped policy and capacity envelope.
- ResourceFlavor: describes available resource classes/capacity slices.

## 5. Deployment Topology

- Local mode: run app on developer machine and connect via kubeconfig.
- In-cluster mode: run app as Deployment + ServiceAccount with least-privilege RBAC.

## 6. Notes for Public Report

- Include one screenshot for each of dashboard, queue state, and admitted workload.
- Document cluster size and node resources used for reproduction.
- Include command output evidence for CRD installation and controller readiness.

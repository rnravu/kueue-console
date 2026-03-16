# Kueue Console – System Architecture

This document explains the architecture of Kubernetes Kueue and how Kueue Console interacts with it.

The purpose of this file is to help AI assistants and developers understand the data model used by the Kueue scheduling system.

---

# 1. What is Kueue

Kueue is a Kubernetes scheduling system designed for **batch workloads**.

It manages how workloads are admitted into clusters based on available resources and queue policies.

Typical workloads include:

- machine learning jobs
- batch processing
- simulations
- large compute workloads

Kueue extends Kubernetes using **Custom Resource Definitions (CRDs)**.

---

# 2. Kueue Data Model

Kueue uses several CRDs to manage workloads and queues.

Important CRDs:

Workload  
LocalQueue  
ClusterQueue  
ResourceFlavor  
Cohort  

The most important objects for Kueue Console are:

- Workloads
- LocalQueues
- ClusterQueues

---

# 3. System Overview

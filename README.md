# Distributed Fraud Detection System

![CI Status](https://github.com/CarterFitzgerald/distributed-fraud-detection-system/actions/workflows/ci.yml/badge.svg)

A production-style distributed fraud detection system built with ASP.NET Core and C#, following an event-driven microservices-inspired architecture.

This repository contains a distributed system composed of:

- **TransactionService** – REST API for ingesting and persisting transactions  
- **FraudDetectionWorker** – Background service that consumes transaction events and performs fraud scoring  
- **DistributedFraud.Contracts** – Shared contract library containing cross-service event definitions  

---

## Overview

The system simulates a real-world financial transaction pipeline where:

- Transactions are received via a REST API  
- Transactions are persisted to a relational database  
- A `TransactionCreated` domain event is published to RabbitMQ  
- A background worker consumes events asynchronously  
- A fraud score is computed and stored back in the database  
- Services are containerized and deployable to the cloud  

The system implements a complete **end-to-end event-driven architecture** with clean separation of concerns between services.

---

## Tech Stack

- **Language**: C# (.NET 9)
- **Frameworks**:
  - ASP.NET Core Web API
  - .NET Worker Service
- **Persistence**: Entity Framework Core + SQL Server (`TransactionDb`)
- **Messaging**: RabbitMQ (via Docker)
- **Architecture**:
  - Layered architecture (Controllers → Services → Repositories → Persistence)
  - Dependency Injection throughout
  - Shared contracts project for cross-service event consistency
  - Dedicated messaging abstraction
  - Background consumer service
  - Rule-based fraud scoring engine (replaceable with ML.NET)
- **DevOps**:
  - GitHub Actions CI (build + test validation)
  - Docker (API + RabbitMQ)
- **API Documentation**:
  - Swagger / OpenAPI (Development environment)

---

## Solution Structure

/DistributedFraud.Contracts
/Events
TransactionCreatedEvent.cs

/TransactionService
/Controllers
/Services
/Models
/Data
/Messaging
/Migrations

/FraudDetectionWorker
/Application
/Messaging
/Data
/Scoring

The `DistributedFraud.Contracts` project ensures publisher and consumer remain consistent by sharing event definitions.

---

## Architecture

### TransactionService

#### API Layer
- `HealthController` – basic health check endpoint  
- `TransactionsController` – handles transaction creation and retrieval  

#### Domain Layer
- `Transaction` – core domain entity  
- `CreateTransactionRequest` – validated request DTO  
- `TransactionResponse` – API response DTO  

#### Application / Service Layer
- `ITransactionService`
- `TransactionAppService`
  - Normalizes input (currency/country uppercase)
  - Applies default timestamps
  - Coordinates repository
  - Publishes `TransactionCreated` event after successful persistence  

#### Persistence Layer
- `AppDbContext`
- `ITransactionRepository`
- `EfTransactionRepository`

#### Messaging Layer (Publisher)
- `ITransactionEventPublisher`
- `RabbitMqTransactionEventPublisher`

---

### FraudDetectionWorker

A background worker service responsible for:

- Connecting to RabbitMQ
- Subscribing to the `transactions.created` queue
- Delegating message handling to an application handler
- Computing fraud scores
- Updating the corresponding transaction row in SQL Server
- Acknowledging or rejecting messages appropriately
- Ensuring idempotency (skips already-scored transactions)

#### Internal Structure

- `IMessageConsumer` – abstraction over message consumption
- `RabbitMqMessageConsumer` – RabbitMQ implementation
- `TransactionCreatedHandler` – application-level event handler
- `FraudScorer` – rule-based scoring engine

The worker is intentionally structured so messaging, business logic, and persistence are cleanly separated.

---

## Event Flow
POST /api/transactions

↓

TransactionAppService

↓

Save to SQL Server

↓

Publish TransactionCreated event

↓

RabbitMQ Queue: transactions.created

↓

FraudDetectionWorker consumes event

↓

TransactionCreatedHandler

↓

Compute fraud score

↓

Update TransactionDb with FraudScore

Messages are durable and serialized as JSON.

---

## Fraud Scoring

A rule-based fraud scoring engine has been implemented as a placeholder for a future ML model.

The scoring system:

- Produces a `FraudScore` (0–100)
- Generates explainable `FraudReason` codes (e.g., `HIGH_AMOUNT;HIGH_RISK_COUNTRY`)
- Stores `FraudScoredAt` timestamp

Example risk factors:
- High transaction amount
- High-risk country (demo list)
- Off-hours transaction
- Suspicious identifier patterns

The scoring engine is intentionally designed to be easily replaceable with an ML.NET model in future iterations.

---

## Current Status 

- ✅ Clean layered architecture implemented  
- ✅ Shared contracts project introduced for cross-service events  
- ✅ Entity Framework Core with SQL Server persistence  
- ✅ Repository + Service pattern implemented  
- ✅ Messaging abstraction introduced in worker  
- ✅ RabbitMQ integration complete  
- ✅ End-to-end distributed flow verified  
- ✅ Rule-based fraud scoring implemented  
- ✅ Fraud results persisted back to SQL Server  
- ✅ GitHub Actions CI configured (build + test)  

---

## Running Locally

### 1️⃣ Start RabbitMQ

```bash
docker compose up -d rabbitmq
```
## RabbitMQ UI

http://localhost:15672  
**Username:** guest  
**Password:** guest  

---

## 2️⃣ Run TransactionService (Development)

```bash
cd TransactionService
set ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

## 3️⃣ Run FraudDetectionWorker

```bash
cd FraudDetectionWorker
dotnet run
```

Create a transaction via Swagger and observe:

- Worker logs fraud scoring
- Database row updated with fraud score fields

## Development Roadmap

- [x] Implement Transaction domain model
- [x] Add EF Core and SQL Server persistence
- [x] Introduce service and repository layers
- [x] Add RabbitMQ event publishing
- [x] Add Fraud Detection worker (consumer service)
- [x] Implement rule-based fraud scoring
- [ ] Integrate ML.NET fraud model
- [ ] Dockerize full multi-service environment
- [ ] Add distributed tracing / observability
- [ ] Deploy to Azure or AWS

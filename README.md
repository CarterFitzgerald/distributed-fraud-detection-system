# Distributed Fraud Detection System

A production-style distributed fraud detection system built with ASP.NET Core and C#, following an event-driven microservices-inspired architecture.

This repository contains a distributed system composed of:

- **TransactionService** – REST API for ingesting and persisting transactions
- **FraudDetectionWorker** – Background service that consumes transaction events and performs fraud scoring

---

## Overview

The system simulates a real-world financial transaction pipeline where:

- Transactions are received via a REST API  
- Transactions are persisted to a relational database  
- A `TransactionCreated` domain event is published to RabbitMQ  
- A background worker consumes events asynchronously  
- A fraud score is computed and stored back in the database  
- Services are containerized and deployable to the cloud  

The system now supports a complete **end-to-end event-driven architecture**.

---

## Tech Stack

- **Language**: C# (.NET 9)
- **Frameworks**:
  - ASP.NET Core Web API
  - .NET Worker Service
- **Persistence**: Entity Framework Core + SQL Server (`TransactionDb`)
- **Messaging**: RabbitMQ (via Docker)
- **Architecture**:
  - Controller-based Web API
  - Dependency Injection
  - Layered architecture (Controllers → Services → Repositories → Persistence)
  - Event publishing abstraction
  - Background consumer service
  - Rule-based fraud scoring engine (replaceable with ML.NET)
- **DevOps**:
  - GitHub Actions CI (build + test validation)
  - Docker (API + RabbitMQ)
- **API Documentation**:
  - Swagger / OpenAPI (Development environment)

---

## Architecture (Current Phase)

### TransactionService

#### API Layer
- `HealthController` – basic health check endpoint
- `TransactionsController` – handles transaction creation and retrieval

#### Domain Layer
- `Transaction` – core domain entity
- `CreateTransactionRequest` – validated request DTO
- `TransactionResponse` – API response DTO
- `TransactionCreatedEvent` – domain event published after persistence

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
  - Publishes JSON-serialized events to `transactions.created` queue

---

### FraudDetectionWorker

A background worker service that:

- Connects to RabbitMQ
- Subscribes to `transactions.created`
- Deserializes `TransactionCreated` events
- Computes a fraud score using a rule-based engine
- Updates the corresponding transaction row in SQL Server
- Manually ACKs/NACKs messages
- Ensures idempotency (skips already-scored transactions)

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
- ✅ Entity Framework Core with SQL Server persistence  
- ✅ Repository + Service pattern introduced  
- ✅ Unit tests for service logic  
- ✅ GitHub Actions CI configured (build + test)  
- ✅ Dockerized TransactionService  
- ✅ RabbitMQ integrated via Docker Compose  
- ✅ `TransactionCreated` event published after persistence  
- ✅ FraudDetectionWorker consumes events  
- ✅ Rule-based fraud scoring implemented  
- ✅ Fraud results persisted back to SQL Server  
- ✅ End-to-end distributed flow verified 

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

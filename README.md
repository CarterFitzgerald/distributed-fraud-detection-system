# Distributed Fraud Detection System

A production-style distributed fraud detection system built with ASP.NET Core and C#, following an event-driven microservices-inspired architecture.

This repository currently focuses on the **TransactionService**, which exposes a REST API for ingesting financial transactions, persists them to SQL Server using Entity Framework Core, and publishes domain events to RabbitMQ.

---

## Overview

The system (in its full vision) simulates a real-world financial transaction pipeline where:

- Transactions are received via a REST API  
- Transactions are persisted to a relational database  
- A `TransactionCreated` domain event is published to a message broker  
- Downstream services (fraud detection, analytics, auditing) can consume events asynchronously  
- Services are containerized and deployable to the cloud  
- the system now supports **event-driven architecture via RabbitMQ**.

---

## Tech Stack

- **Language**: C# (.NET 9)
- **Framework**: ASP.NET Core Web API
- **Persistence**: Entity Framework Core + SQL Server (`TransactionDb`)
- **Messaging**: RabbitMQ (via Docker)
- **Architecture**:
  - Controller-based Web API
  - Dependency Injection
  - Layered architecture (Controllers → Services → Repositories → Persistence)
  - Event publishing abstraction
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

#### Messaging Layer
- `ITransactionEventPublisher`
- `RabbitMqTransactionEventPublisher`
  - Publishes JSON-serialized events to `transactions.created` queue

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

Messages are durable and serialized as JSON.

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
- ✅ End-to-end event flow verified in RabbitMQ UI  

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

## Development Roadmap

- [x] Implement Transaction domain model
- [x] Add EF Core and SQL Server persistence
- [x] Introduce service and repository layers
- [x] Add RabbitMQ event publishing
- [ ] Add Fraud Detection worker (consumer service)
- [ ] Integrate ML.NET fraud model
- [ ] Dockerize full multi-service environment
- [ ] Deploy to Azure or AWS

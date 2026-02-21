# Distributed Fraud Detection System

A production-style distributed fraud detection system built with ASP.NET Core and C#, following an event-driven microservices-inspired architecture.

This repository currently focuses on the **TransactionService**, which exposes a REST API for ingesting and retrieving financial transactions and persists them to a SQL Server database using Entity Framework Core.

---

## Overview

The system (in its full vision) will simulate a real-world financial transaction pipeline where:

- Transactions are received via a REST API
- Events are published to a message broker
- A background worker processes transactions for fraud
- An ML model scores each transaction for fraud risk
- Results are stored for audit and reporting
- The whole thing is containerised and deployable to the cloud (Azure/AWS)

Right now, the project is in the **data ingestion and persistence** phase.

---

## Tech Stack

- **Language**: C# (.NET 9)
- **Framework**: ASP.NET Core Web API
- **Persistence**: Entity Framework Core + SQL Server (`TransactionDb`)
- **Architecture**:
  - Controller-based Web API
  - Dependency Injection for services
  - Layered design (Controllers → Services → Persistence)
- **DevOps**:
  - GitHub Actions CI (build + test)
- **API Documentation**:
  - Swagger / OpenAPI (development environment)

---

## Architecture (Current Phase)

**TransactionService** currently consists of:

- **API Layer**
  - `HealthController` – basic health check
  - `TransactionsController` – handles transaction creation and retrieval
- **Domain Layer**
  - `Transaction` – core domain entity representing a financial transaction
  - `CreateTransactionRequest` – request DTO with validation attributes
  - `TransactionResponse` – response DTO returned from the API
- **Persistence Layer**
  - `AppDbContext` – EF Core database context exposing `DbSet<Transaction>`
  - `ITransactionStore` – abstraction for transaction persistence
  - `EfTransactionStore` – SQL Server–backed implementation of `ITransactionStore`
  - (Previously: `InMemoryTransactionStore` for early development)
- **Database**
  - SQL Server database (e.g. `TransactionDb`) created and managed via EF Core migrations

---

## Current Status

- ✅ Project structure initialized with controller-based architecture
- ✅ GitHub Actions CI pipeline configured (build + test validation)
- ✅ Transaction domain model implemented
- ✅ Request/response DTOs with validation attributes added
- ✅ In-memory transaction store implemented for early development
- ✅ Entity Framework Core integrated with SQL Server for real persistence
- ✅ `AppDbContext` and EF Core migrations set up
- ✅ RESTful transaction endpoints available:
  - `POST /api/transactions`
  - `GET /api/transactions/{id}`
- ✅ Transactions are persisted to SQL Server via `EfTransactionStore`
- ✅ Swagger documentation enabled for local development

---

## Development Roadmap

- [x] Implement Transaction domain model
- [x] Add EF Core and SQL Server persistence
- [ ] Introduce messaging with RabbitMQ for event-driven processing
- [ ] Add dedicated Fraud Detection worker service
- [ ] Integrate ML.NET fraud model (using a real fraud dataset, e.g. Kaggle)
- [ ] Dockerize services (API, DB, worker, broker)
- [ ] Deploy to Azure or AWS (App Service / Containers + managed SQL)

---

## Running the TransactionService Locally

1. **Prerequisites**
   - .NET 9 SDK
   - SQL Server (local instance or Docker)
   - Visual Studio or `dotnet` CLI

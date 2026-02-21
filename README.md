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
  - Dependency Injection for services and repositories
  - Layered design (Controllers → Services → Repositories → Persistence)
- **DevOps**:
  - GitHub Actions CI (build + test)
- **API Documentation**:
  - Swagger / OpenAPI (development environment)

---

## Architecture (Current Phase)

**TransactionService** currently consists of:

- **API Layer**
  - `HealthController` – basic health check
  - `TransactionsController` – handles transaction creation and retrieval, delegating business logic to a service layer

- **Domain Layer**
  - `Transaction` – core domain entity representing a financial transaction
  - `CreateTransactionRequest` – request DTO with validation attributes
  - `TransactionResponse` – response DTO returned from the API

- **Application / Service Layer**
  - `ITransactionService` – abstraction for transaction-related business logic
  - `TransactionAppService` – implementation that:
    - Maps request DTOs to domain entities
    - Applies basic normalization (e.g. uppercasing currency/country, default timestamps)
    - Coordinates with the repository layer
    - Maps domain entities back to response DTOs

- **Persistence Layer**
  - `AppDbContext` – EF Core database context exposing `DbSet<Transaction>`
  - `ITransactionRepository` – abstraction for transaction persistence
  - `EfTransactionRepository` – SQL Server–backed implementation of `ITransactionRepository`
  - (Previously: `InMemoryTransactionStore` used during early development before EF Core integration)

- **Database**
  - SQL Server database (e.g. `TransactionDb`) created and managed via EF Core migrations

---

## Current Status

- ✅ Project structure initialized with controller-based architecture  
- ✅ GitHub Actions CI pipeline configured (build + test validation)  
- ✅ Transaction domain model implemented  
- ✅ Request/response DTOs with validation attributes added  
- ✅ Entity Framework Core integrated with SQL Server for real persistence  
- ✅ `AppDbContext` and EF Core migrations set up  
- ✅ Service and repository layers introduced:
  - `ITransactionService` / `TransactionAppService`
  - `ITransactionRepository` / `EfTransactionRepository`
- ✅ Controllers refactored to be thin and delegate business logic to the service layer  
- ✅ RESTful transaction endpoints available:
  - `POST /api/transactions`
  - `GET /api/transactions/{id}`
- ✅ Transactions are persisted to SQL Server via the repository layer  
- ✅ Swagger documentation enabled for local development

---

## Development Roadmap

- [x] Implement Transaction domain model  
- [x] Add EF Core and SQL Server persistence  
- [x] Introduce service and repository layers for clean separation of concerns  
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

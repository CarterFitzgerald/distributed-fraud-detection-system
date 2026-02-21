# Distributed Fraud Detection System

A production-style distributed fraud detection system built with .NET 9, following an event-driven microservices architecture.

## Overview

This project simulates a real-world financial transaction system where:

- Transactions are received via a REST API
- Events are published to a message broker
- A background worker processes transactions
- Fraud probability is calculated using ML.NET
- Results are stored for audit and reporting

## Tech Stack

- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server (local development)
- RabbitMQ (planned)
- ML.NET (planned)
- Docker (planned)
- GitHub Actions (CI)

## Current Status

- Project structure initialized with controller-based architecture
- GitHub Actions CI pipeline configured (build + test validation)
- Transaction domain model implemented
- Request/response DTOs with validation attributes added
- Thread-safe in-memory transaction store implemented
- RESTful endpoints available:
  - POST /api/transactions
  - GET /api/transactions/{id}
- Swagger documentation enabled for local development

## Development Roadmap

- [ ] Implement Transaction domain model
- [ ] Add EF Core and database integration
- [ ] Introduce messaging with RabbitMQ
- [ ] Add Fraud Detection worker
- [ ] Integrate ML.NET model
- [ ] Dockerize services
- [ ] Deploy to Azure

---

This project is being developed using a feature-branch workflow with CI validation.

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

- Initial project setup complete
- CI pipeline configured
- Transaction service scaffolded

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

# AGENTS.md

This file provides guidance for AI coding agents working on the Bussy.Net repository.

## Project Overview

Bussy.Net is a lightweight .NET messaging abstraction for publishing messages and handling them through pluggable transports. It provides:

- A simple `IPublisher` API for sending messages.
- Handler-based message processing via `IHandler<TMessage>`.
- Pluggable transport integrations (currently InMemory and RabbitMQ).

## Repository Structure

```
Bussy.Net/                          # Core library (IPublisher, IHandler, BussyService, etc.)
Bussy.Net.Transports.InMemory/      # In-memory transport implementation
Bussy.Net.Transports.RabbitMq/      # RabbitMQ transport implementation
Bussy.Net.Test/                     # Unit and end-to-end tests
Bussy.Net.sln                       # Solution file
global.json                         # SDK version pin
```

## Building and Testing

```bash
# Build the solution
dotnet build Bussy.Net.sln

# Run all tests
dotnet test Bussy.Net.sln
```

The RabbitMQ end-to-end tests automatically spin up a RabbitMQ container using Testcontainers. `Bussy.Net.Test/rabbitmq.conf` is mounted into the container as its configuration file.

## Key Concepts

- **`IPublisher`** – Injected per-scope; use `PublishAsync<TMessage>` to send a message.
- **`IHandler<TMessage>`** – Implement and register to process messages of a given type. Handlers are scoped.
- **`ITransport`** – Implement to add a new broker/transport backend. Register it through `BussyConfigurator`.
- **`MessageRouteAttribute`** – Optionally decorate a message class to override the default route (type name).
- **`BussyService`** – A `BackgroundService` that starts subscriptions on host start and tears them down on stop.

## Adding a New Transport

1. Create a new class library project (e.g., `Bussy.Net.Transports.MyBroker`).
2. Implement `ITransport` (defined in `Bussy.Net/Transport/`, and optionally `ITransportSender`/`ITransportReceiver`) in your new transport project.
3. Add a `ServiceCollectionExtensions` class with an `AddBussy<MyBroker>Transport` extension method that calls `services.AddBussy(configure => ...)` and registers the transport.
4. Add end-to-end tests that extend `EndToEndTestFixture` in `Bussy.Net.Test/E2E/`.
5. Set `<SymbolPackageFormat>snupkg</SymbolPackageFormat>` in the new `.csproj` to match the other transport projects.

## Coding Conventions

- Target the SDK version pinned in `global.json`.
- Use `sealed` for concrete classes that are not designed for inheritance.
- Use `record` or `sealed record` for message types and value objects.
- Handlers are registered as **scoped** services; transports and registries are **singletons**. Transports generally implement `IPublisher`, so `IPublisher` is often a singleton.
- Tests use **NUnit** with `[TestFixture]`/`[Test]`/`[SetUp]`/`[TearDown]` attributes.
- Keep transport-specific code out of the core `Bussy.Net` library.

## CI / Pull Requests

- PRs must target `main`.
- The CI pipeline (`.github/workflows/dotnet-library.yml`) builds, tests, and publishes NuGet packages on merge to `main`.
- PR titles are validated by `.github/pr-title-checker-config.json` – the title must start with one of: `(MAJOR)`, `(MINOR)`, or `(PATCH)`.

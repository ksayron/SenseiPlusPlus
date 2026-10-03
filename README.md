# Sensei++

Sensei++ helps developers turn learning and engineering work into retained understanding, visible growth, and credible accounts of their experience.

This workspace contains the architecture proposal, modular .NET backend, and a React web client.

Start with the [design package](docs/README.md). The supplied [product brief](docs/inputs/product-brief.md) and [architecture direction](docs/inputs/architecture-direction.md) are preserved unchanged.

The Phase 1 learning subsystem is specified in the [learning system design](docs/09-learning-system.md),
[learner experience](docs/10-learning-experience.md), and [delivery plan](docs/11-learning-delivery-plan.md).
The AI-independent local runtime is implemented; see the [implementation and verification record](docs/13-learning-implementation.md).
The workspace now uses [local email/password authentication](docs/16-local-authentication.md).
The [original learning specification](docs/inputs/learning-system-specification.md) is preserved unchanged.

The optional [free-only AI gateway](docs/17-free-only-ai-gateway.md) has offline adapters and pilot tooling. AI configuration and live provider review are deferred; core learning does not call it.
The [durable assessment pipeline](docs/19-durable-attempt-assessments.md) supports a separate optional request after saving an attempt, with PostgreSQL recovery and attributed exploratory feedback. The shipped route policy remains unavailable while provider activation is deferred.

The [`backend`](backend/README.md) contains the modular-monolith host and domain-aware module CRUDs. The [`client`](client/README.md) is a responsive React + TypeScript workbench for learning, reflection, evidence, and revisioned experience.

Local infrastructure is defined in [`compose.yaml`](compose.yaml). See the
[`backend` setup guide](backend/README.md#postgresql-and-migrations) for PostgreSQL startup,
explicit EF Core migration, connection, and reset commands.

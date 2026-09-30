# SensSera

[![CI](https://github.com/bherduran/SensSera/actions/workflows/ci.yml/badge.svg)](https://github.com/bherduran/SensSera/actions/workflows/ci.yml)

Multi-tenant smart greenhouse monitoring SaaS — sensor ingestion, time-series storage, threshold-based alerts, a real-time dashboard, and an LLM layer that explains alerts and answers questions over your own data.

> Portfolio/learning project. A device simulator stands in for real hardware.

## Quick start (full stack in Docker)

```bash
cp .env.example .env          # optional: add a free Groq key for AI insights
docker compose up -d --build
```

Open <http://localhost:3000> and sign in with the seeded demo account **`admin@demo.com` / `Admin1234!`**.

Nothing else to set up: the API applies migrations on start, seeds the demo org, and the simulator provisions a demo greenhouse with six sensors and thresholds, then streams readings in alert mode — alerts start appearing within a minute. API docs (Scalar) are at <http://localhost:5010/scalar>.

AI insights (alert explanations, "Ask SensSera") need an LLM key in `.env` — [Groq](https://console.groq.com/keys) is free. Without one those two features answer `503` and the rest of the app works normally.

## Stack

- **Backend:** ASP.NET Core (.NET 10), EF Core, PostgreSQL 18, SignalR, FluentValidation, Serilog
- **Auth:** JWT access token (in memory) + rotating httpOnly refresh cookie with reuse detection, BCrypt passwords, SHA-256 device tokens
- **Frontend:** Next.js 16, TypeScript, Tailwind v4, shadcn/ui, TanStack Query, Recharts; API types generated from the OpenAPI doc
- **AI:** provider-agnostic `ILlmClient` — Anthropic (default `claude-opus-5`) or Groq; the model only picks whitelisted typed tools, never writes SQL
- **Ops:** Docker Compose, GitHub Actions CI, Dependabot, Testcontainers

## Architecture

Clean/layered, one-way references:

```
Api → Application + Infrastructure
Infrastructure → Application + Domain
Application → Domain
Domain → (nothing)
```

Three main flows:

1. **Ingestion** — device → `POST /api/ingest` (`X-Device-Token`) → `SensorReading` → SignalR `ReadingReceived`
2. **Alerts** — `ThresholdEvaluationJob` compares latest readings to thresholds → `Alert` (severity from overshoot) → SignalR `AlertRaised`
3. **Query** — Next.js → REST + JWT → dashboard reads rollup aggregates, never raw scans

Multi-tenancy is a shared schema with `OrganizationId` on every tenant-owned row, enforced by an EF Core global query filter fed from the JWT `org_id` claim. Cross-tenant access returns `404`, not `403`.

## Local development

```bash
docker compose up -d db                        # just PostgreSQL
dotnet ef database update --project src/SensSera.Infrastructure --startup-project src/SensSera.Api
dotnet run --project src/SensSera.Api          # http://localhost:5010, Scalar at /scalar
dotnet run --project src/SensSera.Simulator    # self-provisions demo devices
cd senssera-web && npm install && npm run dev  # http://localhost:3000
```

The API needs a local `src/SensSera.Api/appsettings.Development.json` (gitignored) with `ConnectionStrings:DefaultConnection` and `Jwt:Key/Issuer/Audience`. For AI insights locally:

```bash
cd src/SensSera.Api
dotnet user-secrets set "Llm:ApiKey" "<your key>"
```

and set `"Llm": { "Provider": "Groq", "Model": "openai/gpt-oss-120b" }` in `appsettings.Development.json`.

## Tests

```bash
dotnet test SensSera.slnx
```

- `tests/SensSera.UnitTests` — services on EF InMemory with a fake clock
- `tests/SensSera.IntegrationTests` — the real API on a throwaway PostgreSQL container (Docker required), including the end-to-end path ingest → threshold breach → alert → SignalR event

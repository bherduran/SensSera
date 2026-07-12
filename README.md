# SensSera                                                                 
                                                                                                                                                         
  Multi-tenant smart greenhouse monitoring SaaS — sensor ingestion, time-series storage, threshold-based alerts, real-time dashboard.                    
                                                                                                                                                         
  > 🚧 **Work in progress.** Portfolio/learning project.                    
                                                                                                                                                         
  ## Stack                                                                                                                                               

  - **Backend:** ASP.NET Core (.NET 10), EF Core, PostgreSQL 18
  - **Auth:** JWT access + httpOnly refresh cookie, BCrypt passwords, SHA-256 device tokens
  - **Frontend:** Next.js + TypeScript + Tailwind (Stage 9+)
  - **Realtime:** SignalR (Stage 10)
  - **Simulator:** .NET console app that POSTs fake sensor readings

  ## Architecture

  Clean/layered, one-way references:

  ```
  Api → Application + Infrastructure
  Infrastructure → Application + Domain
  Application → Domain
  Domain → (nothing)
  ```

  Three main flows:
  1. **Ingestion** — device → `POST /api/ingest` (X-Device-Token) → SensorReading → SignalR push
  2. **Alerts** — background job compares readings to thresholds → Alert → SignalR push
  3. **Query** — Next.js → REST + JWT → dashboard reads from rollup aggregates

  ## Run locally

  ```bash
  # 1. Start PostgreSQL
  docker compose up -d

  # 2. Apply migrations
  dotnet ef database update --project src/SensSera.Infrastructure --startup-project src/SensSera.Api

  # 3. Run the API (Scalar UI at /scalar)
  dotnet run --project src/SensSera.Api

  # 4. (Optional) Run the simulator
  dotnet run --project src/SensSera.Simulator
  ```

  Requires a local `src/SensSera.Api/appsettings.Development.json` (gitignored) with a connection string and JWT signing key.

  ## Tests

  ```bash
  dotnet test SensSera.slnx
  ```


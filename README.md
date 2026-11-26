# QuizFlow

QuizFlow is being built as a modular monolith. Phase 1 establishes the .NET 10 API, PostgreSQL/Identity persistence, secure account flows, and Angular 22 SSR shell. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the architecture and phase plan and [DEVELOPMENT_PROGRESS.md](DEVELOPMENT_PROGRESS.md) for current status.

## Prerequisites

- .NET 10 SDK
- Node.js 22+ and npm 10+
- PostgreSQL 16+ (or Docker Compose)

## Local development

1. Start PostgreSQL with `docker compose up -d postgres` after setting `POSTGRES_PASSWORD` in the current shell.
2. Configure API environment variables before starting the API:

   ```powershell
   $env:ConnectionStrings__Postgres = 'Host=localhost;Port=5432;Database=quizflow;Username=quizflow;Password=<your-local-password>'
   $env:Jwt__Issuer = 'QuizFlow'
   $env:Jwt__Audience = 'QuizFlow.Web'
   $env:Jwt__SigningKey = '<generate-at-least-32-random-bytes>'
   $env:App__PublicUrl = 'http://localhost:4200'
   $env:Email__SmtpHost = '<smtp-host>'
   $env:Email__From = '<verified-sender>'
   $env:Google__ClientId = '<google-oauth-client-id>'
   $env:Google__ClientSecret = '<google-oauth-client-secret>'
   ```

   SMTP and Google settings are required only to use those flows. Keep secrets in environment variables or .NET user secrets, never in committed configuration.
3. Generate/apply the EF migration. On PowerShell, set `QUIZFLOW_DESIGN_CONNECTION` to the PostgreSQL connection string for design-time commands:

   ```powershell
   $env:QUIZFLOW_DESIGN_CONNECTION = $env:ConnectionStrings__Postgres
   dotnet ef database update --project src/backend/QuizFlow.Infrastructure --startup-project src/backend/QuizFlow.Api
   dotnet run --project src/backend/QuizFlow.Api
   ```

4. Install and run the Angular app from `src/frontend` with `npm install` and `npm start`. The dev proxy sends `/api` to the API on port 5242.
5. Build and run backend tests with `dotnet build QuizFlow.slnx` and `dotnet test QuizFlow.slnx`.

Google OAuth redirect URI for local development: `http://localhost:5242/signin-google`. Use a Google OAuth web client and configure the allowed JavaScript origin for the frontend. Email/password registration requires a working SMTP sender to deliver the time-limited confirmation link.

This repository does not include production deployment or real-payment configuration. Razorpay and production operations require a separate explicit approval.
# quiz

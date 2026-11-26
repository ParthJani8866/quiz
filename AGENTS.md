# QuizFlow development standards

## Architecture
- Build a modular monolith with ASP.NET Core 10, Clean Architecture boundaries, Angular 22 SSR, and PostgreSQL.
- Keep domain rules in `src/backend/QuizFlow.Domain`; use cases/contracts in `Application`; persistence and external adapters in `Infrastructure`; HTTP composition in `Api`.
- Keep tenant-owned records scoped to an organization. Tenant identifiers come from authenticated membership, never request bodies or query strings.
- Use EF Core migrations for schema changes. Store only hashes of refresh and one-time tokens.
- Keep credentials in environment variables or a secret manager. Do not commit secrets.
- Keep public marketing routes SSR-capable; authenticated/private routes must not be indexed.

## Implementation standards
- Use async I/O, cancellation tokens, structured logs, validation at API boundaries, and explicit DTO mapping.
- Require authorization on protected endpoints. Validate Google OIDC state/nonce and email verification before linking identities.
- Do not implement payment flows without verified webhook signatures. Never use production payment credentials in development.
- Update `DEVELOPMENT_PROGRESS.md` whenever a phase changes. Complete one phase before starting another.
- Run builds and requested tests, record exact commands and outcomes, and resolve critical errors before moving on.

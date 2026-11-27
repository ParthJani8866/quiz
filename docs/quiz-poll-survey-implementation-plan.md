# Quiz, poll, and survey implementation plan

The repository currently contains the ASP.NET Core 10 Clean Architecture scaffold, Identity and refresh-token persistence, and an Angular 22 SSR application. There are no organization or quiz/poll/survey domain models yet. Work will follow the requested phase order and retain the existing `Domain` / `Application` / `Infrastructure` / `Api` boundaries.

## Delivery phases

1. **Database schema and domain models (current):** establish organization ownership, shared auditing/soft-delete conventions, quiz/poll/survey aggregates and question/option/section structures, media metadata, EF Core mappings, indexes, foreign keys, and an additive PostgreSQL migration. Avoid committing binary media or storing derived analytics as source data.
2. **Quiz authoring:** implement organization-scoped quiz CRUD, folders, question authoring, settings, validation, DTOs, and protected APIs.
3. **Quiz participation and scoring:** implement access/join flows, durable attempts and answers, submission idempotency, server-authoritative scoring, result policies, and tests.
4. **Live quiz:** add host/participant session state, SignalR isolation, timers, leaderboard, reconnection behavior, and concurrency coverage.
5. **Polls:** add scoped authoring, vote validation and deduplication, result aggregation, live result events, and exports.
6. **Survey builder and collection:** add pages/sections, question types, branching rules, drafts/resume, response validation, and participant access.
7. **Survey analytics and exports:** add permitted response analysis, filtering, aggregation, and export formats.
8. **Presentation and shared live sessions:** build presentations and reusable host/participant session orchestration across activities.
9. **Dashboard and reports:** connect real aggregates to responsive Angular dashboard, listings, authoring and reporting experiences.
10. **Security and performance hardening:** tenant-boundary, authorization, concurrent submission, load, accessibility, and end-to-end validation.

Each phase will update `DEVELOPMENT_PROGRESS.md`; implementation will not advance to a later phase until the current phase is complete.

## Phase 1 boundaries and decisions

- Organization is introduced as the tenant root, with membership linked to the existing Identity user. Activity records carry an organization key so later APIs can derive scope from authenticated membership.
- Quiz, poll, and survey content use separate aggregates because their lifecycle and response semantics differ. Questions and options are normalized; flexible settings remain JSONB only where the settings are genuinely variable.
- Media rows store metadata and a storage key, not uploaded bytes. Response/attempt/session records are added with the phase that establishes their behavior, avoiding a speculative schema for live runtime workflows.
- Existing Identity tables remain intact. The new migration is additive and does not apply itself to a database.

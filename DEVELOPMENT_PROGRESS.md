# QuizFlow development progress

## Phase 1 — project setup, architecture, database and authentication
- [x] Architecture, project structure and initial ER diagram documented.
- [x] .NET 10 Clean Architecture solution scaffolded.
- [x] Angular 22 SSR application scaffolded.
- [x] PostgreSQL Identity schema, refresh-token table and initial EF migration.
- [x] Registration, email verification, login, refresh rotation, logout/revoke-all, password reset/change and profile APIs.
- [x] Google OIDC authorization-code flow with state/nonce checks and verified-email linking.
- [x] Angular SSR auth experience, Google popup, forms, refresh-cookie flow and bearer interceptor.
- [x] Backend/frontend builds and unit/component tests.
- [ ] Apply the EF migration against a configured local PostgreSQL role and verify SMTP/Google credentials.

## Later phases
- [ ] Phase 2: organizations and tenant isolation.
- [ ] Phase 3: presentation builder.
- [ ] Phase 4: quiz builder.
- [ ] Phase 5: polls and surveys.
- [ ] Phase 6: live sessions and SignalR.
- [ ] Phase 7: participant experience and analytics.
- [ ] Phase 8: subscriptions and Razorpay.
- [ ] Phase 9: super-admin and billing management.
- [ ] Phase 10: SEO, marketing site and blog.
- [ ] Phase 11: security, performance and test hardening.
- [ ] Phase 12: deployment and production launch.

No production deployment or real-payment operation has been performed.

## Quiz, poll, and survey implementation track
- [x] Reviewed the existing solution and documented the implementation sequence and Phase 1 schema boundaries in `docs/quiz-poll-survey-implementation-plan.md`.
- [x] Phase 1: added organization and membership domain models, tenant-scoped quiz/poll/survey aggregates, normalized question/option/page models, settings, survey logic rules, media metadata, EF configurations, and additive `ActivitySchemaInitial` migration.
- [ ] Phase 1: apply the additive migration to a configured local PostgreSQL role and verify database schema.
- [~] Phase 2: quiz authoring APIs and validation (first full-stack slice implemented; database-backed integration remains unverified).
- [x] Added authenticated organization-scoped quiz list/read/create/update/archive APIs, server-side request validation, quiz folders, nested question/option persistence, and per-user workspace provisioning on registration/login.
- [x] Added responsive Angular quiz library and editor with search/status filters, folders, settings, question/option editing, preview, save/publish/archive, and sign out.
- [x] Added quiz workspace component tests and a single-worker Vitest configuration for stable Windows execution.
- [ ] Phase 3: quiz participation, scoring, and results.
- [ ] Phase 4: live quiz, SignalR, and leaderboard.
- [ ] Phase 5: poll authoring, voting, and live results.
- [ ] Phase 6: survey builder, question types, and response validation.
- [ ] Phase 7: survey branching, logic, and response collection.
- [ ] Phase 8: survey analytics and exports.
- [ ] Phase 9: presentation builder and shared live-session engine.
- [ ] Phase 10: dashboard, reports, security, and performance hardening.

Latest module check: `dotnet test QuizFlow.slnx` passed (3 tests); `dotnet build QuizFlow.slnx` passed (0 warnings/errors); `dotnet ef migrations has-pending-model-changes --project src/backend/QuizFlow.Infrastructure/QuizFlow.Infrastructure.csproj --startup-project src/backend/QuizFlow.Api/QuizFlow.Api.csproj` passed with `QUIZFLOW_DESIGN_CONNECTION` set (no pending changes). Migration scaffolding succeeded with a design-time placeholder connection string. Applying the migration remains pending because no PostgreSQL password/role is configured.

Latest quiz authoring check: `dotnet build QuizFlow.slnx` passed (0 warnings/errors); `dotnet test QuizFlow.slnx` passed (3 tests); `npm.cmd run build` passed (Angular SSR production build); `npm.cmd test -- --watch=false` passed (4 tests); `git diff --check` passed. Live API/database integration remains unverified until PostgreSQL credentials are configured.

Latest checks: `dotnet build QuizFlow.slnx` passed (0 warnings/errors); `dotnet test QuizFlow.slnx` passed (3 tests); `dotnet ef migrations has-pending-model-changes` passed; `npm run build` passed; `npm test -- --watch=false` passed (2 tests). API startup and `/health` returned 200; the unconfigured Google endpoint correctly returned 503. PostgreSQL accepts connections on localhost, but the available `quizflow` role password did not authenticate, so migration application and live database API verification remain pending.

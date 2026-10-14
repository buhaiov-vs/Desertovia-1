# Contributing

Setup, running locally and the test suites are described in [README.md](README.md). Game rules live in [docs/game-design.md](docs/game-design.md), plans in [docs/roadmap.md](docs/roadmap.md).

## Workflow

1. Pick an issue or open one first for anything larger than a small fix, so the idea can be discussed before work starts.
2. Create a branch from `main` (contributors without write access fork the repository first). Name it after the change: `tavern-upgrades`, `fix-storage-overflow`.
3. Commit in small steps. A commit message says what the step did, not how: `Tsar appoints Governors: API endpoint and tests`.
4. Open a pull request to `main` and fill in the template. Keep one topic per pull request.
5. Continuous integration must pass and a code owner must approve before merging. Pull requests are squash-merged.

## Continuous integration

Every pull request and every push to `main` runs [.github/workflows/ci.yml](.github/workflows/ci.yml):

| Job | Runs | Locally |
|---|---|---|
| Backend tests | build of `Desertod.slnx`, domain tests, API tests on a throwaway PostgreSQL container | `dotnet test Desertod.slnx` (needs Docker) |
| Frontend lint and build | `oxlint`, TypeScript check, production build | `cd web; npm run lint; npm run build` |
| Browser tests | Playwright on desktop and phone against a real backend | `docker compose up -d; cd web; npm run test:e2e` |
| Docker image | builds the production `Dockerfile` without publishing it | `docker build .` |

Failed runs keep their test results, and for browser tests the Playwright report with screenshots and traces, as downloadable artifacts on the run page.

## Conventions

- **Tests.** Game rules get unit tests in `tests/Desertod.Domain.Tests`; new endpoints get API tests in `tests/Desertod.Api.Tests`; new pages or menu tabs get a browser smoke test in `web/e2e`.
- **Translations.** All player-facing text goes into `web/src/i18n`. `en.ts` is the source of truth; add the same key to `ru.ts` (a missing key fails the build).
- **Server stays language-neutral.** Rule errors return a `code` and `arguments`; the client translates them.
- **Database migrations** are generated, never written by hand: `dotnet ef migrations add <MigrationName> --project src/Desertod.Infrastructure --startup-project src/Desertod.Api --output-dir Data/Migrations`. If two pull requests both add migrations, the one merged second regenerates its migration on top of `main`.
- **Secrets** never go into the repository: use `dotnet user-secrets` locally and `deploy/.env` on the server.
- **Names** are spelled out: `button`, not `btn`.
- **Documentation.** Update `README.md` or `docs/` in the same pull request when behaviour, settings or rules change.

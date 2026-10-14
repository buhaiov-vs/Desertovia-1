## What and why

<!-- What changes for players or developers, and why. Link the issue: "Closes #123". -->

## How it was tested

<!-- Tests added or run, and what you checked by hand (desktop and phone if the user interface changed). -->

## Checklist

- [ ] `dotnet test Desertod.slnx` passes
- [ ] `npm run lint` and `npm run build` pass in `web`
- [ ] New player-facing text is in both `web/src/i18n/en.ts` and `web/src/i18n/ru.ts`
- [ ] Database changes come with a migration made by `dotnet ef migrations add`
- [ ] `README.md` or `docs/` updated if behaviour, settings or rules changed
- [ ] Screenshots attached if the user interface changed

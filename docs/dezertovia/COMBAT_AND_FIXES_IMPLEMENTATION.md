# D-069 — реализация новой обороны и связанных исправлений

Поручение: весь код и проверки D-068 разрешены. Ветка feat/live-village-combat,
исходныйmain2e8ee2623464822ac9834489149d5e122a5eaf87. LIVE0.6.1 пока неизменён.
Установка не раньше16.10.2026 07:00 Europe/Oslo (05:00UTC), только после готовности
и точного успешногоCI. User authorization сохраняется после сжатия контекста.

| Блок | Статус |
| --- | --- |
| COMBAT-01 | in_progress: аудит геометрии и серверного цикла |
| COMBAT-UI | pending |
| ENERGY-02 | pending |
| UI-05 | pending |
| CYCLE-02 | pending |
| ENERGY-03 | pending |
| LAND-01 | pending |
| STORAGE-UX | pending |
| Checks | after code package |
| Scheduled rollout | Automation confirmed for16.10.2026 07:00 Europe/Oslo; only ready exact CI; no switch before05:00UTC |

План: COMBAT_AND_FIXES_PATCH_PLAN.md + RAID_REWORK_PLAN.md.
Записывать реальные файлы/решения/числа/проверки/точныеSHA; не считать план результатом.

## Checkpoint1 — in progress, not tested/not ready to deploy

Added Domain RaidBattle/units/fighters, RaidNavigation, RaidBalance/RaidSimulation,
persistent palisade Fortification + repair compatibility; API preparation/dispatch/
orders/gate and EF mappings started. No migration/build/tests yet. Source needs
review and completion, not a deployable artifact. Next: close lifecycle/cancellation/
wave overlap; bounded delta networking; client actor selection/cards; energy/cycle
repros/fixes, storage and boundary preview; AI speech JSON screenshot fix.

Additional user steering: screenshot shows service JSON option/reason published as
Father speech. Confirmed competing JSON+plain text instructions in bot almighty.py.
Include AI-SPEECH-01: separate speech prompt and defense at API publication, preserve
quiet-hour privacy. No actual messages need deletion or synthetic test messages.
Scheduled task is registered; do not claim deployment completed before it runs.

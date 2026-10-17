# Isolated third-party algorithms

- Nez UtilityAI: prime31/Nez@2862974e423f1dccaed4887673cd5c241faa1c55, MIT.
  Five generic utility selection classes only; no MonoGame dependencies.
- RVO2-CS: snape/RVO2-CS@8e7cf2f9bdc331fe0802cdfb10d6c19fb5b20745, Apache-2.0.
  Seven RVOCS algorithm files only. Copyright notices retained in every file.

Adaptations: nullable analysis disabled for unchanged legacy sources.
Domain adapters own all gameplay rules and validate movement against terrain.
No network, game-state ownership, or engine code is imported.

RVO adaptations: public per-battle Simulator constructor; Agent/KdTree receive
their owning instance instead of accessing the upstream singleton, which is removed. NumWorkers=1
uses ordinary loops to avoid scheduling parallel jobs under the game lock.

# Cyberland.Demo.Pong

**Purpose:** single-session Pong on one `State` + `Control` entity, explicit sprite/text handles, `PongInputSetup` for default bindings, and `VisualSyncSystem` interpolation with `GameHostServices.FixedAccumulatorSeconds`.

**Run:** enable in `manifest.json`, run **Cyberland.Host**.

**Teaches:** `SimulationSystem` uses circle-vs-rectangle tests for paddle hits (not `Trigger` events), because engine `TriggerSystem` runs before mod fixed updates—see `TriggerSystem` engine remarks. Uses `ModLayoutViewport` in late (lights, visuals) and in simulation for arena sizing.

See root `README.md` for *Reference examples*.

# Original flight diagnostic status

Launch this isolated scene with `PlayFlight.ps1`. It is not reached from the normal start-screen flow and does not implement a stock game day.

## Source-backed pieces

- The runtime reads the active profile slot's body, launcher, stage IDs, and levels from the original registry. It does not force the old Crate/Coil/Rocket test loadout.
- `OriginalFlightPhysics.cs` translates the implemented subset of the stock player and physics rules, including the Nape integration, drag buffer, launch power, fuel, stage mass, and weight-scaled stage power. Active stage power is recalculated after stage separation using the remaining mass.
- Flight music starts the three stock Punk tracks and applies the speed-zone fades from `Music.as`. The scene uses original art and selected sound effects.

## Not implemented

This is not the original mode engine, shop-to-flight integration, equipment-controller set, obstacle/wind/boost simulation, camera/HUD, day-end timer, result screen, payout/progression, or replay flow. The simplified grounded end condition is diagnostic. In verification mode, the scene writes only `user://original-flight-test.json`; it does not update a game slot.

## Recorded comparison limits

The AS3 free-flight solver oracle matched with zero error in the recorded run. Ground-contact comparison remains at maximum error `0.022545844871274312` against `1e-7` tolerance. The comparison has not been resolved by loosening tolerance. See the oracle files under `Assets/Flight/` and the source-parity audit for context. A successful build does not prove numerical, visual, or full-game parity.

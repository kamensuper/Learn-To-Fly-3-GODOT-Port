# Learn to Fly 3 C# port

This is a source-grounded, incomplete port using Godot .NET. It is not a complete or pixel-identical recreation. The project entry point is `StartScreen.tscn`; open `project.godot` with Godot .NET or launch it with `Play.ps1`.

## Current behavior

- The start screen uses exported game artwork, the embedded font, source-backed menu timing, and the original menu music. Play retains the selected one of six save slots; new-game setup retains the chosen stock mode. Stock profile unlock flags gate the three alternate modes.
- Port-local profile and slot data are persisted in Godot's `user://ltf3-port-profile.json`. No Steam save or account data is read. A new slot uses the decompiled game's empty equipment IDs and initial $10 state.
- The equipment screen reads the selected slot's values and registry-backed loadout. Equipment buying, equipping, drag reorder, and the day/reward loop are not implemented, so the screen is not yet the stock interactive shop.
- The Bonus Shop reads registry data, charges available BP costs, and saves supported bonus levels locally. Bonus hooks and paid `bonusOver` sub-upgrades are incomplete; unsupported costs are blocked instead of treated as free.
- `OriginalFlight.tscn`, launched separately with `PlayFlight.ps1`, is a diagnostic flight scene. It reads equipment from the selected slot and uses a source-derived physics kernel and the original Nape library, but it is not connected to normal menu progression, stock results, rewards, or day advancement. Its verification record is test-only.
- The original menu, shop, bonus-shop, results, and layered flight music files are available to the adapters. Source music crossfades are implemented for the diagnostic flight; settings, track selection, and every sound event are not.

## Evidence and limits

The decompiled AS3 tree is a reconstruction of the stock SWF bytecode. The source-parity report inventories all available AS3 and C# lines but makes semantic comparisons only for mapped systems; inventory counts are not a port-completion score. Some Godot code is necessarily an engine adapter, while diagnostic UI/physics scenes are not stock game screens.

The last recorded C# build completed with zero warnings and errors. Historical screenshots and oracle captures document earlier states; they do not establish current visual or gameplay parity. The aligned Nape free-flight oracle matched exactly in the recorded comparison. Ground-contact error remains `0.022545844871274312` against tolerance `1e-7` and is unresolved.

Remaining major work includes the real equipment transaction flow, mode-specific game engines, full player/equipment controllers, obstacles and wind, the stock result and reward loop, bonus hooks and unlock progression, complete menu actions, all music/settings behavior, and exact Flash layout, filters, display-list animation, and resize behavior.

See `FLIGHT-STATUS.md` and the source-parity audit for subsystem detail. The earlier invented generic prototype (`Main.cs`, `Main.tscn`, and its capture) has been moved outside this active project into the Codex work archive; do not treat it as LTF3 behavior.

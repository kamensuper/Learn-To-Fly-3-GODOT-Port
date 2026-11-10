Learn to Fly 3 — Godot Port
Status: Work in progress · Private preservation / engineering project · Not an official release

A high-fidelity port of Learn to Fly 3 from its original Flash / ActionScript 3 (AS3) implementation to Godot 4.5.1 .NET (C#). The project aims to reproduce the original game's behavior—including its physics, equipment, launch sequences, progression, user interface, visuals, and audio—rather than build a loosely inspired remake.

The original AS3 game is the behavioral specification. When the Godot implementation disagrees with the original, investigate the discrepancy before changing either the design or the numerical model.
At a glance
Area	Target / current state
Engine	Godot 4.5.1 .NET
Language	C#
Reference implementation	Original Flash / AS3 game
Physics	Ported Nape implementation, with numerical parity testing
Save system	Six profile slots; persistence and mode handling under audit
Core loop	Profile → build/shop → launch → flight → results → next day
Assets	Original assets imported in the working project; distribution rights must be respected
Release status	Incomplete, under active development


Project goals
1. Gameplay fidelity. Preserve original mechanics, progression rules, equipment behavior, and flight outcomes.
2. Physics fidelity. Reuse and validate the original Nape behavior instead of substituting Godot's default rigid-body physics and calling it equivalent.
3. Presentation fidelity. Match the launchpad, flight camera, altitude regions, HUD, menus, effects, and sound timing.
4. Reliable persistence. Keep profile state, purchases, upgrades, unlocks, and results consistent across sessions.
5. Traceable changes. Back significant fixes with tests, comparison data, and clear commits.
Current implementation status
The following reflects the latest known development notes, not a fresh audit of this repository. Check the code and test evidence before treating any item as complete.
Subsystem	Known progress	Remaining verification / work
Menu and profiles	Six-slot menu flow; empty slots select a mode and initialize the selected slot	Confirm persistent save/load, slot IDs, and mode restoration
Starting progression	New profiles intended to start in Story Mode with zero Bonus Points	Validate against original initialization and all save paths
Build/loadout	Starter Crate, Coil, and two Rockets represented in the flight path	Ensure loadout always comes from the active saved profile
Flight scene	Separate playable flight scene with launch, steering, and cutoff	Reconnect all normal gameplay systems and verify full loop
Nape physics	Exact-release Nape compiled for C#; prior free-flight trajectory comparisons reported zero measured difference	Preserve regression evidence; extend comparison to collisions and complex interactions
Launchpad	Coil animation uses a 164-frame timeline; release around frame 103, with spring audio handoff	Verify frame timing, equipment variants, and flight transition
Camera and environment	Ground → Clouds → Space → Moon ascent behavior is a target	Finish/verify camera, sky palette, obstacles, and red-zone effects
Flight HUD	Speed, altitude, drag, abort, fuel, and staging indicators are targets	Complete and compare with original presentation
Results	Max altitude, max speed, airtime, distance, payout, and day advancement defined as targets	Verify actual results calculation and progression updates
Shop and upgrades	Shop and Bonus Shop paths exist in the working design	Repair purchase validation, spending, state mutation, and persistence
Imported visuals	Original art has been imported; anchor markers were hidden to match runtime presentation	Audit asset mapping, alignment, and effects
Audio	Original sound effects and loop timing are part of the fidelity goal	Confirm playback, loop points, and event synchronization


Known parity problems
An earlier parity audit identified issues requiring explicit fixes:
- Profile slot IDs or selected modes could be lost or discarded.
- The shop could display items without performing real transactions.
- Bonus upgrades could bypass payment.
- A separate flight prototype could start with a fabricated default loadout instead of the player's saved equipment.
- Normal menu → shop → launch → results → save transitions were not fully connected.
These are tracked defects, not acceptable simplifications. Fix them at their source and verify the entire player-facing path.
Architecture and fidelity principles
AS3-first behavior
The port should be developed by examining original implementation behavior, identifying the corresponding Godot component, implementing it, and comparing the result. Avoid inventing mechanics or silently changing formulas for convenience.
Nape physics
The original game uses Nape, and the port includes a C# build of the corresponding implementation. Earlier tests reported zero measured difference for sampled free-flight trajectories between the Flash and C# paths. This is an important milestone, but it does not establish parity for every collision, constraint, or gameplay interaction.

For each physics change:
1. Identify the relevant AS3/Nape behavior.
2. Capture a reproducible original-game test case.
3. Run the equivalent Godot/C# case with the same initial state and inputs.
4. Compare position, velocity, angle, collision behavior, and relevant gameplay state over time.
5. Document discrepancies and their causes before adjusting the implementation.
Do not replace Nape with Godot physics without explicitly changing the project's fidelity goals.
Launchpad and flight handoff
The Coil launchpad has been modeled around a 164-frame animation, with release at approximately frame 103. The spring sound, equipment pose, initial velocity, and camera handoff should remain synchronized. Treat timing as a parity requirement, not a cosmetic detail.
Coordinate limits and high-altitude flight
The original Flash game exhibited a high-altitude issue around 107,375,182 altitude. Investigation found that Flash display-object coordinates could saturate near ±107,374,182.4, while Nape's numeric physics state could represent much larger values. The game's ground offset helps explain the altitude shown to the player.

The engineering direction is to decouple visual coordinates from simulation coordinates (for example, camera-relative rendering or visual-origin rebasing), rather than clamp the underlying physics state. Verify this independently against the original and any subsequent official patches.
Intended player loop
Select profile slot
        ↓
Load saved profile / create new Story profile
        ↓
View equipment, shop, upgrades, and available resources
        ↓
Configure a valid loadout
        ↓
Play launchpad animation and release
        ↓
Fly, steer, stage equipment, and monitor HUD
        ↓
Calculate results and payout
        ↓
Advance day and save updated profile
        ↓
Return to preparation / shop
A feature is not complete merely because a standalone test scene demonstrates it. It must work through this real gameplay loop, with the selected profile's actual state.
Building and running
Prerequisites
- Godot 4.5.1 .NET (not the standard non-.NET editor)
- A compatible .NET SDK for the project
- The project files and any required original-game assets / local dependencies
Local setup
1. Clone the private repository.
2. Install the .NET-enabled Godot editor and the SDK required by the project.
3. Open the directory containing project.godot in Godot.
4. Allow C# compilation and asset import to finish.
5. Build the C# project and run the configured main scene.
Repository layout note: Exact paths, main-scene names, and build commands must be verified against the checked-in project. This README deliberately does not invent a folder structure or a test command that may not exist.

Testing strategy
Physics regression
Compare deterministic trajectories, launch conditions, staging, drag, and collisions with original AS3 traces. Keep tolerances explicit and avoid interpreting visual similarity as numerical equality.
Persistence and economy
Test at minimum:
- New slot creation and correct slot selection.
- Story-mode initialization and correct starting currency / Bonus Points.
- Purchasing equipment and upgrades only when affordable.
- Correct deductions, caps, unlocks, and inventory changes.
- Save → quit → relaunch → reload with unchanged state.
- Isolation between all six profile slots.
- Results payout, day advancement, and persistence after a completed flight.
Presentation
Compare launchpad timing, camera tracking, background transitions, HUD readings, sprite placement, sound cues, and audio loop points. Use captured reference evidence where available.
End-to-end smoke test
Create a new profile, buy or configure equipment, launch, fly, finish, receive results, save, restart the game, and confirm that the same profile resumes with the correct progression.
Development roadmap
These are priorities, not promises or completed milestones.
Priority	Milestone	Acceptance criteria
P0	Repository baseline	Project builds; entry scene identified; existing features and failures documented
P0	Profile integrity	Six slots and modes survive restart; no cross-slot contamination
P0	Economy integrity	Shop and Bonus Shop transactions validate funds, apply changes, and persist
P0	Connected Story loop	Real profile loadout flows through launch, flight, results, day advancement, and save
P1	Collision parity	Reproducible comparisons cover contact and constraint behavior
P1	Presentation parity	Launchpad, camera, HUD, sky regions, sprites, and audio closely match reference
P1	High-altitude robustness	Visual rendering remains stable beyond the historical Flash display limit
P2	Expanded mode / equipment coverage	Additional original modes, items, and edge cases validated systematically
P2	Polish and regression suite	Repeatable checks and documented known differences


Rules for contributors and AI coding agents
Read this section before changing gameplay code.
- Inspect first. Understand the existing scenes, scripts, physics library, assets, and save schema before editing.
- Preserve prior work. Do not rewrite a working subsystem just because a different architecture is easier to generate.
- AS3 is the reference. Cite the original behavior or a reproducible test when changing mechanics.
- No fake completions. A display-only shop, hardcoded loadout, or scripted mock result does not count as a working system.
- No fabricated parity claims. Report the exact cases compared and the observed differences.
- Keep builds green. Run the available build and tests before committing.
- Commit by milestone. Use descriptive commits and push checkpoints to the intended branch; do not force-push or delete unrelated work.
- Record unknowns. If reference source, assets, or test data are unavailable, say so rather than guessing.
- Respect provenance. Do not add proprietary materials to public branches or releases without authorization.
Legal and attribution
Learn to Fly 3 is the original work of its respective creators and rights holders, including Light Bringer Games. This is an unofficial, independently developed porting and preservation project, not an endorsed or authorized release.

The repository may contain or depend on copyrighted original-game code, art, audio, and other assets. Private repository visibility does not itself grant redistribution rights. Do not publish, mirror, license, or distribute those materials without the necessary permissions. Any future public release requires a separate rights and licensing review.

No open-source license is implied for third-party game assets or source material.
Project philosophy
Faithfulness over shortcuts. Evidence over assumptions. A working gameplay loop over disconnected prototypes.

The goal is not merely to make something that looks like Learn to Fly 3. The goal is to understand why the original behaves as it does, reproduce that behavior carefully, and make the result maintainable and testable in Godot.

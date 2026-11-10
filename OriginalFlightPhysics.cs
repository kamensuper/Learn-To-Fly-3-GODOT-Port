using System;
using System.Collections.Generic;
using System.Linq;

// Translated flight kernel: PhysicsEngine, ZPP_Space.updateVel, SimpleBody.tickDrag,
// Player.applyThrust and StatPowerAtWeight. All equipment values come from the
// selected local GameState; this kernel does not supply a default test loadout.
public sealed class OriginalFlightPhysics
{
    private readonly OriginalNapeWorld world;
    private readonly double bodyMass;
    private readonly double[] stageMasses;
    private readonly double[] stagePowers;
    private readonly double[] stageFuels;
    private readonly OriginalEntry[] stageEntries;
    private readonly int[] stageLevels;
    private readonly double[] drag = Enumerable.Repeat(.991, 30).ToArray();
    private int dragIndex;
    private double delay = 40;

    public double X, Y, Vx, Vy, Angle, AngularVelocity, Peak, Time, Fuel, Mass, TopSpeed, Power, LaunchPower;
    public bool Ended, EngineOn;
    public int Stage;
    public int StageCount { get; }
    public double SpaceAltitude = 200000;

    public static double Interpolate(double a, double b, double lo, double hi, double x, double power = 1, bool clamp = true)
    {
        if (lo == hi) return b;
        double t = (x - lo) / (hi - lo);
        if (clamp) t = Math.Clamp(t, 0, 1);
        return a + (b - a) * Math.Pow(t, power);
    }

    public OriginalFlightPhysics(OriginalEntry body, int bodyLevel, OriginalEntry launcher, int launcherLevel,
        IReadOnlyList<(OriginalEntry Entry, int Level)> stages)
    {
        double Value(OriginalEntry entry, string key, int level, double fallback) =>
            entry.stats.ContainsKey(key) ? entry.Value(key, level) : fallback;

        StageCount = stages.Count;
        stageEntries = stages.Select(stage => stage.Entry).ToArray();
        stageLevels = stages.Select(stage => stage.Level).ToArray();
        stageMasses = stages.Select(stage => Value(stage.Entry, "statWeight", stage.Level, 0)).ToArray();
        stagePowers = stages.Select(stage => Value(stage.Entry, "statPower", stage.Level, 0)).ToArray();
        stageFuels = stages.Select(stage => Value(stage.Entry, "statFuel", stage.Level, 0)).ToArray();
        bodyMass = Value(body, "statWeight", bodyLevel, 1);
        Mass = Math.Max(1, bodyMass + stageMasses.Sum());
        TopSpeed = Value(body, "statTopSpeed", bodyLevel, 10);
        Power = StageCount > 0 ? stagePowers[0] : 0;
        Fuel = StageCount > 0 ? stageFuels[0] : 0;

        Power = StagePowerAtCurrentMass(0);
        LaunchPower = Value(launcher, "statPower", launcherLevel, 0) * WeightRatio(launcher, launcherLevel, Mass);
        Vy = -LaunchPower * 4.15;
        world = new OriginalNapeWorld(Mass);
    }

    // Kept for the isolated kernel-oracle checks; runtime loadout construction uses
    // the state-driven overload above. The repeated stage is only a test fixture.
    public OriginalFlightPhysics(OriginalEntry body, OriginalEntry launcher, OriginalEntry stage, int level = 0)
        : this(body, level, launcher, level, new[] { (stage, level), (stage, level) })
    {
    }

    public void Step(bool cut = false, double steering = 0)
    {
        if (Ended) return;
        Time += 1.0 / 60;
        // PhysicsEngine advances its Nape world in twelve 1/720-second substeps per game tick.
        world.Body.get_velocity().setxy(Vx, Vy);
        world.Body.set_rotation(Angle);
        world.Body.set_angularVel(AngularVelocity);
        world.Step();
        X = world.Body.get_position().get_x();
        Y = world.Body.get_position().get_y();
        Vx = world.Body.get_velocity().get_x();
        Vy = world.Body.get_velocity().get_y();
        Angle = world.Body.get_rotation();
        AngularVelocity = world.Body.get_angularVel();

        double speed = Math.Sqrt(Vx * Vx + Vy * Vy) / 60;
        double d = speed <= TopSpeed
            ? Interpolate(.009 / 2, .009, 0, TopSpeed, speed, 1.5)
            : .009 + .035 * Soft((speed - TopSpeed) / TopSpeed);
        double altitude = -Y;
        double density = altitude <= SpaceAltitude * .75
            ? Interpolate(1, .9, 0, SpaceAltitude * .75, altitude)
            : altitude <= SpaceAltitude * .95
                ? Interpolate(.9, .8, SpaceAltitude * .75, SpaceAltitude * .95, altitude)
                : Interpolate(.8, .5, SpaceAltitude * .95, SpaceAltitude, altitude);
        drag[dragIndex++ % drag.Length] = 1 - d * density;
        double dragMultiplier = 1;
        double launchDuration = Interpolate(220, 3000, 100, 700000, LaunchPower);
        double ticks = Time * 60;
        if (ticks < launchDuration) dragMultiplier = Interpolate(.01, 1, 0, launchDuration, ticks, 1.6, true);
        double retention = 1 - (1 - drag.Average()) * dragMultiplier;
        double speedAngle = Math.Atan2(Vy, Vx) * 180 / Math.PI;
        if (speedAngle >= 0 && speedAngle <= 180)
        {
            double fall = Interpolate(.1, 1, 0, 90, Math.Abs(speedAngle - 90), 1, true);
            retention = 1 - (1 - retention) * fall;
        }
        Vx *= retention;
        Vy *= retention;

        EngineOn = false;
        if (Stage < StageCount)
        {
            if (delay < 40) delay++;
            else if (Fuel > 0 && !cut)
            {
                EngineOn = true;
                Vx += Math.Sin(Angle) * Power * 4.15;
                Vy -= Math.Cos(Angle) * Power * 4.15;
                Fuel--;
            }
            else if (Fuel <= 0)
            {
                Stage++;
                Mass = Math.Max(1, bodyMass + stageMasses.Skip(Stage).Sum());
                world.Body.set_mass(Mass);
                if (Stage < StageCount)
                {
                    Fuel = stageFuels[Stage];
                    Power = StagePowerAtCurrentMass(Stage);
                }
                else
                {
                    Fuel = 0;
                    Power = 0;
                }
                delay = 0;
            }
        }

        // SimpleRudder steering kernel retained for this diagnostic flight scene.
        AngularVelocity = (AngularVelocity + steering * .15 * 2) * .9;
        Peak = Math.Max(Peak, -Y);
        if (Time > 1 && Stage >= StageCount && world.Body.get_arbiters().get_length() > 0)
        {
            Y = 0;
            Ended = true;
            EngineOn = false;
        }
    }

    private static double Soft(double x) => x >= 1 ? 1 + (x - 1) * .1 : x;

    private double StagePowerAtCurrentMass(int index) => index >= 0 && index < StageCount
        ? stagePowers[index] * WeightRatio(stageEntries[index], stageLevels[index], Mass)
        : 0;

    private static double WeightRatio(OriginalEntry entry, int level, double mass)
    {
        if (!entry.stats.TryGetValue("statPowerAtWeightMult", out var rows)) return 1;
        return OriginalStatRules.WeightRatio(rows.Select(row => row.ToArray()).ToArray(), level, mass);
    }

    public static void NapeFreeStep(ref double x, ref double y, ref double vx, ref double vy)
    {
        const double dt = 1.0 / 720;
        for (int i = 0; i < 12; i++)
        {
            vx = (1 - dt * .015) * vx;
            vy = (1 - dt * .015) * vy + 250 * dt;
            x += vx * dt;
            y += vy * dt;
        }
    }

    public void Abort() { Ended = true; EngineOn = false; }
}

extern alias NapeEngine;

using System;
using System.Collections.Generic;
using System.Linq;
using NapeEngine::nape.dynamics;
using NapeEngine::nape.geom;
using NapeEngine::nape.phys;
using NapeEngine::nape.shape;
using NapeEngine::nape.space;

// GameEngine, PhysicsEngine, EmptyBody/SimpleBody, Player, SimpleRudder and WindEngine.
// World coordinates are Flash pixels, with positive y down.
public sealed class CoilStoryPhysics
{
    public const int FrameRate = 65;
    public const double Tick = 60.0 / FrameRate;
    public const double GroundLevel = 1000;
    private const double PowerPerMass = 4.15;
    private const double SteeringMultiplier = 2.0; // Global.STEERING_POWER_MULT
    private readonly Body[] floors = new Body[5];
    private readonly double[] drag = Enumerable.Repeat(.991, 30).ToArray();
    private readonly Random random = new();
    private readonly double goal;
    private readonly double mass;
    private readonly double torque;
    private readonly double launchPower;
    private readonly double launchDuration;
    private readonly double windMultiplier;
    private readonly double windMassFactor;
    private readonly double dragMultiplier;
    private double xBefore, yBefore, lastRotation, groundSeconds = double.NegativeInfinity;
    private double dayEndVelocity, dayEndTotal, groundHitCount, elapsedTicks, flightTicks;
    private double previousSpeed, stableSpeedCount;
    private double obstacleRotationDrag;
    private double windPhase, windFadeDuration, windFadeIn, windFadeOut, windForce, windDirection;
    private bool windBlowing;
    private int dragIndex;

    internal Space World { get; }
    internal Body Player { get; }
    public double X => Player.get_position().get_x();
    public double Y => Player.get_position().get_y();
    public double Rotation => Player.get_rotation();
    public double Altitude => GroundLevel - Y;
    public double Speed { get; private set; }
    public double SpeedAngle { get; private set; }
    public double TopSpeed { get; }
    public double CurrentDrag { get; private set; } = .991;
    public double Wind { get; private set; }
    public double DayEndTicks { get; private set; }
    public bool Launched { get; private set; }
    public double FlightTimeMs { get; private set; }
    public double MaxAltitude { get; private set; }
    public double MaxSpeed { get; private set; }
    public double MaxDistanceLeft { get; private set; }
    public double MaxDistanceRight { get; private set; }
    public double LaunchProgress => Math.Clamp(flightTicks / launchDuration, 0, 1);
    public double LaunchPower => launchPower;

    public CoilStoryPhysics(PortGameSlot slot)
    {
        var entries = OriginalCatalog.Load().ToDictionary(e => e.id);
        var body = entries[slot.BodyId];
        var launcher = entries[slot.LauncherId];
        double Bonus(string id, string stat, double fallback) =>
            entries.TryGetValue(id, out var entry) && entry.stats.ContainsKey(stat)
                ? entry.Value(stat, PortProfile.State.BonusLevels.GetValueOrDefault(id)) : fallback;

        goal = slot.StoryModeSpaceLevel;
        double Medal(string id) => entries[id].Value("statPower", PortProfile.State.Medals.GetValueOrDefault(id));
        mass = Math.Max(1, body.Value("statWeight", slot.BodyLevel));
        torque = body.Value("statTorque", slot.BodyLevel);
        TopSpeed = Math.Floor(body.Value("statTopSpeed", slot.BodyLevel) + .5);
        launchPower = launcher.Value("statPower", slot.LauncherLevel) * Bonus("bonusLaunchPower", "statPower", 1) * Medal("medalDays") *
            OriginalStatRules.WeightRatio(launcher.stats["statPowerAtWeightMult"].ToArray(), slot.LauncherLevel, mass);
        launchDuration = Curve(220, 3000, 100, 700000, launchPower);
        windMultiplier = Bonus("bonusWind", "statPower", 1);
        windMassFactor = Curve(1.25, .15, 3, 140, mass, .3);
        dragMultiplier = Bonus("bonusDrag", "statPower", 1) * Medal("medalSpeed");

        World = new Space(new Vec2(0, 250), null);
        double[][] half = { new[] {0d,-44}, new[] {14d,-40}, new[] {18d,-35}, new[] {29d,-18},
            new[] {32d,-1}, new[] {29d,25}, new[] {22d,31}, new[] {12d,35} };
        var points = half.Concat(half.Skip(1).Reverse().Select(v => new[] { -v[0], v[1] })).ToArray();
        Player = CreateBody(points);
        Player.set_mass(mass);
        Player.set_gravMass(0);
        Player.setShapeFilters(new InteractionFilter(25, 25, default, default, default, default));
        Player.set_space(World);
        for (int i = 0; i < floors.Length; i++)
        {
            var floor = new Body(BodyType.get_KINEMATIC(), new Vec2((i - 2) * 2000, GroundLevel + 1000));
            var shape = new Polygon(Polygon.rect(-1000, -1000, 2000, 2000, default), null, null);
            shape.get_material().set_dynamicFriction(.2);
            shape.get_material().set_staticFriction(5);
            shape.get_material().set_elasticity(.1);
            floor.get_shapes().push(shape);
            floor.setShapeFilters(new InteractionFilter(31, 31, default, default, default, default));
            floor.set_space(World);
            floors[i] = floor;
        }
        windPhase = Range(150, 400);
        MoveOnTracer(0, GroundLevel, 0);
    }

    internal static Body CreateBody(double[][] points)
    {
        var vertices = new NapeEngine::Array<object>();
        foreach (var point in points) vertices.push(new Vec2(point[0], point[1]));
        var parts = new GeomPoly(vertices).convexDecomposition(default, null);
        var result = new Body(null, null);
        for (int i = 0; i < parts.get_length(); i++)
            result.get_shapes().push(new Polygon(parts.at(i), null, null));
        result.align();
        return result;
    }

    public void MoveOnTracer(double x, double y, double angle)
    {
        const double anchorX = .1;
        const double anchorY = 36.2;
        double dx = Math.Cos(angle) * anchorX - Math.Sin(angle) * anchorY;
        double dy = Math.Sin(angle) * anchorX + Math.Cos(angle) * anchorY;
        Player.get_position().setxy(x - dx, y - dy);
        Player.set_rotation(angle);
        xBefore = X;
        yBefore = Y;
        lastRotation = angle;
    }

    public void Takeoff()
    {
        Player.get_velocity().setxy(0, 0);
        double angle = Rotation + Range(-3, 3) * Math.PI / 180 - Math.PI / 2;
        double impulse = launchPower * PowerPerMass * mass;
        Player.applyImpulse(new Vec2(Math.Cos(angle) * impulse, Math.Sin(angle) * impulse), null, default);
        Player.set_gravMass(mass);
        Launched = true;
    }

    public void Step(int steering, Action? afterSubstep = null)
    {
        elapsedTicks += Tick;
        for (int i = 0; i < 12; i++)
        {
            World.step(Tick / 720, default, default);
            var contacts = Player.get_arbiters();
            for (int j = 0; j < contacts.get_length(); j++)
            {
                var contact = contacts.at(j);
                if (floors.Contains(contact.get_body1()) || floors.Contains(contact.get_body2()))
                    groundSeconds = elapsedTicks / 60;
            }
            afterSubstep?.Invoke();
        }
        if (Launched)
        {
            flightTicks += Tick;
            ApplyDragAndWind();
            double dx = X - xBefore;
            double dy = Y - yBefore;
            Speed = Math.Sqrt(dx * dx + dy * dy) / Tick;
            SpeedAngle = Math.Atan2(dy, dx);
            Player.set_angularVel((Player.get_angularVel() + steering * torque * SteeringMultiplier * Tick) * (1 - .1 * Lerp(Tick, 1, .4)));
            RecordStats();
            TickDayEnd(dy);
        }
        if (obstacleRotationDrag > 0)
        {
            Player.set_angularVel(Player.get_angularVel() * Curve(.9, 1, 10, 0, obstacleRotationDrag));
            obstacleRotationDrag -= Tick;
        }
        double speedSum = Math.Abs(Player.get_velocity().get_x()) + Math.Abs(Player.get_velocity().get_y());
        Player.set_rotation(Lerp(Rotation, lastRotation, Curve(0, .25, 18000, 150000, speedSum, .5)));
        Player.set_angularVel(Player.get_angularVel() * Curve(1, .5, 18000, 150000, speedSum, .5));
        lastRotation = Rotation;
        if (Y >= GroundLevel + 100)
        {
            Player.get_position().setxy(X, GroundLevel - 100);
            Player.get_velocity().setxy(0, 0);
        }
        xBefore = X;
        yBefore = Y;
        RefreshGroundTiles();
        TickWind();
    }

    public void ObstacleImpact() => obstacleRotationDrag = 10;

    private void RefreshGroundTiles()
    {
        double center = Math.Floor((X + 1000) / 2000) * 2000;
        for (int i = 0; i < floors.Length; i++)
        {
            double old = floors[i].get_position().get_x();
            double offset = old - center;
            if (offset < -4000 || offset > 4000)
                floors[i].get_position().setxy(center + offset - Math.Round(offset / 10000) * 10000, GroundLevel + 1000);
        }
    }

    private void ApplyDragAndWind()
    {
        if (Wind != 0)
        {
            Player.set_angularVel(Player.get_angularVel() + Wind * Math.Cos(Rotation) * .02 * windMassFactor);
            Player.applyImpulse(new Vec2(Wind * mass * PowerPerMass * .5 * windMassFactor, 0), null, default);
        }
        double density = Altitude <= goal * .75 ? Curve(1, .9, 0, goal * .75, Altitude) :
            Altitude <= goal * .95 ? Curve(.9, .8, goal * .75, goal * .95, Altitude) :
            Curve(.8, .5, goal * .95, goal, Altitude);
        double excess = (Speed - TopSpeed) / TopSpeed;
        double loss = Speed <= TopSpeed ? Curve(.0045, .009, 0, TopSpeed, Speed, 1.5)
            : .009 + .035 * (excess >= 1 ? 1 + (excess - 1) * .1 : excess);
        drag[dragIndex++ % drag.Length] = 1 - loss * density;
        CurrentDrag = drag.Average();
        double speedAngleDeg = SpeedAngle * 180 / Math.PI;
        double fall = speedAngleDeg >= 0 && speedAngleDeg <= 180
            ? Curve(.1, 1, 0, 90, Math.Abs(speedAngleDeg - 90)) : 1;
        // AbstractLauncher.applyLauncherThrust and TimedMod.updateTimedMod.
        double launchDrag = .01 + .99 * Math.Pow(LaunchProgress, 1.6);
        double retention = 1 - (1 - CurrentDrag) * Lerp(Tick, .1, .04) * fall * dragMultiplier * launchDrag;
        var velocity = Player.get_velocity();
        velocity.setxy(velocity.get_x() * retention, velocity.get_y() * retention);
    }

    private void RecordStats()
    {
        MaxAltitude = Math.Max(MaxAltitude, Altitude);
        MaxDistanceLeft = Math.Min(MaxDistanceLeft, X);
        MaxDistanceRight = Math.Max(MaxDistanceRight, X);
        if (flightTicks >= 60)
        {
            if (Speed >= previousSpeed * .99999)
            {
                stableSpeedCount += Tick;
                double candidate = stableSpeedCount >= 3 ? Speed : Speed * .1 * Math.Min(1, Tick);
                double growth = Curve(0, 50, 0, 1500, stableSpeedCount, 1.4) * Tick;
                if (Tick <= .95) growth *= Curve(.95, .5, .95, .45, Tick);
                if (candidate > MaxSpeed) MaxSpeed = Math.Min(candidate, MaxSpeed + growth);
            }
            else
                stableSpeedCount = Math.Max(0, stableSpeedCount - Tick * Curve(.05, .001, 0, 10000, flightTicks / 60 * 1000, .5));
        }
        previousSpeed = Speed;
    }

    private void TickDayEnd(double dy)
    {
        bool grounded = elapsedTicks / 60 - groundSeconds <= .4;
        groundHitCount = grounded ? groundHitCount + Tick : Math.Max(0, groundHitCount - Tick * .015);
        bool descending = -dy < 1 || Speed <= 1.5;
        if (!grounded && !descending) FlightTimeMs += Tick * 1000 / FrameRate;
        if (descending || grounded)
        {
            double groundAdd = grounded ? Curve(0, .15, 100, 1000, groundHitCount, 1.1) : 0;
            dayEndVelocity = Math.Min(1, dayEndVelocity + (descending ? .01 * Tick : 0) + groundAdd);
        }
        else
        {
            double decay = Curve(1, .05, 0, 1500, dayEndTotal, .8);
            if (dayEndVelocity > 0) dayEndVelocity *= Curve(.5, .999, 1, 0, decay);
            dayEndVelocity = Math.Max(-1, dayEndVelocity - .02 * decay);
        }
        DayEndTicks = Math.Max(0, DayEndTicks + Tick * dayEndVelocity);
        dayEndTotal += Math.Max(0, Tick * dayEndVelocity);
    }

    private void TickWind()
    {
        if (!windBlowing)
        {
            if (windPhase > 0) { windPhase -= Tick; Wind = 0; return; }
            windBlowing = true;
            windDirection = random.NextDouble() <= .5 ? 1 : -1;
            windPhase = Range(AltitudeCurve(100, 100, 100, 0), AltitudeCurve(250, 500, 500, 1));
            windForce = Range(AltitudeCurve(1, 5, .5, 0), AltitudeCurve(3, 12, 3, 0));
            windFadeDuration = Range(15, 60);
            windFadeIn = windFadeOut = 0;
        }
        double strength;
        if (windFadeIn < windFadeDuration)
        {
            strength = Curve(0, windForce, 0, windFadeDuration, windFadeIn);
            windFadeIn += Tick;
        }
        else if (windPhase > 0) { strength = windForce; windPhase -= Tick; }
        else if (windFadeOut < windFadeDuration)
        {
            strength = Curve(0, windForce, windFadeDuration, 0, windFadeOut);
            windFadeOut += Tick;
        }
        else
        {
            windBlowing = false;
            windPhase = Range(AltitudeCurve(150, 150, 250, 20000), AltitudeCurve(400, 600, 800, 20000));
            strength = 0;
        }
        Wind = strength * windDirection * windMultiplier;
    }

    private double AltitudeCurve(double low, double mid, double high, double space) =>
        Altitude <= goal * .75 ? Curve(low, mid, 0, goal * .75, Altitude) :
        Altitude <= goal * .95 ? Curve(mid, high, goal * .75, goal * .95, Altitude, 2) :
        Curve(high, space, goal * .95, goal, Altitude);

    private double Range(double lo, double hi) => Lerp(lo, hi, random.NextDouble());
    internal static double Lerp(double a, double b, double t) => a + (b - a) * t;
    internal static double Curve(double a, double b, double lo, double hi, double x, double power = 1) =>
        Lerp(a, b, Math.Pow(Math.Clamp((x - lo) / (hi - lo), 0, 1), power));
}

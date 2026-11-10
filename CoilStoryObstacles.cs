extern alias NapeEngine;

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using NapeEngine::nape.dynamics;
using NapeEngine::nape.geom;
using NapeEngine::nape.phys;
using static CoilStoryPhysics;

// The five low-altitude entries of ObstacleData and their flight controllers.
internal sealed class CoilStoryObstacles
{
    private sealed record Species(string Art, Vector2 Offset, int Frames, double Scale, double Mass,
        double Life, double Power, double Drag, double Recovery, double[][] Points);

    private sealed class Active
    {
        public required Species Kind;
        public required Body Body;
        public required Sprite2D Visual;
        public double Life, Target, BaseAngle, TurnTime, TurnDuration, Straight, Correction = 1,
            CollisionDuration, Power, FlapTime, FlapWait, Animation;
        public bool Flipped;
    }

    private static readonly Species Paper = new("PaperPlane", new(-35.2f, -29.85f), 1, 1, .2, 15, 12, .95, .0005,
        Points(55,0,-16,23,-30,-30));
    private static readonly Species Kite = new("Kite", new(-109.55f, -35.1f), 1, 1, .6, 25, 5, .98, 0,
        Points(0,-29,31,-3,4,22,-46,4));
    private static readonly Species Gull = new("Seagull", new(-46.9f, -66.9f), 9, 1.8, 2, 40, 6, .95, .0025,
        Mirror(0,-12,11,-9,16,0,10,8,0,10));
    private static readonly Species Flappy = new("FlappyBird", new(-86,-54.5f), 1, .5, 5, 200, 15, .93, .0005,
        Mirror(34,-50,62,-22,70,49,0,57));
    private static readonly Species Rc = new("RCPlane", new(-262.9f, -163), 3, .3, 3, 40, 12, .95, .0005,
        Points(52,-154,105,-157,48,-44,136,-14,129,23,-3,40,-160,246,-238,227,-103,22,-235,5,-197,-30,-46,-40));
    private readonly Random random = new();
    private readonly List<Active> active = new();
    private readonly HashSet<Body> hit = new();
    private readonly CoilStoryPhysics physics;
    private readonly Node2D layer;
    private readonly double goal, amountMultiplier;
    private Vector2 lastSpawn;
    private double nextDistance = 1500;

    public int HitCount => hit.Count;

    public CoilStoryObstacles(CoilStoryPhysics physics, Node parent, double goal)
    {
        this.physics = physics;
        this.goal = goal;
        layer = new Node2D();
        parent.AddChild(layer);
        var bonus = OriginalCatalog.Load().Single(e => e.id == "bonusLessObstacles");
        amountMultiplier = bonus.Value("statPower", PortProfile.State.BonusLevels.GetValueOrDefault(bonus.id));
    }

    public void Advance(double zoom)
    {
        var current = new Vector2((float)physics.X, (float)physics.Y);
        if (amountMultiplier > 0 && lastSpawn.DistanceTo(current) >= nextDistance)
        {
            Spawn(current, zoom);
            lastSpawn = current;
            double altitude = physics.Altitude;
            double spacing = altitude <= 30000 ? 1500 : altitude <= 70000 ? Curve(1500,2500,30000,70000,altitude)
                : altitude <= 180000 ? Curve(2500,6000,70000,180000,altitude) : Curve(6000,10000,180000,goal,altitude,.8);
            nextDistance = spacing * Range(1, 1.5) / amountMultiplier / 1.3;
        }
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var item = active[i];
            var pos = item.Body.get_position();
            if (item.Life <= 0 || current.DistanceTo(new((float)pos.get_x(), (float)pos.get_y())) >= 4000 / zoom)
            {
                item.Body.set_space(null);
                item.Visual.QueueFree();
                active.RemoveAt(i);
                continue;
            }
            item.Visual.Position = new((float)pos.get_x(), (float)pos.get_y());
            item.Visual.Rotation = (float)item.Body.get_rotation();
            item.Animation += Tick;
            item.Visual.Texture = Texture(item.Kind, 1 + (int)item.Animation % item.Kind.Frames);
            if (item.Correction < 1) item.Correction += item.Kind.Recovery;
            item.Body.set_angularVel(item.Body.get_angularVel() * (1 - item.Correction * .2));
            double rotation = Lerp(item.Body.get_rotation(), item.Target * Math.PI / 180, item.Correction);
            item.Body.set_rotation(rotation);
            double angle = rotation - (item.Flipped ? Math.PI : 0);
            double impulse = item.Power * 4.15 * item.Body.get_mass();
            item.Body.applyImpulse(new Vec2(Math.Cos(angle) * impulse, Math.Sin(angle) * impulse), null, default);
            var velocity = item.Body.get_velocity();
            velocity.setxy(velocity.get_x() * item.Kind.Drag, velocity.get_y() * item.Kind.Drag);
            item.CollisionDuration = Math.Max(0, item.CollisionDuration - .1);
            item.Body.set_angularVel(item.Body.get_angularVel() * .98);
            if (item.Kind == Paper || item.Kind == Kite)
            {
                if (item.Straight >= 0) item.Straight -= Tick;
                else item.TurnTime -= Tick;
                item.Target = Curve(item.BaseAngle, (item.BaseAngle + 60) * (item.Flipped ? -1 : 1),
                    item.TurnDuration, 0, item.TurnTime, 1.6);
                if (item.Kind == Kite) item.Body.set_angularVel(item.Body.get_angularVel() * .9);
            }
            if (item.Kind == Flappy) TickFlappy(item);
            double launchMass = Curve(.6, .05, 100, 6000, physics.LaunchPower);
            item.Body.set_mass(item.Kind.Mass * .8 * Lerp(launchMass, 1, physics.LaunchProgress));
        }
    }

    private void TickFlappy(Active item)
    {
        if (item.FlapTime < 50) item.FlapTime = Math.Min(50, item.FlapTime + Tick);
        else if (item.FlapWait <= 0)
        {
            item.FlapTime = 0;
            item.Correction = .001;
            item.FlapWait = Range(10, 30);
        }
        else item.FlapWait -= Tick;
        item.Body.set_rotation(Curve(-70, 50, 0, 50, item.FlapTime) * (item.Flipped ? -1 : 1) * Math.PI / 180);
        item.Power = 25 - 11 * (1 - Math.Pow(1 - item.FlapTime / 50, 2));
    }

    public void ReadContacts()
    {
        var contacts = physics.Player.get_arbiters();
        for (int i = 0; i < contacts.get_length(); i++)
        {
            var arbiter = contacts.at(i);
            var other = arbiter.get_body1() == physics.Player ? arbiter.get_body2() : arbiter.get_body1();
            var item = active.Find(candidate => candidate.Body == other);
            if (item == null || item.Life <= 0) continue;
            if (hit.Add(other)) physics.ObstacleImpact();
            item.CollisionDuration++;
            item.Correction = .001;
            double playerMass = physics.Player.get_mass();
            double factor = Curve(1,2.5,other.get_mass(),other.get_mass() * 3,playerMass,.8);
            double baseDamage = Curve(5,200,1,300,playerMass,.8);
            double speed = physics.Speed;
            double speedDamage = speed <= 20 ? .6 * (1 - Math.Pow(1 - speed / 20, 2)) : Curve(.6,5,20,200,speed,.8);
            var impulse = arbiter.get_collisionArbiter().normalImpulse(null, default);
            double strength = (Math.Abs(impulse.get_x()) + Math.Abs(impulse.get_y())) / 2 / (playerMass + other.get_mass());
            double impact = strength == 0 ? speedDamage : strength <= 50 ? .25 + .75 * (1 - Math.Pow(1 - strength / 50,2))
                : Curve(1,2,50,1000,strength,.5);
            item.Life -= (baseDamage * (impact + speedDamage) + Curve(0,1000,0,60,item.CollisionDuration,4)) * factor;
        }
    }

    private void Spawn(Vector2 current, double zoom)
    {
        double ratio = Math.Max(0, physics.Altitude / goal);
        var choices = new (Species Kind, double Weight)[]
        {
            (Paper, Curve(1,0,0,.1,ratio)), (Kite, Curve(1,0,0,.15,ratio)),
            (Gull, ratio <= .1 ? Curve(.1,1,0,.1,ratio) : Curve(1,0,.1,.2,ratio)),
            (Flappy, ratio <= .1 ? Curve(0,.3,0,.1,ratio) : Curve(.3,0,.1,.21,ratio)),
            (Rc, ratio <= .1 ? Curve(0,1,0,.1,ratio) : Curve(1,0,.1,.21,ratio))
        };
        double weight = choices.Sum(option => option.Weight);
        if (weight <= 0) return;
        double pick = Range(0, weight);
        Species kind = choices.First(option => (pick -= option.Weight) <= 0).Kind;
        bool flipped = random.NextDouble() <= .5;
        double scaleX = kind.Scale * (flipped ? -1 : 1);
        var points = kind.Points.Select(point => new[] { point[0] * scaleX, point[1] * kind.Scale }).ToArray();
        var body = CreateBody(points);
        body.set_gravMass(0);
        body.set_mass(kind.Mass * .8 * Lerp(Curve(.6, .05, 100, 6000, physics.LaunchPower), 1, physics.LaunchProgress));
        body.setShapeMaterials(new NapeEngine::nape.phys.Material(1, .1, .5, 1, .0001));
        body.setShapeFilters(new InteractionFilter(1, 1, default, default, default, default));
        double angle = (current - lastSpawn).Angle() + Range(-120, 120) * Math.PI / 180;
        var position = current + Vector2.FromAngle((float)angle) * (float)(600 / zoom);
        body.get_position().setxy(position.X, position.Y);
        body.set_space(physics.World);
        var sprite = new Sprite2D { Texture = Texture(kind, 1), Centered = false, Offset = kind.Offset,
            Position = position, Scale = new((float)scaleX, (float)kind.Scale) };
        layer.AddChild(sprite);
        var item = new Active { Kind = kind, Body = body, Visual = sprite, Flipped = flipped, Power = kind.Power, Life = kind.Life };
        if (kind == Paper || kind == Kite)
        {
            item.BaseAngle = Range(-10, 10);
            item.TurnTime = item.TurnDuration = kind == Kite ? Range(200,400) : Range(100,250);
            item.Straight = Range(10,80);
        }
        else if (kind == Rc) item.Target = Range(-20,20);
        active.Add(item);
    }

    private static Texture2D Texture(Species kind, int frame) =>
        GD.Load<Texture2D>($"res://Assets/CoilStory/{kind.Art}/{frame}.png");
    private double Range(double low, double high) => Lerp(low, high, random.NextDouble());
    private static double[][] Points(params double[] values) => values.Chunk(2).ToArray();
    private static double[][] Mirror(params double[] values)
    {
        var half = Points(values);
        return half.Concat(half.Reverse().Where((p,i) => p[0] != 0 || (i != 0 && i != half.Length - 1))
            .Select(point => new[] { -point[0], point[1] })).ToArray();
    }
}

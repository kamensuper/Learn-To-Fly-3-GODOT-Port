extern alias NapeEngine;
using NapeEngine::nape.space;
using NapeEngine::nape.phys;
using NapeEngine::nape.geom;
using NapeEngine::nape.shape;
public sealed class OriginalNapeWorld {
 public readonly Space Space; public readonly Body Body;
 public OriginalNapeWorld(double mass,bool ground=true) {
  Space=new Space(new Vec2(0,250),null);Body=new Body(null,null);
  if(ground){
   double[,] points={{0,-56},{26,-38},{32,-20},{41,-20},{41,36},{-41,36},{-41,-20},{-32,-20},{-26,-38},{0,-56}};
   var verts=new NapeEngine::Array<object>();for(int i=0;i<points.GetLength(0);i++)verts.push(new Vec2(points[i,0],points[i,1]));
   var pieces=new GeomPoly(verts).convexDecomposition(default,null);
   for(int i=0;i<pieces.get_length();i++)Body.get_shapes().push(new Polygon(pieces.at(i),null,null));Body.align();
   var floor=new Body(BodyType.get_STATIC(),new Vec2(0,1060));floor.get_shapes().push(new Polygon(Polygon.box(200000,2000,default),null,null));floor.set_space(Space);
  }else Body.get_shapes().push(new Circle(10,null,null,null));
  Body.set_mass(mass);Body.set_space(Space);
 }
 public void Step(){for(int i=0;i<12;i++)Space.step(1.0/720,default,default);}
 public static double CheckOracle(string json,bool contact=false){
  using var doc=System.Text.Json.JsonDocument.Parse(json);var world=new OriginalNapeWorld(9,false);world.Body.get_position().setxy(0,-1000000);world.Body.get_velocity().setxy(1234,-700);if(contact){for(int warm=0;warm<600;warm++)world.Step();var floor=new Body(BodyType.get_STATIC(),new Vec2(0,1060));floor.get_shapes().push(new Polygon(Polygon.box(200000,2000,default),null,null));floor.set_space(world.Space);world.Body.get_position().setxy(0,-1000);world.Body.get_velocity().setxy(1234,350);world.Body.set_angularVel(2);} int tick=0;double max=0;
  foreach(var row in doc.RootElement.EnumerateArray()) {while(tick<row.GetProperty("tick").GetInt32()){world.Step();tick++;}
   max=System.Math.Max(max,System.Math.Abs(world.Body.get_position().get_x()-row.GetProperty("x").GetDouble()));max=System.Math.Max(max,System.Math.Abs(world.Body.get_position().get_y()-row.GetProperty("y").GetDouble()));
   max=System.Math.Max(max,System.Math.Abs(world.Body.get_velocity().get_x()-row.GetProperty("vx").GetDouble()));max=System.Math.Max(max,System.Math.Abs(world.Body.get_velocity().get_y()-row.GetProperty("vy").GetDouble()));
   if(contact){max=System.Math.Max(max,System.Math.Abs(world.Body.get_rotation()-row.GetProperty("angle").GetDouble()));max=System.Math.Max(max,System.Math.Abs(world.Body.get_angularVel()-row.GetProperty("spin").GetDouble()));}
  }if(max>1e-7)throw new System.Exception("Original Nape trajectory mismatch: "+max);return max;
 }
}
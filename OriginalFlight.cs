using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
public partial class OriginalFlight : Control {
 private OriginalFlightPhysics flight=null!; private Label hud=null!,details=null!; private AudioStreamPlayer jet=null!,launch=null!;
 private AudioStreamPlayer music=null!;
 private OriginalFlightMusicSet flightMusic=null!;
 private TextureRect sky=null!,ground=null!,body=null!,rocket=null!,coil=null!; private readonly List<Texture2D> flames=new(),coils=new(); private readonly List<string> skyPaths=new(); private int lastSky=-1;
 private bool started,finished,verify; private double accumulator,animation; private int frames; private double best; private Button mainButton=null!; private Font font=null!;
 private const string TestRecordPath="user://original-flight-test.json";
 private Texture2D Cropped(Texture2D texture){using var image=Godot.Image.LoadFromFile(ProjectSettings.GlobalizePath(texture.ResourcePath));var rect=image.GetUsedRect();return new AtlasTexture{Atlas=texture,Region=rect};}
 private TextureRect Art(string path,Vector2 pos,Vector2 size){var n=new TextureRect{ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore};n.Texture=GD.Load<Texture2D>(path);if(path.Contains("/Sky/")||path.Contains("body.png")||path.Contains("/Rocket/")||path.Contains("/Coil/"))n.Texture=Cropped(n.Texture);n.Position=pos;n.Size=size;AddChild(n);n.SetDeferred(Control.PropertyName.Size,size);return n;}
 private Label Text(string value,Vector2 pos,int size){var n=new Label{Text=value,Position=pos};n.AddThemeFontOverride("font",font);n.AddThemeFontSizeOverride("font_size",size);n.AddThemeColorOverride("font_color",Colors.White);n.AddThemeColorOverride("font_shadow_color",Colors.Black);n.AddThemeConstantOverride("shadow_offset_x",2);n.AddThemeConstantOverride("shadow_offset_y",2);AddChild(n);return n;}
 private AudioStreamPlayer Sound(string file,float db){var n=new AudioStreamPlayer{Stream=GD.Load<AudioStream>("res://Assets/Flight/"+file),VolumeDb=db};AddChild(n);return n;}
 public override void _Ready(){
  verify=OS.GetCmdlineUserArgs().Contains("--verify-flight")||OS.GetCmdlineUserArgs().Contains("--capture-flight");font=GD.Load<Font>("res://Assets/Start/font.ttf");
  for(int i=1;ResourceLoader.Exists($"res://Assets/Flight/Sky/{i}.png");i++)skyPaths.Add($"res://Assets/Flight/Sky/{i}.png");
  for(int i=1;ResourceLoader.Exists($"res://Assets/Flight/Rocket/{i}.png");i++)flames.Add(Cropped(GD.Load<Texture2D>($"res://Assets/Flight/Rocket/{i}.png")));
  for(int i=1;ResourceLoader.Exists($"res://Assets/Flight/Coil/{i}.png");i++)coils.Add(Cropped(GD.Load<Texture2D>($"res://Assets/Flight/Coil/{i}.png")));
  sky=Art("res://Assets/Flight/Sky/1.png",Vector2.Zero,new(960,540));sky.StretchMode=TextureRect.StretchModeEnum.Scale;
  ground=Art("res://Assets/Flight/ground.png",new(-200,430),new(1360,340));
  coil=Art("res://Assets/Flight/Coil/1.png",new(420,352),new(120,140));
  rocket=Art("res://Assets/Flight/Rocket/1.png",new(444,350),new(72,150));
  body=Art("res://Assets/Flight/body.png",new(442,330),new(76,76));
  hud=Text("",new(24,18),24);details=Text("",new(24,62),16);
  mainButton=new Button{Text="LAUNCH",Position=new(760,475),Size=new(180,46)}; mainButton.AddThemeFontOverride("font",font);mainButton.Pressed+=()=>{if(!started||finished)Begin();else Finish();};AddChild(mainButton);
  var back=new Button{Text="MAIN MENU",Position=new(24,475),Size=new(180,46)};back.AddThemeFontOverride("font",font);back.Pressed+=()=>GetTree().ChangeSceneToFile("res://StartScreen.tscn");AddChild(back);
  jet=Sound("83_sound.JetCrackling.mp3",-12);launch=Sound("31_sound.SpringBoing.mp3",-7);
  music=OriginalMusic.Create(this);flightMusic=OriginalMusic.CreateFlightSet(this);
  ResetFlight();
  if(verify){try{if(!OS.GetCmdlineUserArgs().Contains("--capture-flight"))Check();Begin();}catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}}
 }
 private void ResetFlight(){
  var slot=PortProfile.CurrentSlot;var entries=OriginalCatalog.Load().ToDictionary(x=>x.id,x=>x);
  OriginalEntry Get(string id)=>entries.TryGetValue(id,out var entry)?entry:throw new InvalidOperationException($"Saved equipment ID is not in the original registry: {id}");
  var body=Get(slot.BodyId);var launcher=Get(slot.LauncherId);
  int bodySlots=body.stats.ContainsKey("statStageSlots")?(int)Math.Floor(body.Value("statStageSlots",slot.BodyLevel)):0;
  var stages=new List<(OriginalEntry Entry,int Level)>();
  for(int i=0;i<Math.Min(Math.Clamp(bodySlots,0,slot.StageIds.Length),slot.StageIds.Length);i++)
   if(slot.StageIds[i]!="emptyStage")stages.Add((Get(slot.StageIds[i]),slot.StageLevels[i]));
  flight=new OriginalFlightPhysics(body,slot.BodyLevel,launcher,slot.LauncherLevel,stages);
  flight.SpaceAltitude=slot.GameMode=="Story Mode"?slot.StoryModeSpaceLevel:200000;
  hud.Text=$"{slot.GameMode} — DAY {slot.Day}";
  details.Text=$"{body.name} / {launcher.name} / {stages.Count} ACTIVE STAGES    SPACE: launch / abort    LEFT / RIGHT: steer    DOWN: cut engine";
 }
 private void Begin(){ResetFlight();started=true;finished=false;accumulator=0;animation=0;mainButton.Text="ABORT";if(!verify){flightMusic.Play();launch.Play();}}
 private void Finish(){if(finished||!started)return;flight.Abort();finished=true;jet.Stop();flightMusic.Stop();rocket.Texture=flames[0];best=Math.Max(best,flight.Peak);mainButton.Text="FLY AGAIN";details.Text=$"FLIGHT ENDED   ALTITUDE {flight.Peak:N0}   AIR TIME {flight.Time:N1}s\nDiagnostic flight scene: stock day rewards and progression are not applied here.";if(verify){using var f=Godot.FileAccess.Open(TestRecordPath,Godot.FileAccess.ModeFlags.Write);f?.StoreString(JsonSerializer.Serialize(new{bestAltitude=best,lastAltitude=flight.Peak,seconds=flight.Time}));}else OriginalMusic.Play(music,OriginalMusic.Results,false); }
 public override void _UnhandledKeyInput(InputEvent e){if(e is InputEventKey k&&k.Pressed&&!k.Echo&&k.Keycode==Key.Space){if(!started||finished)Begin();else Finish();}}
 public override void _Process(double delta){
  if(!started||finished)return;accumulator+=Math.Min(delta,.25);while(accumulator>=1.0/60){flight.Step(!verify&&Input.IsKeyPressed(Key.Down),verify?0:(Input.IsKeyPressed(Key.Right)?1:0)-(Input.IsKeyPressed(Key.Left)?1:0));accumulator-=1.0/60;animation++;}
  double speed=Math.Sqrt(flight.Vx*flight.Vx+flight.Vy*flight.Vy)/60;
  hud.Text=$"ALTITUDE {Math.Max(0,-flight.Y):N0}    SPEED {speed:N1}    FUEL {flight.Fuel:N0}    STAGE {Math.Min(flight.Stage+1,flight.StageCount)}/{flight.StageCount}";
  if(!verify)flightMusic.UpdateSpeed(speed);
  int skyFrame=Math.Clamp((int)(-flight.Y/flight.SpaceAltitude*(skyPaths.Count-1)),0,skyPaths.Count-1);if(skyFrame!=lastSky){sky.Texture=Cropped(GD.Load<Texture2D>(skyPaths[skyFrame]));lastSky=skyFrame;}
  float camera=(float)Math.Max(0,-flight.Y-100);float py=(float)(400+flight.Y+camera);body.Position=new(442,py-38);body.PivotOffset=body.Size/2;body.Rotation=(float)flight.Angle;rocket.PivotOffset = new Vector2(36, -2);
  rocket.Position = new Vector2(444, py + 2);
  rocket.Rotation = (float)flight.Angle;
  rocket.Visible = flight.Stage < flight.StageCount;
  rocket.Texture = flight.EngineOn ? flames[3 + (int)animation % 18] : flames[0];
  ground.Position=new(-200,430+camera);coil.Position=new(420,352+camera);if(animation<coils.Count)coil.Texture=coils[(int)animation];
  if(flight.EngineOn&&!verify&&!jet.Playing)jet.Play();if(!flight.EngineOn&&jet.Playing)jet.Stop();
  if(flight.Ended)Finish();
  if(verify&&animation>=120){GD.Print("VISUAL_BOUNDS body="+body.Size+" texture="+body.Texture.GetSize()+" root="+Size+" pos="+body.Position+" scale="+body.Scale);Finish();using var f=Godot.FileAccess.Open(TestRecordPath,Godot.FileAccess.ModeFlags.Read);if(!f.GetAsText().Contains("bestAltitude"))throw new Exception("Save record");CaptureFinishedFlight();}
 }
 private async void CaptureFinishedFlight()
 {
  if (DisplayServer.GetName() != "headless")
  {
   await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
   GetViewport().GetTexture().GetImage().SavePng("res://flight-abort-preview.png");
  }
  GD.Print("FLIGHT_RENDER_PASS: post-abort frame and save");
  GetTree().Quit();
 }
 private void Check(){
  using(var oracle=Godot.FileAccess.Open("res://Assets/Flight/nape-oracle.json",Godot.FileAccess.ModeFlags.Read))GD.Print("NAPE_AS3_ORACLE_PASS max error "+OriginalNapeWorld.CheckOracle(oracle.GetAsText()));
  using(var oracle=Godot.FileAccess.Open("res://Assets/Flight/nape-contact-oracle.json",Godot.FileAccess.ModeFlags.Read))GD.Print("NAPE_CONTACT_ORACLE_PASS max error "+OriginalNapeWorld.CheckOracle(oracle.GetAsText(),true));
  var entries=OriginalCatalog.Load();OriginalStatRules.Check();var p=new OriginalFlightPhysics(entries.Single(x=>x.id=="crate"),entries.Single(x=>x.id=="coil"),entries.Single(x=>x.id=="rocket"));
  if(p.Mass!=9||p.TopSpeed!=8||p.Fuel!=325)throw new Exception("Original gear stats");
  if(Math.Abs(p.LaunchPower-(170*(1.5-.5*Math.Pow(.9,1.4))))>1e-8)throw new Exception("Launch weight curve");
  p.Step(true);if(p.Fuel!=325||p.Peak<=0)throw new Exception("Cut engine/gravity");
  p.Step();if(p.Fuel!=324)throw new Exception("Fuel tick");
  for(int i=0;i<20000&&!p.Ended;i++)p.Step();if(!p.Ended||p.Stage!=2||!double.IsFinite(p.Peak))throw new Exception("Staging/landing");
  double t=p.Time;p.Abort();p.Step();if(p.Time!=t)throw new Exception("Abort guard");
  GD.Print("FLIGHT_KERNEL_PASS: original registry mass, speed cap, fuel, weight curve, gravity, cutoff, staging, landing, abort");
 }
}

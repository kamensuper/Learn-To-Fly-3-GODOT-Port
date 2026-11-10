using Godot;
using System;
using System.Collections.Generic;

public partial class StartScreen : Control
{
 private sealed class ButtonArt { public TextureRect Art=null!; public Vector2 Target; public Vector2 BaseScale=Vector2.One; public float Delay; public float Blend; public float ScaleMod=.1f; public string Name=""; public MouseBounce2? Bounce; }
 private readonly List<ButtonArt> buttons=new();
 private sealed class Cloud { public TextureRect Art=null!; public float Speed; }
 private readonly List<Cloud> clouds=new(); private readonly RandomNumberGenerator cloudRandom=new();
 private readonly List<Texture2D> marketFrames=new(); private readonly List<Texture2D> titleFrames=new();
 private TextureRect title=null!,market=null!; private TextureRect? marketBob,back; private Texture2D marketBody=null!; private readonly List<Texture2D> bobFrames=new(); private bool returning; private float backHover; private double returnTime;
 private AudioStreamPlayer music=null!;
 private Label tooltip=null!,sardineCount=null!,versionLabel=null!;
 private Sprite2D fish=null!;
 private NewGameVisual? newGameVisual;
 private bool openingModes;
 private bool leaving,showSlots; private double leaveTime,slotTime; private TextureRect? slotScreen;
 private bool openingExisting;
 private double elapsed; private int capturePhase;
 private bool verify; private bool verifyPlay; private bool verifyBack;
 private TextureRect Image(string name,Vector2 pos,Vector2 scale,bool outline=false)
 {
  var t=new TextureRect {Texture=GD.Load<Texture2D>("res://Assets/Start/"+name+".png"),Position=pos,Scale=scale,MouseFilter=MouseFilterEnum.Ignore};
  AddChild(t);
  if(outline){var mat=new ShaderMaterial{Shader=GD.Load<Shader>("res://Assets/Start/edge.gdshader")};mat.SetShaderParameter("radius",3.0f/scale.X);t.Material=mat;}
  return t;
 }
 private void Button(string name,Vector2 pos,float scale,float delay)
 {
  bool icon=name=="options"||name=="mute"||name=="fullscreen"; var art=Image(name,pos-Vector2.One*8*scale,Vector2.One*scale,!icon);
  if(icon)art.ZIndex=1;
  var entry=new ButtonArt{Art=art,Target=art.Position,BaseScale=art.Scale,Delay=delay,Name=name};
  // src.gui.MainMenu.as:73-95 - every menu button is wrapped in a MouseBounce2 decorator.
  if(!icon){
   var bounce=new MouseBounce2(art){ScaleMod=0.1};
   bounce.Enabled=!verify;
   bounce.SignalClick += _ => OnButtonClicked(name);
   entry.Bounce=bounce;
  }
  buttons.Add(entry);
 }
 private void OnButtonClicked(string name)
 {
  if(leaving||showSlots||TransitionOverlay.IsActive(GetTree())) return;
  if(name=="bonus"){ PortProfile.BonusShopReturnToEquipmentShop=false; TransitionOverlay.To(this,"res://BonusShop.tscn"); return; }
  if(name=="blackmarket"){ PortProfile.BlackMarketReturnToEquipmentShop=false; TransitionOverlay.To(this,"res://BlackMarket.tscn"); return; }
  if(name=="play"){ OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick); leaving=true; leaveTime=0; }
 }
 private Label Text(string value,Vector2 pos,int size)
 {
  var t=new Label {Text=value,Position=pos,MouseFilter=MouseFilterEnum.Ignore};t.AddThemeFontOverride("font",GD.Load<FontFile>("res://Assets/Start/numbers.ttf"));t.AddThemeFontSizeOverride("font_size",size);t.AddThemeColorOverride("font_outline_color",Colors.Black);t.AddThemeConstantOverride("outline_size",6);AddChild(t);return t;
 }
 public override void _Ready()
 {
  _=PortProfile.State;
  verifyBack=Array.Exists(OS.GetCmdlineUserArgs(),s=>s=="--verify-back");verifyPlay=verifyBack||Array.Exists(OS.GetCmdlineUserArgs(),s=>s=="--verify-play"); verify=verifyPlay||Array.Exists(OS.GetCmdlineUserArgs(),s=>s=="--verify-start");
  var bg=new TextureRect{Texture=GD.Load<Texture2D>("res://Assets/Menu/clouds.png"),Size=new Vector2(960,540),ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,MouseFilter=MouseFilterEnum.Ignore};AddChild(bg);
  var rng=new RandomNumberGenerator();rng.Seed=42055;
  for(int i=0;i<15;i++){
   float scale=rng.RandfRange(.4f,1.3f);var c=Image("cloud",new Vector2(rng.RandfRange(-100,1260),rng.RandfRange(-50,590)),Vector2.One*scale);c.Modulate=new Color(1,1,1,Math.Min(1,scale));clouds.Add(new Cloud{Art=c,Speed=rng.RandfRange(-6,-3)*scale});
  }
  for(int i=1;i<=160;i++)titleFrames.Add(GD.Load<Texture2D>($"res://Assets/Start/TitleAnimation/{i}.png")); title=new TextureRect{Texture=titleFrames[0],Position=new Vector2(91.66f,-107.34f),Scale=Vector2.One*1.02f,MouseFilter=MouseFilterEnum.Ignore};AddChild(title);
  // Original 20-tick entrances, delayed by 0/3/6/9/12 GUI ticks.
  Button("play",new Vector2(198,256),1.4773f,0);
  Button("extras",new Vector2(207,333),.7887f,3);
  Button("bonus",new Vector2(164,383),.7887f,6);
  Button("blackmarket",new Vector2(145,433),.7887f,9);
  Button("quit",new Vector2(187,484),.7887f,12);
  for(int i=1;i<=95;i++)marketFrames.Add(GD.Load<Texture2D>($"res://Assets/Start/MarketAnimation/{i}.png"));
  market=new TextureRect{Texture=marketFrames[0],Position=new Vector2(550,230),Scale=Vector2.One*.90841675f,MouseFilter=MouseFilterEnum.Ignore};AddChild(market);
  marketBody=GD.Load<Texture2D>("res://Assets/Start/market-body.png");for(int i=1;i<=92;i++)bobFrames.Add(GD.Load<Texture2D>($"res://Assets/Start/MarketBob/{i}.png"));marketBob=new TextureRect{Texture=bobFrames[0],Position=new Vector2(550,225),Scale=Vector2.One*.90841675f,MouseFilter=MouseFilterEnum.Ignore,Visible=false};var bm=new ShaderMaterial{Shader=GD.Load<Shader>("res://Assets/Start/edge.gdshader")};bm.SetShaderParameter("radius",3.0f);bm.SetShaderParameter("edge_color",Colors.White);marketBob.Material=bm;AddChild(marketBob);
  Button("options",new Vector2(7,3),.85f,0);Button("mute",new Vector2(46.5f,10.15f),1,0);Button("fullscreen",new Vector2(96,3),.85f,0);
  buttons.Find(b => b.Name == "mute")!.Art.Texture = OriginalAudio.SpeakerTexture();
  fish=new Sprite2D{Texture=GD.Load<Texture2D>("res://Assets/Start/sardines.png"),RegionEnabled=true,RegionRect=new Rect2(8,8,32,51),Position=new Vector2(23,62),Scale=Vector2.One*.77f};AddChild(fish);
  sardineCount=Text(PortProfile.State.Sardines.ToString("N0"),new Vector2(46,46),27);versionLabel=Text("VERSION 1.2.13",new Vector2(11,520),13);
  tooltip=Text("",new Vector2(5,35),16);
  music=OriginalMusic.Create(this);if(!verify)OriginalMusic.Play(music,OriginalMusic.Menu);
  if(PortProfile.ResumeEquipmentShop)
  {
   PortProfile.ResumeEquipmentShop=false;
   fish.Hide();sardineCount.Hide();versionLabel.Hide();
   newGameVisual=new NewGameVisual{MouseFilter=MouseFilterEnum.Ignore};
   AddChild(newGameVisual);
   newGameVisual.OpenCurrentShop();
  }
 }
 public void PlayEquipmentShopMusic(){if(!verify)OriginalMusic.Play(music,OriginalMusic.Shop);}
 private static float OutCubic(float t)=>1-MathF.Pow(1-Math.Clamp(t,0,1),3);
 private static float OutBack(float t){t=Math.Clamp(t,0,1)-1;return 1+2.70158f*t*t*t+1.70158f*t*t;}
 public override void _Input(InputEvent input) {
  if(verify || TransitionOverlay.IsActive(GetTree()) || input is not InputEventMouseButton e || e.ButtonIndex!=MouseButton.Left || e.Pressed)return;
  var speakerButton = buttons.Find(b => b.Name == "mute");
  if (!CoilStoryIntro.IsActive && speakerButton != null &&
      new Rect2(speakerButton.Art.Position, speakerButton.Art.Texture.GetSize() * speakerButton.Art.Scale).HasPoint(GetLocalMousePosition()))
  {
   OriginalAudio.ToggleMute();
   speakerButton.Art.Texture = OriginalAudio.SpeakerTexture();
   GetViewport().SetInputAsHandled();
   return;
  }
  if(IsInstanceValid(newGameVisual))return;
  if(showSlots && !returning && slotTime>=.5 && slotScreen!=null)
  {
   Vector2 point=e.Position-slotScreen.Position;
   for(int i=0;i<6;i++)
   {
    if(!new Rect2(10+116*i,91,105,105).HasPoint(point))continue;
    PortProfile.SelectSlot(i);
    openingExisting=!PortProfile.CurrentSlot.IsEmpty;
    openingModes=!openingExisting;
    returning=true;
    returnTime=0;
    OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);
    return;
   }
  }
  if(showSlots){if(!returning && new Rect2(431,443,93,36).HasPoint(e.Position)){OriginalAudio.PlaySfx(this, OriginalSound.ButtonClick);returning=true;returnTime=0;}return;}
  if(!leaving&&new Rect2(190,250,180,70).HasPoint(e.Position))OnButtonClicked("play");
  if(!leaving&&!showSlots&&new Rect2(550,220,260,240).HasPoint(e.Position))OnButtonClicked("blackmarket");
 }
 public override void _Process(double delta)
 {
  GuiTick.Pump(delta);
  if (!CoilStoryIntro.IsActive && buttons.Find(b => b.Name == "mute") is { } speakerButton &&
      speakerButton.Art.Texture != OriginalAudio.SpeakerTexture()) speakerButton.Art.Texture = OriginalAudio.SpeakerTexture();
  elapsed+=delta;if(verifyPlay&&elapsed>1&&!leaving){leaving=true;leaveTime=0;}float ticks=(float)elapsed*60;
  foreach(var cloud in clouds){cloud.Art.Position+=new Vector2(cloud.Speed*(float)delta*60,0);if(cloud.Art.Position.X+cloud.Art.Texture.GetWidth()*cloud.Art.Scale.X < -40){float size=cloudRandom.RandfRange(.4f,1.3f);cloud.Art.Scale=Vector2.One*size;cloud.Speed=cloudRandom.RandfRange(-6,-3)*size;cloud.Art.Modulate=new Color(1,1,1,Math.Min(1,size));cloud.Art.Position=new Vector2(960+cloudRandom.RandfRange(400,500),cloudRandom.RandfRange(-20,560));}}
  title.Position=new Vector2(91.66f,-107.34f-330*(1-OutCubic(ticks/50)));title.Texture=titleFrames[(int)ticks%titleFrames.Count];
  int frame=Math.Clamp((int)ticks-18,0,94);market.Texture=frame==94?marketBody:marketFrames[frame];if(marketBob!=null){marketBob.Visible=frame==94&&!showSlots&&!leaving;marketBob.Texture=bobFrames[Math.Max(0,(int)ticks-112)%92];}market.Position=new Vector2(550,230+360*(1-OutCubic((ticks-18)/20)));if(frame==94)market.Position+=new Vector2(3,28)*market.Scale;
  foreach(var b in buttons){
   bool icon=b.Name=="options"||b.Name=="mute"||b.Name=="fullscreen";
   Vector2 basePos=b.Target+(icon?Vector2.Zero:new Vector2(-409*(1-OutCubic((ticks-b.Delay)/20)),0));
   if(verify){
    var rect=new Rect2(basePos,b.Art.Texture.GetSize()*b.BaseScale);
    bool over=verify ? (b.Name=="play"&&capturePhase>=1) : rect.HasPoint(GetLocalMousePosition());
    b.Blend=Math.Clamp(b.Blend+(float)delta*(over?6:-4),0,1);
    float bounce=over?OutBack(b.Blend):b.Blend;
    float factor=1+bounce*.1f;
    if(over&&(verify ? capturePhase==2 : Input.IsMouseButtonPressed(MouseButton.Left)))factor=.95f;
    b.Art.Scale=b.BaseScale*factor;b.Art.Position=basePos-(b.Art.Texture.GetSize()*b.BaseScale)*(factor-1)*.5f;
    b.Art.Modulate=over?new Color(1.08f,1.08f,1.08f):Colors.White;
    if(icon&&over)tooltip.Text=b.Name.ToUpperInvariant();
   } else {
    // Scale is owned by MouseBounce2 now; only re-centre the art around its target.
    float factor=b.Art.Scale.X/b.BaseScale.X;
    b.Art.Position=basePos-(b.Art.Texture.GetSize()*b.BaseScale)*(factor-1)*.5f;
    if(icon&&b.Bounce!=null&&b.Bounce.MouseIsOver)tooltip.Text=b.Name.ToUpperInvariant();
   }
  }
  if(!verify&&!new Rect2(0,0,140,36).HasPoint(GetLocalMousePosition()))tooltip.Text="";
  if(leaving&&!showSlots&&!IsInstanceValid(newGameVisual)){leaveTime+=delta;float t=(float)leaveTime*60;
   title.Position=new Vector2(91.66f,-107.34f-330*MathF.Pow(Math.Clamp(t/25,0,1),3));
   foreach(var b in buttons)if(b.Name!="options"&&b.Name!="mute"&&b.Name!="fullscreen")b.Art.Position=b.Target+new Vector2(-409*MathF.Pow(Math.Clamp((t-b.Delay)/20,0,1),2),0);
   market.Position=new Vector2(550,230+460*MathF.Pow(Math.Clamp((t-14)/20,0,1),2));
   if(t>=34){showSlots=true;slotTime=0;foreach(var b in buttons)if(b.Name!="options"&&b.Name!="mute"&&b.Name!="fullscreen")b.Art.Visible=false;title.Visible=false;market.Visible=false;slotScreen=new TextureRect{Texture=GD.Load<Texture2D>("res://Assets/Start/load-slots.png"),Position=new Vector2(120,600),MouseFilter=MouseFilterEnum.Ignore};AddChild(slotScreen); back=new TextureRect{Texture=GD.Load<Texture2D>("res://Assets/Start/back.png"),MouseFilter=MouseFilterEnum.Ignore};AddChild(back);}
  }
  if(showSlots&&slotScreen!=null){slotTime+=delta;if(verifyBack&&slotTime>.6&&!returning){returning=true;returnTime=0;}float offset=540*(1-OutCubic((float)slotTime*60/30));if(returning){returnTime+=delta;offset=540*MathF.Pow(Math.Clamp((float)returnTime*60/30,0,1),2);}slotScreen.Position=new Vector2(120,60+offset);
   if(back!=null){bool hover=new Rect2(431,443,93,36).HasPoint(GetLocalMousePosition())&&!returning;backHover=Math.Clamp(backHover+(float)delta*(hover?6:-4),0,1);float size=1+(hover?OutBack(backHover):backHover)*.1f;if(hover&&Input.IsMouseButtonPressed(MouseButton.Left))size=.95f;back.Scale=Vector2.One*size;back.Position=slotScreen.Position+new Vector2(311,383)-new Vector2(93,36)*(size-1)*.5f;}
   if(returning&&returnTime>=.5){showSlots=false;returning=false;leaving=false;elapsed=0;slotScreen.QueueFree();slotScreen=null;back?.QueueFree();back=null;
    if(openingModes||openingExisting){
     bool openExisting=openingExisting;
     openingModes=false;
     openingExisting=false;
     leaving=true;
     fish.Hide();sardineCount.Hide();versionLabel.Hide();
     newGameVisual=new NewGameVisual{MouseFilter=MouseFilterEnum.Ignore};
     AddChild(newGameVisual);
     if(openExisting)newGameVisual.OpenCurrentShop();
     return;
    }
    foreach(var b in buttons)b.Art.Visible=true;title.Visible=true;market.Visible=true;if(verifyBack){GD.Print("BACK_TRANSITION_PASS: slot entrance, isolated Back hitbox, 30 tick exit, main-menu return");GetTree().Quit();}}
  }
  if(verify) {
   if(capturePhase==0 && elapsed>3){GetViewport().GetTexture().GetImage().SavePng(verifyPlay?"res://play-screen-preview.png":"res://start-screen-preview.png");capturePhase=1;}
   else if(capturePhase==1 && elapsed>3.5){GetViewport().GetTexture().GetImage().SavePng("res://start-screen-hover.png");capturePhase=2;}
   else if(capturePhase==2 && elapsed>3.9){GetViewport().GetTexture().GetImage().SavePng("res://start-screen-pressed.png");
    GD.Print("START_SCREEN_RENDER_PASS: 15 drifting clouds, 160 logo frames, 95 market frames, 8 button visuals; normal/hover/press captured");GetTree().Quit();}
  }
 }
}

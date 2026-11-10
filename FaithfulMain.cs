using Godot;
using System;
using System.Linq;
public partial class FaithfulMain : Control
{
 private Control page=null!;
 private FontFile font=null!;
 public override void _Ready() {font=GD.Load<FontFile>("res://Assets/original-font.ttf"); Menu(); if(Array.Exists(OS.GetCmdlineUserArgs(),x=>x=="--verify")){ Catalog(false); Catalog(true); Menu(); foreach(Node child in page.GetChildren()) if(child is BaseButton b) b.Disabled=true; OriginalStatRules.Check(); GD.Print("FAITHFUL_UI_SMOKE_PASS"); GetTree().CreateTimer(2).Timeout+=()=>{GetViewport().GetTexture().GetImage().SavePng("res://faithful-preview.png");GetTree().Quit();}; }}
 private void Clear() {if(page!=null){RemoveChild(page); page.QueueFree();} page=new Control{Size=new Vector2(1100,700)};AddChild(page);}
 private void Art(string path,Rect2 rect) {var image=new TextureRect{Texture=GD.Load<Texture2D>(path),Position=rect.Position,Size=rect.Size,ExpandMode=TextureRect.ExpandModeEnum.IgnoreSize,StretchMode=path.Contains("clouds")?TextureRect.StretchModeEnum.Scale:TextureRect.StretchModeEnum.KeepAspectCentered,MouseFilter=MouseFilterEnum.Ignore};page.AddChild(image);}
 private void NativeButton(int id,Vector2 at,Action action) {string root=$"res://Assets/Menu/buttons/DefineButton2_{id}/"; var b=new TextureButton{TextureNormal=GD.Load<Texture2D>(root+"1_up.png"),TextureHover=GD.Load<Texture2D>(root+"2_over.png"),TexturePressed=GD.Load<Texture2D>(root+"3_down.png"),Position=at,Scale=new Vector2(.8f,.8f)}; b.Pressed+=action;page.AddChild(b);}
 private void Text(string text,Vector2 at,int size=22) {var l=new Label{Text=text,Position=at};l.AddThemeFontOverride("font",font);l.AddThemeFontSizeOverride("font_size",size);page.AddChild(l);}
 private void Menu() {
  Clear();Art("res://Assets/Menu/clouds.png",new Rect2(0,0,1100,700));Art("res://Assets/Menu/title.png",new Rect2(200,-30,700,410));
  NativeButton(3409,new Vector2(130,370),()=>Catalog(false));NativeButton(3415,new Vector2(130,480),()=>Catalog(true));NativeButton(3417,new Vector2(130,590),()=>{});
  Text("C# PORT · ORIGINAL ASSET INTEGRATION",new Vector2(590,640),17);
 }
 private void Catalog(bool bonus) {
  Clear();Art("res://Assets/Menu/clouds.png",new Rect2(0,0,1100,700));
  Text(bonus?"BONUS SHOP — ORIGINAL DEFINITIONS":"EQUIPMENT — ORIGINAL DEFINITIONS",new Vector2(30,20),28);
  var all=OriginalCatalog.Load();var entries=all.Where(e=>bonus?e.type.Contains("BONUS"):new[]{"TYPE_BODY","TYPE_LAUNCHER","TYPE_STAGE","TYPE_BOOST"}.Contains(e.type)).ToList();
  var list=new ItemList{Position=new Vector2(30,80),Size=new Vector2(400,500)};page.AddChild(list);
  var detail=new Label{Position=new Vector2(460,90),Size=new Vector2(600,450)};detail.AddThemeFontOverride("font",font);detail.AddThemeFontSizeOverride("font_size",23);page.AddChild(detail);
  foreach(var e in entries)list.AddItem(e.name);
  list.ItemSelected+=i=>{var e=entries[(int)i];string result=e.name+"\n"+e.id+"\nBASE MAX LEVEL: "+e.maxLevel;
    foreach(var key in new[]{"statCost","statWeight","statPower","statFuel","statTopSpeed","statStageSlots","statBoostSlots"})if(e.stats.ContainsKey(key)){try{double value=key=="statCost"?OriginalStatRules.Cost(e,0):e.Value(key,0); result+="\n"+key.Replace("stat","")+": "+value.ToString("G8");}catch(Exception){result+="\n"+key+": SPECIAL CURVE";}}
    detail.Text=result;};
  var back=new Button{Text="BACK",Position=new Vector2(30,610),Size=new Vector2(180,50)};back.Pressed+=Menu;page.AddChild(back);
  Text("CATALOG ONLY — EQUIPMENT AND UNLOCK LOGIC NOT YET WIRED",new Vector2(250,625),16);
 }
}




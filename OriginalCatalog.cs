using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;

public sealed class OriginalDescription
{
 public string level {get;set;}=""; public string text {get;set;}="";
}

public sealed class OriginalEntry
{
 public string id {get;set;}=""; public string name {get;set;}=""; public string type {get;set;}=""; public string maxLevel {get;set;}="";
 public int MaxLevelInt => int.TryParse(maxLevel, out int m) ? m : 0;
 public Dictionary<string,List<double[]>> stats {get;set;}=new();
 public List<OriginalDescription> descriptions {get;set;}=new();
 public string source {get;set;}="";
 // Exact ordinary Stat.valueAt / NumberUtils.interpolate2 semantics.
 // Four-argument weight curves use another AS3 class and are explicitly rejected.
 public double Value(string key,double level)
 {
  if(!stats.TryGetValue(key,out var rows) || rows.Count==0) throw new InvalidOperationException($"Missing original stat {id}.{key}");
  if(rows.Any(r=>r.Length>3))throw new NotSupportedException($"{id}.{key}: weight-dependent stat requires StatWeight port");
  if(level<rows[0][0]) throw new ArgumentOutOfRangeException(nameof(level));
  if(level>rows[^1][0]) return rows[^1][1];
  for(int i=0;i<rows.Count;i++) {
   var a=rows[i]; if(level==a[0])return a[1];
   if(i+1<rows.Count && a[0]<=level && rows[i+1][0]>level) {
    var b=rows[i+1]; double t=Math.Clamp((level-a[0])/(b[0]-a[0]),0,1); return a[1]+(b[1]-a[1])*Math.Pow(t,a.Length>2?a[2]:1);
   }
  } return 0;
 }
}
public static class OriginalCatalog
{
 public static List<OriginalEntry> Load() {
  using var f=FileAccess.Open("res://Assets/registry.json",FileAccess.ModeFlags.Read);
  return JsonSerializer.Deserialize<List<OriginalEntry>>(f.GetAsText()) ?? throw new Exception("Registry failed");
 }
 public static int Check(List<OriginalEntry> entries) {
  int checkedRows=0;
  foreach(var e in entries) foreach(var pair in e.stats) {
   if(pair.Value.Any(r=>r.Length>3))continue;
   foreach(var row in pair.Value) {
    var first=pair.Value.First(x=>x[0]==row[0]);
    if(e.Value(pair.Key,row[0])!=first[1])throw new Exception($"Anchor mismatch {e.id}.{pair.Key}"); checkedRows++;
   }
  } return checkedRows;
 }
}

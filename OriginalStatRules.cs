using System;
using System.Linq;
public static class OriginalStatRules
{
 private static double Round(double x)=>Math.Floor(x+0.5);
 public static double Nice(double x) {
  if(x<=10)return Round(x*100)/100;
  if(x<100)return Round(x);
  if(x<1000)return Round(Round(x)/10)*10;
  x=Round(x); double step=Math.Pow(10,x.ToString("F0",System.Globalization.CultureInfo.InvariantCulture).Length)/1000;
  return Round(x/step)*step;
 }
 private static double Curve(double[][] rows,double level,int column) {
  if(level<rows[0][0])throw new ArgumentOutOfRangeException(nameof(level));
  if(level>rows[^1][0])return rows[^1][column];
  for(int i=0;i<rows.Length;i++) {
   var a=rows[i]; if(level==a[0])return a[column];
   if(i+1<rows.Length && a[0]<=level && rows[i+1][0]>level) {var b=rows[i+1]; return a[column]+(b[column]-a[column])*Math.Pow((level-a[0])/(b[0]-a[0]),a.Length>4?a[4]:1);}
  } return 0;
 }
 public static double WeightRatio(double[][] rows,double level,double mass) {
  double zero=Curve(rows,level,1), cutoff=Curve(rows,level,2), half=Curve(rows,level,3);
  if(mass<=0)return zero;
  if(mass<cutoff)return zero+(1-zero)*Math.Pow(mass/cutoff,1.4);
  if(mass==cutoff)return 1;
  if(mass<cutoff+half)return 1-0.5*Math.Pow((mass-cutoff)/half,1.6);
  return Math.Pow(0.5,(mass-cutoff)/half);
 }
 public static double Cost(OriginalEntry entry,int level)=>Nice(entry.Value("statCost",level));
 public static void Check() {
  if(Nice(1234)!=1230 || Nice(154)!=150 || Nice(1.234)!=1.23)throw new Exception("StatNice");
  double[][] rows={new double[]{0,1.35,120,120},new double[]{10,1.35,350,350}};
  if(WeightRatio(rows,0,120)!=1 || WeightRatio(rows,0,240)!=0.5 || WeightRatio(rows,0,0)!=1.35)throw new Exception("StatPowerAtWeight");
 }
}

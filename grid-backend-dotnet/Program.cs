using System;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
class GridBackend{
 static readonly object gate=new object();
 static string gridPath="";
 [STAThread] static void Main(){
  gridPath=Path.Combine(AppContext.BaseDirectory,"facility-navdata.json");
  var form=new Form{Text="SCP-049 Grid + A* Pathfinding",Width=460,Height=180};
  form.Controls.Add(new Label{Dock=DockStyle.Fill,Text="EXE OWNS GRID + A* PATHS\n127.0.0.1:8766\n/grid · /chunk · /path",TextAlign=System.Drawing.ContentAlignment.MiddleCenter});
  var listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:8766/");listener.Start();
  Task.Run(async()=>{while(listener.IsListening){HttpListenerContext c;try{c=await listener.GetContextAsync();}catch{break;}c.Response.Headers.Add("Access-Control-Allow-Origin","*");c.Response.Headers.Add("Access-Control-Allow-Methods","GET,POST,OPTIONS");c.Response.Headers.Add("Access-Control-Allow-Headers","Content-Type");if(c.Request.HttpMethod=="OPTIONS"){c.Response.StatusCode=204;c.Response.Close();continue;}try{
   if(c.Request.Url!.AbsolutePath=="/health"){await Send(c,"ready");continue;}
   if(c.Request.Url.AbsolutePath=="/grid"||c.Request.Url.AbsolutePath=="/map.json"){lock(gate){c.Response.ContentType="application/json";Send(c,File.ReadAllText(gridPath)).GetAwaiter().GetResult();}continue;}
   if(c.Request.Url.AbsolutePath=="/chunk"&&c.Request.HttpMethod=="POST"){using var sr=new StreamReader(c.Request.InputStream);var chunk=JsonNode.Parse(await sr.ReadToEndAsync())!;lock(gate){UpdateGrid(chunk);}await Send(c,"{\"ok\":true}");continue;}
   if(c.Request.Url.AbsolutePath=="/path"&&c.Request.HttpMethod=="POST"){using var sr=new StreamReader(c.Request.InputStream);var req=JsonNode.Parse(await sr.ReadToEndAsync())!;JsonNode result;lock(gate){result=PathRequest(req);}c.Response.ContentType="application/json";await Send(c,result.ToJsonString());continue;}
   c.Response.StatusCode=404;await Send(c,"not found");
  }catch(Exception ex){c.Response.StatusCode=500;await Send(c,ex.Message);}}});
  Application.Run(form);
 }
 static void UpdateGrid(JsonNode chunk){var root=JsonNode.Parse(File.ReadAllText(gridPath))!;var grid=root["grid"]!.AsArray();int w=(int)root["width"]!,h=(int)root["height"]!;double ox=(double)root["origin"]!["x"]!,oz=(double)root["origin"]!["z"]!,cell=(double)root["cellSize"]!;var walls=chunk["walls"]!.AsArray();int minx=0,minz=0,maxx=w-1,maxz=h-1;if(chunk["chunk"] is JsonNode bounds){double bx=(double)bounds["x"]!,bz=(double)bounds["z"]!,size=(double)bounds["size"]!;minx=Math.Min(minx,(int)Math.Floor((bx-ox)/cell));maxx=Math.Max(maxx,(int)Math.Ceiling((bx+size-ox)/cell)-1);minz=Math.Min(minz,(int)Math.Floor((bz-oz)/cell));maxz=Math.Max(maxz,(int)Math.Ceiling((bz+size-oz)/cell)-1);}
 foreach(var a in walls){double x=(double)a!["x"]!,z=(double)a["z"]!,ww=(double)a["w"]!,dd=(double)a["d"]!;minx=Math.Min(minx,(int)Math.Floor((x-ww/2-ox)/cell));maxx=Math.Max(maxx,(int)Math.Ceiling((x+ww/2-ox)/cell)-1);minz=Math.Min(minz,(int)Math.Floor((z-dd/2-oz)/cell));maxz=Math.Max(maxz,(int)Math.Ceiling((z+dd/2-oz)/cell)-1);}
 int l=Math.Max(0,-minx),t=Math.Max(0,-minz),r=Math.Max(0,maxx-w+1),b=Math.Max(0,maxz-h+1);
 if(l+r+t+b>0){foreach(var n in grid){var row=n!.AsArray();for(int i=0;i<l;i++)row.Insert(0,JsonValue.Create(1));for(int i=0;i<r;i++)row.Add(JsonValue.Create(1));}for(int i=0;i<t;i++){var row=new JsonArray();for(int x=0;x<w+l+r;x++)row.Add(JsonValue.Create(1));grid.Insert(0,row);}for(int i=0;i<b;i++){var row=new JsonArray();for(int x=0;x<w+l+r;x++)row.Add(JsonValue.Create(1));grid.Add(row);}w+=l+r;h+=t+b;ox-=l*cell;oz-=t*cell;root["width"]=w;root["height"]=h;root["origin"]!["x"]=ox;root["origin"]!["z"]=oz;}
 foreach(var a in walls){double x=(double)a!["x"]!,z=(double)a["z"]!,ww=(double)a["w"]!,dd=(double)a["d"]!;int x0=(int)Math.Floor((x-ww/2-ox)/cell),x1=(int)Math.Ceiling((x+ww/2-ox)/cell),z0=(int)Math.Floor((z-dd/2-oz)/cell),z1=(int)Math.Ceiling((z+dd/2-oz)/cell);for(int iz=z0;iz<z1;iz++)for(int ix=x0;ix<x1;ix++)if(ix>=0&&iz>=0&&ix<w&&iz<h)grid[iz]![ix]=JsonValue.Create(0);}
 File.WriteAllText(gridPath,root.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));}
 static JsonNode PathRequest(JsonNode q){var root=JsonNode.Parse(File.ReadAllText(gridPath))!;var grid=root["grid"]!.AsArray();int w=(int)root["width"]!,h=(int)root["height"]!;double ox=(double)root["origin"]!["x"]!,oz=(double)root["origin"]!["z"]!,cell=(double)root["cellSize"]!;int sx=(int)Math.Floor(((double)q["sx"]!-ox)/cell),sz=(int)Math.Floor(((double)q["sz"]!-oz)/cell),tx=(int)Math.Floor(((double)q["tx"]!-ox)/cell),tz=(int)Math.Floor(((double)q["tz"]!-oz)/cell);
 int[] start=Nearest(grid,w,h,sx,sz),goal=Nearest(grid,w,h,tx,tz);if(start==null||goal==null)return JsonSerializer.SerializeToNode(new{path=Array.Empty<object>(),ok=false})!;
 int Key(int x,int z)=>z*w+x;var open=new List<(int x,int z,double g,double f)>{(start[0],start[1],0,Math.Sqrt((goal[0]-start[0])*(goal[0]-start[0])+(goal[1]-start[1])*(goal[1]-start[1])))};var costs=new Dictionary<int,double>{{Key(start[0],start[1]),0}};var came=new Dictionary<int,int>();var closed=new HashSet<int>();int[] dx={1,-1,0,0,1,1,-1,-1},dzs={0,0,1,-1,1,-1,1,-1};
 while(open.Count>0){open.Sort((a,b)=>a.f.CompareTo(b.f));var n=open[0];open.RemoveAt(0);int nk=Key(n.x,n.z);if(!closed.Add(nk))continue;if(n.x==goal[0]&&n.z==goal[1]){var pts=new List<object>();int k=nk;while(true){int x=k%w,z=k/w;pts.Add(new{x=ox+(x+.5)*cell,z=oz+(z+.5)*cell});if(!came.ContainsKey(k))break;k=came[k];}pts.Reverse();return JsonSerializer.SerializeToNode(new{ok=true,path=pts})!;}
 for(int i=0;i<8;i++){int x=n.x+dx[i],z=n.z+dzs[i];if(x<0||z<0||x>=w||z>=h||!Open(grid,x,z))continue;if(dx[i]!=0&&dzs[i]!=0&&(!Open(grid,n.x+dx[i],n.z)||!Open(grid,n.x,n.z+dzs[i])))continue;int k=Key(x,z);double g=n.g+(dx[i]!=0&&dzs[i]!=0?1.41421356:1);if(g>=(costs.GetValueOrDefault(k,double.PositiveInfinity)))continue;costs[k]=g;came[k]=nk;open.Add((x,z,g,g+Math.Sqrt((goal[0]-x)*(goal[0]-x)+(goal[1]-z)*(goal[1]-z))));}}
 return JsonSerializer.SerializeToNode(new{ok=false,path=Array.Empty<object>()})!;}
 static bool Open(JsonArray g,int x,int z)=>g[z]![x]!.GetValue<int>()!=0;
 static int[] Nearest(JsonArray g,int w,int h,int x,int z){for(int r=0;r<20;r++)for(int dz=-r;dz<=r;dz++)for(int dx=-r;dx<=r;dx++){if(Math.Max(Math.Abs(dx),Math.Abs(dz))!=r)continue;int a=x+dx,b=z+dz;if(a>=0&&b>=0&&a<w&&b<h&&Open(g,a,b))return new[]{a,b};}return null;}
 static async Task Send(HttpListenerContext c,string s){byte[] b=Encoding.UTF8.GetBytes(s);c.Response.ContentLength64=b.Length;await c.Response.OutputStream.WriteAsync(b);c.Response.Close();}
}
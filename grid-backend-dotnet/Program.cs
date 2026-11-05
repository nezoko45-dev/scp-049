using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
class GridBackend{
 [STAThread] static void Main(){
  string root=AppContext.BaseDirectory;string gridPath=Path.Combine(root,"facility-navdata.json");
  var form=new Form{Text="SCP-049 Grid Sync",Width=420,Height=150};var label=new Label{Dock=DockStyle.Fill,Text="Grid sync running at http://127.0.0.1:8766\nPlace this EXE beside facility-navdata.json",TextAlign=System.Drawing.ContentAlignment.MiddleCenter};form.Controls.Add(label);
  var listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:8766/");listener.Start();
  Task.Run(async()=>{while(listener.IsListening){HttpListenerContext ctx;try{ctx=await listener.GetContextAsync();}catch{break;}ctx.Response.Headers.Add("Access-Control-Allow-Origin","*");ctx.Response.Headers.Add("Access-Control-Allow-Methods","GET,POST,OPTIONS");ctx.Response.Headers.Add("Access-Control-Allow-Headers","Content-Type");if(ctx.Request.HttpMethod=="OPTIONS"){ctx.Response.StatusCode=204;ctx.Response.Close();continue;}try{
   if(ctx.Request.Url!.AbsolutePath=="/health"){await Send(ctx,"ready");continue;}
   if(ctx.Request.Url.AbsolutePath=="/grid"&&File.Exists(gridPath)){ctx.Response.ContentType="application/json";await Send(ctx,File.ReadAllText(gridPath));continue;}
   if(ctx.Request.Url.AbsolutePath=="/chunk"&&ctx.Request.HttpMethod=="POST"){using var sr=new StreamReader(ctx.Request.InputStream);var body=await sr.ReadToEndAsync();var chunk=JsonNode.Parse(body)!;var rootNode=JsonNode.Parse(File.ReadAllText(gridPath))!;var grid=rootNode["grid"]!.AsArray();int width=(int)rootNode["width"]!,height=(int)rootNode["height"]!;double ox=(double)rootNode["origin"]!["x"]!,oz=(double)rootNode["origin"]!["z"]!,cell=(double)rootNode["cellSize"]!;
   foreach(var w in chunk["walls"]!.AsArray()){double x=(double)w!["x"]!,z=(double)w["z"]!,ww=(double)w["w"]!,dd=(double)w["d"]!;int x0=(int)Math.Floor((x-ww/2-ox)/cell),x1=(int)Math.Ceiling((x+ww/2-ox)/cell),z0=(int)Math.Floor((z-dd/2-oz)/cell),z1=(int)Math.Ceiling((z+dd/2-oz)/cell);for(int iz=z0;iz<z1;iz++)for(int ix=x0;ix<x1;ix++)if(ix>=0&&iz>=0&&ix<width&&iz<height)grid[iz]![ix]=0;}
   File.WriteAllText(gridPath,rootNode.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));await Send(ctx,"grid updated");continue;}
   ctx.Response.StatusCode=404;await Send(ctx,"not found");
  }catch{ctx.Response.StatusCode=500;await Send(ctx,"backend error");}}});
  Application.Run(form);
 }
 static async Task Send(HttpListenerContext c,string s){byte[] b=Encoding.UTF8.GetBytes(s);c.Response.ContentLength64=b.Length;await c.Response.OutputStream.WriteAsync(b);c.Response.Close();}
}
using System;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.IO;
using System.Windows.Forms;
class GridBackend{
 [STAThread] static void Main(){
  string root=AppContext.BaseDirectory;string gridPath=Path.Combine(root,"facility-navdata.json");
  var form=new Form{Text="SCP-049 Grid Updater",Width=440,Height=170};
  var label=new Label{Dock=DockStyle.Fill,Text="EXE OWNS THE NAV GRID\nhttp://127.0.0.1:8766/health\n/grid = live grid | /chunk = update walls + expand grid",TextAlign=System.Drawing.ContentAlignment.MiddleCenter};form.Controls.Add(label);
  var listener=new HttpListener();listener.Prefixes.Add("http://127.0.0.1:8766/");listener.Start();
  Task.Run(async()=>{while(listener.IsListening){HttpListenerContext ctx;try{ctx=await listener.GetContextAsync();}catch{break;}ctx.Response.Headers.Add("Access-Control-Allow-Origin","*");ctx.Response.Headers.Add("Access-Control-Allow-Methods","GET,POST,OPTIONS");ctx.Response.Headers.Add("Access-Control-Allow-Headers","Content-Type");if(ctx.Request.HttpMethod=="OPTIONS"){ctx.Response.StatusCode=204;ctx.Response.Close();continue;}try{
   if(ctx.Request.Url!.AbsolutePath=="/health"){await Send(ctx,"ready");continue;}
   if(ctx.Request.Url.AbsolutePath=="/grid"&&File.Exists(gridPath)){ctx.Response.ContentType="application/json";await Send(ctx,File.ReadAllText(gridPath));continue;}
   if(ctx.Request.Url.AbsolutePath=="/chunk"&&ctx.Request.HttpMethod=="POST"){
    using var sr=new StreamReader(ctx.Request.InputStream);var chunk=JsonNode.Parse(await sr.ReadToEndAsync())!;
    var rootNode=JsonNode.Parse(File.ReadAllText(gridPath))!;var grid=rootNode["grid"]!.AsArray();
    int width=(int)rootNode["width"]!,height=(int)rootNode["height"]!;
    double ox=(double)rootNode["origin"]!["x"]!,oz=(double)rootNode["origin"]!["z"]!,cell=(double)rootNode["cellSize"]!;
    var walls=chunk["walls"]!.AsArray();int minX=0,minZ=0,maxX=width-1,maxZ=height-1;
    foreach(var w in walls){double x=(double)w!["x"]!,z=(double)w["z"]!,ww=(double)w["w"]!,dd=(double)w["d"]!;
     minX=Math.Min(minX,(int)Math.Floor((x-ww/2-ox)/cell));maxX=Math.Max(maxX,(int)Math.Ceiling((x+ww/2-ox)/cell)-1);
     minZ=Math.Min(minZ,(int)Math.Floor((z-dd/2-oz)/cell));maxZ=Math.Max(maxZ,(int)Math.Ceiling((z+dd/2-oz)/cell)-1);}
    int left=Math.Max(0,-minX),top=Math.Max(0,-minZ),right=Math.Max(0,maxX-width+1),bottom=Math.Max(0,maxZ-height+1);
    if(left+right+top+bottom>0){
     foreach(var rowNode in grid){var row=rowNode!.AsArray();for(int i=0;i<left;i++)row.Insert(0,JsonValue.Create(1));for(int i=0;i<right;i++)row.Add(JsonValue.Create(1));}
     for(int i=0;i<top;i++){var row=new JsonArray();for(int x=0;x<width+left+right;x++)row.Add(JsonValue.Create(1));grid.Insert(0,row);}
     for(int i=0;i<bottom;i++){var row=new JsonArray();for(int x=0;x<width+left+right;x++)row.Add(JsonValue.Create(1));grid.Add(row);}
     width+=left+right;height+=top+bottom;ox-=left*cell;oz-=top*cell;
     rootNode["width"]=width;rootNode["height"]=height;rootNode["origin"]!["x"]=ox;rootNode["origin"]!["z"]=oz;
    }
    foreach(var w in walls){double x=(double)w!["x"]!,z=(double)w["z"]!,ww=(double)w["w"]!,dd=(double)w["d"]!;
     int x0=(int)Math.Floor((x-ww/2-ox)/cell),x1=(int)Math.Ceiling((x+ww/2-ox)/cell),z0=(int)Math.Floor((z-dd/2-oz)/cell),z1=(int)Math.Ceiling((z+dd/2-oz)/cell);
     for(int iz=z0;iz<z1;iz++)for(int ix=x0;ix<x1;ix++)if(ix>=0&&iz>=0&&ix<width&&iz<height)grid[iz]![ix]=JsonValue.Create(0);}
    File.WriteAllText(gridPath,rootNode.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));
    ctx.Response.ContentType="application/json";await Send(ctx,JsonSerializer.Serialize(new{ok=true,width,height,expanded=left+right+top+bottom>0}));continue;}
   ctx.Response.StatusCode=404;await Send(ctx,"not found");
  }catch(Exception ex){ctx.Response.StatusCode=500;await Send(ctx,ex.Message);}}});
  Application.Run(form);
 }
 static async Task Send(HttpListenerContext c,string s){byte[] b=Encoding.UTF8.GetBytes(s);c.Response.ContentLength64=b.Length;await c.Response.OutputStream.WriteAsync(b);c.Response.Close();}
}
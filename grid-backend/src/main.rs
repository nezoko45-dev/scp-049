use serde::{Deserialize,Serialize};
use std::{fs,process::Command,sync::{Arc,Mutex}};
use tiny_http::{Server,Response,StatusCode,Method};
#[derive(Serialize,Deserialize,Clone)] struct Grid{version:u32,#[serde(rename="cellSize")]cell_size:f32,origin:Origin,width:usize,height:usize,grid:Vec<Vec<u8>>}
#[derive(Serialize,Deserialize,Clone)] struct Origin{x:f32,z:f32}
#[derive(Deserialize)]struct Wall{x:f32,z:f32,w:f32,d:f32}
#[derive(Deserialize)]struct Chunk{walls:Vec<Wall>}
fn main(){
 let path="facility-navdata.json";let data=fs::read_to_string(path).expect("Run EXE from SCP-049 repository root");let grid:Grid=serde_json::from_str(&data).expect("Invalid grid JSON");let state=Arc::new(Mutex::new(grid));let server=Server::http("127.0.0.1:8766").expect("Port 8766 busy");println!("Grid backend: http://127.0.0.1:8766");
 for mut req in server.incoming_requests(){let url=req.url().to_string();
 if req.method()==&Method::Get&&url=="/health"{let _=req.respond(Response::from_string("ready"));continue}
 if req.method()==&Method::Get&&url=="/grid"{let s=serde_json::to_string(&*state.lock().unwrap()).unwrap();let _=req.respond(Response::from_string(s).with_header(tiny_http::Header::from_bytes("Content-Type","application/json").unwrap()));continue}
 if req.method()==&Method::Post&&url=="/chunk"{let mut b=String::new();if req.as_reader().read_to_string(&mut b).is_err(){let _=req.respond(Response::empty(StatusCode(400)));continue}let c:Chunk=match serde_json::from_str(&b){Ok(v)=>v,Err(_)=>{let _=req.respond(Response::empty(StatusCode(400)));continue}};let mut g=state.lock().unwrap();for w in c.walls{let x0=((w.x-w.w/2.0-g.origin.x)/g.cell_size).floor()as isize;let x1=((w.x+w.w/2.0-g.origin.x)/g.cell_size).ceil()as isize;let z0=((w.z-w.d/2.0-g.origin.z)/g.cell_size).floor()as isize;let z1=((w.z+w.d/2.0-g.origin.z)/g.cell_size).ceil()as isize;for z in z0..z1{for x in x0..x1{if x>=0&&z>=0&&(x as usize)<g.width&&(z as usize)<g.height{g.grid[z as usize][x as usize]=0}}}}
 let out=serde_json::to_string_pretty(&*g).unwrap();if fs::write(path,out).is_err(){let _=req.respond(Response::empty(StatusCode(500)));continue}let _=Command::new("git").args(["add",path]).status();let _=Command::new("git").args(["commit","-m","Update streamed facility grid"]).status();let pushed=Command::new("git").args(["push"]).status().map(|s|s.success()).unwrap_or(false);let msg=if pushed{"saved and pushed"}else{"saved locally; Git push needs configured credentials"};let _=req.respond(Response::from_string(msg));continue}
 let _=req.respond(Response::empty(StatusCode(404)));
 }}

// Dependency-free local static server. Bind loopback only.
const http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const root=__dirname,port=Number(process.env.PORT||4173);
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.svg':'image/svg+xml','.wav':'audio/wav','.mp3':'audio/mpeg'};
function handler(req,res){
  let relative;
  try{relative=decodeURIComponent(new URL(req.url,'http://localhost').pathname);}catch{res.writeHead(400);return res.end('Bad request');}
  if(relative==='/')relative='/index.html';
  const file=path.resolve(root,'.'+relative),ext=path.extname(file).toLowerCase();
  if(!file.startsWith(root+path.sep)||relative.split(/[\\/]/).some(p=>p.startsWith('.'))||!types[ext]){res.writeHead(403);return res.end('Forbidden');}
  fs.readFile(file,(err,data)=>{if(err){res.writeHead(404);return res.end('Not found');}res.writeHead(200,{'Content-Type':types[ext],'Cache-Control':'no-store'});res.end(data);});
}
if(require.main===module){const server=http.createServer(handler);server.on('error',e=>{console.error(e.code==='EADDRINUSE'?`Port ${port} is in use. Open http://127.0.0.1:${port}, or set PORT to another value.`:e.message);process.exitCode=1;});server.listen(port,'127.0.0.1',()=>console.log(`FOUR running at http://127.0.0.1:${port}\nPress Ctrl+C to stop.`));}
module.exports=handler;

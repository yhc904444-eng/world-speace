import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url)),port=Number(process.env.PORT||4185);
http.createServer((request,response)=>{
  let name;
  try{name=decodeURIComponent(new URL(request.url,'http://localhost').pathname);}catch{response.writeHead(400);response.end();return;}
  const target=path.resolve(root,'.'+(name==='/'?'/index.html':name));
  if(!target.startsWith(root+path.sep)){response.writeHead(403);response.end();return;}
  fs.readFile(target,(error,data)=>{
    if(error){response.writeHead(404);response.end();return;}
    const mime={'.html':'text/html','.mjs':'text/javascript','.js':'text/javascript','.css':'text/css','.png':'image/png','.svg':'image/svg+xml','.ttf':'font/ttf','.json':'application/json','.webmanifest':'application/manifest+json','.tsv':'text/plain'}[path.extname(target)]||'text/plain';
    response.writeHead(200,{'Content-Type':mime+(mime.startsWith('image/')?'':'; charset=utf-8'),'Cache-Control':'no-cache'});response.end(data);
  });
}).listen(port,'127.0.0.1',()=>console.log('world speace: http://127.0.0.1:'+port));

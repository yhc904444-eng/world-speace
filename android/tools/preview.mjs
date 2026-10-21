import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../app/src/main/assets');
const port=Number(process.env.PORT||4178);
http.createServer((request,response)=>{
  const url=new URL(request.url,'http://localhost');
  if(url.pathname==='/__tests/native-contract-mock.js'){response.setHeader('Content-Type','text/javascript; charset=utf-8');response.end(fs.readFileSync(new URL('native-contract-mock.js',import.meta.url)));return;}
  const nativeTest=url.pathname==='/native-contract.html';
  const target=path.resolve(root,'.'+decodeURIComponent(url.pathname==='/'||nativeTest?'/index.html':url.pathname));
  if(!target.startsWith(root+path.sep)){response.writeHead(403);response.end();return;}
  fs.readFile(target,(error,bytes)=>{if(error){response.writeHead(404);response.end();return;}response.setHeader('Content-Type',({'.html':'text/html; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.css':'text/css; charset=utf-8','.png':'image/png','.json':'application/json','.tsv':'text/plain; charset=utf-8'})[path.extname(target)]||'text/plain');response.setHeader('Cache-Control','no-store');response.end(nativeTest?bytes.toString('utf8').replace('<script type="module" src="app.mjs">','<script src="/__tests/native-contract-mock.js"></script><script type="module" src="app.mjs">'):bytes);});
}).listen(port,'127.0.0.1',()=>console.log('Preview: http://127.0.0.1:'+port));

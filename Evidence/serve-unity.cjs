const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '../UnityCloud/Builds/WebGL');
const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const types = {'.html':'text/html; charset=utf-8','.js':'application/javascript','.wasm':'application/wasm','.data':'application/octet-stream','.json':'application/json','.png':'image/png','.css':'text/css'};
const server = http.createServer((req,res) => {
 const url = new URL(req.url,'http://localhost');
 if (url.pathname === '/__stop' && req.method === 'POST') { res.end('Stopping'); server.close(()=>process.exit(0)); return; }
 if (url.pathname === '/status' || (url.pathname === '/' && !fs.existsSync(path.join(root,'index.html')))) {
  const logs = fs.readdirSync(__dirname).filter(n=>/^CU-.*\.log$/.test(n));
  const items = logs.map(n=>`<h2>${esc(n)}</h2><pre>${esc(fs.readFileSync(path.join(__dirname,n),'utf8').split(/\r?\n/).slice(-24).join('\n'))}</pre>`).join('');
  res.writeHead(200,{'Content-Type':'text/html; charset=utf-8','Cache-Control':'no-store'});
  res.end(`<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="8"><title>RLS Unity build status</title><style>body{background:#111;color:#ddd;font:16px system-ui;max-width:1100px;margin:48px auto;padding:0 24px}a{color:#b6d3ff}pre{white-space:pre-wrap;background:#1b1b1b;padding:18px;border:1px solid #444;font:13px monospace}h1{font-size:28px}h2{font-size:18px}</style><h1>🛠️ RLS · Unity cloud observer</h1><p>Live local build status. This page refreshes every eight seconds.</p><p>${fs.existsSync(path.join(root,'index.html'))?'<a href="/">▶ Open the Unity player</a>':'The Unity player will appear at this server when its WebGL build succeeds.'}</p>${items||'<p>Waiting for Unity import log.</p>'}`); return;
 }
 let file;
 try {file=path.resolve(root,'.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));} catch {res.writeHead(400);res.end();return;}
 if(!file.startsWith(root+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);res.end('Not found');return;}
 res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Cache-Control':'no-store'});fs.createReadStream(file).pipe(res);
});
server.listen(8080,'127.0.0.1',()=>process.stdout.write('Managed Unity preview http://localhost:8080/status; expires after 60 minutes.\n'));
setTimeout(()=>server.close(()=>process.exit(0)),60*60*1000);
process.on('SIGTERM',()=>server.close(()=>process.exit(0)));
process.on('SIGINT',()=>server.close(()=>process.exit(0)));

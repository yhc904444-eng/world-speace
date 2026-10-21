const PREFIX='world-speace-offline-'+new URL(self.registration.scope).pathname+'-';
const CACHE=PREFIX+'__VERSION__';
const FILES=__FILES__;
self.addEventListener('install',event=>event.waitUntil(caches.open(CACHE).then(cache=>cache.addAll(FILES)).then(()=>self.skipWaiting())));
self.addEventListener('activate',event=>event.waitUntil(caches.keys().then(keys=>Promise.all(keys.filter(key=>key.startsWith(PREFIX)&&key!==CACHE).map(key=>caches.delete(key)))).then(()=>self.clients.claim())));
self.addEventListener('fetch',event=>{
  const url=new URL(event.request.url);
  if(event.request.method!=='GET'||url.origin!==self.location.origin||!url.href.startsWith(self.registration.scope))return;
  event.respondWith(caches.open(CACHE).then(async cache=>{
    const key=event.request.mode==='navigate'?new URL('index.html',self.registration.scope).href:event.request;
    return await cache.match(key,{ignoreSearch:true})||fetch(event.request);
  }));
});

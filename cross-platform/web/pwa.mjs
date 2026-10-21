if('serviceWorker' in navigator){
  navigator.serviceWorker.register('./service-worker.js',{scope:'./'}).catch(error=>console.warn('离线缓存暂未就绪：',error.message));
}

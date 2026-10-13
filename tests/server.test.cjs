const test=require('node:test'),assert=require('node:assert/strict'),http=require('node:http'),fs=require('node:fs'),path=require('node:path');
const handler=require('../server.cjs');
test('local server serves game assets and blocks hidden / unsupported paths',async()=>{
  const server=http.createServer(handler);await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  try{const base=`http://127.0.0.1:${server.address().port}`;for(const file of ['/','/styles.css','/src/app.js','/src/editor.js','/src/editor-core.js','/src/optional-audio.js','/charts/karma.js','/vendor/judgement.js']){const r=await fetch(base+file);assert.equal(r.status,200);assert.ok((await r.text()).length>0);}const song=await fetch(base+'/assets/karma.mp3');const localSong=path.join(__dirname,'../assets/karma.mp3');if(fs.existsSync(localSong)){assert.equal(song.headers.get('content-type'),'audio/mpeg');assert.equal((await song.arrayBuffer()).byteLength,fs.statSync(localSong).size);}else assert.equal(song.status,404);for(const file of ['/.git/config','/start.bat','/%2e%2e%5csecret.html'])assert.equal((await fetch(base+file)).status,403);assert.equal((await fetch(base+'/missing.html')).status,404);}
  finally{await new Promise(resolve=>server.close(resolve));}
});

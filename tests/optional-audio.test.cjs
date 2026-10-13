const test=require('node:test'),assert=require('node:assert/strict');
const {createLoader}=require('../src/optional-audio.js');
function documentMock(){const scripts=[];return {scripts,head:{appendChild(script){scripts.push(script);}},createElement(){return {remove(){this.removed=true;}};}};}
test('optional local music is loaded on demand and concurrent requests share one load',async()=>{
  const scope={},doc=documentMock(),loader=createLoader(scope,doc);
  assert.equal(doc.scripts.length,0);
  const first=loader.loadKarma(),second=loader.loadKarma();assert.equal(first,second);assert.equal(doc.scripts.length,1);
  scope.KARMA_AUDIO_BASE64='local-audio';doc.scripts[0].onload();assert.equal(await first,'local-audio');assert.equal(await loader.loadKarma(),'local-audio');assert.equal(doc.scripts.length,1);
});
test('missing optional music gives an import instruction and allows retry',async()=>{
  const scope={},doc=documentMock(),loader=createLoader(scope,doc);
  const missing=loader.loadKarma();doc.scripts[0].onerror();await assert.rejects(missing,/导入歌曲/);
  const retry=loader.loadKarma();assert.equal(doc.scripts.length,2);scope.KARMA_AUDIO_BASE64='recovered';doc.scripts[1].onload();assert.equal(await retry,'recovered');
});
test('a local wrapper without audio data is rejected',async()=>{
  const doc=documentMock(),pending=createLoader({},doc).loadKarma();doc.scripts[0].onload();await assert.rejects(pending,/不包含示例歌曲/);
});

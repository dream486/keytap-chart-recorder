const test=require('node:test'),assert=require('node:assert/strict'),E=require('../src/editor-core.js'),{validateChart,Session}=require('../src/core.js');
test('8th/16th quantization respects BPM and first-beat offset',()=>{
  assert.equal(E.snap(131,120,16),125);assert.equal(E.snap(131,120,8),250);assert.equal(E.snap(575,120,16,450),575);assert.equal(E.snap(491,120,16,450),450);
  assert.deepEqual(E.quantizeNote({lane:0,time:110,endTime:660},120,16),{lane:0,time:125,endTime:625});
});
test('recording captures chords, tap, hold and ignores repeat / preroll input',()=>{
  const r=new E.Recorder({bpm:120,division:16,start:1000,end:3000});assert.equal(r.press(0,999),false);r.press(0,1018);r.press(1,1022);assert.equal(r.press(0,1100),false);r.release(0,1100);r.release(1,1540);r.press(2,2018);const notes=r.stop(3000);
  assert.deepEqual(notes,[{lane:0,time:1000},{lane:1,time:1000,endTime:1500},{lane:2,time:2000,endTime:3000}]);
  const c={version:1,keys:4,title:'Recorded',bpm:120,duration:3000,notes};assert.doesNotThrow(()=>validateChart(c));const session=new Session(c);session.advance(3000,true);assert.equal(session.score,1000000);
});
test('snap can be disabled and stop clips in-progress holds',()=>{
  const r=new E.Recorder({bpm:120,start:0,end:2000,snapEnabled:false});r.press(2,111);r.release(2,170);r.press(3,500);assert.deepEqual(r.stop(1300),[{lane:2,time:111},{lane:3,time:500,endTime:1300}]);
});
test('half-speed recording recognizes real-time holds and stores original song times',()=>{
  const rate=.5,r=new E.Recorder({bpm:120,start:0,end:3000,threshold:180*rate,snapEnabled:false});
  // 1 second on the clock means 500 ms in the original song; a 250 ms real hold is 125 song ms.
  r.press(0,1000*rate);r.release(0,1250*rate);r.press(1,1500*rate);r.release(1,1550*rate);
  assert.deepEqual(r.stop(2000*rate),[{lane:0,time:500,endTime:625},{lane:1,time:750}]);
});
test('segment append preserves outside notes; replace removes intersecting holds',()=>{
  const old=[{lane:0,time:0,endTime:1000},{lane:1,time:1000},{lane:2,time:1500,endTime:2500},{lane:3,time:3000}],take=[{lane:1,time:1250}];
  assert.equal(E.mergeTake(old,take,'append',1000,2000).length,5);
  assert.deepEqual(E.mergeTake(old,take,'replace',1000,2000),[{lane:0,time:0,endTime:1000},{lane:1,time:1250},{lane:3,time:3000}]);
});
test('quantization collisions merge while different-lane chords are preserved',()=>{
  assert.deepEqual(E.normalize([{lane:0,time:1000},{lane:0,time:1000,endTime:1500},{lane:0,time:1250,endTime:1750},{lane:1,time:1000}]),[{lane:0,time:1000,endTime:1750},{lane:1,time:1000}]);
});

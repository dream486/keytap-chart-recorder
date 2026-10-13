const test=require('node:test'),assert=require('node:assert/strict');
const {Session,validateChart}=require('../src/core.js'),demos=require('../charts/demo.js');
const chart=notes=>({version:1,keys:4,title:'Test',bpm:120,duration:3000,notes});
test('all demo charts are valid and autoplay reaches maximum score',()=>{
  for(const c of [...demos,require('../charts/karma.js')]){const s=new Session(c);s.advance(c.duration,true);assert.equal(s.score,1000000);assert.equal(s.accuracy,100);assert.equal(s.maxCombo,s.totalJudgements);assert.equal(s.counts.miss,0);}
});
test('hold head and tail score separately, held tail finishes automatically',()=>{
  const s=new Session(chart([{lane:0,time:500,endTime:1500}]));assert.equal(s.hit(0,500).name,'perfect');assert.equal(s.score,500000);assert.equal(s.judged,1);assert.ok(s.activeHolds[0]);s.advance(1499);assert.equal(s.judged,1);s.advance(1500);assert.equal(s.score,1000000);assert.equal(s.maxCombo,2);assert.equal(s.activeHolds[0],null);assert.equal(s.release(0,1700),null);
});
test('early release breaks hold exactly once, late re-press cannot recover it',()=>{
  const s=new Session(chart([{lane:1,time:500,endTime:1500}]));s.hit(1,500);assert.equal(s.release(1,1000).name,'miss');assert.equal(s.combo,0);assert.equal(s.hit(1,1200),null);s.advance(3000);assert.equal(s.counts.miss,1);assert.equal(s.judged,2);
});
test('missing hold head misses head and tail; tail-window releases receive grades',()=>{
  const s=new Session(chart([{lane:0,time:500,endTime:1500}]));s.advance(641);s.advance(3000);assert.equal(s.counts.miss,2);assert.equal(s.judged,2);
  for(const [at,name] of [[1455,'perfect'],[1410,'great'],[1360,'good'],[1359,'miss']]){const take=new Session(chart([{lane:0,time:500,endTime:1500}]));take.hit(0,500);assert.equal(take.release(0,at).name,name);}
});
test('chord holds run independently and same-lane overlap is rejected',()=>{
  const s=new Session(chart([{lane:0,time:500,endTime:1500},{lane:3,time:500,endTime:2000}]));s.hit(0,500);s.hit(3,500);s.release(0,700);s.advance(2000);assert.equal(s.counts.perfect,3);assert.equal(s.counts.miss,1);assert.equal(s.score,750000);
  for(const notes of [[{lane:0,time:500,endTime:500}],[{lane:0,time:500,endTime:3500}],[{lane:0,time:500,endTime:1500},{lane:0,time:1000}]])assert.throws(()=>validateChart(chart(notes)));
  assert.doesNotThrow(()=>validateChart(chart([{lane:0,time:500,endTime:1500},{lane:0,time:1500}])));
});
test('timing windows are inclusive, sign preserves early / late',()=>{
  for(const [error,name] of [[-45,'perfect'],[45,'perfect'],[-90,'great'],[90,'great'],[-140,'good'],[140,'good']]){const s=new Session(chart([{lane:0,time:1000}]));assert.equal(s.hit(0,1000+error).name,name);assert.equal(s.last.error,error);}
});
test('early spam and wrong lanes do not consume notes',()=>{
  const s=new Session(chart([{lane:0,time:1000}]));assert.equal(s.hit(0,859),null);assert.equal(s.hit(1,1000),null);assert.equal(s.judged,0);assert.equal(s.hit(0,1000).name,'perfect');assert.equal(s.hit(0,1000),null);assert.equal(s.judged,1);
});
test('overdue notes miss exactly once, reset combo and allow next note',()=>{
  const s=new Session(chart([{lane:0,time:500},{lane:1,time:1000},{lane:1,time:1500}]));s.hit(0,500);s.advance(1140);assert.equal(s.counts.miss,0);s.advance(1141);assert.equal(s.counts.miss,1);assert.equal(s.combo,0);s.advance(1200);assert.equal(s.counts.miss,1);s.hit(1,1500);assert.equal(s.combo,1);assert.equal(s.maxCombo,1);assert.equal(s.score,666667);
});
test('chords judge each lane independently; fresh session has no old state',()=>{
  const c=chart([{lane:0,time:1000},{lane:3,time:1000}]),s=new Session(c);s.hit(0,1000);s.hit(3,1000);assert.equal(s.combo,2);const fresh=new Session(c);assert.equal(fresh.judged,0);assert.equal(fresh.score,0);
});
test('malformed charts and duplicate same-lane notes are rejected; notes sorted',()=>{
  for(const c of [null,{...chart([]),keys:5},chart([]),chart([{lane:4,time:1}]),chart([{lane:0,time:-1}]),chart([{lane:0,time:4000}]),chart([{lane:0,time:1},{lane:0,time:1}]),{...chart([{lane:0,time:0}]),bpm:NaN}])assert.throws(()=>validateChart(c));
  assert.equal(validateChart(chart([{lane:3,time:2000},{lane:0,time:1000}])).notes[0].time,1000);
});

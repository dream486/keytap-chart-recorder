const test=require('node:test'),assert=require('node:assert/strict'),vm=require('node:vm'),fs=require('node:fs');
test('audio timeline supports countdown, pause, resume and restart without drift',async()=>{
  const sources=[];
  class FakeContext {
    constructor(){this.currentTime=10;this.destination={};}
    async resume(){}
    createGain(){return {connect(){},gain:{setValueAtTime(){}}};}
    createBufferSource(){const s={playbackRate:{value:1},connect(){},disconnect(){},stop(){this.stopped=true;},start(at,offset){this.at=at;this.offset=offset;}};sources.push(s);return s;}
  }
  const sandbox={AudioContext:FakeContext};vm.createContext(sandbox);vm.runInContext(fs.readFileSync('src/audio.js','utf8'),sandbox);
  const clock=new sandbox.AudioClock();await clock.init();const buffer={duration:25};clock.play(buffer,-2000);
  assert.equal(clock.time,-2000);assert.equal(sources[0].at,12);assert.equal(sources[0].offset,0);
  clock.context.currentTime=11;clock.pause();assert.equal(clock.time,-1000);assert.ok(sources[0].stopped);
  clock.context.currentTime=20;clock.resume();assert.equal(clock.time,-1000);assert.equal(sources[1].at,21);
  clock.context.currentTime=23;assert.equal(clock.time,2000);clock.pause();clock.context.currentTime=30;clock.resume();assert.equal(sources[2].offset,2);assert.equal(clock.time,2000);
  clock.context.currentTime=31;assert.equal(clock.time,3000);clock.play(buffer,-2000);assert.ok(sources[2].stopped);assert.equal(clock.time,-2000);
});
test('slow audio keeps original song time through countdown, pause and resume',async()=>{
  const sources=[];
  class FakeContext {
    constructor(){this.currentTime=10;this.destination={};}
    async resume(){}
    createGain(){return {connect(){},gain:{setValueAtTime(){}}};}
    createBufferSource(){const s={playbackRate:{value:1},connect(){},disconnect(){},stop(){this.stopped=true;},start(at,offset){this.at=at;this.offset=offset;}};sources.push(s);return s;}
  }
  const sandbox={AudioContext:FakeContext};vm.createContext(sandbox);vm.runInContext(fs.readFileSync('src/audio.js','utf8'),sandbox);
  const clock=new sandbox.AudioClock();await clock.init();const buffer={duration:25};clock.play(buffer,-2000,.5);
  assert.equal(sources[0].playbackRate.value,.5);assert.equal(sources[0].at,14);clock.context.currentTime=12;assert.equal(clock.time,-1000);
  clock.context.currentTime=16;assert.equal(clock.time,1000);clock.pause();clock.context.currentTime=30;clock.resume();assert.equal(sources[1].offset,1);assert.equal(sources[1].playbackRate.value,.5);
  clock.context.currentTime=34;assert.equal(clock.time,3000);clock.stop();clock.context.currentTime=40;assert.equal(clock.time,3000);
  clock.play(buffer,5000,.25);clock.context.currentTime=44;assert.equal(clock.time,6000);
  assert.throws(()=>clock.play(buffer,0,0));assert.throws(()=>clock.play(buffer,0,Infinity));
});

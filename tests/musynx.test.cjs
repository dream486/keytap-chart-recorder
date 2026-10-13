const test=require('node:test');
const assert=require('node:assert/strict');
const {convert}=require('../tools/musynx/convert.cjs');
test('MUSYNX export preserves original-song timing, chords and hold ends',()=>{
 const notes=[{time:1250,lane:0},{time:1250,lane:3},{time:1750,lane:1,endTime:2500},{time:1750,lane:2,endTime:2500}];
 const text=convert({version:1,keys:4,title:'test',bpm:120,duration:4000,notes});
 const commands=text.trim().split('\n').map(l=>l.split('\t'));
 const exported=commands.filter(c=>c[0]==='Note'||c[0]==='LongNote');
 assert.deepEqual(exported,[['Note','32500000','3','01'],['Note','32500000','7','01'],['LongNote','37500000','4','01','45000000'],['LongNote','37500000','6','01','45000000']]);
 assert(commands.some(c=>c.join('\t')==='MusicNote\t20000000\t1\t02'));
 assert(commands.some(c=>c.join('\t')==='WAV\t02\tlazhuren'));
 assert(!commands.some(c=>c[0]==='WAV'&&c[1]==='01'));
 assert(commands.filter(c=>c[0]==='BeatLine').every(c=>Number(c[4])>=Number(c[1])));
});

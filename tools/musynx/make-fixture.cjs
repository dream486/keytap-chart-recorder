const fs=require('node:fs'),vm=require('node:vm');
const {convert}=require('./convert.cjs');
const notes=[];
for(let i=0;i<8;i++)notes.push({time:2000+i*500,lane:i%4});
notes.push({time:6500,lane:0},{time:6500,lane:3});
for(let lane=0;lane<4;lane++)notes.push({time:8000,lane});
notes.push({time:10000,lane:0,endTime:12000},{time:10500,lane:2},
 {time:11000,lane:3},{time:12500,lane:1,endTime:14000},
 {time:15000,lane:2,endTime:17000},{time:15000,lane:3,endTime:17000});
for(let i=0;i<8;i++)notes.push({time:18000+i*500,lane:(i+2)%4});
const chart={version:1,keys:4,title:'敲谱接入测试 / 120 BPM',bpm:120,gridOffset:0,duration:24000,notes};
fs.mkdirSync('artifacts/musynx-test',{recursive:true});
fs.writeFileSync('artifacts/musynx-test/test-chart.json',JSON.stringify(chart,null,2));
fs.writeFileSync('.tmp/musynx-research/new-chart.txt',convert(chart));
vm.runInThisContext(fs.readFileSync('src/audio.js','utf8'));
const audio=new AudioClock();let samples;
audio.context={sampleRate:44100,createBuffer(ch,size,rate){samples=new Float32Array(size);return {getChannelData(){return samples;}};}};
audio.synth(chart);
const wav=Buffer.alloc(44+samples.length*4);
wav.write('RIFF');wav.writeUInt32LE(wav.length-8,4);wav.write('WAVEfmt ',8);wav.writeUInt32LE(16,16);wav.writeUInt16LE(1,20);wav.writeUInt16LE(2,22);wav.writeUInt32LE(44100,24);wav.writeUInt32LE(44100*4,28);wav.writeUInt16LE(4,32);wav.writeUInt16LE(16,34);wav.write('data',36);wav.writeUInt32LE(wav.length-44,40);
for(let i=0;i<samples.length;i++){const sample=Math.round(samples[i]*32767);wav.writeInt16LE(sample,44+i*4);wav.writeInt16LE(sample,46+i*4);}
fs.writeFileSync('artifacts/musynx-test/test-music.wav',wav);
console.log('Fixture:',notes.length,'notes,',notes.filter(n=>n.endTime!==undefined).length,'holds, 24 seconds');

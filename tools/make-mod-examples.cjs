// Generate owned synthetic music and a clean demo library, without a game copy.
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const root=path.resolve(__dirname,'..'),examples=path.join(root,'mods/ChartRecorder/examples');
function writeWave(chart,file){
  const scope={};vm.runInNewContext(fs.readFileSync(path.join(root,'src/audio.js'),'utf8'),scope);
  const audio=new scope.AudioClock();let samples;
  audio.context={sampleRate:44100,createBuffer(ch,size){samples=new Float32Array(size);return {getChannelData(){return samples;}};}};
  audio.synth(chart);const wave=Buffer.alloc(44+samples.length*4);
  wave.write('RIFF');wave.writeUInt32LE(wave.length-8,4);wave.write('WAVEfmt ',8);wave.writeUInt32LE(16,16);wave.writeUInt16LE(1,20);wave.writeUInt16LE(2,22);wave.writeUInt32LE(44100,24);wave.writeUInt32LE(176400,28);wave.writeUInt16LE(4,32);wave.writeUInt16LE(16,34);wave.write('data',36);wave.writeUInt32LE(wave.length-44,40);
  for(let i=0;i<samples.length;i++){const sample=Math.max(-32768,Math.min(32767,Math.round(samples[i]*32767)));wave.writeInt16LE(sample,44+i*4);wave.writeInt16LE(sample,46+i*4);}
  fs.writeFileSync(file,wave);
}
function generate(output=path.join(root,'artifacts/musynx-mod/ExampleLibrary')){
  fs.mkdirSync(output,{recursive:true});const songs=[];
  for(const [index,names] of [['1',['full','simple']],['2',['short']]]){
    const id=index.padStart(32,'0'),folder=path.join(output,id);fs.mkdirSync(folder,{recursive:true});
    const first=JSON.parse(fs.readFileSync(path.join(examples,names[0]+'.json'),'utf8'));
    const song={id,title:first.title,audio:id+'/audio.wav',durationMs:first.duration,charts:[]};writeWave(first,path.join(folder,'audio.wav'));
    for(let i=0;i<names.length;i++){const chartId=(index+'0'+(i+1)).padStart(32,'0'),file=id+'/'+chartId+'.json';fs.copyFileSync(path.join(examples,names[i]+'.json'),path.join(output,file));song.charts.push({id:chartId,name:({full:'多押长条',simple:'简化版本',short:'短曲'})[names[i]],file});}
    songs.push(song);
  }
  fs.writeFileSync(path.join(output,'library.json'),JSON.stringify({version:1,selectedSong:songs[0].id,selectedChart:songs[0].charts[0].id,songs},null,2)+'\n');
  return output;
}
if(require.main===module)console.log(generate(process.argv[2]?path.resolve(process.argv[2]):undefined));
module.exports={generate,writeWave};

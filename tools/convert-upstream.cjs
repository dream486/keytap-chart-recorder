// Converts the reviewed upstream example (four lanes) into our millisecond chart.
// Original source: vendor/rhy-game/examples/play.js, MIT, see vendor/rhy-game/LICENSE.
const fs=require('node:fs'),vm=require('node:vm');
function convert(song){
  const step=240000/song.info.bpm/song.info.split,shift=-(song.info.delay||0),notes=[];
  let length=0;
  for(let lane=0;lane<4;lane++){
    const text=song.chart.normal.map(group=>group['lane'+(lane+1)].replace(/\|/g,'')).join('');length=Math.max(length,text.length);
    for(let i=0;i<text.length;i++){
      const time=Math.round(i*step+shift);
      if(text[i]==='s')notes.push({lane,time});
      else if(text[i]==='l'){
        let end=i+1;while(text[end]==='l')end++;
        notes.push({lane,time,endTime:Math.round(end*step+shift)});i=end-1;
      }
    }
  }
  return {version:1,keys:4,title:'KARMA / 上游示例片段',bpm:song.info.bpm,gridOffset:shift,duration:Math.ceil(length*step+shift+1000),audioId:'karma',notes:notes.sort((a,b)=>a.time-b.time||a.lane-b.lane)};
}
if(require.main===module){
  const source=fs.readFileSync('vendor/rhy-game/examples/play.js','utf8');
  const begin=source.indexOf('const song = new Song(')+'const song = new Song('.length;
  const end=source.indexOf('\nfunction play()',begin);
  const literal=source.slice(begin,end).trim().replace(/\)\s*$/,'');
  const chart=convert(vm.runInNewContext('('+literal+')',{}, {timeout:1000}));
  fs.writeFileSync('charts/karma.json',JSON.stringify(chart,null,2)+'\n');
  fs.writeFileSync('charts/karma.js','// Converted upstream example; see THIRD_PARTY_NOTICES.md.\n(function(root){const chart='+JSON.stringify(chart)+';if(typeof module!=="undefined")module.exports=chart;else root.KARMA_CHART=chart;})(globalThis);\n');
  if(fs.existsSync('assets/karma.mp3'))fs.writeFileSync('assets/karma-audio.js','// Dareharu — Karma. Upstream example audio; learning/testing asset, not MIT-licensed music.\n// Embedded to support offline file:// playback.\nwindow.KARMA_AUDIO_BASE64='+JSON.stringify(fs.readFileSync('assets/karma.mp3').toString('base64'))+';\n');
  if(!fs.existsSync('assets/karma.mp3'))console.log('Chart converted; optional local assets/karma.mp3 is absent, so no audio wrapper was generated.');
  console.log(`${chart.notes.length} notes, ${chart.notes.filter(n=>n.endTime!==undefined).length} holds, ${chart.duration} ms`);
}
module.exports=convert;

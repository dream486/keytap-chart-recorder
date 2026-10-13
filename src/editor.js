/* Local listening recorder and waveform timeline editor. */
(() => {
  'use strict';
  const $=id=>document.getElementById(id), E=ChartEditorCore, canvas=$('editorCanvas'),ctx=canvas.getContext('2d'),audio=new AudioClock();
  const codes=['KeyD','KeyF','KeyJ','KeyK'],laneNames=['D','F','J','K'],held=new Set(),keyButtons=[...document.querySelectorAll('[data-record-lane]')];
  let model={version:1,keys:4,title:'我的谱面',bpm:120,gridOffset:0,duration:10000,notes:[]},buffer=null,wave=[],audioId=null;
  let playback='idle',record=null,selected=-1,seek=0,drag=null,busy=false,w=0,h=0,history=[],future=[],scroll=0,zoom=100,metroNext=0;
  const metroNodes=new Set();
  function msg(text){$('editorMessage').textContent=text;}
  const format=t=>{t=Math.max(0,Math.round(t));return `${Math.floor(t/60000).toString().padStart(2,'0')}:${Math.floor(t/1000)%60}`.replace(/:(\d)$/ ,':0$1')+'.'+(t%1000).toString().padStart(3,'0');};
  function chart(){const data={...model,notes:model.notes.map(n=>({...n}))};delete data.audioId;if(audioId)data.audioId=audioId;return data;}
  function save(){try{localStorage.setItem('four-editor-draft',JSON.stringify(chart()));}catch{}}
  function snapshot(){return {model:JSON.stringify(model),audioId,buffer,wave,song:$('editorSong').textContent};}
  function remember(){history.push(snapshot());if(history.length>50)history.shift();future=[];}
  function syncProperties(){const n=model.notes[selected];$('noteLane').value=n?.lane??0;$('noteTime').value=n?.time??'';$('noteEnd').value=n?.endTime??'';for(const id of ['noteLane','noteTime','noteEnd','noteApply','noteDelete'])$(id).disabled=!n||playback!=='idle'||busy;}
  function sync(){
    $('editorStats').textContent=`${model.notes.length} NOTES / ${model.notes.filter(n=>n.endTime!==undefined).length} LN`;
    $('editorState').textContent=busy?'LOADING':playback==='recording'?'RECORDING ●':playback==='listening'?'LISTENING':'EDITING';
    const locked=playback!=='idle'||busy;
    for(const id of ['editorAudio','editorChart','editorKarma','editorTitle','editorBpm','editorOffset','editorGrid','editorSnap','segmentIn','segmentOut','recordMode','editorRate','editorMetronome','editorInputOffset','editorTool','editorQuantize','editorExport','editorClear'])$(id).disabled=locked;
    $('editorRecord').disabled=locked||!buffer;$('editorListen').disabled=locked||!buffer;$('editorStop').disabled=!locked||busy;
    $('editorTest').disabled=locked||!buffer||!model.notes.length;$('editorUndo').disabled=locked||!history.length;$('editorRedo').disabled=locked||!future.length;
    $('editorScroll').max=Math.max(0,model.duration/1000-visibleSeconds());scroll=Math.max(0,Math.min(scroll,Number($('editorScroll').max)));$('editorScroll').value=scroll;syncProperties();
  }
  function setFields(resetRange=true){$('editorTitle').value=model.title;$('editorBpm').value=model.bpm;$('editorOffset').value=model.gridOffset||0;if(resetRange||Number($('segmentIn').value)*1000>=model.duration){$('segmentIn').value=0;$('segmentOut').value=(model.duration/1000).toFixed(3);seek=0;scroll=0;}else{$('segmentOut').value=Math.min(Number($('segmentOut').value),model.duration/1000);seek=Math.min(seek,model.duration);}sync();}
  function validate(notes){if(notes.length)RhythmCore.validateChart({...chart(),notes});}
  function commit(notes,label){
    try{validate(notes);remember();model.notes=notes.map(n=>({...n}));selected=-1;save();sync();msg(label);return true;}
    catch(e){msg('无法修改：'+e.message);return false;}
  }
  function undo(redo=false){const from=redo?future:history,to=redo?history:future;if(!from.length)return;to.push(snapshot());const previous=from.pop();model=JSON.parse(previous.model);audioId=previous.audioId;buffer=previous.buffer;wave=previous.wave;$('editorSong').textContent=previous.song;selected=-1;setFields(false);save();msg(redo?'已重做':'已撤销');}
  function number(id,fallback){const value=Number($(id).value);return Number.isFinite(value)?value:fallback;}
  function gridTime(time,bypass=false){return Math.round(Math.max(0,Math.min(model.duration,$('editorSnap').checked&&!bypass?E.snap(time,model.bpm,number('editorGrid',16),model.gridOffset||0):time)));}
  function visibleSeconds(){return Math.max(1,(w-54)/zoom);}
  function buildWave(){wave=[];if(!buffer)return;const data=buffer.getChannelData(0),bins=Math.min(8192,data.length);for(let b=0;b<bins;b++){const start=Math.floor(b*data.length/bins),end=Math.floor((b+1)*data.length/bins);let peak=0;for(let i=start;i<end;i+=Math.max(1,Math.floor((end-start)/128)))peak=Math.max(peak,Math.abs(data[i]));wave.push(peak);}}
  async function loadKarma(){
    if(playback!=='idle'||busy)return;busy=true;sync();msg('载入 Karma 原谱与音乐…');
    try{const keepDraft=audioId==='karma'&&!buffer&&model.notes.length>0;await audio.init();const next=await audio.decode(Uint8Array.from(atob(await RhythmOptionalAudio.loadKarma()),c=>c.charCodeAt(0)).buffer);if(!keepDraft){remember();model={...RhythmCore.validateChart(KARMA_CHART),gridOffset:450,duration:Math.ceil(next.duration*1000)};}buffer=next;audioId='karma';buildWave();selected=-1;setFields();$('segmentOut').value=(KARMA_CHART.duration/1000).toFixed(3);save();$('editorSong').textContent=`Dareharu · Karma / ${format(model.duration)} · 原谱为前 10 秒片段`;msg(keepDraft?'已恢复 Karma 音乐，保留草稿里的全部录制与修改。':'已载入原曲与原谱。可选任意区间继续录制，或直接精修原谱。');}
    catch(e){msg('载入失败：'+e.message);}finally{busy=false;sync();}
  }
  function segment(){const start=number('segmentIn',0)*1000,end=Math.min(model.duration,number('segmentOut',0)*1000);if(start<0||end<=start||start>=model.duration)throw new Error('分段终点必须大于起点，且位于音乐范围内');return {start,end};}
  function begin(recording){
    if(!buffer||busy||playback!=='idle')return;
    try{
      const range=segment(),rate=number('editorRate',1),nextRecord=recording?new E.Recorder({...range,bpm:model.bpm,division:number('editorGrid',16),offset:model.gridOffset||0,threshold:180*rate,snapEnabled:$('editorSnap').checked}):{...range};
      busy=true;sync();audio.init().then(()=>{
        if(playback!=='idle'||$('editorView').hidden)return;
        held.clear();selected=-1;playback=recording?'recording':'listening';
        record=nextRecord;
        record.mode=$('recordMode').value;record.rate=rate;record.compensation=number('editorInputOffset',0)*rate;
        const start=recording?range.start-120000/model.bpm:(seek>=range.start&&seek<range.end?seek:range.start);
        audio.setVolume(.65);audio.play(buffer,start,rate);seek=start;metroNext=Math.ceil((start-(model.gridOffset||0))/(60000/model.bpm));
        sync();msg(recording?`${rate.toFixed(2)}× 录制：准备两拍后按 D / F / J / K，按住 ≥180 ms 生成长条；自动保存原曲时间。`:`${rate.toFixed(2)}× 试听中 · Esc 停止；停止后可继续精修。`);
      }).catch(e=>{playback='idle';msg('播放失败：'+e.message);}).finally(()=>{busy=false;sync();});
    }catch(e){msg(e.message);}
  }
  function stop(reason='已停止'){
    if(playback==='idle')return;
    const time=Math.min(record.end,audio.time);audio.stop();for(const node of metroNodes){try{node.stop();}catch{}node.disconnect();}metroNodes.clear();
    const wasRecording=playback==='recording';playback='idle';held.clear();seek=Math.max(0,time);
    if(wasRecording&&time>=record.start){const take=record.stop(time-record.compensation);const notes=E.mergeTake(model.notes,take,record.mode,record.start,time);commit(notes,`${reason} · 本段录入 ${take.length} 个音符（${take.filter(n=>n.endTime!==undefined).length} 个长条），已${record.mode==='replace'?'覆盖实际录制区间':'追加'}。吸附后同轨重叠音符合并。`);}else msg(wasRecording?'录制在准备阶段停止，谱面未改变。':reason);
    record=null;sync();
  }
  function press(lane){if(held.has(lane))return;held.add(lane);if(playback==='recording')record.press(lane,audio.time-record.compensation);}
  function release(lane){if(!held.has(lane))return;held.delete(lane);if(playback==='recording'){const note=record.release(lane,audio.time-record.compensation);if(note)msg(`录入 ${laneNames[lane]} · ${Math.round(note.time)} ms${note.endTime===undefined?' · 单点':` → ${Math.round(note.endTime)} ms · 长条`}`);}}
  function metronome(){
    if(playback!=='recording'||!$('editorMetronome').checked)return;
    const beat=60000/model.bpm,t=audio.time,rate=audio.rate;
    while(metroNext*beat+(model.gridOffset||0)<=Math.min(t+120*rate,record.end)){
      const at=metroNext*beat+(model.gridOffset||0),when=audio.context.currentTime+Math.max(0,(at-t)/1000/rate);metroNext++;
      if(at<t-100*rate)continue;
      const osc=audio.context.createOscillator(),gain=audio.context.createGain();osc.frequency.value=metroNext%4===1?1400:950;gain.gain.setValueAtTime(.12,when);gain.gain.exponentialRampToValueAtTime(.001,when+.045);osc.connect(gain);gain.connect(audio.gain);metroNodes.add(osc);osc.onended=()=>{metroNodes.delete(osc);osc.disconnect();gain.disconnect();};osc.start(when);osc.stop(when+.05);
    }
  }
  $('modePlay').addEventListener('click',()=>show('play'));$('modeEditor').addEventListener('click',()=>show('editor'));
  function show(mode){if(mode==='play'&&playback!=='idle')stop('切换到游玩，已停止');$('playView').hidden=mode!=='play';$('editorView').hidden=mode!=='editor';$('modePlay').classList.toggle('active',mode==='play');$('modeEditor').classList.toggle('active',mode==='editor');document.dispatchEvent(new CustomEvent('four-viewchange',{detail:mode}));if(mode==='editor')resize();}
  $('editorRecord').addEventListener('click',()=>{$('editorRecord').blur();begin(true);});$('editorListen').addEventListener('click',()=>{$('editorListen').blur();begin(false);});$('editorStop').addEventListener('click',()=>stop());
  $('editorKarma').addEventListener('click',loadKarma);
  $('editorAudio').addEventListener('change',async e=>{
    const file=e.target.files[0];if(!file)return;busy=true;sync();msg('正在解码歌曲…');
    try{if(file.size>100000000)throw new Error('音乐不能超过 100 MB');const next=await audio.decode(await file.arrayBuffer()),duration=Math.ceil(next.duration*1000);if(duration>600000)throw new Error('歌曲最长支持 10 分钟');if(model.notes.some(n=>(n.endTime??n.time)>duration))throw new Error('音乐短于已有音符，请先清空或修剪谱面');buffer=next;model.duration=duration;audioId=null;if(model.title==='我的谱面')model.title=file.name.replace(/\.[^.]+$/,'');buildWave();setFields();save();$('editorSong').textContent=file.name+' / '+format(duration);msg('歌曲已载入。设置 BPM、首拍位置和录制区间即可开始。');}
    catch(err){msg('导入失败：'+err.message);}finally{busy=false;sync();e.target.value='';}
  });
  $('editorChart').addEventListener('change',async e=>{
    const file=e.target.files[0];if(!file)return;
    try{if(file.size>3000000)throw new Error('谱面不能超过 3 MB');const raw=JSON.parse(await file.text()),next=RhythmCore.validateChart(raw);if(buffer&&buffer.duration*1000+50<next.duration)throw new Error('当前音乐短于谱面，请先载入匹配的歌曲');remember();model={...next,gridOffset:Number.isFinite(raw.gridOffset)?raw.gridOffset:0};audioId=next.audioId||null;selected=-1;setFields();save();msg('谱面已导入，请确认 BPM、首拍位置与歌曲匹配。');}
    catch(err){msg('导入失败：'+err.message);}e.target.value='';
  });
  for(const [id,key] of [['editorTitle','title'],['editorBpm','bpm'],['editorOffset','gridOffset']])$(id).addEventListener('change',()=>{
    const value=key==='title'?$(id).value.trim():Number($(id).value);
    if((key==='title'&&!value)||(key==='bpm'&&(!Number.isFinite(value)||value<30||value>300))||(key==='gridOffset'&&!Number.isFinite(value))){msg('请输入有效标题、30–300 的 BPM 和有限的首拍位置。');setFields();return;}model[key]=value;save();
  });
  $('editorUndo').addEventListener('click',()=>undo());$('editorRedo').addEventListener('click',()=>undo(true));
  $('editorClear').addEventListener('click',()=>commit([],'已清空谱面，可撤销恢复。'));
  $('editorQuantize').addEventListener('click',()=>{const notes=E.normalize(model.notes.map(n=>E.quantizeNote(n,model.bpm,number('editorGrid',16),model.gridOffset||0,model.duration)));commit(notes,`已按 1/${$('editorGrid').value} 网格吸附所有起点和终点；同轨重叠合并。`);});
  function exportChart(){try{const data=RhythmCore.validateChart(chart());data.gridOffset=model.gridOffset||0;const url=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'})),a=document.createElement('a');a.href=url;a.download='four-recorded-chart.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);msg('谱面已导出。自选音乐不包含在 JSON 中，游玩时需另行选择。');}catch(e){msg('无法导出：'+e.message);}}
  $('editorExport').addEventListener('click',exportChart);
  $('editorTest').addEventListener('click',()=>{
    try{
      const {start,end}=segment(),notes=model.notes.filter(n=>n.time<end&&(n.endTime??n.time)>=start).map(n=>({lane:n.lane,time:Math.max(start,n.time)-start,...(n.endTime!==undefined?{endTime:Math.min(end,n.endTime)-start}:{})}));
      const data=RhythmCore.validateChart({version:1,keys:4,title:model.title.slice(0,65)+' / '+format(start)+'–'+format(end),bpm:model.bpm,gridOffset:(model.gridOffset||0)-start,duration:end-start,notes});
      const first=Math.floor(start/1000*buffer.sampleRate),length=Math.ceil((end-start)/1000*buffer.sampleRate),clip=audio.context.createBuffer(buffer.numberOfChannels,length,buffer.sampleRate);
      for(let c=0;c<buffer.numberOfChannels;c++)clip.copyToChannel(buffer.getChannelData(c).subarray(first,first+length),c);
      show('play');document.dispatchEvent(new CustomEvent('four-test-chart',{detail:{chart:data,buffer:clip}}));
    }catch(e){msg('无法测试：'+e.message);}
  });
  $('noteApply').addEventListener('click',()=>{
    if(selected<0)return;const notes=model.notes.map(n=>({...n})),note={lane:Number($('noteLane').value),time:Number($('noteTime').value)};if($('noteEnd').value!=='')note.endTime=Number($('noteEnd').value);notes[selected]=note;commit(notes,'精修已应用（数字输入保留精确毫秒，不自动吸附）。');
  });
  function removeSelected(){if(selected>=0)commit(model.notes.filter((_,i)=>i!==selected),'已删除音符，可撤销恢复。');}
  $('noteDelete').addEventListener('click',removeSelected);
  $('editorZoom').addEventListener('change',()=>{zoom=number('editorZoom',100);sync();});$('editorScroll').addEventListener('input',()=>{scroll=Number($('editorScroll').value);});$('editorFollow').addEventListener('click',()=>{scroll=Math.max(0,Math.min(Number($('editorScroll').max),(playback==='idle'?seek:audio.time)/1000-visibleSeconds()/3));$('editorScroll').value=scroll;});
  window.addEventListener('keydown',e=>{
    if($('editorView').hidden)return;
    if(['INPUT','SELECT','TEXTAREA','BUTTON'].includes(e.target.tagName))return;
    if(e.code==='Escape'){e.preventDefault();if(playback!=='idle')stop();return;}
    if(playback==='recording'){const lane=codes.indexOf(e.code);if(lane>=0){e.preventDefault();if(!e.repeat)press(lane);}return;}
    if(playback!=='idle')return;
    if((e.ctrlKey||e.metaKey)&&e.code==='KeyZ'){e.preventDefault();undo(e.shiftKey);return;}
    if((e.ctrlKey||e.metaKey)&&e.code==='KeyY'){e.preventDefault();undo(true);return;}
    if(e.code==='Delete'||e.code==='Backspace'){e.preventDefault();removeSelected();return;}
    if(e.code==='Space'){e.preventDefault();begin(false);return;}
    if(selected>=0&&['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.code)){
      e.preventDefault();const old=model.notes[selected],n={...old},delta=e.shiftKey?1:Math.round(E.step(model.bpm,number('editorGrid',16)));
      if(e.code==='ArrowLeft'||e.code==='ArrowRight'){const d=e.code==='ArrowLeft'?-delta:delta;n.time+=d;if(n.endTime!==undefined)n.endTime+=d;}else n.lane=Math.max(0,Math.min(3,n.lane+(e.code==='ArrowUp'?-1:1)));
      const notes=model.notes.map((v,i)=>i===selected?n:v);if(commit(notes,'已微调音符')){selected=notes.indexOf(n);syncProperties();}
    }
  });
  window.addEventListener('keyup',e=>{const lane=codes.indexOf(e.code);if(lane>=0)release(lane);});
  keyButtons.forEach((b,lane)=>{b.addEventListener('pointerdown',e=>{e.preventDefault();b.setPointerCapture(e.pointerId);press(lane);});for(const event of ['pointerup','pointercancel','lostpointercapture'])b.addEventListener(event,()=>release(lane));});
  window.addEventListener('blur',()=>{if(playback!=='idle')stop('窗口失焦，已保留本段录制');held.clear();});document.addEventListener('visibilitychange',()=>{if(document.hidden&&playback!=='idle')stop('切换标签页，已保留本段录制');});
  function point(e){const r=canvas.getBoundingClientRect();return {x:e.clientX-r.left,y:e.clientY-r.top};}
  function timeAt(x){return Math.max(0,Math.min(model.duration,(scroll+(x-54)/zoom)*1000));}
  function laneAt(y){return Math.max(0,Math.min(3,Math.floor((y-100)/((h-110)/4))));}
  function noteAt(p){const lane=laneAt(p.y);let best=-1;for(let i=0;i<model.notes.length;i++){const n=model.notes[i],start=54+(n.time/1000-scroll)*zoom,end=54+((n.endTime??n.time)/1000-scroll)*zoom;if(n.lane===lane&&p.x>=start-8&&p.x<=end+10)best=i;}return best;}
  canvas.addEventListener('pointerdown',e=>{
    if(playback!=='idle'||busy)return;canvas.focus();const p=point(e);if(p.y<100){seek=timeAt(p.x);msg('播放头已定位到 '+format(seek));return;}const tool=$('editorTool').value,index=noteAt(p);canvas.setPointerCapture(e.pointerId);
    if(tool==='delete'){if(index>=0){selected=index;removeSelected();}return;}
    if(tool==='tap'||tool==='hold'){
      const n={lane:laneAt(p.y),time:gridTime(timeAt(p.x),e.altKey)};if(tool==='hold')n.endTime=Math.min(model.duration,n.time+Math.round(E.step(model.bpm,number('editorGrid',16))*2));drag={mode:'create',note:n,preview:n,before:model.notes.map(n=>({...n})),startX:p.x};selected=-1;
    }else if(index>=0){selected=index;const n=model.notes[index],end=54+((n.endTime??n.time)/1000-scroll)*zoom;drag={mode:n.endTime!==undefined&&Math.abs(p.x-end)<12?'tail':'move',note:{...n},preview:{...n},index,before:model.notes.map(n=>({...n})),startX:p.x};syncProperties();}else{selected=-1;syncProperties();}
  });
  canvas.addEventListener('pointermove',e=>{
    if(!drag)return;const p=point(e),n={...drag.note};
    if(drag.mode==='tail'||drag.mode==='create'&&n.endTime!==undefined)n.endTime=Math.max(n.time+1,gridTime(timeAt(p.x),e.altKey));
    else if(drag.mode==='move'){const time=gridTime(n.time+(p.x-drag.startX)/zoom*1000,e.altKey),delta=time-n.time;n.time=time;if(n.endTime!==undefined)n.endTime+=delta;n.lane=laneAt(p.y);}
    drag.preview=n;
  });
  canvas.addEventListener('pointerup',()=>{if(!drag)return;const d=drag;drag=null;if(d.mode==='create')commit([...d.before,d.preview],'音符已新增，可拖动或输入毫秒精修。');else{const notes=d.before.map((n,i)=>i===d.index?d.preview:n);if(JSON.stringify(notes)!==JSON.stringify(d.before))commit(notes,'音符位置 / 长度已更新。');}});
  canvas.addEventListener('pointercancel',()=>{drag=null;});
  function resize(){const r=canvas.getBoundingClientRect();if(!r.width)return;const dpr=Math.min(devicePixelRatio||1,2);w=r.width;h=r.height;canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);ctx.setTransform(dpr,0,0,dpr,0,0);sync();}
  new ResizeObserver(resize).observe(canvas);
  function drawNote(n,index,preview=false){const x=54+(n.time/1000-scroll)*zoom,end=54+((n.endTime??n.time)/1000-scroll)*zoom,laneH=(h-110)/4,y=100+n.lane*laneH+laneH/2;if(end<54||x>w)return;ctx.fillStyle=preview?'#e3d394':index===selected?'#e6f8c7':n.endTime!==undefined?'#aecf76':'#b9c4cc';ctx.fillRect(Math.max(54,x-4),y-12,Math.max(8,end-Math.max(x,54)+8),24);if(n.endTime!==undefined){ctx.fillStyle='#f2f4f5';ctx.fillRect(end-3,y-15,6,30);}if(index===selected){ctx.strokeStyle='#fff';ctx.strokeRect(x-6,y-15,Math.max(12,end-x+12),30);}}
  function frame(){
    if(!$('editorView').hidden){
      const t=playback==='idle'?seek:audio.time;
      if(playback!=='idle'){
        metronome();if(t>=record.end)stop('本段完成');
        if(t/1000<scroll||t/1000>scroll+visibleSeconds()*.8){scroll=Math.max(0,Math.min(Number($('editorScroll').max),t/1000-visibleSeconds()*.25));$('editorScroll').value=scroll;}
        if(playback==='recording'&&t<record.start)$('editorState').textContent='准备 '+Math.max(1,Math.ceil((record.start-t)/(60000/model.bpm)))+' 拍';
        else if(playback==='recording')$('editorState').textContent='RECORDING ●';
      }
      $('editorTime').textContent=format(t);ctx.clearRect(0,0,w,h);ctx.fillStyle='#111519';ctx.fillRect(0,0,w,h);const laneH=(h-110)/4;
      // Local waveform envelope, drawn above the four recording lanes.
      ctx.fillStyle='#63736d';for(let x=54;x<w;x+=2){const time=(scroll+(x-54)/zoom)*1000,index=Math.floor(time/model.duration*wave.length),peak=wave[index]||0;ctx.fillRect(x,59-peak*22,1,peak*44);}
      for(let lane=0;lane<4;lane++){ctx.fillStyle=held.has(lane)?'#293626':lane%2?'#191e23':'#15191e';ctx.fillRect(54,100+lane*laneH,w-54,laneH);ctx.fillStyle='#b9c4cc';ctx.font='bold 16px monospace';ctx.fillText(laneNames[lane],20,100+lane*laneH+laneH/2+5);ctx.strokeStyle='#30373e';ctx.beginPath();ctx.moveTo(54,100+lane*laneH);ctx.lineTo(w,100+lane*laneH);ctx.stroke();}
      const grid=E.step(model.bpm,number('editorGrid',16)),first=Math.floor((scroll*1000-(model.gridOffset||0))/grid),last=Math.ceil(((scroll+visibleSeconds())*1000-(model.gridOffset||0))/grid);
      for(let i=first;i<=last;i++){const at=i*grid+(model.gridOffset||0);if(at<0)continue;const x=54+(at/1000-scroll)*zoom;ctx.strokeStyle=i%(number('editorGrid',16)/4)===0?'#414b50':'#262e32';ctx.beginPath();ctx.moveTo(x,100);ctx.lineTo(x,h);ctx.stroke();}
      ctx.fillStyle='#92999f';ctx.font='10px monospace';for(let sec=Math.ceil(scroll);sec<scroll+visibleSeconds();sec++){const x=54+(sec-scroll)*zoom;ctx.fillText(sec+'s',x+3,17);}
      const inX=54+(number('segmentIn',0)-scroll)*zoom,outX=54+(number('segmentOut',0)-scroll)*zoom;ctx.fillStyle='rgba(197,236,125,.06)';ctx.fillRect(Math.max(54,inX),100,Math.max(0,Math.min(w,outX)-Math.max(54,inX)),h-100);ctx.fillStyle='#b4cd8b';ctx.fillRect(inX,25,2,h-25);ctx.fillRect(outX,25,2,h-25);
      model.notes.forEach((n,i)=>{if(drag&&drag.index===i)return;drawNote(n,i);});
      if(record instanceof E.Recorder){for(const n of record.notes)drawNote(n,-1,true);for(const [lane,time] of record.down)drawNote({lane,time,endTime:Math.max(time+1,audio.time-record.compensation)},-1,true);}
      if(drag)drawNote(drag.preview,-1,true);
      const playX=54+(Math.max(0,t)/1000-scroll)*zoom;ctx.fillStyle='#eeaa8c';ctx.fillRect(playX,0,2,h);ctx.fillRect(playX-4,0,10,6);
      keyButtons.forEach((b,lane)=>b.classList.toggle('pressed',held.has(lane)));
    }
    requestAnimationFrame(frame);
  }
  try{const raw=JSON.parse(localStorage.getItem('four-editor-draft'));if(raw?.version===1&&raw.keys===4&&Number.isFinite(raw.bpm)&&raw.bpm>=30&&raw.bpm<=300&&Number.isFinite(raw.duration)&&raw.duration>0&&raw.duration<=600000&&typeof raw.title==='string'){if(raw.notes.length)RhythmCore.validateChart(raw);model={...raw,gridOffset:raw.gridOffset||0};audioId=raw.audioId||null;msg('已恢复本地草稿。自选音乐需重新导入；Karma 可点击载入按钮。');}}catch{}
  setFields();requestAnimationFrame(frame);
})();

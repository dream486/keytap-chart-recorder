/* UI and Canvas rendering. No build tools, packages, or remote requests. */
(() => {
  'use strict';
  const $=id=>document.getElementById(id), canvas=$('canvas'), ctx=canvas.getContext('2d');
  const audio=new AudioClock(), charts=[...DEMO_CHARTS,KARMA_CHART].map(RhythmCore.validateChart), keys=['KeyD','KeyF','KeyJ','KeyK'];
  const buttons=[...document.querySelectorAll('[data-lane]')], pressed=new Set(), flashes=[0,0,0,0];
  let chart=charts[0], session=new RhythmCore.Session(chart), state='ready', auto=false, customAudio=null, busy=false, w=0,h=0;
  let fall=3600/2.5, offset=0, lastResult=0, karmaBuffer=null,startRequest=0;
  async function builtInAudio(){if(chart.audioId!=='karma')return audio.synth(chart);if(!karmaBuffer){const bytes=Uint8Array.from(atob(await RhythmOptionalAudio.loadKarma()),c=>c.charCodeAt(0));karmaBuffer=await audio.decode(bytes.buffer);}return karmaBuffer;}
  const fmt=ms=>{const sec=Math.max(0,Math.floor(ms/1000));return `${Math.floor(sec/60).toString().padStart(2,'0')}:${(sec%60).toString().padStart(2,'0')}`;};
  function message(text){$('message').textContent=text;}
  function overlay(title,text){$('overlayTitle').textContent=title;$('overlayText').textContent=text;$('overlay').hidden=false;}
  function controls(){
    const active=state==='playing'||state==='paused';
    $('state').textContent=({ready:'READY',playing:auto?'AUTO / DEMO':'PLAYING',paused:'PAUSED',finished:'FINISHED'})[state];
    $('pause').disabled=!active;$('pause').textContent=state==='paused'?'继续 / Esc':'暂停 / Esc';
    $('restart').disabled=!active&&state!=='finished';$('start').disabled=busy||active;
    $('quit').disabled=!active&&!busy;
    for(const id of ['chartSelect','offset','auto','chartFile','audioFile','resetAudio'])$(id).disabled=active||busy;
  }
  function feedback(result){
    if(!result)return;
    $('judgement').textContent=result.name.toUpperCase();$('judgement').style.color=result.name==='miss'?'#f19191':'#c5ec7d';
    $('timing').textContent=result.error===null?(result.part==='tail'?'长按中断':'漏击'):auto?'自动演示':`${Math.round(result.error)} ms · ${result.hold?(result.part==='head'?'按住直到尾部':'长按完成'):result.error<0?'偏早':result.error>0?'偏晚':'正好'}`;
    if(result.name!=='miss')flashes[result.lane]=performance.now();lastResult=performance.now();
  }
  function hud(){
    $('score').textContent=session.score.toString().padStart(7,'0');$('accuracy').textContent=session.accuracy===null?'—':session.accuracy.toFixed(2)+'%';
    $('combo').textContent=session.combo;$('maxCombo').textContent=session.maxCombo;
    for(const name of ['perfect','great','good','miss'])$(name+'Count').textContent=session.counts[name];
  }
  function selectChart(){
    audio.stop();pressed.clear();state='ready';chart=charts[Number($('chartSelect').value)];session=new RhythmCore.Session(chart);
    $('songTitle').textContent=chart.title;$('bpmInfo').textContent=chart.bpm+' BPM';$('noteInfo').textContent=chart.notes.length+' NOTES / '+chart.notes.filter(n=>n.endTime!==undefined).length+' LN';$('durationInfo').textContent=fmt(chart.duration);
    $('time').textContent=`00:00 / ${fmt(chart.duration)}`;$('progress').style.width='0';$('judgement').textContent='WAITING';$('timing').textContent='等待第一颗音符';
    $('audioName').textContent=customAudio?'已选择本地音乐':chart.audioId==='karma'?'Karma 示例谱 / 请导入对应本地音乐':'使用内置合成音乐';
    overlay('准备好了吗？','音符落到白线时，按 D / F / J / K。\n长条按住直到尾部，提前松开会中断。');controls();hud();
  }
  function addOption(index){const o=document.createElement('option');o.value=index;o.textContent=charts[index].title;$('chartSelect').append(o);}
  charts.forEach((_,i)=>addOption(i));
  async function start(){
    if(busy||state==='playing'||state==='paused')return;
    const request=++startRequest;
    busy=true;controls();message('准备音频…');
    try{
      await audio.init();if(request!==startRequest)return;audio.setVolume(Number($('volume').value));
      if(customAudio&&customAudio.duration*1000+50<chart.duration)throw new Error('音乐长度小于谱面 duration，请使用更长的音乐或缩短谱面。');
      session=new RhythmCore.Session(chart);updateSpeed();offset=Number($('offset').value);auto=$('auto').checked;
      const buffer=customAudio||await builtInAudio();if(request!==startRequest)return;if($('playView').hidden){message('已取消开始，请返回游玩页面重新开始。');return;}audio.play(buffer,-Math.max(2000,fall+250));state='playing';
      $('overlay').hidden=true;$('judgement').textContent='WAITING';$('timing').textContent='等待第一颗音符';pressed.clear();hud();
      if(canvas.getBoundingClientRect().bottom>window.innerHeight)canvas.scrollIntoView({block:'end',behavior:'smooth'});
      message(auto?'自动演示中 · 不记录成绩':'D / F / J / K 击打 · Esc 暂停');
    }catch(e){if(request===startRequest)message('无法开始：'+e.message);}
    finally{if(request===startRequest){busy=false;controls();}}
  }
  function quit(){startRequest++;audio.stop();pressed.clear();busy=false;selectChart();overlay('已放弃本次练习','音乐已停止，成绩不保存。\n可以换谱面，或点击开始重新练习。');message('已放弃本次练习 · 未保存成绩');}
  function updateSpeed(){const speed=Number($('speed').value);fall=3600/speed;$('speedValue').textContent=speed.toFixed(2)+'×';$('speedHint').textContent=`提前可见 ${(fall/1000).toFixed(2)} 秒 · 视野时长已翻倍，越大越快`;$('liveSpeed').textContent=`下落 ${speed.toFixed(2)}× · F1 / F2 调整`;}
  function pause(){
    if(state==='playing'){audio.pause();state='paused';pressed.clear();overlay('已暂停','按 Esc / Space 或点击「继续」恢复。');message('已暂停 · 音乐与谱面一起停止');}
    else if(state==='paused'){
      if(document.hidden)return;
      const required=auto?[]:session.activeHolds.flatMap((n,i)=>n&&!pressed.has(i)?[keys[i].slice(-1)]:[]);
      if(required.length){message('请先按住长按键 '+required.join(' / ')+'，再按 Esc 或点击继续。');return;}
      audio.init().then(()=>{if(state!=='paused')return;audio.resume();state='playing';$('overlay').hidden=true;controls();message(auto?'自动演示中':'练习继续 · D / F / J / K');}).catch(e=>message(e.message));
    }
    controls();
  }
  function finish(){
    state='finished';audio.stop();pressed.clear();hud();controls();
    const acc=session.accuracy.toFixed(2), label=auto?'演示结束':session.counts.miss===0?'FULL COMBO':'练习完成';
    let best='';
    if(!auto){
      const key='four-best:'+JSON.stringify(chart);
      try{const old=Number(localStorage.getItem(key)||0);const value=Math.max(old,session.score);localStorage.setItem(key,value);best=`\n本地最高分 ${value.toLocaleString()}`;}catch{best='\n浏览器未允许保存本地成绩';}
    }
    overlay(label,`得分 ${session.score.toLocaleString()} · 准确率 ${acc}%\n最大连击 ${session.maxCombo} / ${session.totalJudgements}${best}\n点击「开始练习」再来一次。`);
    message(auto?'自动演示完成 · 未保存成绩':'结算完成 · 可重新练习或导入自己的谱面');
  }
  function hit(lane){
    if(state!=='playing'||auto)return;
    // Process overdue misses before recording the new key, so feedback stays current.
    for(const r of session.advance(audio.time-offset))feedback(r);
    feedback(session.hit(lane,audio.time-offset));hud();
  }
  function clearHeld(){pressed.clear();}
  function release(lane){if(!pressed.has(lane))return;pressed.delete(lane);if(state==='playing'&&!auto){for(const r of session.advance(audio.time-offset))feedback(r);feedback(session.release(lane,audio.time-offset));hud();}}
  window.addEventListener('keydown',e=>{
    if($('playView').hidden)return;
    if(['INPUT','SELECT','TEXTAREA','BUTTON'].includes(e.target.tagName)&&!['speed','volume'].includes(e.target.id))return;
    if(e.code==='F1'||e.code==='F2'){e.preventDefault();if(!e.repeat){$('speed').value=Math.max(.4,Math.min(3,Number($('speed').value)+(e.code==='F1'?-.1:.1)));updateSpeed();}return;}
    if(e.code==='KeyQ'&&(state==='playing'||state==='paused'||busy)){e.preventDefault();if(!e.repeat)quit();return;}
    if(e.code==='Escape'||e.code==='Space'){e.preventDefault();if(e.repeat)return;if(state==='playing'||state==='paused')pause();else if(e.code==='Space')start();return;}
    const lane=keys.indexOf(e.code);if(lane<0)return;e.preventDefault();if(e.repeat||pressed.has(lane))return;pressed.add(lane);hit(lane);
  });
  window.addEventListener('keyup',e=>{const lane=keys.indexOf(e.code);if(lane>=0)release(lane);});
  buttons.forEach((b,lane)=>{
    b.addEventListener('pointerdown',e=>{e.preventDefault();b.setPointerCapture(e.pointerId);if(pressed.has(lane))return;pressed.add(lane);hit(lane);});
    for(const event of ['pointerup','pointercancel','lostpointercapture'])b.addEventListener(event,()=>release(lane));
  });
  window.addEventListener('blur',()=>{clearHeld();if(state==='playing')pause();});
  document.addEventListener('visibilitychange',()=>{if(document.hidden&&state==='playing')pause();});
  document.addEventListener('four-viewchange',e=>{if(e.detail==='editor'&&state==='playing')pause();});
  $('start').addEventListener('click',()=>{$('start').blur();start();});
  $('pause').addEventListener('click',()=>{$('pause').blur();pause();});
  $('restart').addEventListener('click',()=>{audio.stop();state='ready';$('restart').blur();start();});
  $('quit').addEventListener('click',()=>{$('quit').blur();quit();});
  $('chartSelect').addEventListener('change',()=>{customAudio=null;selectChart();});
  for(const id of ['speed','offset','volume'])$(id).addEventListener('input',()=>{
    const value=Number($(id).value);if(id==='speed'){updateSpeed();return;}$(id+'Value').textContent=id==='offset'?value+' ms':Math.round(value*100)+'%';
    if(id==='volume')audio.setVolume(value);
  });
  $('chartFile').addEventListener('change',async e=>{
    const file=e.target.files[0];if(!file)return;
    try{if(file.size>3000000)throw new Error('谱面文件不能超过 3 MB');const imported=RhythmCore.validateChart(JSON.parse(await file.text()));charts.push(imported);addOption(charts.length-1);$('chartSelect').value=charts.length-1;selectChart();message('已导入：'+imported.title);}
    catch(err){message('导入失败：'+err.message);}e.target.value='';
  });
  $('audioFile').addEventListener('change',async e=>{
    const file=e.target.files[0];if(!file)return;busy=true;controls();
    try{if(file.size>100000000)throw new Error('音频文件不能超过 100 MB');message('正在解码本地音乐…');const decoded=await audio.decode(await file.arrayBuffer());customAudio=decoded;$('audioName').textContent=file.name+' · '+fmt(decoded.duration*1000);message('本地音乐已就绪，请选择与它匹配的 JSON 谱面。');}
    catch(err){message('音乐导入失败：'+err.message);}finally{busy=false;controls();e.target.value='';}
  });
  $('resetAudio').addEventListener('click',()=>{customAudio=null;$('audioName').textContent=chart.audioId==='karma'?'Karma 示例谱 / 请导入对应本地音乐':'使用内置合成音乐';message('已恢复谱面自带音乐');});
  document.addEventListener('four-test-chart',e=>{audio.stop();state='ready';charts.push(RhythmCore.validateChart(e.detail.chart));addOption(charts.length-1);$('chartSelect').value=charts.length-1;customAudio=e.detail.buffer;selectChart();message('已加载编辑中的谱面与音乐，点击开始练习试听。');});
  $('export').addEventListener('click',()=>{
    const url=URL.createObjectURL(new Blob([JSON.stringify(chart,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='four-chart.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);message('已导出谱面 JSON，可修改 time / lane 后重新导入。');
  });
  function resize(){const r=canvas.getBoundingClientRect(),dpr=Math.min(devicePixelRatio||1,2);w=r.width;h=r.height;canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);ctx.setTransform(dpr,0,0,dpr,0,0);}
  new ResizeObserver(resize).observe(canvas);
  function draw(now){
    const t=(state==='playing'||state==='paused')?audio.time-offset:state==='finished'?chart.duration+500:-2000;
    if(state==='playing'){
      for(const result of session.advance(t,auto))feedback(result);hud();
      const p=Math.min(1,Math.max(0,audio.time/chart.duration));$('progress').style.width=p*100+'%';$('time').textContent=`${fmt(audio.time)} / ${fmt(chart.duration)}`;
      if(audio.time>=Math.max(chart.duration,session.lastEnd+offset+141))finish();
    }
    ctx.clearRect(0,0,w,h);const left=w*.05,width=w*.90,laneW=width/4,judgeY=h-94,top=0,travel=judgeY-top;
    for(let lane=0;lane<4;lane++){
      ctx.fillStyle=pressed.has(lane)||(auto&&session.activeHolds[lane])?'#293626':lane%2?'#191e23':'#161a1f';ctx.fillRect(left+lane*laneW,0,laneW,h);
      ctx.strokeStyle='#30373e';ctx.beginPath();ctx.moveTo(left+lane*laneW,0);ctx.lineTo(left+lane*laneW,h);ctx.stroke();
      const flash=Math.max(0,1-(now-flashes[lane])/220);if(flash){ctx.fillStyle=`rgba(197,236,125,${flash*.35})`;ctx.fillRect(left+lane*laneW,judgeY-30,laneW,60);}
    }
    // Beat guides are driven by the same audio time as notes and judgments.
    const beat=60000/chart.bpm,gridOffset=chart.gridOffset||0,start=Math.max(Math.ceil(-gridOffset/beat),Math.floor((t-gridOffset)/beat));
    for(let b=start;b*beat+gridOffset<t+fall+beat;b++){const y=judgeY-(b*beat+gridOffset-t)/fall*travel;if(y<0||y>judgeY)continue;ctx.strokeStyle=b%4?'#23292e':'#3d464d';ctx.beginPath();ctx.moveTo(left,y);ctx.lineTo(left+width,y);ctx.stroke();}
    for(const n of session.notes){
      if(n.judged)continue;const x=left+n.lane*laneW+7,y=n.headJudged?judgeY:judgeY-(n.time-t)/fall*travel;
      if(n.endTime!==undefined){const tail=judgeY-(n.endTime-t)/fall*travel;if(y<-16||tail>h)continue;ctx.fillStyle=n.headJudged?'#aacb73':'#64795c';ctx.fillRect(x+9,Math.max(-6,tail),laneW-32,Math.min(h,y)-Math.max(-6,tail));ctx.fillStyle='#c5ec7d';ctx.fillRect(x,tail-4,laneW-14,8);}
      else if(y<-16||y>h)continue;
      ctx.fillStyle=n.endTime!==undefined?'#c5ec7d':n.lane===0||n.lane===3?'#e3e6e8':'#b9c4cc';ctx.fillRect(x,y-6,laneW-14,12);ctx.fillStyle='#ffffff';ctx.fillRect(x,y-6,laneW-14,2);
    }
    ctx.fillStyle='#ecf1e9';ctx.fillRect(left-4,judgeY-1,width+8,3);ctx.fillStyle='#6f7b72';ctx.font='9px monospace';ctx.fillText('HIT LINE',left,judgeY+19);
    buttons.forEach((b,lane)=>b.classList.toggle('pressed',pressed.has(lane)||(auto&&!!session.activeHolds[lane])));
    if(state==='playing'&&lastResult&&now-lastResult>1200){$('judgement').textContent='·';$('timing').textContent='跟上节拍';lastResult=0;}
    requestAnimationFrame(draw);
  }
  updateSpeed();selectChart();resize();requestAnimationFrame(draw);
})();

/* Four-lane tap/hold engine. All times are milliseconds on the audio timeline. */
(function (root) {
  const Judgement = typeof module !== 'undefined' ? require('../vendor/judgement.js') : root.Judgement;
  const windows = [new Judgement('perfect',45,1),new Judgement('great',90,.75),new Judgement('good',140,.5)];
  function validateChart(raw) {
    if (!raw || raw.version !== 1 || raw.keys !== 4) throw new Error('谱面需要 version: 1、keys: 4');
    if (typeof raw.title !== 'string' || !raw.title.trim() || raw.title.length > 100) throw new Error('title 必须是 1–100 字符的标题');
    if (!Number.isFinite(raw.bpm) || raw.bpm < 30 || raw.bpm > 300) throw new Error('bpm 必须在 30–300 之间');
    if (!Number.isFinite(raw.duration) || raw.duration <= 0 || raw.duration > 600000) throw new Error('duration 必须是 0–600000 的毫秒数');
    if (!Array.isArray(raw.notes) || !raw.notes.length || raw.notes.length > 20000) throw new Error('notes 必须包含 1–20000 颗音符');
    if(raw.gridOffset!==undefined&&(!Number.isFinite(raw.gridOffset)||Math.abs(raw.gridOffset)>600000))throw new Error('gridOffset 必须是 ±600000 范围内的毫秒数');
    const seen = new Set();
    const notes = raw.notes.map((n,i) => {
      if (!n || !Number.isInteger(n.lane) || n.lane < 0 || n.lane > 3 || !Number.isFinite(n.time) || n.time < 0 || n.time > raw.duration) throw new Error(`第 ${i+1} 颗音符无效：lane 为 0–3，time 为歌曲范围内的毫秒数`);
      const key = `${n.lane}:${n.time}`;
      if (seen.has(key)) throw new Error(`第 ${i+1} 颗音符与同轨音符重叠`);
      if(n.endTime!==undefined && (!Number.isFinite(n.endTime)||n.endTime<=n.time||n.endTime>raw.duration))throw new Error(`第 ${i+1} 颗长按无效：endTime 必须大于 time 且不超过 duration`);
      seen.add(key); return n.endTime===undefined?{lane:n.lane,time:n.time}:{lane:n.lane,time:n.time,endTime:n.endTime};
    }).sort((a,b)=>a.time-b.time || a.lane-b.lane);
    const occupied=[-1,-1,-1,-1];
    for(const note of notes){if(note.time<occupied[note.lane])throw new Error('同轨音符不能落在长按持续区间内');occupied[note.lane]=note.endTime??note.time;}
    return {version:1,keys:4,title:raw.title.trim(),bpm:raw.bpm,duration:raw.duration,...(raw.audioId==='karma'?{audioId:'karma'}:{}),...(raw.gridOffset!==undefined?{gridOffset:raw.gridOffset}:{}),notes};
  }
  class Session {
    constructor(chart) {
      this.chart = validateChart(chart);
      this.notes = this.chart.notes.map(n=>({...n,judged:false,headJudged:false,tailJudged:false}));
      this.lanes = [0,1,2,3].map(l=>this.notes.filter(n=>n.lane===l));
      this.heads = [0,0,0,0];
      this.activeHolds = [null,null,null,null];
      this.totalJudgements=this.notes.reduce((sum,n)=>sum+(n.endTime===undefined?1:2),0);
      this.lastEnd=Math.max(...this.notes.map(n=>n.endTime??n.time));
      this.counts = {perfect:0,great:0,good:0,miss:0};
      this.combo=0;this.maxCombo=0;this.weight=0;this.judged=0;this.last=null;
    }
    record(note,judgement,error,part='head') {
      const flag=part==='tail'?'tailJudged':'headJudged';
      if (note[flag]) return null;
      note[flag]=true;note.judged=note.headJudged&&(note.endTime===undefined||note.tailJudged);
      if(part==='tail')this.activeHolds[note.lane]=null;
      this.counts[judgement.name]++;this.judged++;this.weight+=judgement.scoreRatio;
      this.combo=judgement.isCombo ? this.combo+1 : 0;this.maxCombo=Math.max(this.maxCombo,this.combo);
      return this.last={name:judgement.name,error,lane:note.lane,part,hold:note.endTime!==undefined};
    }
    advance(time,auto=false) {
      const results=[];
      for(let lane=0;lane<4;lane++) {
        const held=this.activeHolds[lane];
        if(held&&time>=held.endTime)results.push(this.record(held,windows[0],0,'tail'));
        const notes=this.lanes[lane];
        while(this.heads[lane]<notes.length) {
          const note=notes[this.heads[lane]];
          if(note.headJudged){this.heads[lane]++;continue;}
          if(auto && time>=note.time){
            results.push(this.record(note,windows[0],0));this.heads[lane]++;
            if(note.endTime!==undefined){this.activeHolds[lane]=note;if(time>=note.endTime)results.push(this.record(note,windows[0],0,'tail'));}
          }
          else if(time-note.time>140){results.push(this.record(note,Judgement.miss,null));if(note.endTime!==undefined)results.push(this.record(note,Judgement.miss,null,'tail'));this.heads[lane]++;}
          else break;
        }
      }
      return results;
    }
    hit(lane,time) {
      this.advance(time);
      const note=this.lanes[lane]?.[this.heads[lane]];
      if(!note || note.headJudged || this.activeHolds[lane]) return null;
      const error=time-note.time;
      // Adapted from upstream Note.judge: absolute timing error selects a Judgement.
      // Inclusive boundaries and early-input guard avoid consuming the next note.
      const judgement=windows.find(j=>Math.abs(error)<=j.time);
      if(!judgement) return null;
      this.heads[lane]++;const result=this.record(note,judgement,error);
      if(note.endTime!==undefined)this.activeHolds[lane]=note;
      return result;
    }
    release(lane,time){
      const note=this.activeHolds[lane];if(!note)return null;
      const error=time-note.endTime;
      // Holding through the tail completes it automatically; release timing is optional.
      const judgement=error>=0?windows[0]:windows.find(j=>Math.abs(error)<=j.time)||Judgement.miss;
      return this.record(note,judgement,judgement===Judgement.miss?null:error,'tail');
    }
    get score(){return Math.round(this.weight/this.totalJudgements*1000000);}
    get accuracy(){return this.judged ? this.weight/this.judged*100 : null;}
  }
  const api={Session,validateChart,windows};
  if(typeof module!=='undefined') module.exports=api;else root.RhythmCore=api;
})(globalThis);

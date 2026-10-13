/* Pure recording / quantization / segment-editing model; no browser dependencies. */
(function(root){
  const step=(bpm,division)=>240000/bpm/division;
  function snap(time,bpm,division,offset=0){return Math.round((time-offset)/step(bpm,division))*step(bpm,division)+offset;}
  function quantizeNote(note,bpm,division,offset=0,duration=600000){
    const time=Math.max(0,Math.min(duration,Math.round(snap(note.time,bpm,division,offset))));
    if(note.endTime===undefined)return {lane:note.lane,time};
    const endTime=Math.min(duration,Math.max(time+Math.round(step(bpm,division)),Math.round(snap(note.endTime,bpm,division,offset))));
    return endTime>time?{lane:note.lane,time,endTime}:{lane:note.lane,time};
  }
  function normalize(notes){
    const result=[];
    for(let lane=0;lane<4;lane++){
      const list=notes.filter(n=>n.lane===lane).map(n=>({...n})).sort((a,b)=>a.time-b.time),out=[];
      for(const n of list){const prev=out.at(-1);if(prev&&(n.time===prev.time||(prev.endTime!==undefined&&n.time<prev.endTime))){if(n.endTime!==undefined)prev.endTime=Math.max(prev.endTime??prev.time,n.endTime);continue;}out.push(n);}
      result.push(...out);
    }
    return result.sort((a,b)=>a.time-b.time||a.lane-b.lane);
  }
  function mergeTake(existing,take,mode,start,end){
    const kept=mode==='replace'?existing.filter(n=>n.time>=end||(n.endTime!==undefined?n.endTime<=start:n.time<start)):existing;
    return normalize([...kept,...take]);
  }
  class Recorder {
    constructor({bpm=120,division=16,offset=0,start=0,end=600000,threshold=180,snapEnabled=true}={}){
      if(!Number.isFinite(bpm)||bpm<30||bpm>300||![4,8,16,32].includes(division)||!Number.isFinite(offset)||!Number.isFinite(start)||!Number.isFinite(end)||start<0||end<=start)throw new Error('无效的录制设置');
      Object.assign(this,{bpm,division,offset,start,end,threshold,snapEnabled});this.down=new Map();this.notes=[];
    }
    press(lane,time){if(!Number.isInteger(lane)||lane<0||lane>3||time<this.start||time>=this.end||this.down.has(lane))return false;this.down.set(lane,time);return true;}
    release(lane,time){
      if(!this.down.has(lane))return null;const start=this.down.get(lane);this.down.delete(lane);time=Math.min(this.end,Math.max(start,time));
      const raw=time-start>=this.threshold?{lane,time:start,endTime:time}:{lane,time:start};
      const note=this.snapEnabled?quantizeNote(raw,this.bpm,this.division,this.offset,this.end):raw;
      note.time=Math.max(this.start,note.time);if(note.endTime!==undefined&&note.endTime<=note.time)delete note.endTime;
      this.notes.push(note);return note;
    }
    stop(time){for(const lane of [...this.down.keys()])this.release(lane,time);return normalize(this.notes);}
  }
  const api={step,snap,quantizeNote,normalize,mergeTake,Recorder};
  if(typeof module!=='undefined')module.exports=api;else root.ChartEditorCore=api;
})(globalThis);

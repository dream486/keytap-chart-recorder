(function(root){
  class AudioClock {
    constructor(){this.context=null;this.source=null;this.buffer=null;this.pausedAt=0;this.running=false;this.rate=1;}
    async init(){if(!this.context){this.context=new AudioContext();this.gain=this.context.createGain();this.gain.connect(this.context.destination);}await this.context.resume();}
    setVolume(v){if(this.gain)this.gain.gain.setValueAtTime(v,this.context.currentTime);}
    async decode(data){await this.init();return this.context.decodeAudioData(data);}
    synth(chart){
      const rate=this.context.sampleRate,buffer=this.context.createBuffer(1,Math.ceil(chart.duration/1000*rate),rate),data=buffer.getChannelData(0);
      const beat=60/chart.bpm;
      function tone(at,duration,freq,volume,kind){
        const start=Math.round(at*rate),length=Math.round(duration*rate);
        for(let i=0;i<length&&start+i<data.length;i++){
          const t=i/rate,env=Math.exp(-t/(duration/5));
          const sample=kind==='kick'?Math.sin(2*Math.PI*(48*t+6*(1-Math.exp(-t*22)))):Math.sin(2*Math.PI*freq*t);
          data[start+i]+=sample*env*volume*Math.min(1,t*500);
        }
      }
      for(let b=0;b*beat<chart.duration/1000;b++){
        tone(b*beat,.16,b%4===0?1500:1100,.17);
        if(b>=4){tone(b*beat,.23,50,.26,'kick');if(b%2)tone(b*beat,.12,190,.13);tone(b*beat,.3,[130.81,164.81,196,146.83][Math.floor(b/4)%4],.14);}
      }
      // Musical accents follow the demo chart; each key has its own pitch.
      for(const n of chart.notes)tone(n.time/1000,.18,[261.63,329.63,392,523.25][n.lane],.12);
      for(let i=0;i<data.length;i++)data[i]=Math.tanh(data[i]);
      return buffer;
    }
    play(buffer,position=-2000,rate=1){
      if(!Number.isFinite(rate)||rate<.25||rate>4)throw new Error('播放倍率必须在 0.25–4 之间');
      this.stop();this.buffer=buffer;this.pausedAt=position;this.rate=rate;
      const wait=Math.max(0,-position/1000/rate);this.origin=this.context.currentTime-position/1000/rate;
      this.source=this.context.createBufferSource();this.source.buffer=buffer;this.source.connect(this.gain);
      this.source.playbackRate.value=rate;
      if(position/1000<buffer.duration)this.source.start(this.context.currentTime+wait,Math.max(0,position/1000));
      this.running=true;
    }
    get time(){return this.running?(this.context.currentTime-this.origin)*1000*this.rate:this.pausedAt;}
    stop(){if(this.running)this.pausedAt=this.time;if(this.source){try{this.source.stop();}catch{}this.source.disconnect();this.source=null;}this.running=false;}
    pause(){this.pausedAt=this.time;this.stop();}
    resume(){this.play(this.buffer,this.pausedAt,this.rate);}
  }
  root.AudioClock=AudioClock;
})(globalThis);

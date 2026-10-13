(function(root){
  function make(title,bpm,beats,pattern){
    const step=60000/bpm, notes=[];
    for(let beat=4;beat<beats;beat++) for(const [fraction,lane] of pattern(beat)) notes.push({time:Math.round((beat+fraction)*step),lane});
    return {version:1,keys:4,title,bpm,duration:Math.round((beats+2)*step),notes};
  }
  const charts=[
    make('PULSE / 基础练习',120,48,b=>[[0,[0,1,2,3,2,1,0,3][b%8]]]),
    make('FLOW / 八分音符',120,48,b=>b%8<4?[[0,b%4]]:[[0,b%4],[.5,(b+2)%4]]),
    make('PAIR / 双键练习',110,48,b=>b%4===0?[[0,0],[0,3]]:b%4===2?[[0,1],[0,2]]:[[0,b%4]])
  ];
  const holdChart=make('HOLD / 长按练习',120,32,b=>[[0,b%4]]);
  holdChart.notes=holdChart.notes.filter((_,i)=>i%2===0).map((n,i)=>({...n,endTime:n.time+(i%3===0?750:500)}));
  charts.push(holdChart);
  if(typeof module!=='undefined') module.exports=charts;else root.DEMO_CHARTS=charts;
})(globalThis);

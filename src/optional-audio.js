/* Load a user-provided offline music wrapper only when requested. */
(function(root){
  'use strict';
  function createLoader(scope,doc){
    let pending=null;
    const missing=()=>new Error('公开仓库不包含示例歌曲音频。请通过“选择本地音乐”或“导入歌曲”提供对应音频。');
    function loadKarma(){
      if(typeof scope.KARMA_AUDIO_BASE64==='string'&&scope.KARMA_AUDIO_BASE64.length)return Promise.resolve(scope.KARMA_AUDIO_BASE64);
      if(pending)return pending;
      pending=new Promise((resolve,reject)=>{
        const script=doc.createElement('script');
        script.src='assets/karma-audio.js';
        script.onload=()=>{script.remove();if(typeof scope.KARMA_AUDIO_BASE64==='string'&&scope.KARMA_AUDIO_BASE64.length)resolve(scope.KARMA_AUDIO_BASE64);else reject(missing());};
        script.onerror=()=>{script.remove();reject(missing());};
        doc.head.appendChild(script);
      }).catch(error=>{pending=null;throw error;});
      return pending;
    }
    return {loadKarma};
  }
  const api={createLoader};
  if(root.document)Object.assign(api,createLoader(root,root.document));
  if(typeof module!=='undefined')module.exports=api;else root.RhythmOptionalAudio=api;
})(globalThis);

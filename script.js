/* FlyGround — UI logic (GSX-like) */
document.addEventListener("DOMContentLoaded", () => {
  const logEl = document.getElementById("log");
  const queueEl = document.getElementById("queue");
  const autoClose = document.getElementById("autoClose");
  const clearLogBtn = document.getElementById("clearLog");

  const services = {
    pushback: {progressId: 'progress-pushback'},
    catering: {progressId: 'progress-catering'},
    fuel: {progressId: 'progress-fuel'},
    passengers: {progressId: 'progress-passengers'},
    cleaning: {progressId: 'progress-cleaning'}
  };

  function writeLog(msg){
    const t = new Date().toLocaleTimeString();
    logEl.textContent = `[${t}] ${msg}\n` + logEl.textContent;
  }

  function setProgress(id, pct){
    const el = document.getElementById(id);
    if(el) el.style.width = Math.max(0, Math.min(100, pct)) + '%';
  }

  function addQueue(text){
    const li = document.createElement('li');
    li.textContent = text;
    queueEl.prepend(li);
    setTimeout(()=> li.style.background = 'rgba(255,255,255,0.03)', 100);
  }

  // event delegation for service buttons
  document.body.addEventListener('click', async (ev) => {
    const btn = ev.target.closest('button[data-action]');
    if(!btn) return;
    const service = btn.getAttribute('data-service');
    const action = btn.getAttribute('data-action');
    if(!service) return;

    if(action === 'start'){
      startService(service);
    } else if(action === 'cancel'){
      cancelService(service);
    }
  });

  clearLogBtn.addEventListener('click', ()=> logEl.textContent = '');

  function getCallsign(){
    const cs = document.getElementById('callsign');
    return cs ? cs.value.trim() || 'UNKNOWN' : 'UNKNOWN';
  }
  function getGate(){ return (document.getElementById('gate')||{}).value || ''; }

  async function startService(service){
    writeLog(`Solicitando servicio: ${service}`);
    addQueue(`${service} → ${getCallsign()} (${getGate()})`);

    // payload por servicio
    let payload = { callsign: getCallsign(), gate: getGate() };
    if(service === 'pushback'){
      payload.heading = parseFloat(document.getElementById('pushbackHeading').value || 90);
      payload.speed = parseFloat(document.getElementById('pushbackSpeed').value || 1);
    } else if(service === 'catering'){
      payload.time = parseInt(document.getElementById('cateringTime').value || 25);
    } else if(service === 'fuel'){
      payload.amount = parseInt(document.getElementById('fuelAmount').value || 2000);
      payload.time = parseInt(document.getElementById('fuelTime').value || 40);
    } else if(service === 'passengers'){
      payload.time = parseInt(document.getElementById('paxTime').value || 30);
    } else if(service === 'cleaning'){
      payload.time = parseInt(document.getElementById('cleanTime').value || 20);
    }

    // UI feedback y llamada
    writeLog(`Enviando petición al bridge: ${JSON.stringify(payload)}`);
    try {
      const res = await fetch(`http://localhost:5000/service`, {
        method: 'POST',
        headers: {'Content-Type':'application/json'},
        body: JSON.stringify({ service, payload })
      });
      if(!res.ok) throw new Error('HTTP ' + res.status);
      const json = await res.json();
      if(json.ok){
        writeLog(`${service} aceptado por el bridge. Iniciando progreso.`);
        runProgress(service, payload.time || 15);
      } else {
        writeLog(`${service} rechazado: ${JSON.stringify(json)}`);
      }
    } catch (err){
      console.error(err);
      writeLog(`Error al conectar con FlyGroundBridge: ${err.message}`);
      alert('Error: arranca FlyGroundBridge.exe (puente SimConnect) y prueba de nuevo.');
    }
  }

  async function cancelService(service){
    writeLog(`Solicitud de cancelación: ${service}`);
    try {
      const res = await fetch(`http://localhost:5000/service/cancel`, {
        method: 'POST',
        headers: {'Content-Type':'application/json'},
        body: JSON.stringify({ service, callsign: getCallsign() })
      });
      if(!res.ok) throw new Error('HTTP ' + res.status);
      const json = await res.json();
      if(json.ok) writeLog(`${service} cancelado correctamente.`);
      else writeLog(`${service} cancel: respuesta inesperada.`);
      setProgress(services[service].progressId, 0);
    } catch (err){
      writeLog(`Error cancelando: ${err.message}`);
    }
  }

  function runProgress(service, seconds){
    const progId = services[service].progressId;
    let elapsed = 0;
    const total = seconds || 15;
    setProgress(progId, 0);
    const id = setInterval(()=>{
      elapsed++;
      const pct = Math.round( (elapsed/total)*100 );
      setProgress(progId, pct);
      if(elapsed >= total){
        clearInterval(id);
        setProgress(progId, 100);
        writeLog(`${service} COMPLETADO para ${getCallsign()}`);
        addQueue(`${service} COMPLETADO → ${getCallsign()}`);
        if(autoClose && autoClose.checked){
          // si queremos cerrar algo en el futuro podemos notificar al SDK
        }
      }
    }, 1000);
  }

});

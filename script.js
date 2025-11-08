// FlyGround minimal GSX-like menu
const services = [
  { id: 'deboard', name: '1 - Deboarding', note: 'Not available long', enabled: true },
  { id: 'catering', name: '2 - Catering', note: 'Catering not available during', enabled: true },
  { id: 'cargo', name: '3 - Fuel Truck', note: 'Fuel Truck not available', enabled: false },
  { id: 'boarding', name: '4 - Boarding', note: 'Boarding no longer possible', enabled: true },
  { id: 'pushback', name: '5 - Continue Pushback', note: '', enabled: true },
  { id: 'nojetways', name: '6 - No Jetways here', note: '', enabled: false },
  { id: 'operate', name: '7 - Operate Stairs', note: '', enabled: true },
  { id: 'additional', name: '8 - Additional Services', note: '', enabled: true },
  { id: 'reset', name: '9 - Reset position', note: '', enabled: true },
  { id: 'reposition', name: '10 - Reposition Aircraft', note: '', enabled: true }
];

const menuList = document.getElementById('menuList');
const statusEl = document.getElementById('status');
const gateLabel = document.getElementById('gateLabel');
let selectedId = null;

// Build UI
services.forEach((s, idx) => {
  const item = document.createElement('button');
  item.className = 'item';
  if(!s.enabled) item.classList.add('disabled');
  item.setAttribute('data-id', s.id);
  item.setAttribute('aria-label', s.name + (s.note ? ' — ' + s.note : ''));
  item.innerHTML = `
    <div class="index">${idx+1}</div>
    <div class="label">
      <div class="name">${s.name}</div>
      <div class="small">${s.note}</div>
    </div>
    <div class="badge">${s.enabled ? 'OK' : '—'}</div>
  `;
  menuList.appendChild(item);
});

// click handling + keyboard navigation
menuList.addEventListener('click', (e) => {
  const btn = e.target.closest('.item');
  if(!btn || btn.classList.contains('disabled')) return;
  const id = btn.getAttribute('data-id');
  selectItem(id, btn);
  triggerService(id);
});

document.addEventListener('keydown', (e) => {
  if(e.key === 'ArrowDown' || e.key === 'j') moveSelection(1);
  if(e.key === 'ArrowUp' || e.key === 'k') moveSelection(-1);
  if(e.key === 'Enter') {
    if(selectedId) triggerService(selectedId);
  }
});

function moveSelection(dir){
  const items = Array.from(document.querySelectorAll('.item')).filter(i => !i.classList.contains('disabled'));
  if(items.length === 0) return;
  if(!selectedId){
    selectItem(items[0].getAttribute('data-id'), items[0]);
    return;
  }
  const idx = items.findIndex(i => i.getAttribute('data-id') === selectedId);
  let ni = idx + dir;
  if(ni < 0) ni = items.length - 1;
  if(ni >= items.length) ni = 0;
  selectItem(items[ni].getAttribute('data-id'), items[ni]);
}

function selectItem(id, btnEl){
  // clear previous
  document.querySelectorAll('.item.selected').forEach(n => n.classList.remove('selected'));
  btnEl = btnEl || document.querySelector(`.item[data-id="${id}"]`);
  if(btnEl) btnEl.classList.add('selected');
  selectedId = id;
  statusEl.textContent = `Seleccionado: ${btnEl.querySelector('.name').textContent}`;
}

// Trigger service via bridge
async function triggerService(id){
  statusEl.textContent = `Enviando: ${id}...`;
  const payload = { callsign: 'UNKNOWN', gate: gateLabel.textContent };

  // map services -> payload customizations
  if(id === 'pushback'){
    payload.heading = 90; payload.speed = 1;
  } else if(id === 'catering'){
    payload.time = 25;
  } else if(id === 'fuel'){
    payload.amount = 2000; payload.time = 40;
  }

  try {
    const res = await fetch('http://localhost:5000/service', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({ service: id, payload })
    });
    if(!res.ok) throw new Error('HTTP ' + res.status);
    const json = await res.json();
    if(json.ok){
      statusEl.textContent = `Servicio ${id} aceptado.`;
      flashSelected();
    } else {
      statusEl.textContent = `Servicio ${id} rechazado.`;
    }
  } catch (err){
    statusEl.textContent = `Error bridge: arranca FlyGroundBridge`;
    console.error(err);
  }
}

function flashSelected(){
  const el = document.querySelector(`.item.selected`);
  if(!el) return;
  el.style.transition = 'box-shadow .25s, transform .2s';
  el.style.transform = 'scale(1.01)';
  setTimeout(()=>{ el.style.transform = ''; }, 200);
}

// optional: set initial selection to first enabled
(function init(){
  const first = document.querySelector('.item:not(.disabled)');
  if(first) selectItem(first.getAttribute('data-id'), first);
})();

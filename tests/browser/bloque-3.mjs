// Node 24 + Chrome headless con CDP. Usar exclusivamente una instancia local de QA.
import fs from 'node:fs/promises';
const origin = process.env.QA_ORIGIN || 'http://127.0.0.1:5285';
const port = process.env.QA_CDP_PORT || '9225';
const artifacts = process.env.QA_ARTIFACT_DIR || 'artifacts';
await fs.mkdir(artifacts, { recursive: true });
const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
const target = targets.find(t => t.type === 'page');
if (!target) throw new Error('Chrome debe tener una pestaña abierta.');
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { ws.addEventListener('open', resolve); ws.addEventListener('error', reject); });
let sequence = 0, navigations = 0;
const pending = new Map(), checks = [], layouts = [], errors = [];
ws.addEventListener('message', ({ data }) => {
    const m = JSON.parse(data);
    if (m.id) { const p = pending.get(m.id); if (p) { clearTimeout(p.timer); pending.delete(m.id); m.error ? p.reject(m.error) : p.resolve(m.result); } }
    if (m.method === 'Page.frameNavigated' && !m.params.frame.parentId) navigations++;
    if (m.method === 'Runtime.exceptionThrown') errors.push(m.params.exceptionDetails);
    if (m.method === 'Network.responseReceived' && m.params.response.status >= 400) errors.push(m.params.response);
});
function send(method, params = {}) { return new Promise((resolve, reject) => { const id = ++sequence; const timer = setTimeout(() => { pending.delete(id); reject(new Error(method + ' sin respuesta')); }, 15000); pending.set(id, { resolve, reject, timer }); ws.send(JSON.stringify({ id, method, params })); }); }
async function evaluate(expression) { const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true }); if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails)); return r.result.value; }
async function loaded(before) { for (let i = 0; i < 150; i++) { if (navigations > before && await evaluate(`document.readyState==='complete'&&!!document.querySelector('#contenido')`)) { await evaluate('document.fonts.ready.then(()=>true)'); return; } await new Promise(r => setTimeout(r, 100)); } throw new Error('Navegación incompleta'); }
async function navigate(path) { const before = navigations; await send('Page.navigate', { url: origin + path }); await loaded(before); }
async function action(expression) { const before = navigations; await evaluate(expression); await loaded(before); }
function assert(name, value) { checks.push({ name, passed: !!value }); if (!value) throw new Error(name); }
async function viewport(width, height) { await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile: width < 768 }); }
async function profile(value) { await action(`(()=>{const s=document.querySelector('#perfil');s.value=${JSON.stringify(value)};s.dispatchEvent(new Event('change',{bubbles:true}));})()`); }
async function screenshot(name) { const r = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false }); await fs.writeFile(`${artifacts}/bloque3-${name}.png`, Buffer.from(r.data, 'base64')); }
async function inspect(name) {
    const layout = await evaluate(`(()=>{const s=document.querySelector('#perfil').getBoundingClientRect();return{width:innerWidth,documentWidth:document.documentElement.scrollWidth,selectorVisible:s.left>=0&&s.right<=innerWidth,logo:document.querySelector('.logo-frame img').naturalWidth};})()`);
    layouts.push({ name, ...layout }); assert('Sin desbordamiento ' + name, layout.documentWidth <= layout.width); assert('Perfil visible ' + name, layout.selectorVisible); assert('Logo cargado ' + name, layout.logo > 0);
}
try {
    await Promise.all([send('Page.enable'), send('Runtime.enable'), send('Network.enable')]);
    await viewport(1366, 900); await navigate('/'); await profile('Estudiante');
    // Reinicia solo la sesión de QA antes de comprobar los seis ejemplos.
    await action(`(()=>{const f=document.querySelector('#reiniciarDemo form');f.querySelector('[name="confirmar"]').checked=true;f.requestSubmit();})()`);
    await navigate('/Complementos/Indicadores');
    assert('Seis expedientes iniciales', await evaluate(`document.querySelector('[data-kpi="total"]').textContent==='6'`));
    assert('Un expediente finalizado', await evaluate(`document.querySelector('[data-kpi="finalizadas"]').textContent==='1'`));
    assert('Denominador de convalidación explícito', await evaluate(`document.querySelector('[data-kpi="convalidacion"]').parentElement.textContent.includes('3 aprobadas / 6')`));
    const routes = [['/Complementos/Calculadora', 'calculadora'], ['/Complementos/Equivalencias', 'equivalencias'], ['/Complementos/Indicadores', 'indicadores'], ['/Complementos/Proceso', 'proceso'], ['/Complementos/Ayuda', 'ayuda'], ['/Notificaciones', 'notificaciones']];
    for (const [width, height] of [[1366, 900], [1920, 1080], [1024, 768], [768, 1024], [390, 844], [320, 740]]) {
        await viewport(width, height);
        for (const [path, name] of routes) { await navigate(path); await inspect(name + '@' + width); await screenshot(name + '-' + width); }
    }
    await viewport(1366, 900);
    for (const role of ['Estudiante', 'Admision', 'Director', 'Decano', 'RegistrosAcademicos']) {
        await profile(role);
        for (const [path, name] of routes) { await navigate(path); assert(role + ' navega ' + name, await evaluate(`document.querySelector('#perfil').value===${JSON.stringify(role)}`)); }
    }
    await profile('Estudiante'); await navigate('/Complementos/Calculadora');
    await evaluate(`document.querySelector('#AsignaturaId').value='1';document.querySelector('#AsignaturaId').dispatchEvent(new Event('change'));`);
    assert('Consulta de créditos de destino', await evaluate(`document.querySelector('#creditosDestino').textContent==='4'`));
    for (const [percent, credits, affinity, text] of [[80, 4, false, 'referencia general'], [70, 4, true, 'excepción orientativa'], [69, 4, true, 'No alcanza'], [90, 3, false, 'insuficientes']]) {
        await action(`(()=>{const f=document.querySelector('#calculadora');f.querySelector('#AsignaturaId').value='1';f.querySelector('#AsignaturaOrigen').value='Curso ficticio QA';f.querySelector('#CreditosOrigen').value='${credits}';f.querySelector('#Coincidencia').value='${percent}';f.querySelector('#AfinidadDirecta').checked=${affinity};f.requestSubmit();})()`);
        assert('Orientación ' + percent + '% / ' + credits + ' créditos', await evaluate(`document.querySelector('.orientation-result').textContent.includes(${JSON.stringify(text)})&&document.querySelector('.orientation-result').textContent.includes('No constituye aprobación')`));
    }
    await navigate('/Complementos/Equivalencias');
    await action(`(()=>{const f=document.querySelector('.catalog-filters');f.querySelector('#buscar').value='DEMO-103';f.querySelector('#creditos').value='3';f.requestSubmit();})()`);
    assert('Búsqueda y filtro del catálogo', await evaluate(`document.querySelectorAll('[data-catalog-course]').length===1&&document.querySelector('[data-catalog-course]').textContent.includes('Gestión de procesos')`));
    await navigate('/Complementos/Equivalencias');
    await action(`(()=>{const f=document.querySelector('.catalog-filters');f.querySelector('#expedienteId').selectedIndex=1;f.requestSubmit();})()`);
    assert('Comparación con resultados del expediente', await evaluate(`!!document.querySelector('.result-Aprobada')&&!!document.querySelector('.result-Rechazada')`));
    await navigate('/Notificaciones');
    const unread = await evaluate(`Number(document.querySelector('[data-unread]').textContent)`);
    const firstId = await evaluate(`document.querySelector('[data-notification]').dataset.notification`);
    assert('Campana cuenta todos los sin leer', await evaluate(`Number(document.querySelector('.notification-count').textContent)===${unread}&&${unread}>5`));
    await action(`document.querySelector('.notification-item form').requestSubmit()`);
    assert('Marcar leída reduce contador', await evaluate(`Number(document.querySelector('[data-unread]').textContent)===${unread - 1}`));
    await navigate('/Notificaciones');
    assert('Recargar conserva lectura', await evaluate(`Number(document.querySelector('[data-unread]').textContent)===${unread - 1}&&!document.querySelector('[data-notification="${firstId}"] form')`));
    await profile('Director'); await navigate('/Notificaciones');
    assert('Perfil Director tiene avisos propios', await evaluate(`!document.querySelector('[data-notification="${firstId}"]')&&document.querySelector('#contenido').textContent.includes('Director del Departamento Académico')`));
    await profile('Estudiante'); await navigate('/Complementos/Ayuda');
    assert('Ayuda contiene doce respuestas', await evaluate(`document.querySelectorAll('.faq-list details').length===12`));
    await evaluate(`document.querySelectorAll('.faq-list details')[9].open=true`);
    assert('Ayuda explica criterio 80/70', await evaluate(`document.querySelectorAll('.faq-list details')[9].textContent.includes('80%')&&document.querySelectorAll('.faq-list details')[9].textContent.includes('70%')`));
    await viewport(390, 844); await evaluate(`document.querySelector('.menu-toggle').click()`); await new Promise(r => setTimeout(r, 350));
    await evaluate(`document.querySelector('.sidebar-reset').scrollIntoView();document.querySelector('.sidebar-reset').click()`); await new Promise(r => setTimeout(r, 400));
    assert('Confirmación de reinicio visible en móvil', await evaluate(`document.querySelector('#reiniciarDemo').classList.contains('show')&&document.querySelectorAll('.modal-backdrop').length===1&&!document.querySelector('.offcanvas-backdrop')`));
    await screenshot('confirmacion-movil');
    await evaluate(`document.querySelector('#reiniciarDemo [data-bs-dismiss="modal"]').click()`); await new Promise(r => setTimeout(r, 400));
    await navigate('/Notificaciones'); assert('Cancelar conserva avisos leídos', await evaluate(`Number(document.querySelector('[data-unread]').textContent)===${unread - 1}`));
    await viewport(1366, 900); await evaluate(`document.querySelector('.sidebar-reset').click()`); await new Promise(r => setTimeout(r, 350));
    await action(`(()=>{const f=document.querySelector('#reiniciarDemo form');f.querySelector('[name="confirmar"]').checked=true;f.requestSubmit();})()`);
    assert('Reinicio muestra confirmación', await evaluate(`document.querySelector('.page-notice').textContent.includes('Demostración reiniciada')`));
    await navigate('/Notificaciones'); assert('Reinicio recupera avisos iniciales sin leer', await evaluate(`Number(document.querySelector('[data-unread]').textContent)===${unread}`));
    await navigate('/Complementos/Indicadores'); assert('Reinicio recalcula indicadores', await evaluate(`document.querySelector('[data-kpi="total"]').textContent==='6'&&document.querySelector('[data-kpi="finalizadas"]').textContent==='1'`));
    assert('Sin errores JavaScript ni recursos HTTP fallidos', errors.length === 0);
    const report = { checks: checks.length, passed: checks.filter(c => c.passed).length, errors, layouts, results: checks };
    await fs.writeFile(`${artifacts}/bloque3-browser-qa.json`, JSON.stringify(report, null, 2));
    console.log(JSON.stringify({ checks: report.checks, passed: report.passed, errors }, null, 2));
} catch (error) {
    await fs.writeFile(`${artifacts}/bloque3-browser-qa.json`, JSON.stringify({ error: String(error), checks, layouts, errors }, null, 2)); console.error(error); process.exitCode = 1;
} finally { ws.close(); }

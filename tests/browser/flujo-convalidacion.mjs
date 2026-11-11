// Node 24, Chrome con DevTools en localhost y una instancia con una base exclusiva de pruebas.
// Crea datos ficticios. No ejecutar contra una instancia con datos que se quieran conservar intactos.
import fs from 'node:fs/promises';
const origin = process.env.QA_ORIGIN || 'http://127.0.0.1:5285';
const port = process.env.QA_CDP_PORT || '9225';
const artifacts = process.env.QA_ARTIFACT_DIR || 'artifacts';
await fs.mkdir(artifacts, {recursive:true});
const targets = await (await fetch(`http://127.0.0.1:${port}/json/list`)).json();
const target = targets.find(t=>t.type==='page');
if (!target) throw new Error('Abre una pestaña en el Chrome de pruebas.');
const ws = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve);ws.addEventListener('error',reject);});
let sequence=0, navigations=0;
const pending=new Map(), errors=[], checks=[], layouts=[];
ws.addEventListener('message',({data})=>{
    const msg=JSON.parse(data);
    if(msg.id){const p=pending.get(msg.id);if(p){clearTimeout(p.timer);pending.delete(msg.id);msg.error?p.reject(msg.error):p.resolve(msg.result);}}
    if(msg.method==='Page.frameNavigated'&&!msg.params.frame.parentId)navigations++;
    if(msg.method==='Runtime.exceptionThrown')errors.push(msg.params.exceptionDetails);
    if(msg.method==='Log.entryAdded'&&msg.params.entry.level==='error')errors.push(msg.params.entry);
    if(msg.method==='Network.responseReceived'&&msg.params.response.status>=400)errors.push({url:msg.params.response.url,status:msg.params.response.status});
});
function send(method,params={}){return new Promise((resolve,reject)=>{const id=++sequence;const timer=setTimeout(()=>{pending.delete(id);reject(new Error(method+' sin respuesta'));},15000);pending.set(id,{resolve,reject,timer});ws.send(JSON.stringify({id,method,params}));});}
async function evaluate(expression){const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;}
async function loaded(before){for(let i=0;i<150;i++){if(navigations>before&&await evaluate(`document.readyState==='complete'&&!!document.querySelector('#contenido')`)){await evaluate('document.fonts.ready.then(()=>true)');return;}await new Promise(r=>setTimeout(r,100));}throw new Error('Navegación incompleta');}
async function navigate(path){const before=navigations;await send('Page.navigate',{url:origin+path});await loaded(before);}
async function action(expression){const before=navigations;await evaluate(expression);await loaded(before);}
function assert(name,value){checks.push({name,passed:!!value});if(!value)throw new Error(name);}
async function viewport(width,height){await send('Emulation.setDeviceMetricsOverride',{width,height,deviceScaleFactor:1,mobile:width<768});}
async function screenshot(name){await evaluate('new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)))');const r=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});await fs.writeFile(`${artifacts}/flujo-${name}.png`,Buffer.from(r.data,'base64'));}
async function profile(value){await action(`(()=>{const s=document.querySelector('#perfil');s.value=${JSON.stringify(value)};s.dispatchEvent(new Event('change',{bubbles:true}));})()`);assert('Perfil instantáneo '+value,await evaluate(`location.pathname==='/'&&document.querySelector('#perfil').value===${JSON.stringify(value)}`));}
async function activeDetail(path){await action(`document.querySelector('.active-request a').click()`);assert('Expediente activo conservado',await evaluate(`location.pathname===${JSON.stringify(path)}`));}
async function transition(value,extra={}){
    await action(`(()=>{const select=document.querySelector('#Accion');select.value=${JSON.stringify(value)};const form=select.form;form.querySelector('[name="Comentario"]').value='Sustento ficticio para '+${JSON.stringify(value)};for(const [name,value]of Object.entries(${JSON.stringify(extra)})){const input=form.querySelector('[name="'+name+'"]');if(input.type==='checkbox')input.checked=value;else input.value=value;}form.requestSubmit();})()`);
    assert('Transición '+value,await evaluate(`!!document.querySelector('.page-notice')&&!document.querySelector('.validation-summary-errors')`));
}
async function evaluateCourse(index,percent,result,exception=false){
    await action(`(()=>{const f=document.querySelectorAll('.evaluation-form')[${index}];f.querySelector('[name="Coincidencia"]').value=${percent};f.querySelector('[name="Resultado"]').value=${JSON.stringify(result)};f.querySelector('[name="Justificacion"]').value='Evaluación manual ficticia del curso ${index+1}.';f.querySelector('[name="ExcepcionAfinidad"]').checked=${exception};f.querySelector('[name="JustificacionExcepcion"]').value=${JSON.stringify(exception?'Afinidad temática directa ficticia y créditos equivalentes.':'')};f.requestSubmit();})()`);
    assert('Evaluación individual '+index+' '+result,await evaluate(`!!document.querySelector('.page-notice')&&!document.querySelector('.validation-summary-errors')`));
}
async function inspect(name){const m=await evaluate(`(()=>{const r=document.querySelector('#perfil').getBoundingClientRect();return{width:innerWidth,documentWidth:document.documentElement.scrollWidth,bodyWidth:document.body.scrollWidth,selectorVisible:r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight,logo:document.querySelector('.logo-frame img').naturalWidth,profile:document.querySelector('#perfil').value};})()`);layouts.push({name,...m});assert('Sin desbordamiento '+name,m.documentWidth<=m.width&&m.bodyWidth<=m.width);assert('Selector visible '+name,m.selectorVisible);assert('Logotipo cargado '+name,m.logo>0);}

try{
    await Promise.all([send('Page.enable'),send('Runtime.enable'),send('Network.enable'),send('Log.enable')]);
    await viewport(1366,768);await navigate('/');
    assert('Exactamente cinco perfiles',await evaluate(`document.querySelector('#perfil').options.length===5`));
    assert('Sin botón Cambiar',await evaluate(`!Array.from(document.querySelectorAll('.profile-form button')).some(b=>b.getBoundingClientRect().width>0)`));
    for(const [width,height]of [[1366,768],[1920,1080],[1024,768],[768,1024],[390,844],[320,740]]){
        await viewport(width,height);
        for(const role of ['Estudiante','Admision','Director','Decano','RegistrosAcademicos']){
            await profile(role);await inspect(role+' @'+width);await screenshot('panel-'+role.toLowerCase()+'-'+width);
        }
    }
    await viewport(390,844);await evaluate(`document.querySelector('.menu-toggle').click()`);
    await new Promise(r=>setTimeout(r,350));assert('Menú móvil abre',await evaluate(`document.querySelector('#menuLateral').classList.contains('show')`));await screenshot('menu-movil');
    await evaluate(`document.querySelector('.sidebar-close').click()`);await new Promise(r=>setTimeout(r,350));assert('Menú móvil cierra',await evaluate(`!document.querySelector('.offcanvas-backdrop')`));
    await viewport(1366,768);await profile('Estudiante');
    await action(`document.querySelector('.banner-content a').click()`);
    await evaluate(`document.querySelector('#agregarCurso').click();document.querySelector('#agregarCurso').click();document.querySelectorAll('[data-remove-course]')[1].click();document.querySelector('#agregarCurso').click()`);
    assert('Agregar y quitar conserva índices continuos',await evaluate(`document.querySelectorAll('[data-course]').length===3&&!!document.querySelector('[name="Cursos[2].AsignaturaOrigen"]')&&!document.querySelector('[name="Cursos[3].AsignaturaOrigen"]')`));
    await evaluate(`document.querySelector('form[data-submit-once] button[type="submit"]').click()`);
    assert('Validación cliente impide documentos vacíos',await evaluate(`location.pathname==='/Solicitudes/Crear'&&document.querySelectorAll('.input-validation-error').length>0&&!document.querySelector('form[data-submit-once] button[type="submit"]').disabled`));
    // Un formulario nuevo permite capturar el registro sin los mensajes del caso inválido anterior.
    await navigate('/Solicitudes/Crear');
    await evaluate(`document.querySelector('#agregarCurso').click();document.querySelector('#agregarCurso').click()`);
    await evaluate(`document.querySelector('#Estudiante').value='Estudiante QA Flujo';document.querySelector('#InstitucionOrigen').value='Instituto Ficticio QA';document.querySelector('#CertificadoDocumento').value='CERT-DEMO-01';document.querySelectorAll('[data-course]').forEach((row,i)=>{row.querySelector('[name$=".AsignaturaOrigen"]').value=['Matemática ficticia','Programación ficticia','Física ficticia'][i];row.querySelector('[name$=".AsignaturaId"]').value=i+1;row.querySelector('[name$=".CreditosOrigen"]').value=i===2?3:4;row.querySelector('[name$=".Nota"]').value=16;row.querySelector('[name$=".SilaboDocumento"]').value='SIL-DEMO-01';})`);
    await evaluate(`document.querySelector('#cursosSolicitud').scrollIntoView({behavior:'instant',block:'start'})`);
    await screenshot('registro-multiple');await action(`document.querySelector('#formNuevaSolicitud').requestSubmit()`);
    const detail=await evaluate('location.pathname');const id=detail.split('/').at(-1);
    assert('Expediente con tres asignaturas presentado',await evaluate(`document.querySelectorAll('.course-detail').length===3&&!!document.querySelector('.status-Presentada')`));
    await profile('Admision');await activeDetail(detail);await transition('Recibir');await transition('ObservarDocumentos');await transition('DevolverEstudiante');
    await profile('Estudiante');await activeDetail(detail);await screenshot('subsanacion');
    await action(`(()=>{const f=document.querySelector('form[action="/Solicitudes/Subsanar"]');f.querySelector('[name="CertificadoDocumento"]').value='CERT-DEMO-CORREGIDO';f.querySelector('[name="Comentario"]').value='Se reemplaza el certificado ficticio observado.';f.requestSubmit();})()`);
    assert('Subsanación y conservación documental',await evaluate(`!!document.querySelector('.status-Subsanada')&&document.querySelector('#contenido').textContent.includes('CERT-DEMO-01 → CERT-DEMO-CORREGIDO')`));
    await profile('Admision');await activeDetail(detail);await transition('VerificarCorreccion',{DocumentosVerificados:true});await transition('DerivarDepartamento',{DocumentosVerificados:true});
    await profile('Director');await activeDetail(detail);
    await evaluate(`document.querySelector('.evaluation-form').scrollIntoView({behavior:'instant',block:'start'})`);await screenshot('evaluacion-individual');
    await evaluateCourse(0,80,'Aprobada');await evaluateCourse(1,70,'Aprobada',true);await evaluateCourse(2,60,'Rechazada');
    await transition('EmitirDictamen');await transition('RemitirDecano');
    await profile('Decano');await activeDetail(detail);await transition('DevolverDictamen');
    await profile('Director');await activeDetail(detail);
    assert('Devolución exige nueva revisión',await evaluate(`Array.from(document.querySelectorAll('.course-detail .section-heading .status')).every(e=>e.textContent==='Pendiente')`));
    await evaluateCourse(0,80,'Aprobada');await evaluateCourse(1,70,'Aprobada',true);await evaluateCourse(2,60,'Rechazada');await transition('EmitirDictamen');await transition('RemitirDecano');
    await profile('Decano');await activeDetail(detail);await transition('AprobarDictamen');
    const number='DEMO-RD-QA-'+Date.now();await transition('EmitirResolucion',{NumeroResolucion:number,FechaResolucion:'2026-11-09'});
    await transition('Notificar');await transition('RemitirRegistro');
    await profile('Estudiante');assert('Notificación al estudiante',await evaluate(`document.querySelector('.notification-menu').textContent.includes(${JSON.stringify(number)})`));
    await profile('RegistrosAcademicos');assert('Notificación a Registros Académicos',await evaluate(`document.querySelector('.notification-menu').textContent.includes(${JSON.stringify(number)})`));
    await activeDetail(detail);await transition('RegistrarAsignaturas');await transition('Finalizar');
    assert('Carga parcial y finalización',await evaluate(`!!document.querySelector('.status-Finalizada')&&Array.from(document.querySelectorAll('.course-detail .alert-success')).length===2`));
    assert('Historial íntegro con las cinco responsabilidades',await evaluate(`document.querySelectorAll('.timeline li').length===24&&!document.querySelector('.timeline').textContent.includes('Secretaría')&&!document.querySelector('.timeline').textContent.includes('Comisión académica')`));
    for(const [width,height]of [[1366,768],[390,844]]){
        await viewport(width,height);
        for(const [path,name]of [[detail,'expediente-finalizado'],['/Solicitudes/Crear','formulario'],['/Solicitudes/Dictamen/'+id+'?version=1','dictamen-v1'],['/Solicitudes/Dictamen/'+id,'dictamen-v2'],['/Solicitudes/Resolucion/'+id,'resolucion']]){
            await navigate(path);await inspect(name+' @'+width);await screenshot(name+'-'+width);
            if(name==='resolucion')assert('Resolución con aprobación parcial '+width,await evaluate(`document.querySelector('#contenido').textContent.includes('DOCUMENTO ACADÉMICO DE DEMOSTRACIÓN — SIN VALIDEZ OFICIAL.')&&document.querySelectorAll('tbody .result-Aprobada').length===2&&document.querySelectorAll('tbody .result-Rechazada').length===1`));
        }
    }
    await viewport(1366,768);await profile('Estudiante');
    assert('Ningún error JavaScript ni recurso HTTP fallido',errors.length===0);
    const report={checks:checks.length,passed:checks.filter(c=>c.passed).length,errors,detail,number,layouts,results:checks};
    await fs.writeFile(`${artifacts}/flujo-browser-qa.json`,JSON.stringify(report,null,2));
    console.log(JSON.stringify({checks:report.checks,passed:report.passed,errors,detail,number},null,2));
}catch(error){await fs.writeFile(`${artifacts}/flujo-browser-qa.json`,JSON.stringify({error:String(error),checks,layouts,errors},null,2));console.error(error);process.exitCode=1;}
finally{ws.close();}

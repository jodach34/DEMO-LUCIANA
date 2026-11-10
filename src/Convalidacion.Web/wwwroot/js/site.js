// La validación de negocio y los perfiles se comprueban también en el servidor.
document.querySelectorAll('form[data-submit-once]').forEach(form => {
    form.addEventListener('submit', event => {
        if (!form.checkValidity() || (window.jQuery && jQuery.validator && !jQuery(form).valid())) return;
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        form.dataset.submitting = 'true';
        const button = form.querySelector('button[type="submit"]');
        if (button) {
            button.dataset.originalText = button.textContent;
            button.disabled = true;
            button.textContent = 'Registrando…';
        }
    });
});
window.addEventListener('pageshow', () => {
    document.querySelectorAll('form[data-submitting="true"]').forEach(form => {
        delete form.dataset.submitting;
        const button = form.querySelector('button[type="submit"]');
        if (button) { button.disabled = false; button.textContent = button.dataset.originalText; }
    });
});

// El perfil cambia la perspectiva de la sesión y abre su panel; no autentica al usuario.
document.querySelector('#selectorPerfil select')?.addEventListener('change', event => event.target.form.requestSubmit());

const courseContainer = document.querySelector('#cursosSolicitud');
const addCourse = document.querySelector('#agregarCurso');
if (courseContainer && addCourse) {
    const updateCourses = () => {
        const rows = courseContainer.querySelectorAll('[data-course]');
        rows.forEach((row, index) => {
            row.querySelector('[data-course-number]').textContent = index + 1;
            row.querySelector('[data-remove-course]').disabled = rows.length === 1;
            row.querySelectorAll('[name], [id], [for], [data-valmsg-for]').forEach(element => {
                ['name', 'id', 'for', 'data-valmsg-for'].forEach(attribute => {
                    const value = element.getAttribute(attribute);
                    if (value) element.setAttribute(attribute, value.replace(/Cursos\[\d+\]/g, `Cursos[${index}]`).replace(/Cursos_\d+__/g, `Cursos_${index}__`));
                });
            });
        });
        addCourse.disabled = rows.length >= 20;
        document.querySelector('#avisoCursos').textContent = `${rows.length} de 20 asignaturas en este expediente.`;
        if (window.jQuery?.validator?.unobtrusive) {
            const form = jQuery(courseContainer.closest('form'));
            form.removeData('validator').removeData('unobtrusiveValidation');
            jQuery.validator.unobtrusive.parse(form);
        }
    };
    addCourse.addEventListener('click', () => {
        if (courseContainer.children.length >= 20) return;
        const row = courseContainer.querySelector('[data-course]').cloneNode(true);
        row.querySelectorAll('input').forEach(input => {
            input.value = input.name.endsWith('.CreditosOrigen') ? '4' : input.name.endsWith('.Nota') ? '15' : '';
            input.classList.remove('input-validation-error');
        });
        row.querySelectorAll('select').forEach(select => { select.selectedIndex = 0; select.classList.remove('input-validation-error'); });
        row.querySelectorAll('[data-valmsg-for]').forEach(span => { span.textContent = ''; span.classList.remove('field-validation-error'); span.classList.add('field-validation-valid'); });
        courseContainer.append(row);
        updateCourses();
        row.querySelector('input').focus();
    });
    courseContainer.addEventListener('click', event => {
        const remove = event.target.closest('[data-remove-course]');
        if (remove && courseContainer.querySelectorAll('[data-course]').length > 1) { remove.closest('[data-course]').remove(); updateCourses(); }
    });
    updateCourses();
}

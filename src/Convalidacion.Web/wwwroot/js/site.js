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

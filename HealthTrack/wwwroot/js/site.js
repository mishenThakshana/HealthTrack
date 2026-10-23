document.addEventListener('submit', event => {
    const message = event.target.dataset?.confirm;
    if (message && !confirm(message)) {
        event.preventDefault();
    }
});

// Shows or hides fields by the role chosen in the same form (a select or radio buttons named "Role").
// Hidden fields are disabled, so they aren't validated or sent. For example: gender is hidden for admins,
// and each account type has its own section on the registration page.
document.querySelectorAll('[data-show-for-role], [data-hide-for-role]').forEach(wrapper => {
    const form = wrapper.closest('form');
    if (!form?.querySelector('[name$="Role"]')) {
        return;
    }
    const currentRole = () => (form.querySelector('input[name$="Role"]:checked') ?? form.querySelector('[name$="Role"]:not([type=radio])'))?.value;
    const update = () => {
        const role = currentRole();
        const hide = wrapper.dataset.showForRole !== undefined
            ? !wrapper.dataset.showForRole.split(',').includes(role)
            : wrapper.dataset.hideForRole.split(',').includes(role);
        wrapper.hidden = hide;
        wrapper.querySelectorAll('input, select, textarea').forEach(field => field.disabled = hide);
    };
    form.addEventListener('change', event => {
        if (event.target.name?.endsWith('Role')) {
            update();
        }
    });
    update();
});

// A list of inputs with "Add another" and remove buttons, e.g. a trainer's qualifications.
// Names stay numbered 0..n-1 so the server binds them as a list. Only the first input carries validation,
// so removing a row shifts the values up and drops the last row instead of the clicked one.
document.querySelectorAll('[data-repeat-list]').forEach(list => {
    const max = Number(list.dataset.repeatMax) || 10;
    const addButton = list.querySelector('[data-repeat-add]');
    const items = () => [...list.querySelectorAll('[data-repeat-item]')];
    const refresh = () => {
        const all = items();
        all.forEach(item => item.querySelector('[data-repeat-remove]').hidden = all.length === 1);
        addButton.hidden = all.length >= max;
    };

    addButton.addEventListener('click', () => {
        const all = items();
        const copy = all[0].cloneNode(true);
        const input = copy.querySelector('input');
        const index = all.length;
        [...input.attributes].filter(a => a.name.startsWith('data-val') || a.name.startsWith('aria-')).forEach(a => input.removeAttribute(a.name));
        input.classList.remove('input-validation-error', 'valid');
        input.value = '';
        input.placeholder = 'Another qualification';
        input.name = input.name.replace(/\[\d+\]$/, `[${index}]`);
        input.id = input.id.replace(/_\d+_$/, `_${index}_`);
        all[all.length - 1].after(copy);
        refresh();
        input.focus();
    });

    list.addEventListener('click', event => {
        const remove = event.target.closest('[data-repeat-remove]');
        if (!remove) {
            return;
        }
        const all = items();
        const inputs = all.map(item => item.querySelector('input'));
        for (let i = all.indexOf(remove.closest('[data-repeat-item]')); i < inputs.length - 1; i++) {
            inputs[i].value = inputs[i + 1].value;
        }
        all[all.length - 1].remove();
        refresh();
    });
});

// Bar widths come from data attributes because the Content Security Policy blocks inline styles.
document.querySelectorAll('[data-width]').forEach(bar => {
    bar.style.width = `${Math.min(100, Math.max(0, Number(bar.dataset.width) || 0))}%`;
});

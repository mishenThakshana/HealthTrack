document.addEventListener('submit', event => {
    const message = event.target.dataset?.confirm;
    if (message && !confirm(message)) {
        event.preventDefault();
    }
});

// Bar widths come from data attributes because the Content Security Policy blocks inline styles.
document.querySelectorAll('[data-width]').forEach(bar => {
    bar.style.width = `${Math.min(100, Math.max(0, Number(bar.dataset.width) || 0))}%`;
});

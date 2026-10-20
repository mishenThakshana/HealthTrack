(() => {
    const palette = ['#ea580c', '#2a78d6', '#1baf7a', '#6250d6'];
    const reference = '#6b7280';
    const grid = '#f1e4d8';
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
    Chart.defaults.color = '#6b7280';

    const dataset = (series, index, stacked) => {
        const isReference = series.kind === 'reference';
        const color = isReference ? reference : palette[index % palette.length];
        const isBar = series.kind === 'bar';
        return {
            label: series.label,
            data: series.values,
            type: isBar ? 'bar' : 'line',
            borderColor: isBar ? '#fff' : color,
            backgroundColor: color,
            borderWidth: isBar ? (stacked ? 1 : 0) : 2,
            borderRadius: isBar ? 4 : 0,
            borderSkipped: 'start',
            borderDash: isReference ? [6, 4] : [],
            pointRadius: isBar || isReference ? 0 : 4,
            pointHoverRadius: isReference ? 0 : 6,
            pointBackgroundColor: color,
            pointBorderColor: '#fff',
            pointBorderWidth: 2,
            spanGaps: true,
            tension: 0,
            maxBarThickness: 28,
            order: isReference ? 0 : 1
        };
    };

    document.querySelectorAll('canvas[data-chart]').forEach(canvas => {
        const source = document.getElementById(canvas.dataset.chart);
        if (!source) return;
        const data = JSON.parse(source.textContent);
        const multi = data.series.length > 1;
        const counts = data.series.every(s => s.values.every(v => v === null || Number.isInteger(v)));

        new Chart(canvas, {
            data: {
                labels: data.labels,
                datasets: data.series.map((s, i) => dataset(s, i, data.stacked))
            },
            options: {
                maintainAspectRatio: false,
                animation: reducedMotion ? false : undefined,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { display: multi, position: 'bottom', labels: { usePointStyle: true, boxWidth: 8 } },
                    tooltip: {
                        backgroundColor: '#1f2937',
                        padding: 10,
                        filter: item => item.raw !== null
                    }
                },
                scales: {
                    x: { stacked: data.stacked, grid: { display: false }, ticks: { maxRotation: 0, autoSkipPadding: 12 } },
                    y: {
                        stacked: data.stacked,
                        beginAtZero: data.series.some(s => s.kind === 'bar'),
                        grid: { color: grid },
                        ticks: { precision: counts ? 0 : undefined },
                        border: { display: false },
                        title: { display: !!data.yTitle, text: data.yTitle }
                    }
                }
            }
        });
    });
})();

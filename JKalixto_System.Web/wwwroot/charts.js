// Envoltorio fino sobre Chart.js (wwwroot/lib/chart.umd.min.js, autohospedado —
// ver comentario en App.razor) para el Informe Mensual. Cada función recibe el
// id de un <canvas>, destruye cualquier gráfico previo en ese mismo canvas
// (Blazor vuelve a llamar a la misma función cuando cambian los datos, ej. al
// elegir otro mes) y dibuja uno nuevo. Los colores de texto/grilla se leen del
// tema activo (data-theme en <html>) para que el gráfico se vea bien tanto en
// oscuro como en claro.
window.jkalixtoCharts = (function () {
    var instancias = {};

    function colorTexto() {
        var tema = document.documentElement.getAttribute('data-theme') || 'dark';
        return tema === 'light' ? '#1f2023' : '#e8eaed';
    }

    function colorGrilla() {
        var tema = document.documentElement.getAttribute('data-theme') || 'dark';
        return tema === 'light' ? 'rgba(32,33,36,.12)' : 'rgba(255,255,255,.12)';
    }

    function destruir(id) {
        if (instancias[id]) {
            instancias[id].destroy();
            delete instancias[id];
        }
    }

    return {
        barras: function (canvasId, etiquetas, series) {
            destruir(canvasId);
            var el = document.getElementById(canvasId);
            if (!el) { return; }
            instancias[canvasId] = new Chart(el, {
                type: 'bar',
                data: {
                    labels: etiquetas,
                    datasets: series.map(function (s) { return { label: s.nombre, data: s.datos, backgroundColor: s.color }; })
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    // Sin animación a propósito: además de ser lo más apropiado para
                    // un panel gerencial (sin movimiento innecesario), hace que el
                    // gráfico se dibuje en el mismo ciclo síncrono en vez de esperar
                    // un requestAnimationFrame — que el navegador puede pausar
                    // indefinidamente si la pestaña no está visible en ese momento
                    // (ej. se abrió en una pestaña de fondo), dejando el canvas en
                    // blanco hasta que alguien la mire.
                    animation: false,
                    plugins: { legend: { display: series.length > 1, labels: { color: colorTexto() } } },
                    scales: {
                        x: { ticks: { color: colorTexto() }, grid: { color: colorGrilla() } },
                        y: { ticks: { color: colorTexto() }, grid: { color: colorGrilla() } }
                    }
                }
            });
        },

        torta: function (canvasId, etiquetas, datos, colores) {
            destruir(canvasId);
            var el = document.getElementById(canvasId);
            if (!el) { return; }
            instancias[canvasId] = new Chart(el, {
                type: 'pie',
                data: { labels: etiquetas, datasets: [{ data: datos, backgroundColor: colores }] },
                options: { responsive: true, maintainAspectRatio: false, animation: false, plugins: { legend: { position: 'right', labels: { color: colorTexto() } } } }
            });
        },

        linea: function (canvasId, etiquetas, series) {
            destruir(canvasId);
            var el = document.getElementById(canvasId);
            if (!el) { return; }
            instancias[canvasId] = new Chart(el, {
                type: 'line',
                data: {
                    labels: etiquetas,
                    datasets: series.map(function (s) {
                        return { label: s.nombre, data: s.datos, borderColor: s.color, backgroundColor: s.color, tension: 0.3, fill: false };
                    })
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: { legend: { labels: { color: colorTexto() } } },
                    scales: {
                        x: { ticks: { color: colorTexto() }, grid: { color: colorGrilla() } },
                        y: { ticks: { color: colorTexto() }, grid: { color: colorGrilla() } }
                    }
                }
            });
        }
    };
})();

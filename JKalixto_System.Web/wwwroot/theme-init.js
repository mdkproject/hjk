// Aplica el tema guardado ANTES de que se pinte la página (si esto fuera
// parte de theme.js, cargado normalmente, se vería un parpadeo: un instante
// en oscuro y después el cambio a claro).
//
// Vive en un archivo aparte (no inline en App.razor) para poder tener una
// Content-Security-Policy con script-src 'self' sin 'unsafe-inline' — un
// script inline necesitaría un nonce/hash que se recalcula en cada request,
// mientras que un archivo con src= ya cumple esa política tal cual.
(function () {
    try {
        var tema = localStorage.getItem('jkalixto-tema');
        if (tema === 'light' || tema === 'dark') {
            document.documentElement.setAttribute('data-theme', tema);
        }
    } catch (e) { }
})();

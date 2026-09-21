// Tema claro/oscuro — el arranque (aplicar el valor guardado ANTES de
// pintar, para no parpadear) vive en App.razor porque tiene que correr
// antes de que cargue este archivo. Acá solo queda la función que el botón
// de la barra lateral llama para cambiar de tema (ver MainLayout.razor).
window.jkalixtoTema = {
    alternar: function () {
        var actual = document.documentElement.getAttribute('data-theme') || 'dark';
        var nuevo = actual === 'light' ? 'dark' : 'light';
        document.documentElement.setAttribute('data-theme', nuevo);
        try {
            localStorage.setItem('jkalixto-tema', nuevo);
        } catch (e) { }
        return nuevo;
    },
    obtenerActual: function () {
        return document.documentElement.getAttribute('data-theme') || 'dark';
    }
};

(function () {
    var STORAGE_KEY = 'andibanterbot.navHidden';

    function applyState(hidden) {
        document.documentElement.classList.toggle('nav-hidden', hidden);
    }

    // Runs synchronously from <head>, before the sidebar markup exists in the
    // DOM, so the collapsed state applies with no flash of the hidden nav.
    applyState(localStorage.getItem(STORAGE_KEY) === 'true');

    window.toggleNavMenu = function () {
        var hidden = !document.documentElement.classList.contains('nav-hidden');
        applyState(hidden);
        localStorage.setItem(STORAGE_KEY, hidden);
    };
})();

(function () {
    const root = document.documentElement;

    try {
        root.dataset.theme = localStorage.getItem("ra-theme") || "light";
    } catch {
        root.dataset.theme = "light";
    }

    document.addEventListener("click", function (event) {
        if (!(event.target instanceof Element)) return;
        if (!event.target.closest("[data-theme-toggle]")) return;

        root.dataset.theme =
            root.dataset.theme === "dark" ? "light" : "dark";

        try {
            localStorage.setItem("ra-theme", root.dataset.theme);
        } catch {
            // نبود دسترسی storage نباید مانع استفاده از UI شود.
        }
    });
})();
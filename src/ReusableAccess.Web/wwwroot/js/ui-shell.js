(() => {
    const root = document.documentElement;

    function setDark(dark) {
        root.dataset.theme = dark ? "dark" : "light";
        try {
            localStorage.setItem("ra-theme", root.dataset.theme);
        } catch {
            // ذخیرهٔ ترجیح ظاهری برای عملکرد نرم‌افزار الزامی نیست.
        }
    }

    let dark = false;
    try {
        dark = localStorage.getItem("ra-theme") === "dark";
    } catch {
        dark = false;
    }
    root.dataset.theme = dark ? "dark" : "light";

    window.raUi = {
        getDark: () => root.dataset.theme === "dark",
        setDark,
        submit: id => {
            const form = document.getElementById(id);
            if (!(form instanceof HTMLFormElement))
                throw new Error("Form is unavailable.");
            form.requestSubmit();
        }
    };
})();
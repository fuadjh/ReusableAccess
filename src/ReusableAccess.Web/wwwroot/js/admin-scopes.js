export async function initialize() {
    const status = document.getElementById("admin-scopes-status");
    const list = document.getElementById("admin-scopes-list");
    const form = document.getElementById("admin-scope-create-form");
    const codeInput = document.getElementById("scope-code");
    const nameInput = document.getElementById("scope-display-name");
    const parentSelect = document.getElementById("scope-parent");
    const createButton = document.getElementById("scope-create-button");

    let requestToken = "";

    try {
        const tokenResponse = await fetch("/bff/antiforgery-token", {
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!tokenResponse.ok) {
            throw new Error("دریافت توکن ضد-CSRF ناموفق بود.");
        }

        ({ requestToken } = await tokenResponse.json());
        form.addEventListener("submit", createScope);
        await loadScopes();
    } catch (error) {
        status.textContent = error.message;
    }

    async function loadScopes() {
        const response = await fetch("/bff/admin/scopes", {
            credentials: "same-origin",
            cache: "no-store"
        });

        const result = await response.json().catch(() => ({}));

        if (response.status === 403) {
            form.hidden = true;
            status.textContent = "این بخش فقط برای مدیر سراسری مجاز است.";
            return;
        }

        if (!response.ok) {
            throw new Error(
                result.detail ?? result.message ?? `خطا: ${response.status}`
            );
        }

        const scopes = result.data ?? [];
        renderParentOptions(scopes);
        renderScopes(scopes);
        status.textContent = `تعداد Scopeها: ${scopes.length}`;
    }

    function renderParentOptions(scopes) {
        parentSelect.replaceChildren();

        const rootOption = document.createElement("option");
        rootOption.value = "";
        rootOption.textContent = "بدون والد";
        parentSelect.appendChild(rootOption);

        for (const scope of scopes) {
            const option = document.createElement("option");
            option.value = scope.scopeId;
            option.textContent = `${scope.displayName} (${scope.scopeCode})`;
            parentSelect.appendChild(option);
        }
    }

    function renderScopes(scopes) {
        list.replaceChildren();

        for (const scope of scopes) {
            const item = document.createElement("li");
            const label = document.createElement("span");
            label.textContent =
                `${scope.displayName} (${scope.scopeCode})`;

            const editButton = document.createElement("button");
            editButton.type = "button";
            editButton.textContent = "ویرایش نام";
            editButton.addEventListener("click", () => editScope(scope));

            item.append(
                label,
                document.createTextNode(" — "),
                editButton
            );
            list.appendChild(item);
        }
    }

    async function createScope(event) {
        event.preventDefault();
        createButton.disabled = true;
        status.textContent = "در حال ایجاد Scope...";

        try {
            await sendJson("/bff/admin/scopes", "POST", {
                code: codeInput.value.trim(),
                displayName: nameInput.value.trim(),
                parentScopeId: parentSelect.value || null
            });

            form.reset();
            await loadScopes();
            status.textContent = "Scope ایجاد شد.";
        } catch (error) {
            status.textContent = error.message;
        } finally {
            createButton.disabled = false;
        }
    }

    async function editScope(scope) {
        const displayName = window.prompt(
            "نام نمایشی جدید:",
            scope.displayName
        );

        if (displayName === null || !displayName.trim()) {
            return;
        }

        try {
            await sendJson(
                `/bff/admin/scopes/${encodeURIComponent(scope.scopeId)}`,
                "PUT",
                { displayName: displayName.trim() }
            );

            await loadScopes();
            status.textContent = "نام Scope به‌روزرسانی شد.";
        } catch (error) {
            status.textContent = error.message;
        }
    }

    async function sendJson(url, method, body) {
        const response = await fetch(url, {
            method,
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "X-CSRF-TOKEN": requestToken
            },
            body: JSON.stringify(body)
        });

        const result = await response.json().catch(() => ({}));

        if (!response.ok) {
            throw new Error(
                result.detail
                ?? result.message
                ?? result.title
                ?? `خطا: ${response.status}`
            );
        }

        return result;
    }
}
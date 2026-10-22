export async function initialize() {
    const status = document.getElementById("admin-bundles-status");
    const searchInput = document.getElementById("bundle-user-search");
    const searchButton = document.getElementById("bundle-user-search-button");
    const userResults = document.getElementById("bundle-user-results");
    const bundleList = document.getElementById("admin-bundles-list");
    const form = document.getElementById("admin-bundle-form");
    const nameInput = document.getElementById("bundle-name");
    const rolesSelect = document.getElementById("bundle-roles");
    const modeSelect = document.getElementById("bundle-scope-mode");
    const scopesSelect = document.getElementById("bundle-scopes");
    const orderInput = document.getElementById("bundle-sort-order");
    const enabledInput = document.getElementById("bundle-enabled");
    const saveButton = document.getElementById("bundle-save-button");
    const cancelButton = document.getElementById("bundle-cancel-button");
    const formTitle = document.getElementById("bundle-form-title");

    let requestToken = "";
    let selectedUser = null;
    let editingBundleId = null;

    try {
        const tokenResponse = await fetch("/bff/antiforgery-token", {
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!tokenResponse.ok) {
            throw new Error("دریافت توکن ضد-CSRF ناموفق بود.");
        }

        ({ requestToken } = await tokenResponse.json());

        const [roles, scopes] = await Promise.all([
            getData("/bff/admin/roles"),
            getData("/bff/admin/scopes")
        ]);

        fillOptions(rolesSelect, roles, "roleId", "displayName", "roleCode");
        fillOptions(scopesSelect, scopes, "scopeId", "displayName", "scopeCode");

        searchButton.addEventListener("click", searchUsers);
        form.addEventListener("submit", saveBundle);
        cancelButton.addEventListener("click", resetForm);

        modeSelect.addEventListener("change", () => {
            scopesSelect.disabled = modeSelect.value === "all";
        });
    } catch (error) {
        status.textContent = error.message;
    }

    async function getData(url) {
        const response = await fetch(url, {
            credentials: "same-origin",
            cache: "no-store"
        });
        const result = await response.json().catch(() => ({}));

        if (!response.ok) {
            throw new Error(
                result.detail ?? result.message ?? `خطا: ${response.status}`
            );
        }

        return result.data ?? [];
    }

    function fillOptions(select, items, idKey, nameKey, codeKey) {
        select.replaceChildren();

        for (const item of items) {
            const option = document.createElement("option");
            option.value = item[idKey];
            option.textContent = `${item[nameKey]} (${item[codeKey]})`;
            select.appendChild(option);
        }
    }

    async function searchUsers() {
        const query = searchInput.value.trim();

        if (query.length < 2) {
            status.textContent = "حداقل دو حرف وارد کنید.";
            return;
        }

        try {
            const users = await getData(
                `/bff/admin/users?q=${encodeURIComponent(query)}`
            );

            userResults.replaceChildren();

            for (const user of users) {
                const item = document.createElement("li");
                const button = document.createElement("button");
                button.type = "button";
                button.textContent =
                    `${user.displayName} (${user.userName})`;
                button.addEventListener("click", () => selectUser(user));
                item.appendChild(button);
                userResults.appendChild(item);
            }

            status.textContent = `کاربران پیدا‌شده: ${users.length}`;
        } catch (error) {
            status.textContent = error.message;
        }
    }

    async function selectUser(user) {
        selectedUser = user;
        resetForm();
        status.textContent = `Bundleهای ${user.displayName}`;

        try {
            const bundles = await getData(
                `/bff/admin/users/${encodeURIComponent(user.userId)}/bundles`
            );

            bundleList.replaceChildren();

            for (const bundle of bundles) {
                const item = document.createElement("li");
                const label = document.createElement("span");
                label.textContent =
                    `${bundle.name} — ${bundle.isEnabled ? "فعال" : "غیرفعال"}`;

                const edit = document.createElement("button");
                edit.type = "button";
                edit.textContent = "ویرایش";
                edit.addEventListener("click", () => editBundle(bundle));

                item.append(label, document.createTextNode(" — "), edit);
                bundleList.appendChild(item);
            }

            form.hidden = false;
            status.textContent =
                `${user.displayName}: ${bundles.length} Bundle`;

        } catch (error) {
            status.textContent = error.message;
        }
    }

    function editBundle(bundle) {
        editingBundleId = bundle.bundleId;
        formTitle.textContent = "ویرایش Bundle";
        nameInput.value = bundle.name;
        orderInput.value = bundle.sortOrder;
        enabledInput.checked = bundle.isEnabled;
        modeSelect.value = bundle.scopeMode;
        scopesSelect.disabled = bundle.scopeMode === "all";

        setSelectedOptions(rolesSelect, bundle.roleIds);
        setSelectedOptions(scopesSelect, bundle.scopeIds);
        saveButton.textContent = "ذخیرهٔ تغییرات";
        form.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    function setSelectedOptions(select, ids) {
        const selected = new Set(ids);
        for (const option of select.options) {
            option.selected = selected.has(option.value);
        }
    }

    function selectedIds(select) {
        return Array.from(select.selectedOptions, option => option.value);
    }

    async function saveBundle(event) {
        event.preventDefault();

        if (!selectedUser) {
            status.textContent = "ابتدا یک کاربر انتخاب کنید.";
            return;
        }

        const roleIds = selectedIds(rolesSelect);
        const scopeMode = modeSelect.value;
        const scopeIds =
            scopeMode === "all" ? [] : selectedIds(scopesSelect);

        if (roleIds.length === 0) {
            status.textContent = "حداقل یک Role انتخاب کنید.";
            return;
        }
        if (scopeMode === "selected" && scopeIds.length === 0) {
            status.textContent = "در حالت انتخابی حداقل یک Scope لازم است.";
            return;
        }

        const request = {
            name: nameInput.value.trim(),
            roleIds,
            scopeMode,
            scopeIds,
            sortOrder: Number(orderInput.value),
            isEnabled: enabledInput.checked
        };

        const creating = editingBundleId === null;
        const url = creating
            ? `/bff/admin/users/${encodeURIComponent(selectedUser.userId)}/bundles`
            : `/bff/admin/bundles/${encodeURIComponent(editingBundleId)}`;

        saveButton.disabled = true;
        try {
            await sendJson(url, creating ? "POST" : "PUT", request);
            resetForm();
            await selectUser(selectedUser);
            status.textContent = creating
                ? "Bundle ایجاد شد."
                : "Bundle به‌روزرسانی شد.";
        } catch (error) {
            status.textContent = error.message;
        } finally {
            saveButton.disabled = false;
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

    function resetForm() {
        editingBundleId = null;
        form.reset();
        modeSelect.value = "selected";
        scopesSelect.disabled = false;
        enabledInput.checked = true;
        formTitle.textContent = "ایجاد Bundle";
        saveButton.textContent = "ذخیرهٔ Bundle";
        form.hidden = selectedUser === null;
    }
}
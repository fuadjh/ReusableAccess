export async function initialize() {
    const status = document.getElementById("admin-roles-status");
    const list = document.getElementById("admin-roles-list");
    const form = document.getElementById("admin-role-create-form");
    const codeInput = document.getElementById("role-code");
    const nameInput = document.getElementById("role-display-name");
    const createButton = document.getElementById("role-create-button");

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

        form.addEventListener("submit", createRole);
        await loadRoles();
    } catch (error) {
        status.textContent = error.message;
    }

    async function loadRoles() {
        status.textContent = "در حال دریافت نقش‌ها...";

        const response = await fetch("/bff/admin/roles", {
            credentials: "same-origin",
            cache: "no-store"
        });

        const result = await response.json().catch(() => ({}));

        if (response.status === 403) {
            status.textContent = "این بخش فقط برای مدیر سراسری مجاز است.";
            form.hidden = true;
            return;
        }

        if (!response.ok) {
            throw new Error(
                result.detail ?? result.message ?? `خطا: ${response.status}`
            );
        }

        const roles = result.data ?? [];
        list.replaceChildren();

        for (const role of roles) {
            const item = document.createElement("li");
            const label = document.createElement("span");
            label.textContent = `${role.displayName} (${role.roleCode})`;

            const editButton = document.createElement("button");
            editButton.type = "button";
            editButton.textContent = "ویرایش نام";
            editButton.addEventListener("click", () => editRole(role));

            const permissionsLink = document.createElement("a");
            permissionsLink.href =
                `/admin/roles/${encodeURIComponent(role.roleId)}/permissions`;
            permissionsLink.textContent = "مدیریت مجوزها";

            item.append(
                label,
                document.createTextNode(" — "),
                editButton,
                document.createTextNode(" — "),
                permissionsLink
            );
            list.appendChild(item);
        }

        status.textContent = `تعداد نقش‌ها: ${roles.length}`;
    }

    async function createRole(event) {
        event.preventDefault();
        createButton.disabled = true;
        status.textContent = "در حال ایجاد نقش...";

        try {
            await sendJson("/bff/admin/roles", "POST", {
                code: codeInput.value.trim(),
                displayName: nameInput.value.trim()
            });

            form.reset();
            await loadRoles();
            status.textContent = "نقش ایجاد شد.";
        } catch (error) {
            status.textContent = error.message;
        } finally {
            createButton.disabled = false;
        }
    }

    async function editRole(role) {
        const displayName = window.prompt(
            "نام نمایشی جدید نقش:",
            role.displayName
        );

        if (displayName === null || !displayName.trim()) {
            return;
        }

        try {
            await sendJson(
                `/bff/admin/roles/${encodeURIComponent(role.roleId)}`,
                "PUT",
                { displayName: displayName.trim() }
            );

            await loadRoles();
            status.textContent = "نام نمایشی نقش به‌روزرسانی شد.";
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
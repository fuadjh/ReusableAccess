export async function initialize(roleId) {
    const search = document.getElementById("permission-search");
    const saveButton = document.getElementById("permission-save");
    const status = document.getElementById("permissions-status");
    const list = document.getElementById("permissions-list");

    let permissions = [];
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

        const response = await fetch(
            `/bff/admin/roles/${encodeURIComponent(roleId)}/permissions`,
            {
                credentials: "same-origin",
                cache: "no-store"
            }
        );

        if (response.status === 403) {
            status.textContent = "این بخش فقط برای مدیر سراسری مجاز است.";
            return;
        }

        const result = await response.json().catch(() => ({}));

        if (!response.ok) {
            throw new Error(
                result.detail ?? result.message ?? `خطا: ${response.status}`
            );
        }

        permissions = result.data ?? [];
        render();

        search.addEventListener("input", render);
        saveButton.disabled = false;
        saveButton.addEventListener("click", save);
        status.textContent = `تعداد مجوزها: ${permissions.length}`;
    } catch (error) {
        status.textContent = error.message;
    }

    function render() {
        const query = search.value.trim().toLowerCase();
        const visible = permissions.filter(permission =>
            `${permission.key} ${permission.description} ${permission.category}`
                .toLowerCase()
                .includes(query)
        );

        list.replaceChildren();

        let currentCategory = null;
        let categoryList = null;

        for (const permission of visible) {
            if (permission.category !== currentCategory) {
                currentCategory = permission.category;

                const heading = document.createElement("h2");
                heading.textContent = currentCategory;
                list.appendChild(heading);

                categoryList = document.createElement("div");
                list.appendChild(categoryList);
            }

            const label = document.createElement("label");
            const checkbox = document.createElement("input");

            checkbox.type = "checkbox";
            checkbox.checked = permission.isSelected;
            checkbox.addEventListener("change", () => {
                permission.isSelected = checkbox.checked;
            });

            label.append(
                checkbox,
                document.createTextNode(
                    ` ${permission.key} — ${permission.description}`
                )
            );

            categoryList.appendChild(label);
        }

        if (visible.length === 0) {
            list.textContent = "مجوزی با این جست‌وجو پیدا نشد.";
        }
    }

    async function save() {
        saveButton.disabled = true;
        status.textContent = "در حال ذخیرهٔ مجوزها...";

        try {
            const response = await fetch(
                `/bff/admin/roles/${encodeURIComponent(roleId)}/permissions`,
                {
                    method: "PUT",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        "X-CSRF-TOKEN": requestToken
                    },
                    body: JSON.stringify({
                        permissionKeys: permissions
                            .filter(permission => permission.isSelected)
                            .map(permission => permission.key)
                    })
                }
            );

            const result = await response.json().catch(() => ({}));

            if (!response.ok) {
                throw new Error(
                    result.detail
                    ?? result.message
                    ?? result.title
                    ?? `خطا: ${response.status}`
                );
            }

            status.textContent = "مجوزهای نقش ذخیره شدند.";
        } catch (error) {
            status.textContent = error.message;
        } finally {
            saveButton.disabled = false;
        }
    }
}
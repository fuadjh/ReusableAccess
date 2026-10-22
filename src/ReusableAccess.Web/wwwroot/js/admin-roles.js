export async function initialize() {
    const status = document.getElementById("admin-roles-status");
    const list = document.getElementById("admin-roles-list");

    try {
        const response = await fetch("/bff/admin/roles", {
            credentials: "same-origin",
            cache: "no-store"
        });

        if (response.status === 403) {
            status.textContent =
                "دسترسی این صفحه فقط برای مدیر سراسری مجاز است.";
            return;
        }

        const responseText = await response.text();
        let result = {};

        try {
            result = responseText ? JSON.parse(responseText) : {};
        } catch {
            // پاسخ غیر JSON را برای کاربر نمایش نمی‌دهیم.
        }

        if (!response.ok) {
            status.textContent =
                result.detail
                ?? result.title
                ?? `دریافت نقش‌ها ناموفق بود: ${response.status}`;
            return;
        }

        const roles = result.data ?? [];
        list.replaceChildren();

        if (roles.length === 0) {
            status.textContent = "نقشی ثبت نشده است.";
            return;
        }

        for (const role of roles) {
            const item = document.createElement("li");
            item.textContent =
                `${role.displayName} (${role.roleCode})`;
            list.appendChild(item);
            const permissionsLink = document.createElement("a");
            permissionsLink.href =
                `/admin/roles/${encodeURIComponent(role.roleId)}/permissions`;
            permissionsLink.textContent = "مدیریت مجوزها";
            item.append(document.createTextNode(" — "), permissionsLink);
        }

        status.textContent = `تعداد نقش‌ها: ${roles.length}`;
    } catch {
        status.textContent = "ارتباط با سرویس مدیریت برقرار نشد.";
    }
}
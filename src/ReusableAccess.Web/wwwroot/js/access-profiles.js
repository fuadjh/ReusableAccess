export async function initialize() {
    const select = document.getElementById("access-profile-select");
    const button = document.getElementById("access-profile-submit");
    const status = document.getElementById("access-profile-status");

    try {
        const tokenResponse = await fetch("/bff/antiforgery-token", {
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!tokenResponse.ok) {
            throw new Error("دریافت توکن ضد-CSRF ناموفق بود.");
        }

        const { requestToken } = await tokenResponse.json();

        const profilesResponse = await fetch("/bff/access-profiles", {
            credentials: "same-origin",
            cache: "no-store"
        });

        if (!profilesResponse.ok) {
            throw new Error(
                `دریافت پروفایل‌ها ناموفق بود: ${profilesResponse.status}`
            );
        }

        const result = await profilesResponse.json();
        const profiles = result.data ?? [];

        select.replaceChildren();

        if (profiles.length === 0) {
            select.add(new Option("پروفایل فعالی وجود ندارد", ""));
            status.textContent = "برای این حساب پروفایل فعالی پیدا نشد.";
            return;
        }

        profiles.forEach((profile, index) => {
            const scopeLabel = profile.scopeMode === "all"
                ? "همهٔ اسکوپ‌ها"
                : profile.scopeCode;

            const label =
                `${profile.bundleName} — ${profile.roleCode} — ${scopeLabel}`;

            select.add(new Option(label, String(index)));
        });

        const preferred = result.selectedProfile;
        const preferredIndex = preferred
            ? profiles.findIndex(profile =>
                profile.bundleId === preferred.bundleId
                && profile.roleId === preferred.roleId
                && profile.scopeId === preferred.scopeId)
            : -1;

        select.value = String(preferredIndex >= 0 ? preferredIndex : 0);
        select.disabled = false;
        button.disabled = false;

        async function activateSelectedProfile() {
            const profile = profiles[Number(select.value)];

            if (!profile) {
                status.textContent = "یک پروفایل انتخاب کن.";
                return;
            }

            button.disabled = true;
            status.textContent = "در حال ذخیرهٔ انتخاب...";

            try {
                const response = await fetch(
                    "/bff/access-profile/select",
                    {
                        method: "POST",
                        credentials: "same-origin",
                        headers: {
                            "Content-Type": "application/json",
                            "X-CSRF-TOKEN": requestToken
                        },
                        body: JSON.stringify({
                            bundleId: profile.bundleId,
                            roleCode: profile.roleCode,
                            scopeCode: profile.scopeCode,
                            scopeMode: profile.scopeMode
                        })
                    }
                );

                const body = await response.json().catch(() => ({}));

                if (!response.ok) {
                    throw new Error(
                        body.detail
                        ?? body.title
                        ?? `خطا: ${response.status}`
                    );
                }

                const scopeLabel = body.scopeMode === "all"
                    ? "همهٔ اسکوپ‌ها"
                    : body.scopeCode;

                status.textContent =
                    `پروفایل فعال شد: ${body.roleCode} — ${scopeLabel}`;
            } catch (error) {
                status.textContent = error.message;
            } finally {
                button.disabled = false;
            }
        }

        button.addEventListener("click", activateSelectedProfile);

        // ترجیح ذخیره‌شده، یا اولین پروفایل برگشتی از API، فعال می‌شود.
        await activateSelectedProfile();
    } catch (error) {
        status.textContent = error.message;
    }
}
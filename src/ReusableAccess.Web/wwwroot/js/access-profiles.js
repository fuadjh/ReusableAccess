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
            throw new Error(`دریافت پروفایل‌ها ناموفق بود: ${profilesResponse.status}`);
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
            const label = `${profile.roleCode} — ${profile.scopeCode}`;
            select.add(new Option(label, String(index)));
        });

        select.disabled = false;
        button.disabled = false;

        button.addEventListener("click", async () => {
            const profile = profiles[Number(select.value)];

            if (!profile) {
                status.textContent = "یک پروفایل انتخاب کن.";
                return;
            }

            button.disabled = true;
            status.textContent = "در حال ذخیرهٔ انتخاب...";

            try {
                const response = await fetch("/bff/access-profile/select", {
                    method: "POST",
                    credentials: "same-origin",
                    headers: {
                        "Content-Type": "application/json",
                        "X-CSRF-TOKEN": requestToken
                    },
                    body: JSON.stringify({
                        roleCode: profile.roleCode,
                        scopeCode: profile.scopeCode
                    })
                });

                const body = await response.json().catch(() => ({}));

                if (!response.ok) {
                    throw new Error(
                        body.detail ?? body.title ?? `خطا: ${response.status}`
                    );
                }

                status.textContent =
                    `پروفایل فعال شد: ${body.roleCode} — ${body.scopeCode}`;
            } catch (error) {
                status.textContent = error.message;
            } finally {
                button.disabled = false;
            }
        });
    } catch (error) {
        status.textContent = error.message;
    }
}
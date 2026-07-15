(function () {
    "use strict";

    // ---------- Password show/hide toggle (: shows, ; hides) ----------

    document.querySelectorAll('input[type="password"]').forEach(input => {
        const wrap = document.createElement("div");
        wrap.className = "pw-wrap";
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        const toggle = document.createElement("button");
        toggle.type = "button";
        toggle.className = "pw-toggle";
        toggle.textContent = ":";
        toggle.title = "Show password";

        toggle.addEventListener("click", () => {
            const show = input.type === "password";
            input.type = show ? "text" : "password";
            toggle.textContent = show ? ";" : ":";
            toggle.title = show ? "Hide password" : "Show password";
            input.focus();
        });

        wrap.appendChild(toggle);
    });

    // ---------- Live validation ----------

    const email = document.querySelector('input[name="Email"]');
    const fullName = document.querySelector('input[name="FullName"]');
    const password = document.querySelector('input[name="Password"]');
    const confirm = document.querySelector('input[name="ConfirmPassword"]');

    function liveMessage(afterEl, id) {
        let el = document.getElementById(id);
        if (!el) {
            el = document.createElement("span");
            el.id = id;
            el.className = "live-error";
            const anchor = afterEl.closest(".pw-wrap") || afterEl;
            anchor.parentNode.insertBefore(el, anchor.nextSibling);
        }
        return el;
    }

    // Email availability (registration page only: it has both FullName and Password).
    if (email && fullName && password) {
        const msg = liveMessage(email, "emailTakenMsg");
        let timer = null;

        email.addEventListener("input", () => {
            msg.textContent = "";
            clearTimeout(timer);
            const value = email.value.trim();
            if (!value || !/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(value)) return;

            timer = setTimeout(async () => {
                try {
                    const res = await fetch("/Account/CheckEmail?email=" + encodeURIComponent(value));
                    const data = await res.json();
                    if (data.taken && email.value.trim() === value)
                        msg.textContent = "This email is already registered. Try signing in instead.";
                } catch { /* network hiccup — server-side validation still applies */ }
            }, 400);
        });
    }

    // Password requirements checklist.
    if (password && confirm) {
        const rules = [
            { test: v => v.length >= 8, label: "At least 8 characters" },
            { test: v => /[A-Z]/.test(v), label: "One capital letter" },
            { test: v => /\d/.test(v), label: "One number" },
            { test: v => /[^A-Za-z0-9]/.test(v), label: "One special character" }
        ];

        const list = document.createElement("ul");
        list.className = "pw-rules";
        list.innerHTML = rules.map(r => `<li><span class="rule-dot"></span>${r.label}</li>`).join("");
        const anchor = password.closest(".pw-wrap") || password;
        anchor.parentNode.insertBefore(list, anchor.nextSibling);

        const items = list.querySelectorAll("li");
        const matchMsg = liveMessage(confirm, "pwMatchMsg");

        function checkRules() {
            rules.forEach((r, i) => items[i].classList.toggle("ok", r.test(password.value)));
        }

        function checkMatch() {
            matchMsg.textContent =
                confirm.value && confirm.value !== password.value ? "Passwords do not match." : "";
        }

        password.addEventListener("input", () => { checkRules(); checkMatch(); });
        confirm.addEventListener("input", checkMatch);
        checkRules();
    }
})();

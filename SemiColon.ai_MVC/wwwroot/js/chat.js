(function () {
    "use strict";

    let currentSessionId = null;
    let isSending = false;

    const messagesEl = document.getElementById("messages");
    const welcomeEl = document.getElementById("welcome");
    const inputEl = document.getElementById("messageInput");
    const sendBtn = document.getElementById("sendBtn");
    const sessionList = document.getElementById("sessionList");
    const chatTitle = document.getElementById("chatTitle");
    const sidebar = document.getElementById("sidebar");
    const scrollDownBtn = document.getElementById("scrollDownBtn");
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    // ---------- Markdown rendering ----------

    marked.setOptions({ breaks: true, gfm: true });

    function renderMarkdown(text) {
        const html = DOMPurify.sanitize(marked.parse(text));
        const container = document.createElement("div");
        container.innerHTML = html;

        // Open links in a new tab so the chat stays put.
        container.querySelectorAll("a").forEach(a => {
            a.target = "_blank";
            a.rel = "noopener noreferrer";
        });

        // Wrap each code block with a header (language + copy button).
        container.querySelectorAll("pre > code").forEach(code => {
            const pre = code.parentElement;
            const lang = (code.className.match(/language-(\w+)/) || [])[1] || "code";

            const wrapper = document.createElement("div");
            wrapper.className = "code-block";

            const header = document.createElement("div");
            header.className = "code-block-header";
            header.innerHTML =
                `<span>${lang}</span>` +
                `<button class="copy-btn" type="button">` +
                `<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>` +
                `<span>Copy code</span></button>`;

            header.querySelector(".copy-btn").addEventListener("click", function () {
                navigator.clipboard.writeText(code.textContent).then(() => {
                    const label = this.querySelector("span:last-child");
                    label.textContent = "Copied!";
                    setTimeout(() => label.textContent = "Copy code", 1500);
                });
            });

            pre.replaceWith(wrapper);
            wrapper.appendChild(header);
            wrapper.appendChild(pre);
            pre.appendChild(code);
            hljs.highlightElement(code);
        });

        return container;
    }

    // ---------- Message rendering ----------

    function hideWelcome() {
        if (welcomeEl) welcomeEl.style.display = "none";
    }

    function addMessage(role, text) {
        hideWelcome();
        const row = document.createElement("div");
        row.className = "message-row " + (role === "user" ? "user" : "model");

        if (role === "user") {
            const content = document.createElement("div");
            content.className = "message-content";
            content.textContent = text;
            row.appendChild(content);
        } else {
            row.innerHTML = '<div class="message-avatar">;</div>';
            const content = document.createElement("div");
            content.className = "message-content";
            content.appendChild(renderMarkdown(text));
            row.appendChild(content);
        }

        messagesEl.appendChild(row);
        scrollToBottom();
        return row;
    }

    function addTyping() {
        hideWelcome();
        const row = document.createElement("div");
        row.className = "message-row model";
        row.id = "typingRow";
        row.innerHTML = '<div class="message-avatar">;</div><div class="typing"><span></span><span></span><span></span></div>';
        messagesEl.appendChild(row);
        scrollToBottom();
    }

    function removeTyping() {
        document.getElementById("typingRow")?.remove();
    }

    function addError(text) {
        const row = document.createElement("div");
        row.className = "message-row model";
        row.innerHTML = '<div class="message-avatar">;</div>';
        const bubble = document.createElement("div");
        bubble.className = "error-bubble";
        bubble.textContent = text;
        row.appendChild(bubble);
        messagesEl.appendChild(row);
        scrollToBottom();
    }

    function scrollToBottom() {
        messagesEl.scrollTop = messagesEl.scrollHeight;
        updateScrollBtn();
    }

    function isNearBottom() {
        return messagesEl.scrollTop + messagesEl.clientHeight >= messagesEl.scrollHeight - 120;
    }

    function updateScrollBtn() {
        scrollDownBtn.classList.toggle("visible", !isNearBottom());
    }

    // ---------- Streaming animation ----------

    function appendCursor(container) {
        // Walk to the deepest last element so the cursor sits at the end of the text.
        let target = container;
        while (target.lastElementChild &&
               !["PRE", "CODE", "TABLE", "svg", "BUTTON"].includes(target.lastElementChild.tagName)) {
            target = target.lastElementChild;
        }
        const cursor = document.createElement("span");
        cursor.className = "stream-cursor";
        target.appendChild(cursor);
    }

    function streamMessage(text) {
        hideWelcome();
        const row = document.createElement("div");
        row.className = "message-row model";
        row.innerHTML = '<div class="message-avatar">;</div>';
        const content = document.createElement("div");
        content.className = "message-content";
        row.appendChild(content);
        messagesEl.appendChild(row);

        // Reveal in ~150 steps so every reply finishes in a few seconds,
        // typing at least 2 characters per step for short replies.
        const step = Math.max(2, Math.round(text.length / 150));

        return new Promise(resolve => {
            let shown = 0;
            const timer = setInterval(() => {
                shown += step;
                const stick = isNearBottom();

                if (shown >= text.length) {
                    clearInterval(timer);
                    content.replaceChildren(renderMarkdown(text));
                    if (stick) scrollToBottom();
                    resolve();
                    return;
                }

                content.replaceChildren(renderMarkdown(text.slice(0, shown)));
                appendCursor(content);
                if (stick) scrollToBottom(); else updateScrollBtn();
            }, 24);
        });
    }

    function clearMessages() {
        messagesEl.querySelectorAll(".message-row").forEach(el => el.remove());
    }

    // ---------- Sending ----------

    async function sendMessage() {
        const text = inputEl.value.trim();
        if (!text || isSending) return;

        isSending = true;
        inputEl.value = "";
        autoGrow();
        updateSendState();

        addMessage("user", text);
        addTyping();

        try {
            const body = new URLSearchParams({ message: text });
            if (currentSessionId) body.append("sessionId", currentSessionId);

            const res = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded",
                    "RequestVerificationToken": token
                },
                body
            });

            const data = await res.json().catch(() => ({}));
            removeTyping();

            if (!res.ok) {
                addError(data.error || "Something went wrong. Please try again.");
                return;
            }

            if (!currentSessionId) {
                currentSessionId = data.sessionId;
                addSessionToSidebar(data.sessionId, data.title);
            }
            chatTitle.textContent = data.title;

            await streamMessage(data.reply);
        } catch {
            removeTyping();
            addError("Network error. Please check your connection and try again.");
        } finally {
            isSending = false;
            updateSendState();
            inputEl.focus();
        }
    }

    // ---------- Sessions sidebar ----------

    function addSessionToSidebar(id, title) {
        const item = document.createElement("div");
        item.className = "session-item active";
        item.dataset.id = id;
        item.innerHTML =
            `<span class="session-title"></span>` +
            `<div class="session-actions">` +
            `<button class="icon-btn rename-btn" title="Rename"><svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M17 3a2.8 2.8 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/></svg></button>` +
            `<button class="icon-btn delete-btn" title="Delete"><svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2m3 0v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6"/></svg></button>` +
            `</div>`;
        item.querySelector(".session-title").textContent = title;

        setActiveItem(item);
        sessionList.prepend(item);
    }

    function setActiveItem(item) {
        sessionList.querySelectorAll(".session-item").forEach(el => el.classList.remove("active"));
        item?.classList.add("active");
    }

    async function loadSession(id, item) {
        currentSessionId = id;
        setActiveItem(item);
        chatTitle.textContent = item.querySelector(".session-title").textContent;
        clearMessages();
        hideWelcome();

        const res = await fetch(`/Chat/Messages?sessionId=${id}`);
        if (!res.ok) return;

        const messages = await res.json();
        messages.forEach(m => addMessage(m.role, m.content));
        scrollToBottom();
    }

    function startNewChat() {
        currentSessionId = null;
        chatTitle.textContent = "New chat";
        setActiveItem(null);
        clearMessages();
        if (welcomeEl) welcomeEl.style.display = "";
        inputEl.focus();
    }

    function startRename(item) {
        const titleEl = item.querySelector(".session-title");
        const oldTitle = titleEl.textContent;

        const input = document.createElement("input");
        input.value = oldTitle;
        titleEl.textContent = "";
        titleEl.appendChild(input);
        item.classList.add("editing");
        input.focus();
        input.setSelectionRange(oldTitle.length, oldTitle.length);

        let done = false;
        async function commit(save) {
            if (done) return;
            done = true;

            item.classList.remove("editing");
            const newTitle = input.value.trim();
            titleEl.textContent = oldTitle;

            if (!save || !newTitle || newTitle === oldTitle) return;

            const res = await fetch("/Chat/Rename", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded",
                    "RequestVerificationToken": token
                },
                body: new URLSearchParams({ sessionId: item.dataset.id, title: newTitle })
            });

            if (res.ok) {
                titleEl.textContent = newTitle;
                if (item.dataset.id == currentSessionId) chatTitle.textContent = newTitle;
            }
        }

        input.addEventListener("keydown", e => {
            if (e.key === "Enter") commit(true);
            if (e.key === "Escape") commit(false);
        });
        input.addEventListener("blur", () => commit(true));
        input.addEventListener("click", e => e.stopPropagation());
    }

    async function deleteSession(item) {
        const title = item.querySelector(".session-title").textContent;
        if (!await showConfirm("Delete chat?", `"${title}" will be permanently deleted. This cannot be undone.`, "Delete"))
            return;

        const res = await fetch("/Chat/Delete", {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded",
                "RequestVerificationToken": token
            },
            body: new URLSearchParams({ sessionId: item.dataset.id })
        });

        if (res.ok) {
            const wasActive = item.dataset.id == currentSessionId;
            item.remove();
            if (wasActive) startNewChat();
            showToast("Chat deleted.", "info");
        }
    }

    // ---------- Input behaviour ----------

    function autoGrow() {
        inputEl.style.height = "auto";
        inputEl.style.height = Math.min(inputEl.scrollHeight, 180) + "px";
    }

    function updateSendState() {
        sendBtn.disabled = isSending || inputEl.value.trim().length === 0;
    }

    // ---------- Events ----------

    inputEl.addEventListener("input", () => { autoGrow(); updateSendState(); });

    inputEl.addEventListener("keydown", e => {
        if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            sendMessage();
        }
    });

    sendBtn.addEventListener("click", sendMessage);
    document.getElementById("newChatBtn").addEventListener("click", startNewChat);

    sessionList.addEventListener("click", e => {
        const item = e.target.closest(".session-item");
        if (!item) return;

        if (e.target.closest(".rename-btn")) { startRename(item); return; }
        if (e.target.closest(".delete-btn")) { deleteSession(item); return; }
        if (item.dataset.id != currentSessionId) loadSession(item.dataset.id, item);
    });

    // ---------- Custom confirm dialog ----------

    function showConfirm(title, message, confirmText) {
        return new Promise(resolve => {
            const overlay = document.createElement("div");
            overlay.className = "modal-overlay";
            overlay.innerHTML =
                '<div class="confirm-modal" role="dialog" aria-modal="true">' +
                '<h3 class="confirm-title"></h3>' +
                '<p class="confirm-message"></p>' +
                '<div class="confirm-actions">' +
                '<button type="button" class="btn-small confirm-cancel">Cancel</button>' +
                '<button type="button" class="btn-danger-solid confirm-ok"></button>' +
                '</div></div>';

            overlay.querySelector(".confirm-title").textContent = title;
            overlay.querySelector(".confirm-message").textContent = message;
            overlay.querySelector(".confirm-ok").textContent = confirmText || "Confirm";

            function onKey(e) {
                if (e.key === "Escape") close(false);
            }

            function close(result) {
                document.removeEventListener("keydown", onKey);
                overlay.classList.add("closing");
                setTimeout(() => { overlay.remove(); resolve(result); }, 200);
            }

            overlay.querySelector(".confirm-cancel").addEventListener("click", () => close(false));
            overlay.querySelector(".confirm-ok").addEventListener("click", () => close(true));
            overlay.addEventListener("click", e => { if (e.target === overlay) close(false); });
            document.addEventListener("keydown", onKey);

            document.body.appendChild(overlay);
            overlay.querySelector(".confirm-cancel").focus();
        });
    }

    // ---------- Profile modal ----------

    const profileOverlay = document.getElementById("profileOverlay");
    const profileModal = profileOverlay.querySelector(".profile-modal");

    function openProfile() {
        profileOverlay.hidden = false;
        profileModal.querySelector("input:not([readonly])")?.focus();
    }

    function closeProfile() {
        profileOverlay.classList.add("closing");
        setTimeout(() => {
            profileOverlay.hidden = true;
            profileOverlay.classList.remove("closing");
        }, 200);
    }

    document.getElementById("profileOpen").addEventListener("click", openProfile);
    document.getElementById("profileClose").addEventListener("click", closeProfile);

    // Clicking the blurred backdrop (not the card) closes the modal.
    profileOverlay.addEventListener("click", e => {
        if (e.target === profileOverlay) closeProfile();
    });

    document.addEventListener("keydown", e => {
        if (e.key === "Escape" && !profileOverlay.hidden) closeProfile();
    });

    document.getElementById("photoInput").addEventListener("change", function () {
        if (this.files.length) document.getElementById("avatarForm").submit();
    });

    messagesEl.addEventListener("scroll", updateScrollBtn);

    scrollDownBtn.addEventListener("click", () =>
        messagesEl.scrollTo({ top: messagesEl.scrollHeight, behavior: "smooth" }));

    document.getElementById("sidebarToggle").addEventListener("click", () => sidebar.classList.toggle("collapsed"));
    document.getElementById("sidebarClose").addEventListener("click", () => sidebar.classList.add("collapsed"));

    // ---------- Time-based greeting ----------

    (function setGreeting() {
        if (!welcomeEl) return;

        const name = welcomeEl.dataset.name || "there";
        const hour = new Date().getHours();

        let greeting, taglines;
        if (hour >= 5 && hour < 12) {
            greeting = "Good morning";
            taglines = [
                "Rise and shine — fresh coffee, fresh ideas. What are we building today? ☀️",
                "A brand new day, a blank chat. Let's make something great of both!",
                "Early bird energy detected. What's first on the list today?"
            ];
        } else if (hour >= 12 && hour < 17) {
            greeting = "Good afternoon";
            taglines = [
                "Hope your day's going great — let's keep the momentum rolling! 🚀",
                "Perfect time for a breakthrough. What's on your mind?",
                "Midday spark needed? I'm all ears — ask away."
            ];
        } else if (hour >= 17 && hour < 21) {
            greeting = "Good evening";
            taglines = [
                "Evenings are for the best ideas. What's brewing? ✨",
                "Winding down or just warming up? Either way, I'm here.",
                "Let's wrap the day with something brilliant."
            ];
        } else {
            greeting = "Hello, night owl";
            taglines = [
                "The best ideas show up after dark. What's yours? 🌙",
                "Quiet night, big thoughts — I'm listening.",
                "Night owl mode: activated. Let's build something cool."
            ];
        }

        const greetEl = document.getElementById("welcomeGreeting");
        greetEl.textContent = hour >= 21 || hour < 5 ? `${greeting} ` : `${greeting}, ${name} `;

        const semicolon = document.createElement("span");
        semicolon.className = "greeting-semicolon";
        semicolon.textContent = ";";
        greetEl.appendChild(semicolon);

        document.getElementById("welcomeTagline").textContent =
            taglines[Math.floor(Math.random() * taglines.length)];
    })();

    // Start collapsed on small screens.
    if (window.innerWidth <= 768) sidebar.classList.add("collapsed");

    inputEl.focus();
})();

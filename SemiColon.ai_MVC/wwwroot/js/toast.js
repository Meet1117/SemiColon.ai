// Minimal toast notifications (top-right). Usage: showToast("Saved!", "success" | "error" | "info")
function showToast(message, type) {
    type = type || "success";
    const stack = document.getElementById("toastStack");
    if (!stack) return;

    const icons = {
        success: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg>',
        error: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><circle cx="12" cy="12" r="10"/><path d="M12 8v4M12 16h.01"/></svg>',
        info: '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg>'
    };

    const toast = document.createElement("div");
    toast.className = "toast toast-" + type;
    toast.innerHTML =
        '<span class="toast-icon">' + (icons[type] || icons.info) + "</span>" +
        '<span class="toast-message"></span>' +
        '<button class="toast-close" type="button">✕</button>';
    toast.querySelector(".toast-message").textContent = message;

    function dismiss() {
        toast.classList.add("toast-out");
        setTimeout(() => toast.remove(), 250);
    }

    toast.querySelector(".toast-close").addEventListener("click", dismiss);
    stack.appendChild(toast);
    setTimeout(dismiss, 4000);
}

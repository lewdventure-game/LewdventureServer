document.addEventListener("submit", function (event) {
    var form = event.target;
    var message = form.getAttribute("data-confirm");

    if (message && window.confirm(message) === false) {
        event.preventDefault();
    }
});

document.addEventListener("click", function (event) {
    var button = event.target.closest("[data-action]");

    if (button === null) {
        return;
    }

    var action = button.getAttribute("data-action");

    if (action === "toggle-password") {
        togglePassword(button);
    } else if (action === "add-group") {
        addGroup();
    } else if (action === "remove-group") {
        removeGroup(button);
    }
});

function togglePassword(button) {
    var input = document.getElementById(button.getAttribute("data-target"));
    var isHidden = input.type === "password";

    input.type = isHidden ? "text" : "password";
    button.textContent = isHidden ? "Скрыть" : "Показать";
    button.setAttribute("aria-pressed", isHidden ? "true" : "false");
}

function addGroup() {
    var template = document.getElementById("group-row-template");
    var rows = document.getElementById("group-rows");
    var key = "n" + Date.now().toString(36) + Math.floor(Math.random() * 1000).toString(36);

    rows.insertAdjacentHTML("beforeend", template.innerHTML.split("__key__").join(key));
    rows.lastElementChild.querySelector("input").focus();
}

function removeGroup(button) {
    var rows = document.getElementById("group-rows");

    if (rows.children.length <= 1) {
        return;
    }

    button.closest("tr").remove();
}

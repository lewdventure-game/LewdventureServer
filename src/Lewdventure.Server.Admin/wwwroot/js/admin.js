document.addEventListener("submit", function (event) {
    var form = event.target;
    var message = form.getAttribute("data-confirm");

    if (message && window.confirm(message) === false) {
        event.preventDefault();
    }
});

document.addEventListener("DOMContentLoaded", function () {
    var tables = document.querySelectorAll("table[data-csv]");

    for (var i = 0; i < tables.length; i++) {
        var button = document.createElement("button");

        button.type = "button";
        button.className = "csv-button";
        button.textContent = "CSV";
        button.title = "Скачать таблицу в CSV";
        button.setAttribute("data-action", "export-csv");
        button.setAttribute("data-index", String(i));
        tables[i].parentNode.insertBefore(button, tables[i]);
    }
});

document.addEventListener("click", function (event) {
    var confirmed = event.target.closest("button[data-confirm]");

    if (confirmed !== null && window.confirm(confirmed.getAttribute("data-confirm")) === false) {
        event.preventDefault();

        return;
    }

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
    } else if (action === "export-csv") {
        exportCsv(button);
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

function exportCsv(button) {
    var table = document.querySelectorAll("table[data-csv]")[Number(button.getAttribute("data-index"))];
    var rows = table.querySelectorAll("tr");
    var lines = [];

    for (var i = 0; i < rows.length; i++) {
        var cells = rows[i].querySelectorAll("th, td");
        var values = [];

        for (var j = 0; j < cells.length; j++) {
            values.push(csvValue(cells[j].textContent));
        }

        lines.push(values.join(";"));
    }

    var blob = new Blob(["\uFEFF" + lines.join("\r\n")], { type: "text/csv;charset=utf-8" });
    var link = document.createElement("a");

    link.href = URL.createObjectURL(blob);
    link.download = "баланс-" + table.getAttribute("data-csv") + ".csv";
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.setTimeout(function () {
        URL.revokeObjectURL(link.href);
    }, 1000);
}

function csvValue(text) {
    var value = text.replace(/\s+/g, " ").trim();

    if (/[";\n]/.test(value)) {
        return "\"" + value.replace(/"/g, "\"\"") + "\"";
    }

    return value;
}

document.addEventListener("submit", function (event) {
    var form = event.target;
    var message = form.getAttribute("data-confirm");

    if (message && window.confirm(message) === false) {
        event.preventDefault();
    }
});

document.addEventListener("DOMContentLoaded", function () {
    var selects = document.querySelectorAll("[data-sheet-select]");

    for (var s = 0; s < selects.length; s++) {
        applySheet(selects[s]);
    }

    var bars = document.querySelectorAll("[data-width]");

    for (var b = 0; b < bars.length; b++) {
        bars[b].style.width = (Number(bars[b].getAttribute("data-width")) * 0.6) + "px";
    }

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

document.addEventListener("change", function (event) {
    if (event.target.matches("[data-sheet-select]")) {
        applySheet(event.target);
    }
});

document.addEventListener("input", function (event) {
    var input = event.target;

    if (input.classList.contains("chip-input")) {
        var chips = input.parentNode.querySelectorAll(".chip");

        for (var i = 0; i < chips.length; i++) {
            chips[i].classList.toggle("active", chips[i].getAttribute("data-value") === input.value);
        }
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
    } else if (action === "tab") {
        showTab(button);
    } else if (action === "set-value") {
        setValue(button);
    } else if (action === "add-edit") {
        addEdit(button);
    } else if (action === "clear-edit") {
        clearEdit(button);
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

function showTab(button) {
    var tab = button.getAttribute("data-tab");
    var form = button.closest("form");
    var tabs = form.querySelectorAll("[data-action='tab']");
    var panels = form.querySelectorAll("[data-tab-panel]");

    for (var i = 0; i < tabs.length; i++) {
        tabs[i].classList.toggle("active", tabs[i] === button);
    }

    for (var j = 0; j < panels.length; j++) {
        panels[j].hidden = panels[j].getAttribute("data-tab-panel") !== tab;
    }

    var results = form.querySelectorAll("[data-result-tab]");

    for (var k = 0; k < results.length; k++) {
        results[k].hidden = results[k].getAttribute("data-result-tab") !== tab;
    }

    form.querySelector("[data-tab-input]").value = tab;
}

function setValue(button) {
    var container = button.parentNode;
    var input = container.querySelector("input[name='" + button.getAttribute("data-target") + "']");
    var chips = container.querySelectorAll(".chip");

    input.value = button.getAttribute("data-value");

    for (var i = 0; i < chips.length; i++) {
        chips[i].classList.toggle("active", chips[i] === button);
    }
}

function addEdit(button) {
    var container = button.closest("[data-edits]");
    var prefix = container.getAttribute("data-edits");
    var rows = container.querySelectorAll("[data-edit-row]");
    var template = rows[rows.length - 1];
    var row = template.cloneNode(true);
    var index = rows.length;
    var fields = row.querySelectorAll("input, select");

    for (var i = 0; i < fields.length; i++) {
        var name = fields[i].getAttribute("name");

        fields[i].setAttribute("name", name.replace(new RegExp("^" + prefix + "\\[\\d+\\]"), prefix + "[" + index + "]"));

        if (fields[i].tagName === "INPUT") {
            fields[i].value = "";
        }
    }

    container.insertBefore(row, button);
    applySheet(row.querySelector("[data-sheet-select]"));
    row.querySelector("[data-row-input]").focus();
}

function clearEdit(button) {
    var inputs = button.closest("[data-edit-row]").querySelectorAll("input");

    for (var i = 0; i < inputs.length; i++) {
        inputs[i].value = "";
    }
}

function applySheet(select) {
    var row = select.closest(".edit-row");

    if (row === null || select.selectedIndex < 0) {
        return;
    }

    var option = select.options[select.selectedIndex];
    var key = option.getAttribute("data-key");
    var column = row.querySelector("[data-column-input]");
    var rowInput = row.querySelector("[data-row-input]");

    column.setAttribute("list", "balance-columns-" + select.value);
    rowInput.placeholder = key ? key + "=…" : "#номер строки";
}

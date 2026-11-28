(function () {
    "use strict";

    var pageSize = 10;
    var icons = {
        edit: "m15 5 4 4M4 20l4-.8L19 8a2.1 2.1 0 0 0-3-3L5 16l-1 4Z",
        view: "M2.5 12s3.2-6 9.5-6 9.5 6 9.5 6-3.2 6-9.5 6-9.5-6-9.5-6Z M12 9.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5Z",
        delete: "M4 7h16M10 11v6m4-6v6M6 7l1 14h10l1-14M9 7V4h6v3",
        download: "M12 3v12m0 0 5-5m-5 5-5-5M4 17v3h16v-3",
        verify: "m5 12 4 4L19 6",
        forward: "M5 12h14m-7-7 7 7-7 7",
        upload: "M12 16V4m0 0L7 9m5-5 5 5M5 14v5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-5",
        toggle: "M12 3v9m6.4-5.4a8 8 0 1 1-12.8 0"
    };

    function normalize(value) {
        return (value || "").replace(/\s+/g, " ").trim().toLowerCase();
    }

    function actionIcon(label) {
        if (/^(edit|modify|update)(\b| )/.test(label)) return "edit";
        if (/^(view|review|open)(\b| )/.test(label)) return "view";
        if (/^(delete|remove)(\b| )/.test(label)) return "delete";
        if (/^download(\b| )/.test(label)) return "download";
        if (/^(verify|approve)(\b| )/.test(label)) return "verify";
        if (/^(forward|send to next)(\b| )/.test(label)) return "forward";
        if (/^(upload|replace|re-upload)(\b| )/.test(label)) return "upload";
        if (/^(activate|deactivate|reactivate|enable|disable)(\b| )/.test(label)) return "toggle";
        return null;
    }

    function hasPagerRow(row) {
        return row.cells.length === 1 &&
            !!row.cells[0].querySelector("table");
    }

    function isEmptyRow(row) {
        return row.cells.length === 1 && row.cells[0].colSpan > 1;
    }

    function isSerialHeader(text) {
        return /^(sr\.?\s*no\.?|serial(\s+number|\s+no\.?)?|s\.?\s*no\.?)$/i.test(normalize(text));
    }

    function addAccessibleIcon(control, label, iconName) {
        if (!control.getAttribute("aria-label")) control.setAttribute("aria-label", label);
        if (!control.getAttribute("title")) control.setAttribute("title", label);
        control.classList.add("grid-row-action");
        control.classList.remove("button", "secondary");
        control.classList.add("icon-action");
        if (iconName === "delete") control.classList.add("danger");

        if (control.tagName.toLowerCase() === "input") {
            var stroke = iconName === "delete" ? "#b42318" : "#5a3760";
            var svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="' +
                stroke + '" stroke-linecap="round" stroke-linejoin="round" stroke-width="1.8"><path d="' +
                icons[iconName] + '"/></svg>';
            control.classList.add("row-action-input-icon");
            control.style.backgroundImage = 'url("data:image/svg+xml,' + encodeURIComponent(svg) + '")';
            return;
        }

        while (control.firstChild) control.removeChild(control.firstChild);
        var svgElement = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svgElement.setAttribute("viewBox", "0 0 24 24");
        svgElement.setAttribute("aria-hidden", "true");
        var path = document.createElementNS("http://www.w3.org/2000/svg", "path");
        path.setAttribute("d", icons[iconName]);
        svgElement.appendChild(path);
        var accessibleText = document.createElement("span");
        accessibleText.className = "sr-only";
        accessibleText.textContent = label;
        control.appendChild(svgElement);
        control.appendChild(accessibleText);
    }

    function enhanceActions(rows, headerRow) {
        if (!headerRow) return;
        var actionColumns = [];
        for (var i = 0; i < headerRow.cells.length; i++) {
            if (/\b(actions?|access|operations?)\b/i.test(headerRow.cells[i].textContent || "")) {
                actionColumns.push(i);
            }
        }

        rows.forEach(function (row) {
            actionColumns.forEach(function (columnIndex) {
                var cell = row.cells[columnIndex];
                if (!cell) return;
                var controls = cell.querySelectorAll("a, button, input[type='submit'], input[type='button']");
                var converted = false;
                for (var j = 0; j < controls.length; j++) {
                    var control = controls[j];
                    if (control.classList.contains("icon-action") && control.querySelector("svg")) continue;
                    var label = (control.getAttribute("aria-label") || control.getAttribute("title") ||
                        (control.tagName.toLowerCase() === "input" ? control.value : control.textContent) || "").trim();
                    var iconName = actionIcon(normalize(label));
                    if (!iconName) continue;
                    addAccessibleIcon(control, label, iconName);
                    converted = true;
                }
                if (converted) {
                    var actionGroup = cell.querySelector(".icon-actions, .document-icon-actions");
                    if (actionGroup) actionGroup.classList.add("icon-actions");
                }
            });
        });
    }

    function addSerialColumn(headerRow, rows) {
        var serialColumn = -1;
        for (var i = 0; i < headerRow.cells.length; i++) {
            if (isSerialHeader(headerRow.cells[i].textContent)) {
                serialColumn = i;
                break;
            }
        }

        if (serialColumn < 0) {
            var header = document.createElement("th");
            header.scope = "col";
            header.className = "grid-serial-cell";
            header.textContent = "Sr. No.";
            headerRow.insertBefore(header, headerRow.cells[0]);
            serialColumn = 0;
            rows.forEach(function (row) {
                var cell = document.createElement("td");
                cell.className = "grid-serial-cell";
                row.insertBefore(cell, row.cells[0]);
            });
        }

        rows.forEach(function (row, index) {
            if (row.cells[serialColumn]) row.cells[serialColumn].classList.add("grid-serial-cell");
            if (!row.cells[serialColumn] || row.cells[serialColumn].querySelector("a, button, input")) return;
            if (!row.cells[serialColumn].textContent.trim()) row.cells[serialColumn].textContent = String(index + 1);
        });
    }

    function addSearch(table, rows) {
        if (!rows.length) return null;
        var search = document.createElement("div");
        search.className = "grid-list-search";
        var label = document.createElement("label");
        label.textContent = "Search this list";
        var input = document.createElement("input");
        input.type = "search";
        input.setAttribute("aria-label", "Search this list");
        input.setAttribute("autocomplete", "off");
        label.appendChild(input);
        search.appendChild(label);
        table.insertAdjacentElement("beforebegin", search);

        var empty = document.createElement("p");
        empty.className = "grid-list-empty";
        empty.setAttribute("role", "status");
        empty.textContent = "No matching records.";
        empty.hidden = true;
        table.insertAdjacentElement("afterend", empty);

        var filteredRows = rows.slice();
        var renderer = null;
        input.addEventListener("input", function () {
            var query = normalize(input.value);
            filteredRows = query
                ? rows.filter(function (row) {
                    return normalize(row.textContent).indexOf(query) !== -1;
                })
                : rows.slice();
            empty.hidden = filteredRows.length !== 0;
            if (renderer) renderer(filteredRows, true);
            else rows.forEach(function (row) {
                row.hidden = query && filteredRows.indexOf(row) === -1;
            });
        });

        return {
            getRows: function () { return filteredRows; },
            setRenderer: function (callback) {
                renderer = callback;
                empty.hidden = filteredRows.length !== 0;
                renderer(filteredRows, false);
            }
        };
    }

    function addPager(table, rows, searchState) {
        var hasPager = rows.length > pageSize;
        var stateKey = "list-page:" + window.location.pathname + ":" + table.id;
        var currentPage = 1;
        if (hasPager) {
            currentPage = parseInt(window.sessionStorage.getItem(stateKey), 10);
            if (!currentPage || currentPage < 1) currentPage = 1;
        }
        var pager = null;
        var previous = null;
        var next = null;
        var status = null;
        if (hasPager) {
            pager = document.createElement("nav");
            pager.className = "grid-list-pager";
            pager.setAttribute("aria-label", "Table pagination");
            previous = document.createElement("button");
            previous.type = "button";
            previous.setAttribute("aria-label", "Previous page");
            previous.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m15 18-6-6 6-6"/></svg>';
            status = document.createElement("span");
            status.setAttribute("aria-live", "polite");
            next = document.createElement("button");
            next.type = "button";
            next.setAttribute("aria-label", "Next page");
            next.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="m9 18 6-6-6-6"/></svg>';
            pager.appendChild(previous);
            pager.appendChild(status);
            pager.appendChild(next);
            table.insertAdjacentElement("afterend", pager);
        }

        function render(filteredRows, resetPage) {
            filteredRows = filteredRows || rows;
            var pageCount = Math.max(1, Math.ceil(filteredRows.length / pageSize));
            if (resetPage) currentPage = 1;
            if (currentPage > pageCount) currentPage = pageCount;
            var start = (currentPage - 1) * pageSize;
            rows.forEach(function (row) { row.hidden = true; });
            filteredRows.slice(start, start + pageSize).forEach(function (row) {
                row.hidden = false;
            });
            if (pager) {
                pager.hidden = filteredRows.length <= pageSize;
                previous.disabled = currentPage === 1;
                next.disabled = currentPage >= pageCount;
                status.textContent = "Page " + currentPage + " of " + pageCount;
                window.sessionStorage.setItem(stateKey, String(currentPage));
            }
        }

        if (pager) {
            previous.addEventListener("click", function () {
                if (currentPage > 1) {
                    currentPage--;
                    render(searchState ? searchState.getRows() : rows, false);
                }
            });
            next.addEventListener("click", function () {
                var filteredRows = searchState ? searchState.getRows() : rows;
                if (currentPage < Math.ceil(filteredRows.length / pageSize)) {
                    currentPage++;
                    render(filteredRows, false);
                }
            });
        }
        return render;
    }

    function enhanceTable(table) {
        if (table.getAttribute("data-list-enhanced") === "true") return;
        var allRows = Array.prototype.slice.call(table.rows);
        var headerRow = null;
        allRows.some(function (row) {
            if (row.querySelector("th") && !row.querySelector("table")) {
                headerRow = row;
                return true;
            }
            return false;
        });
        if (!headerRow) return;

        var nativePager = allRows.some(hasPagerRow);
        var rows = allRows.filter(function (row) {
            return row !== headerRow && !hasPagerRow(row) && !isEmptyRow(row) && !row.querySelector("th");
        });
        rows.forEach(function (row) {
            Array.prototype.forEach.call(row.cells, function (cell) {
                var text = (cell.textContent || "").replace(/\s+/g, " ").trim();
                if (text.length > 40 && !cell.title) cell.title = text;
            });
        });
        enhanceActions(rows, headerRow);
        addSerialColumn(headerRow, rows);
        table.setAttribute("data-list-enhanced", "true");
        var searchState = !nativePager && table.classList.contains("compact-grid")
            ? addSearch(table, rows)
            : null;
        if (!nativePager) {
            var render = addPager(table, rows, searchState);
            if (searchState) searchState.setRenderer(render);
            else render(rows, false);
        }
    }

    function enhanceLists() {
        var tables = document.querySelectorAll("table[id]");
        for (var i = 0; i < tables.length; i++) {
            if (/grid/i.test(tables[i].id)) enhanceTable(tables[i]);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", enhanceLists);
    } else {
        enhanceLists();
    }
}());

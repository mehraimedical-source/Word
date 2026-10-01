(function () {
    "use strict";

    var editor = document.getElementById("editor");
    var status = document.getElementById("status");
    if (!editor) return;

    function exec(command, value) {
        editor.focus();
        document.execCommand(command, false, value || null);
    }

    document.querySelectorAll("[data-cmd]").forEach(function (button) {
        button.addEventListener("mousedown", function (e) {
            e.preventDefault();
            exec(button.getAttribute("data-cmd"));
        });
    });

    document.getElementById("fontName").addEventListener("change", function () {
        exec("fontName", this.value);
    });

    document.getElementById("fontSize").addEventListener("change", function () {
        exec("fontSize", this.value);
    });

    document.getElementById("foreColor").addEventListener("input", function () {
        exec("foreColor", this.value);
    });

    document.getElementById("backColor").addEventListener("input", function () {
        exec("hiliteColor", this.value);
    });

    document.getElementById("rtlBtn").addEventListener("click", function () {
        setDirection("rtl");
    });

    document.getElementById("ltrBtn").addEventListener("click", function () {
        setDirection("ltr");
    });

    function setDirection(direction) {
        editor.focus();
        var selection = window.getSelection();
        if (!selection.rangeCount) return;

        var node = selection.getRangeAt(0).startContainer;
        if (node.nodeType === 3) node = node.parentNode;

        while (node && node !== editor && !/^(P|DIV|TD|TH|LI)$/.test(node.tagName))
            node = node.parentNode;

        (node && node !== editor ? node : editor).setAttribute("dir", direction);
    }

    document.getElementById("tableBtn").addEventListener("click", function () {
        var rows = Math.max(1, Math.min(20, parseInt(prompt("Rows:", "2"), 10) || 0));
        var cols = Math.max(1, Math.min(20, parseInt(prompt("Columns:", "2"), 10) || 0));
        if (!rows || !cols) return;

        var html = '<table><tbody>';
        for (var r = 0; r < rows; r++) {
            html += '<tr>';
            for (var c = 0; c < cols; c++) html += '<td><br></td>';
            html += '</tr>';
        }
        html += '</tbody></table><p><br></p>';
        exec("insertHTML", html);
    });

    document.getElementById("imageFile").addEventListener("change", function () {
        var file = this.files && this.files[0];
        this.value = "";
        if (!file) return;

        var reader = new FileReader();
        reader.onload = function (e) {
            exec("insertImage", e.target.result);
        };
        reader.readAsDataURL(file);
    });

    document.getElementById("docFile").addEventListener("change", function () {
        var file = this.files && this.files[0];
        this.value = "";
        if (!file) return;

        showStatus("Importing " + file.name + " ...", false);
        var data = new FormData();
        data.append("file", file);

        fetch(window.location.pathname.replace(/\/$/, "") + "/Import", {
            method: "POST",
            body: data,
            credentials: "same-origin"
        })
        .then(function (response) { return response.json(); })
        .then(function (result) {
            if (!result.ok) throw new Error(result.error || "Import failed.");
            editor.innerHTML = result.html;
            showStatus("Document imported. Review the layout before editing.", false);
            setTimeout(hideStatus, 3500);
        })
        .catch(function (error) {
            showStatus(error.message, true);
        });
    });

    function showStatus(message, isError) {
        status.textContent = message;
        status.className = "status" + (isError ? " error" : "");
        status.hidden = false;
    }

    function hideStatus() {
        status.hidden = true;
    }
})();
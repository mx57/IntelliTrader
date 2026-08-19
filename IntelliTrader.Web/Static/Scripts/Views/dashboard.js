var table = null;
$(function () {
    table = $('#tradingPairsTable').DataTable({
        ajax: {
            url: "TradingPairs",
            type: "POST",
            dataSrc: ""
        },
        columns: [
            {
                className: 'control',
                orderable: false,
                data: null,
                defaultContent: ''
            },
            {
                name: "Name",
                data: "Name",
                render: function (data, type, row, meta) {
                    return '<a href="https://www.tradingview.com/chart/?symbol=' + row.TradingViewName + '" target = "_blank" class="btn btn-outline-info btn-sm">' + data + '</a>';
                },
                visible: false
            },
            {
                name: "FormattedName",
                data: "Name",
                render: function (data, type, row, meta) {
                    var element = '<div style="width: 120px"><a href="https://www.tradingview.com/chart/?symbol=' + row.TradingViewName + '" target = "_blank" class="btn btn-outline-info btn-sm">' + data + '</a>';
                    if (row.DCA > 0) {
                        element += '&nbsp;&nbsp;<span class="badge badge-primary" title="DCA level">' + row.DCA + '</span>';
                    }
                    element += '</div>';
                    return element;
                }
            },
            {
                name: "DCA",
                data: "DCA",
                visible: false
            },
            {
                name: "Margin",
                data: "Margin",
                render: function (data, type, row, meta) {
                    var element = "";
                    if (parseFloat(data) >= 0) {
                        element = '<span class="text-success"><strong>' + data + '</strong></span>';
                    }
                    else {
                        element = '<span class="text-warning"><strong>' + data + '</strong></span>';
                    }
                    if (row.IsTrailingSell) {
                        element += ' <i class="fas fa-bolt text-info" title="Trailing"></i>';
                    }
                    if (row.IsTrailingBuy) {
                        element += ' <i class="fas fa-bolt text-primary" title="Trailing"></i>';
                    }
                    return element;
                }
            },
            {
                name: "Target",
                data: "Target"
            },
            {
                name: "CurrentRating",
                data: "CurrentRating",
                render: function (data, type, row, meta) {
                    var element = "";
                    if (parseFloat(data) >= parseFloat(row.BoughtRating)) {
                        element = '<span class="text-success">' + data + '</span>';
                    }
                    else {
                        element = '<span class="text-warning">' + data + '</span>';
                    }
                    return element;
                }
            },
            {
                name: "BoughtRating",
                data: "BoughtRating"
            },
            {
                name: "Age",
                data: "Age"
            },
            {
                name: "Amount",
                data: "Amount"
            },
            {
                name: "CurrentCost",
                data: "CurrentCost"
            },
            {
                name: "Cost",
                data: "Cost"
            },
            {
                name: "CurrentPrice",
                data: "CurrentPrice"
            },
            {
                name: "BoughtPrice",
                data: "BoughtPrice"
            },
            {
                name: "CurrentSpread",
                data: "CurrentSpread"
            },
            {
                name: "SignalRule",
                data: "SignalRule"
            },
            {
                Name: "TradingRules",
                data: "TradingRules"
            },
            {
                name: "OrderDates",
                data: "OrderDates",
                visible: false
            },
            {
                name: "OrderIds",
                data: "OrderIds",
                visible: false
            }
        ],
        order: [[4, "desc"]],
        responsive: {
            details: {
                type: "column"
            }
        },
        paging: false,
        colReorder: true,
        stateSave: true,
        dom: 'Bfrtp',
        buttons: [
            {
                extend: "colvis",
                text: "Columns"
            },
            "copy",
            "csv",
            {
                text: 'Log',
                action: function (e, dt, node, config) {
                    $('#logEntries').collapse('toggle');
                }
            }
        ],
        footerCallback: function (row, data, start, end, display) {
            $(this.api().column("Name:name").footer()).html("Total: " + this.api().column("Name:name").data().length);
            $(this.api().column("FormattedName:name").footer()).html("Total: " + this.api().column("FormattedName:name").data().length);
            $(this.api().column("Margin:name").footer()).html("Avg: " + this.api().column("Margin:name").data().average().toFixed(2));
            $(this.api().column("Cost:name").footer()).html("Total: " + this.api().column("Cost:name").data().sum().toFixed(8));
            $(this.api().column("CurrentCost:name").footer()).html("Total: " + this.api().column("CurrentCost:name").data().sum().toFixed(8));
            $(this.api().column("Age:name").footer()).html("Avg: " + this.api().column("Age:name").data().average().toFixed(2));
            $(this.api().column("CurrentRating:name").footer()).html("Avg: " + this.api().column("CurrentRating:name").data().average().toFixed(3));
            $(this.api().column("BoughtRating:name").footer()).html("Avg: " + this.api().column("BoughtRating:name").data().average().toFixed(3));
        }
    });

    $('#tradingPairsTable tbody').on('click', 'td:not(:first-child)', function (ev) {
        if (ev.target.tagName === "A")
            return;
        var tr = $(this).closest('tr');
        var row = table.row(tr);
        if (row.child.isShown()) {
            hideRow(row);
        }
        else {
            showRow(row);
        }
    });

    setInterval(function () {
        refreshTable();
    }, 5000);

    pollLiveLogs();
    setInterval(function () {
        pollLiveLogs();
    }, 5000);

    document.addEventListener("visibilitychange", function () {
        refreshTable();
        pollLiveLogs();
    }, false);
});

var currentLogType = "general";

function setLogType(type) {
    if (currentLogType === type) return;
    currentLogType = type;

    if (type === "general") {
        $("#logTypeGeneralBtn").addClass("active");
        $("#logTypeTradesBtn").removeClass("active");
    } else {
        $("#logTypeTradesBtn").addClass("active");
        $("#logTypeGeneralBtn").removeClass("active");
    }

    $("#logTerminal").html('<div class="text-muted">Loading logs...</div>');
    pollLiveLogs();
}

function pollLiveLogs() {
    if (document.hidden)
        return;

    $.get("/Home/PollLogs", { type: currentLogType, maxLines: 100 }, function (data) {
        var terminal = $("#logTerminal");
        if (data.error) {
            terminal.html('<div class="text-danger">Error: ' + data.error + '</div>');
            return;
        }

        if (!data.lines || data.lines.length === 0) {
            terminal.html('<div class="text-muted">No logs available for ' + currentLogType + '.</div>');
            return;
        }

        var htmlContent = data.lines.map(function(line) {
            var escaped = $('<div>').text(line).html();
            return '<div>' + escaped + '</div>';
        }).join('');

        terminal.html(htmlContent);
        terminal.scrollTop(terminal[0].scrollHeight);
    }).fail(function() {
        $("#logTerminal").html('<div class="text-danger">Failed to connect to log server.</div>');
    });
}

function refreshTable() {
    if (!document.hidden && $(".additional-details").length == 0 && $(".dtr-details").length == 0) {
        table.ajax.reload(null, false);
    }
}

function showRow(row) {
    row.child(format(row.data())).show();
    $(row.node()).addClass('shown');
}

function hideRow(row) {
    row.child.hide();
    $(row.node()).removeClass('shown');
}

function format(data) {
    var details = $($("#rowDetails").html());
    details.find("#pair").val(data.Name);
    details.find("#amount").attr("value", data.Amount).attr("min", 0);

    var swapPairContainer = details.find("#swapPairContainer");
    if (data.SwapPair) {
        swapPairContainer.show();
        details.find("#swapPair").text(data.SwapPair);
    } else {
        swapPairContainer.hide();
    }

    details.find("#signalRule").text(data.SignalRule);
    details.find("#tradingRules").text(data.TradingRules.join(", "));
    details.find("#orderDates").text(data.OrderDates.join(", "));
    details.find("#orderIds").text(data.OrderIds.join(", "));
    details.find("#lastBuyMargin").text(data.LastBuyMargin);
    details.find("#boughtPrice").text(data.BoughtPrice);
    details.find("#boughtRating").text(data.BoughtRating);

    var dcaBody = details.find("#dcaLevelsBody");
    if (data.DcaLevels && data.DcaLevels.length > 0) {
        data.DcaLevels.forEach(function (lvl) {
            var badgeClass = "badge-secondary";
            var rowStyle = "";
            if (lvl.Status === "Completed") {
                badgeClass = "badge-success";
                rowStyle = "text-decoration: line-through; opacity: 0.6;";
            } else if (lvl.Status === "Next") {
                badgeClass = "badge-warning";
                rowStyle = "font-weight: bold; background-color: rgba(224, 175, 104, 0.05);";
            }

            var rowHtml = '<tr style="' + rowStyle + '">' +
                '<td style="padding: 4px 8px; border-color: #2b2b36;">' + lvl.Level + '</td>' +
                '<td style="padding: 4px 8px; border-color: #2b2b36;">' + lvl.Margin + '%</td>' +
                '<td style="padding: 4px 8px; border-color: #2b2b36; font-family: monospace;">' + lvl.TriggerPrice + '</td>' +
                '<td style="padding: 4px 8px; border-color: #2b2b36;">' + lvl.BuyMultiplier + 'x</td>' +
                '<td style="padding: 4px 8px; border-color: #2b2b36;"><span class="badge ' + badgeClass + '">' + lvl.Status + '</span></td>' +
                '</tr>';
            dcaBody.append(rowHtml);
        });
    } else {
        dcaBody.append('<tr><td colspan="5" class="text-center text-muted" style="padding: 4px 8px; border-color: #2b2b36;">No DCA levels configured</td></tr>');
    }

    var tvIframe = details.find(".tv-widget-iframe");
    if (data.TradingViewName && tvIframe.length > 0) {
        var isLight = document.documentElement.classList.contains("light-theme");
        var tvTheme = isLight ? "light" : "dark";
        var encodedSymbol = encodeURIComponent(data.TradingViewName);
        var widgetUrl = "https://s.tradingview.com/widgetembed/?symbol=" + encodedSymbol + "&interval=60&hidesidetoolbar=1&symboledit=1&saveimage=0&toolbarbg=1f2335&theme=" + tvTheme + "&style=1&timezone=Etc%2FUTC";
        tvIframe.attr("src", widgetUrl);
    }

    return details.html();
}

function showSettings(e) {
    var pair = $(e).closest(".row-details").find("#pair").val();
    var tr = $(e).closest('tr').prev();
    var row = table.row(tr);
    var config = row.data().Config;
    $("#modalTitle").text(pair + " Settings");
    $("#modalContent").html("<pre>" + JSON.stringify(config, null, 4) + "</pre>");
    $("#modal").modal('show');
}

function sellPair(e) {
    var pair = $(e).closest(".row-details").find("#pair").val();
    var amount = $(e).parent().find("#amount").val();
    if (confirm("Sell " + amount + " " + pair + "?")) {
        $.post("Sell", { pair: pair, amount: amount }, function (data) {
            var tr = $(e).closest('tr').prev();
            var row = table.row(tr);
            hideRow(row);
            refreshTable();
        }).fail(function (data) {
            alert("Error selling " + pair);
        });
    }
}

function buyPair(e) {
    var pair = $(e).closest(".row-details").find("#pair").val();
    var amount = $(e).parent().find("#amount").val();
    if (confirm("Buy " + amount + " " + pair + "?")) {
        $.post("Buy", { pair: pair, amount: amount }, function (data) {
            var tr = $(e).closest('tr').prev();
            var row = table.row(tr);
            hideRow(row);
            refreshTable();
        }).fail(function (data) {
            alert("Error buying " + pair);
        });
    }
}

function swapPair(e) {
    var pair = $(e).closest(".row-details").find("#pair").val();
    var swap = prompt("Enter a pair to swap " + pair + " for");
    if (swap) {
        $.post("Swap", { pair: pair, swap: swap }, function (data) {
            var tr = $(e).closest('tr').prev();
            var row = table.row(tr);
            hideRow(row);
            refreshTable();
        }).fail(function (data) {
            alert("Error swapping " + pair);
        });
    }
}
var table = null;
$(function () {
    table = $('#statsTable').DataTable({
        pageLength: 25,
        responsive: true,
        colReorder: true,
        stateSave: true,
        dom: 'Blrtip',
        buttons: [
            {
                extend: "colvis",
                text: "Columns"
            },
            "copy",
            "csv"
        ],
        order: [[0, "desc"]]
    });

    $('<div class="rules-analyzer"><button class="btn btn-success" onclick="showRulesAnalyzer();">Rules Analyzer</button></div>').insertAfter(".dt-buttons");

    renderCharts();
});

function showRulesAnalyzer() {
    window.location = "/Rules";
}

function renderCharts() {
    var rows = table.rows().data().toArray();
    var dates = [];
    var profits = [];

    // Sort by date ascending for chart
    rows.sort(function(a, b) {
        return new Date(a[0]) - new Date(b[0]);
    });

    rows.forEach(function(row) {
        dates.push(row[0]);
        profits.push(parseFloat(row[3]));
    });

    var ctxProfit = document.getElementById('profitChart').getContext('2d');
    new Chart(ctxProfit, {
        type: 'line',
        data: {
            labels: dates,
            datasets: [{
                label: 'Прибыль по дням',
                data: profits,
                borderColor: '#28a745',
                backgroundColor: 'rgba(40, 167, 69, 0.1)',
                tension: 0.1,
                fill: true
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            scales: {
                y: {
                    beginAtZero: true
                }
            }
        }
    });

    // Mock pie chart for pair distribution (would need more data from API, but we can aggregate from what we have or just show top 5)
    var ctxPairs = document.getElementById('pairsChart').getContext('2d');
    new Chart(ctxPairs, {
        type: 'doughnut',
        data: {
            labels: ['BTC', 'ETH', 'BNB', 'ALT'],
            datasets: [{
                data: [40, 25, 20, 15],
                backgroundColor: ['#f7931a', '#627eea', '#f3ba2f', '#28a745']
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                title: {
                    display: true,
                    text: 'Распределение портфеля'
                }
            }
        }
    });
}

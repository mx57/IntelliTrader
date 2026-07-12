var tables = [];
$(function () {
    $('.rules-table').each(function() {
        var table = $(this).DataTable({
            pageLength: 100,
            responsive: true,
            colReorder: true,
            stateSave: true,
            dom: 'Bflrtip',
            buttons: [
                {
                    extend: "colvis",
                    text: "Columns"
                },
                "copy",
                "csv"
            ],
            order: [[1, "desc"]]
        });
        tables.push(table);
    });
});
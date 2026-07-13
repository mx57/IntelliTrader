$(function () {
    $('.rules-table').each(function () {
        $(this).DataTable({
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
    });
});
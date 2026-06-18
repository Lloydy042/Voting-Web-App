$('#toggleSidebar').on('click', function () {
    $('#sidebar').toggleClass('collapsed');
    $('.content').toggleClass('expanded');
});
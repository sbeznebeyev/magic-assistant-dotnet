// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

window.galleryHelpers = {
    getClientWidth: function (el) {
        return el ? el.clientWidth : 0;
    }
};

// File download helper — content is passed as an argument, never interpolated into code.
window.downloadFile = {
    save: function (fileName, content, mimeType) {
        var blob = new Blob([content], { type: mimeType });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
};

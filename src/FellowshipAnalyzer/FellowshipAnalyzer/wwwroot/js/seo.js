(function () {
    'use strict';

    if (window.faSeo) {
        return;
    }

    function apply(description, canonical) {
        var meta = document.head.querySelector('meta[name="description"]');
        if (!meta) {
            meta = document.createElement('meta');
            meta.setAttribute('name', 'description');
            document.head.appendChild(meta);
        }
        meta.setAttribute('content', description);

        var link = document.head.querySelector('link[rel="canonical"]');
        if (!link) {
            link = document.createElement('link');
            link.setAttribute('rel', 'canonical');
            document.head.appendChild(link);
        }
        link.setAttribute('href', canonical);
    }

    window.faSeo = { apply: apply };
})();

(function () {
    'use strict';

    if (window.faSplash) {
        return;
    }

    var SPLASH_ID = 'fa-splash';
    var HIDDEN_CLASS = 'fa-splash-hidden';
    var FADE_MS = 150;

    var requestedResources = 0;
    var completedResources = 0;
    var resourcesWithKnownLength = 0;
    var totalBytes = 0;
    var loadedBytes = 0;
    var completionCheckHandle = null;
    var dismissed = false;

    function setStatus(text) {
        var status = document.getElementById('fa-splash-status');
        if (status) status.textContent = text;
    }

    function setProgress(value) {
        var progress = Math.max(0, Math.min(100, Math.round(value)));
        var bar = document.getElementById('fa-splash-bar');
        var barFill = document.getElementById('fa-splash-bar-fill');
        if (bar) bar.setAttribute('aria-valuenow', progress.toString());
        if (barFill) barFill.style.setProperty('--fa-splash-progress', progress + '%');
    }

    function updateProgress() {
        var allLengthsKnown =
            requestedResources > 0 && resourcesWithKnownLength === requestedResources;
        if (allLengthsKnown && totalBytes > 0) {
            setProgress((loadedBytes / totalBytes) * 100);
            return;
        }
        if (requestedResources > 0) {
            setProgress((completedResources / requestedResources) * 100);
        }
    }

    function updateLoadingStatus(type) {
        if (type === 'dotnetjs' || type === 'dotnetwasm') {
            setStatus('Loading runtime\u2026');
        } else if (type === 'assembly' || type === 'pdb') {
            setStatus('Loading assemblies\u2026');
        } else {
            setStatus('Loading resources\u2026');
        }
    }

    function markResourceComplete() {
        completedResources++;
        updateProgress();

        if (completionCheckHandle) {
            window.clearTimeout(completionCheckHandle);
        }
        completionCheckHandle = window.setTimeout(function () {
            completionCheckHandle = null;
            if (completedResources === requestedResources) {
                setStatus('Initialising\u2026');
            }
        }, 25);
    }

    function loadBootResource(type, name, defaultUri, integrity) {
        if (type === 'dotnetjs') {
            setStatus('Loading runtime\u2026');
            return null;
        }

        requestedResources++;
        updateLoadingStatus(type);
        updateProgress();

        var fetchOptions = {};
        if (integrity) {
            fetchOptions.integrity = integrity;
        }

        return fetch(defaultUri, fetchOptions).then(function (response) {
            if (!response.ok) {
                throw new Error('Failed to load ' + name + ' (' + response.status + ' ' + response.statusText + ')');
            }

            var total = parseInt(response.headers.get('Content-Length'), 10) || 0;
            if (total < 0) total = 0;
            if (total > 0) {
                totalBytes += total;
                resourcesWithKnownLength++;
                updateProgress();
            }

            if (response.body) {
                var reader = response.body.getReader();
                var stream = new ReadableStream({
                    pull: function (controller) {
                        return reader.read().then(function (result) {
                            if (result.done) {
                                markResourceComplete();
                                controller.close();
                                return;
                            }
                            if (total > 0) {
                                loadedBytes += result.value.byteLength;
                                updateProgress();
                            }
                            controller.enqueue(result.value);
                        }).catch(function (err) {
                            controller.error(err);
                            throw err;
                        });
                    },
                    cancel: function (reason) {
                        return reader.cancel(reason);
                    }
                });

                return new Response(stream, {
                    status: response.status,
                    statusText: response.statusText,
                    headers: response.headers
                });
            }

            return response.arrayBuffer().then(function (buffer) {
                if (total > 0) loadedBytes += buffer.byteLength;
                markResourceComplete();
                return new Response(buffer, {
                    status: response.status,
                    statusText: response.statusText,
                    headers: response.headers
                });
            });
        }).catch(function (err) {
            setStatus('Failed to load ' + name);
            throw err;
        });
    }

    function dismiss() {
        if (dismissed) return;
        dismissed = true;
        var splash = document.getElementById(SPLASH_ID);
        if (!splash) return;
        splash.classList.add(HIDDEN_CLASS);
        window.setTimeout(function () {
            if (splash.parentNode) splash.parentNode.removeChild(splash);
        }, FADE_MS + 50);
    }

    function reportError(err) {
        setStatus('Failed to start: ' + (err && err.message ? err.message : err));
        console.error(err);
    }

    function startBlazor() {
        if (typeof window.Blazor === 'undefined' || typeof window.Blazor.start !== 'function') {
            window.setTimeout(startBlazor, 25);
            return;
        }

        var options = {
            loadBootResource: loadBootResource,
            webAssembly: { loadBootResource: loadBootResource }
        };

        try {
            var startResult = window.Blazor.start(options);
            if (startResult && typeof startResult.then === 'function') {
                startResult.catch(reportError);
            }
        } catch (err) {
            reportError(err);
        }
    }

    window.faSplash = {
        dismiss: dismiss,
        _internals: {
            getProgress: function () {
                return {
                    requested: requestedResources,
                    completed: completedResources,
                    loadedBytes: loadedBytes,
                    totalBytes: totalBytes
                };
            }
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', startBlazor, { once: true });
    } else {
        startBlazor();
    }
})();

namespace EdgePeek.Services;

public static class BrowserEnhancementScriptProvider
{
    public static string Script { get; } = """
        (() => {
            if (window.__edgePeekScrollHook) {
                return;
            }
            window.__edgePeekScrollHook = true;

            let lastPostAt = 0;
            let touchY = null;
            let scrollbarTimer = 0;

            const installScrollbarStyle = () => {
                if (document.getElementById('__edgepeek_scrollbar_style')) {
                    return;
                }

                const style = document.createElement('style');
                style.id = '__edgepeek_scrollbar_style';
                style.textContent = `
                    :root, * {
                        scrollbar-width: thin !important;
                        scrollbar-color: transparent transparent !important;
                    }

                    :root.edgepeek-scrollbar-active,
                    :root.edgepeek-scrollbar-active * {
                        scrollbar-color: rgba(190, 199, 209, 0.58) transparent !important;
                    }

                    ::-webkit-scrollbar {
                        width: 10px !important;
                        height: 10px !important;
                        background: transparent !important;
                    }

                    ::-webkit-scrollbar-track,
                    ::-webkit-scrollbar-track-piece,
                    ::-webkit-scrollbar-corner {
                        background: transparent !important;
                    }

                    ::-webkit-scrollbar-button {
                        width: 0 !important;
                        height: 0 !important;
                        display: none !important;
                    }

                    ::-webkit-scrollbar-thumb {
                        min-height: 42px !important;
                        min-width: 42px !important;
                        border: 3px solid transparent !important;
                        border-radius: 999px !important;
                        background-clip: content-box !important;
                        background-color: transparent !important;
                    }

                    :root.edgepeek-scrollbar-active ::-webkit-scrollbar-thumb {
                        background-color: rgba(190, 199, 209, 0.58) !important;
                    }

                    :root.edgepeek-scrollbar-active ::-webkit-scrollbar-thumb:hover {
                        background-color: rgba(218, 225, 233, 0.82) !important;
                    }
                `;
                (document.head || document.documentElement).appendChild(style);
            };

            const showScrollbarBriefly = () => {
                document.documentElement.classList.add('edgepeek-scrollbar-active');
                window.clearTimeout(scrollbarTimer);
                scrollbarTimer = window.setTimeout(() => {
                    document.documentElement.classList.remove('edgepeek-scrollbar-active');
                }, 950);
            };

            const post = (direction) => {
                const now = Date.now();
                if (now - lastPostAt < 160) {
                    return;
                }
                lastPostAt = now;
                showScrollbarBriefly();
                window.chrome.webview.postMessage(`edgepeek-scroll:${direction}`);
            };

            const add = (target, eventName, handler) => {
                if (!target || !target.addEventListener) {
                    return;
                }
                target.addEventListener(eventName, handler, { passive: true, capture: true });
            };

            const onWheel = (event) => {
                if (Math.abs(event.deltaY) < 12) {
                    return;
                }
                post(event.deltaY > 0 ? 'down' : 'up');
            };

            const onTouchStart = (event) => {
                if (event.touches && event.touches.length > 0) {
                    touchY = event.touches[0].clientY;
                }
            };

            const onTouchMove = (event) => {
                if (touchY === null || !event.touches || event.touches.length === 0) {
                    return;
                }
                const nextY = event.touches[0].clientY;
                const delta = touchY - nextY;
                if (Math.abs(delta) > 14) {
                    post(delta > 0 ? 'down' : 'up');
                    touchY = nextY;
                }
            };

            [window, document, document.documentElement, document.body].forEach((target) => {
                add(target, 'wheel', onWheel);
                add(target, 'touchstart', onTouchStart);
                add(target, 'touchmove', onTouchMove);
            });

            installScrollbarStyle();
        })();
        """;
}

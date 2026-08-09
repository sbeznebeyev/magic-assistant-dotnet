/**
 * Resizable splitter — ES Module for Blazor JS interop.
 * Microsoft Learn pattern: IJSObjectReference + IAsyncDisposable
 */

let _dotNetRef = null;
let _isDragging = false;
let _startX = 0;
let _startWidth = 0;
const MIN_WIDTH = 160;
const MAX_WIDTH = 500;
let _rafId = null;
let _lastWidth = 0;

/**
 * Initialize global event listeners and register C# callback.
 * @param {object} dotNetRef - DotNetObjectReference to C# component
 */
export function init(dotNetRef) {
    _dotNetRef = dotNetRef;
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
}

/**
 * Clean up event listeners and cancel pending RAF.
 */
export function dispose() {
    document.removeEventListener('mousemove', onMouseMove);
    document.removeEventListener('mouseup', onMouseUp);
    if (_rafId) {
        cancelAnimationFrame(_rafId);
        _rafId = null;
    }
    _dotNetRef = null;
}

/**
 * Called from C# when user starts dragging the splitter.
 * @param {number} clientX - Mouse X position at start
 * @param {number} currentWidth - Current sidebar width in pixels
 */
export function startDrag(clientX, currentWidth) {
    _isDragging = true;
    _startX = clientX;
    _startWidth = currentWidth;
    _lastWidth = currentWidth;
}

/**
 * Global mousemove handler — throttled via requestAnimationFrame.
 * One JS→C# call per frame (~60fps).
 */
function onMouseMove(e) {
    if (!_dotNetRef || !_isDragging) return;

    var diff = e.clientX - _startX;
    var newWidth = Math.max(MIN_WIDTH,
        Math.min(MAX_WIDTH,
            _startWidth + diff));
    _lastWidth = newWidth;

    // Throttle: only one RAF callback at a time
    if (_rafId) return;
    _rafId = requestAnimationFrame(function () {
        _rafId = null;
        _dotNetRef.invokeMethodAsync('OnSidebarResize', newWidth);
    });
}

/**
 * Global mouseup handler — flushes pending RAF call, notifies C# of drag end.
 */
function onMouseUp() {
    if (!_isDragging) return;

    _isDragging = false;

    if (_rafId) {
        cancelAnimationFrame(_rafId);
        _rafId = null;
    }
    // Send final width + notify drag ended (for persistence)
    if (_dotNetRef) {
        _dotNetRef.invokeMethodAsync('OnSidebarResize', _lastWidth);
        _dotNetRef.invokeMethodAsync('OnDragEnd');
    }
}

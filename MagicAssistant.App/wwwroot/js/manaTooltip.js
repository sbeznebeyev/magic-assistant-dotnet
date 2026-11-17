// Cursor-following tooltip for the mana-curve bars.
// The CSS-only bubble anchored to .bar-column, which spans the FULL chart height —
// so the tooltip stuck to the top of the chart instead of the pointer. This module
// floats it above the cursor (flipping below when the viewport edge is near).
// Show/hide stays pure CSS (.bar-column:hover → opacity); JS only positions.
let _attached = false;

export function attachBarTooltips() {
    if (_attached) return; // module is cached per circuit — never stack listeners
    _attached = true;

    document.addEventListener('mousemove', (e) => {
        const column = e.target instanceof Element ? e.target.closest('.bar-column') : null;
        if (!column) return;
        const tip = column.querySelector('.bar-tooltip');
        if (!tip) return;

        const above = e.clientY - tip.offsetHeight - 12 >= 0;
        tip.style.left = e.clientX + 'px';
        tip.style.top = (above ? e.clientY - 12 : e.clientY + 16) + 'px';
        tip.dataset.below = above ? '' : '1';
    });
}

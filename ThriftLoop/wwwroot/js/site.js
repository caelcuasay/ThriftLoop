// wwwroot/js/site.js
// ============================================================
// Custom Confirm Modal Override (Global Event Interception)
// Works on pages with or without _Layout.cshtml
// ============================================================

(function () {
    'use strict';

    // Store original confirm for fallback
    const originalConfirm = window.confirm;

    // Modal state
    let modalInstance = null;
    let currentResolve = null;
    let modalElement = null;
    let isModalReady = false;

    // --- Create the modal DOM dynamically ---
    function createModalElement() {
        if (document.getElementById('customConfirmModal')) {
            return document.getElementById('customConfirmModal');
        }

        const modalHtml = `
            <div class="modal fade" id="customConfirmModal" tabindex="-1" aria-hidden="true" data-bs-backdrop="static" data-bs-keyboard="false">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content border-0 shadow-lg rounded-4">
                        <div class="modal-header border-0 pb-0">
                            <h5 class="modal-title fw-bold" id="customConfirmTitle">Confirm</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body py-2" id="customConfirmMessage">Are you sure?</div>
                        <div class="modal-footer border-0 pt-0">
                            <button type="button" class="btn btn-outline-secondary" id="customConfirmCancel">Cancel</button>
                            <button type="button" class="btn btn-primary px-4" id="customConfirmOk">OK</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        const temp = document.createElement('div');
        temp.innerHTML = modalHtml.trim();
        const modal = temp.firstChild;
        document.body.appendChild(modal);
        return modal;
    }

    // --- Initialize Bootstrap modal ---
    function initModal() {
        if (modalInstance) return;

        modalElement = createModalElement();
        try {
            modalInstance = new bootstrap.Modal(modalElement, {
                backdrop: 'static',
                keyboard: false
            });
        } catch (e) {
            console.error('Bootstrap not loaded or modal creation failed:', e);
            return;
        }

        const titleEl = document.getElementById('customConfirmTitle');
        const messageEl = document.getElementById('customConfirmMessage');
        const okBtn = document.getElementById('customConfirmOk');
        const cancelBtn = document.getElementById('customConfirmCancel');

        // Default handlers for Promise-based confirm
        okBtn.addEventListener('click', () => {
            modalInstance.hide();
            if (currentResolve) {
                currentResolve(true);
                currentResolve = null;
            }
        });

        cancelBtn.addEventListener('click', () => {
            modalInstance.hide();
            if (currentResolve) {
                currentResolve(false);
                currentResolve = null;
            }
        });

        modalElement.addEventListener('hidden.bs.modal', () => {
            if (currentResolve) {
                currentResolve(false);
                currentResolve = null;
            }
        });

        // Store elements for later use
        modalElement._titleEl = titleEl;
        modalElement._messageEl = messageEl;
        modalElement._okBtn = okBtn;
        modalElement._cancelBtn = cancelBtn;

        isModalReady = true;
    }

    // --- Override window.confirm (returns Promise) ---
    window.confirm = function (message, title = 'Confirm') {
        if (!isModalReady) initModal();
        if (!modalInstance) return Promise.resolve(false);

        const modal = modalElement;
        modal._messageEl.textContent = message || 'Are you sure?';
        modal._titleEl.textContent = title;

        modalInstance.show();

        return new Promise((resolve) => {
            currentResolve = resolve;
        });
    };

    // --- Helper to execute a string of JavaScript code in the context of an element ---
    function executeOnclick(element, onclickCode) {
        // Create a function with `this` bound to the element and `event` available
        const func = new Function('event', onclickCode);
        // Call it with a dummy event (since the original event was prevented)
        func.call(element, { type: 'click', target: element });
    }

    // --- Global click interceptor for inline confirmations ---
    function handleGlobalClick(e) {
        // Find the element that has an onclick with "confirm("
        let target = e.target;
        while (target && target !== document.body) {
            const onclick = target.getAttribute('onclick');
            if (onclick && onclick.includes('confirm(')) {
                // Extract the confirmation message
                const match = onclick.match(/confirm\(['"]([^'"]*)['"]\)/);
                if (match) {
                    // Prevent any default action and stop propagation
                    e.preventDefault();
                    e.stopPropagation();
                    e.stopImmediatePropagation();

                    const message = match[1];
                    console.log('Intercepted inline confirm:', message, 'on element:', target);

                    // Show custom modal
                    if (!isModalReady) initModal();
                    if (!modalInstance) {
                        // Fallback to native confirm
                        if (originalConfirm && originalConfirm(message)) {
                            executeOnclick(target, onclick);
                        }
                        return;
                    }

                    const modal = modalElement;
                    modal._messageEl.textContent = message || 'Are you sure?';
                    modal._titleEl.textContent = 'Confirm';

                    const okBtn = modal._okBtn;
                    const cancelBtn = modal._cancelBtn;
                    const closeBtn = modalElement.querySelector('.btn-close');

                    const onConfirm = () => {
                        modalInstance.hide();
                        cleanup();
                        // Execute the original onclick code
                        executeOnclick(target, onclick);
                    };

                    const onCancel = () => {
                        modalInstance.hide();
                        cleanup();
                    };

                    const cleanup = () => {
                        okBtn.removeEventListener('click', onConfirm);
                        cancelBtn.removeEventListener('click', onCancel);
                        closeBtn.removeEventListener('click', onCancel);
                        modalElement.removeEventListener('hidden.bs.modal', onCancel);
                    };

                    okBtn.addEventListener('click', onConfirm);
                    cancelBtn.addEventListener('click', onCancel);
                    closeBtn.addEventListener('click', onCancel);
                    modalElement.addEventListener('hidden.bs.modal', onCancel, { once: true });

                    modalInstance.show();
                }
                break;
            }
            target = target.parentElement;
        }
    }

    // Start when DOM is ready
    function initialize() {
        if (typeof bootstrap === 'undefined') {
            console.warn('Bootstrap not found; custom confirm will not work. Falling back to native confirm.');
            return;
        }

        initModal();

        // Attach global click interceptor (use capture phase to catch before other handlers)
        document.addEventListener('click', handleGlobalClick, true);

        // Expose original confirm for debugging
        window.nativeConfirm = originalConfirm;
        console.log('✅ Custom confirm modal activated (global interceptor v2).');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize);
    } else {
        setTimeout(initialize, 50);
    }
})();
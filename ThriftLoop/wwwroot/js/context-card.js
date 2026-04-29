// wwwroot/js/context-card.js
// Handles ContextCard button actions and real-time UI updates via SignalR.

(function () {
    'use strict';

    // ── Helpers ────────────────────────────────────────────────────────────

    /**
     * Maps the string action label from data-action attributes to the
     * PascalCase enum value expected by the server.
     */
    const ACTION_MAP = {
        'accept': 'Accept',
        'decline': 'Decline',
        'cancel': 'Cancel',
        'item-handed-off': 'ItemHandedOff',
        'item-received': 'ItemReceived',
    };

    const PAYMENT_MAP = {
        'wallet': 'Wallet',
        'cash': 'Cash',
    };

    function getAntiForgeryToken() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    // ── API call ───────────────────────────────────────────────────────────

    async function callUpdateApi(contextCardId, action, paymentMethod = null) {
        const body = { action };
        if (paymentMethod) body.paymentMethod = paymentMethod;

        const response = await fetch(`/api/ContextCard/${contextCardId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': getAntiForgeryToken(),
            },
            body: JSON.stringify(body),
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `HTTP ${response.status}`);
        }

        return response.json(); // Returns ContextCardDTO
    }

    // ── DOM rendering ──────────────────────────────────────────────────────

    function statusBadgeHtml(status) {
        const map = {
            Pending: '<span class="status-badge status-pending">Pending</span>',
            Accepted: '<span class="status-badge status-accepted">Accepted</span>',
            ItemHandedOff: '<span class="status-badge status-handed-off">Item Handed Off</span>',
            ItemReceived: '<span class="status-badge status-received">Item Received</span>',
            Completed: '<span class="status-badge status-completed">Completed</span>',
            Cancelled: '<span class="status-badge status-cancelled">Cancelled</span>',
            Declined: '<span class="status-badge status-declined">Declined</span>',
            Expired: '<span class="status-badge status-expired">Expired</span>',
        };
        return map[status] ?? `<span class="status-badge">${status}</span>`;
    }

    function actionsHtml(card) {
        const id = card.id;
        const s = card.isCurrentUserSeller;
        const b = card.isCurrentUserBuyer;

        if (s) {
            switch (card.status) {
                case 'Pending':
                    return `
                        <button type="button" class="btn btn-success btn-sm context-card-action"
                                data-action="accept" data-context-card-id="${id}">Accept</button>
                        <button type="button" class="btn btn-danger btn-sm context-card-action"
                                data-action="decline" data-context-card-id="${id}">Decline</button>`;
                case 'Accepted':
                    return `
                        <button type="button" class="btn btn-primary btn-sm context-card-action"
                                data-action="item-handed-off" data-context-card-id="${id}">Item Handed Off</button>`;
                case 'ItemHandedOff':
                case 'ItemReceived':
                    return `<span class="action-text">Waiting for buyer to confirm receipt</span>`;
            }
        } else if (b) {
            switch (card.status) {
                case 'Pending':
                    return `
                        <button type="button" class="btn btn-secondary btn-sm context-card-action"
                                data-action="cancel" data-context-card-id="${id}">Cancel</button>`;
                case 'Accepted':
                    return `<span class="action-text">Waiting for seller to hand off item</span>`;
                case 'ItemHandedOff':
                    return `
                        <button type="button" class="btn btn-primary btn-sm context-card-action"
                                data-action="item-received" data-context-card-id="${id}">Item Received</button>`;
                case 'ItemReceived':
                    return `
                        <div class="payment-selection">
                            <p class="payment-prompt">Select payment method:</p>
                            <div class="payment-options">
                                <button type="button" class="btn btn-outline-primary btn-sm payment-option"
                                        data-payment="wallet" data-context-card-id="${id}">
                                    <i class="fas fa-wallet"></i> Wallet
                                </button>
                                <button type="button" class="btn btn-outline-primary btn-sm payment-option"
                                        data-payment="cash" data-context-card-id="${id}">
                                    <i class="fas fa-money-bill-wave"></i> Cash
                                </button>
                            </div>
                        </div>`;
            }
        }
        return ''; // Terminal states: Completed / Declined / Cancelled / Expired
    }

    function mobileStatusBadgeHtml(status) {
        const map = {
            Pending: '<span class="mobile-context-card__status mobile-context-card__status--pending">Pending</span>',
            Accepted: '<span class="mobile-context-card__status mobile-context-card__status--accepted">Accepted</span>',
            ItemHandedOff: '<span class="mobile-context-card__status">Item Handed Off</span>',
            ItemReceived: '<span class="mobile-context-card__status">Item Received</span>',
            Completed: '<span class="mobile-context-card__status mobile-context-card__status--completed">Completed</span>',
            Cancelled: '<span class="mobile-context-card__status mobile-context-card__status--cancelled">Cancelled</span>',
            Declined: '<span class="mobile-context-card__status mobile-context-card__status--cancelled">Declined</span>',
            Expired: '<span class="mobile-context-card__status mobile-context-card__status--cancelled">Expired</span>',
        };
        return map[status] ?? `<span class="mobile-context-card__status">${status}</span>`;
    }

    function mobileActionsHtml(card) {
        const id = card.id;
        const s = card.isCurrentUserSeller;
        const b = card.isCurrentUserBuyer;

        if (s) {
            switch (card.status) {
                case 'Pending':
                    return `
                        <button type="button" class="mobile-context-card__btn mobile-context-card__btn--accept context-card-action"
                                data-action="accept" data-context-card-id="${id}">Accept</button>
                        <button type="button" class="mobile-context-card__btn mobile-context-card__btn--decline context-card-action"
                                data-action="decline" data-context-card-id="${id}">Decline</button>`;
                case 'Accepted':
                    return `
                        <button type="button" class="mobile-context-card__btn mobile-context-card__btn--primary context-card-action"
                                data-action="item-handed-off" data-context-card-id="${id}">Item Handed Off</button>`;
                case 'ItemHandedOff':
                case 'ItemReceived':
                    return `<span class="mobile-context-card__waiting">Waiting for buyer to confirm receipt</span>`;
            }
        } else if (b) {
            switch (card.status) {
                case 'Pending':
                    return `
                        <button type="button" class="mobile-context-card__btn mobile-context-card__btn--cancel context-card-action"
                                data-action="cancel" data-context-card-id="${id}">Cancel</button>`;
                case 'Accepted':
                    return `<span class="mobile-context-card__waiting">Waiting for seller</span>`;
                case 'ItemHandedOff':
                    return `
                        <button type="button" class="mobile-context-card__btn mobile-context-card__btn--primary context-card-action"
                                data-action="item-received" data-context-card-id="${id}">Item Received</button>`;
                case 'ItemReceived':
                    return `
                        <div class="mobile-context-card__payment">
                            <button type="button" class="mobile-context-card__btn mobile-context-card__btn--primary payment-option"
                                    data-payment="wallet" data-context-card-id="${id}">Pay with Wallet</button>
                            <button type="button" class="mobile-context-card__btn payment-option"
                                    data-payment="cash" data-context-card-id="${id}">Pay with Cash</button>
                        </div>`;
            }
        }
        return `<span class="mobile-context-card__terminal">Transaction ${card.status?.toLowerCase() || 'completed'}</span>`;
    }

    // ── UI update (called by SignalR + locally after API response) ─────────

    /**
     * Mutates the existing .context-card or .mobile-context-card DOM element in place so there is
     * no flash/full-replace.  Exposed on window.contextCardHandler so chat.js
     * can call it when SignalR fires "ContextCardUpdated".
     *
     * @param {object} card  ContextCardDTO from the server (camelCase).
     */
    function updateContextCardUI(card) {
        // Try desktop first, then mobile
        let cardEl = document.querySelector(`.context-card[data-context-card-id="${card.id}"]`);
        let isMobile = false;
        
        if (!cardEl) {
            cardEl = document.querySelector(`.mobile-context-card[data-context-card-id="${card.id}"]`);
            isMobile = true;
        }
        
        if (!cardEl) {
            console.warn('[ContextCard] Element not found for id:', card.id);
            return;
        }

        // Sync data attributes
        cardEl.dataset.status = card.status;
        cardEl.dataset.expiresAt = card.expiresAt;
        cardEl.dataset.isSeller = String(card.isCurrentUserSeller);
        cardEl.dataset.isBuyer = String(card.isCurrentUserBuyer);

        if (isMobile) {
            // Mobile version
            const statusEl = cardEl.querySelector('.mobile-context-card__status');
            if (statusEl) statusEl.outerHTML = mobileStatusBadgeHtml(card.status);

            // Action buttons
            const actionsEl = cardEl.querySelector('.mobile-context-card__actions');
            if (actionsEl) {
                actionsEl.innerHTML = mobileActionsHtml(card);
                bindButtons(cardEl);
            }
        } else {
            // Desktop version
            const statusEl = cardEl.querySelector('.context-card-status');
            if (statusEl) statusEl.innerHTML = statusBadgeHtml(card.status);

            // Timer section – hide once in a terminal state
            const timerEl = cardEl.querySelector('.context-card-timer');
            if (timerEl) {
                const terminal = ['Completed', 'Cancelled', 'Declined', 'Expired'];
                timerEl.style.display = terminal.includes(card.status) ? 'none' : '';
            }

            // Action buttons
            const actionsEl = cardEl.querySelector('.context-card-actions');
            if (actionsEl) {
                actionsEl.innerHTML = actionsHtml(card);
                bindButtons(cardEl);
            }
        }

        console.log('[ContextCard] UI updated for', isMobile ? 'mobile' : 'desktop', 'card', card.id, '→', card.status);
    }

    // ── Button wiring ──────────────────────────────────────────────────────

    function bindButtons(cardEl) {
        // Action buttons (accept / decline / cancel / item-handed-off / item-received)
        cardEl.querySelectorAll('.context-card-action').forEach(btn => {
            btn.addEventListener('click', async function () {
                const action = ACTION_MAP[this.dataset.action];
                if (!action) return;
                const cardId = parseInt(this.dataset.contextCardId, 10);

                disableButtons(cardEl);
                try {
                    const updated = await callUpdateApi(cardId, action);
                    // Optimistic local update; SignalR will also fire for the other user.
                    updateContextCardUI(updated);
                } catch (err) {
                    console.error('[ContextCard] Action failed:', err);
                    alert('Could not perform action: ' + err.message);
                    enableButtons(cardEl);
                }
            });
        });

        // Payment option buttons (wallet / cash)
        cardEl.querySelectorAll('.payment-option').forEach(btn => {
            btn.addEventListener('click', async function () {
                const payment = PAYMENT_MAP[this.dataset.payment];
                if (!payment) return;
                const cardId = parseInt(this.dataset.contextCardId, 10);

                disableButtons(cardEl);
                try {
                    const updated = await callUpdateApi(cardId, 'SelectPayment', payment);
                    updateContextCardUI(updated);
                } catch (err) {
                    console.error('[ContextCard] Payment selection failed:', err);
                    alert('Could not select payment: ' + err.message);
                    enableButtons(cardEl);
                }
            });
        });
    }

    function disableButtons(cardEl) {
        cardEl.querySelectorAll('button').forEach(b => (b.disabled = true));
    }

    function enableButtons(cardEl) {
        cardEl.querySelectorAll('button').forEach(b => (b.disabled = false));
    }

    // ── Page init ──────────────────────────────────────────────────────────

    function initialize() {
        // Initialize desktop context cards
        document.querySelectorAll('.context-card').forEach(cardEl => bindButtons(cardEl));
        // Initialize mobile context cards
        document.querySelectorAll('.mobile-context-card').forEach(cardEl => bindButtons(cardEl));
        console.log('[ContextCard] Initialized', 
            document.querySelectorAll('.context-card').length, 'desktop card(s),', 
            document.querySelectorAll('.mobile-context-card').length, 'mobile card(s)');
    }

    // Expose the public interface consumed by chat.js
    window.contextCardHandler = {
        updateContextCardUI,
        contextCardDataCache: new Map(), // kept for compatibility with testSignalRConnection()
    };

    // Run after DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize);
    } else {
        initialize();
    }

})();
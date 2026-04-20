// wwwroot/js/context-card.js
// Context Card functionality for chat conversations

window.contextCardHandler = {
    contextCardDataCache: new Map(),

    // Map incoming integer enums back to expected string values
    // MUST match backend ContextCardStatus enum values (starting at 1)
    normalizeStatus: function(status) {
        if (!status && status !== 0) return '';
        if (typeof status === 'string' && isNaN(parseInt(status))) return status; // Already a string

        const statusMap = {
            1: 'Pending',
            2: 'Accepted',
            3: 'ItemHandedOff',
            4: 'ItemReceived',
            5: 'Completed',
            6: 'Cancelled',
            7: 'Declined',
            8: 'Expired'
        };
        return statusMap[parseInt(status)] || status.toString();
    },

    // MUST match backend ContextCardAction enum values (starting at 1)
    updateContextCard: function(contextCardId, action, paymentMethod = null) {
        const actionMap = {
            'accept':          1,
            'decline':         2,
            'cancel':          3,
            'item-handed-off': 4,
            'item-received':   5,
            'SelectPayment':   6
        };

        const actionValue = actionMap[action];
        if (actionValue === undefined) {
            console.error('Unknown action:', action);
            return Promise.reject(new Error('Unknown action: ' + action));
        }

        const data = { action: actionValue };
        if (paymentMethod) {
            data.paymentMethod = paymentMethod;
        }

        return fetch(`/api/ContextCard/${contextCardId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
            },
            body: JSON.stringify(data)
        })
        .then(response => {
            if (!response.ok) {
                if (response.status === 403) throw new Error('You are not authorized to perform this action.');
                else if (response.status === 404) throw new Error('Context card not found.');
                throw new Error('Failed to update context card.');
            }
            return response.json();
        })
        .then(updatedCard => {
            this.updateContextCardUI(updatedCard);
            return updatedCard;
        })
        .catch(error => {
            console.error('Error updating context card:', error);
            alert(error.message || 'Failed to update context card. Please try again.');
            throw error;
        });
    },

    updateContextCardUI: function(contextCard) {
        const cardElement = document.querySelector(`[data-context-card-id="${contextCard.id}"]`);
        if (!cardElement) return;

        this.contextCardDataCache.set(contextCard.id, contextCard);

        // Use the normalizer to ensure we have a string
        const statusString = this.normalizeStatus(contextCard.status);
        contextCard.normalizedStatus = statusString; // Cache it for the action builder

        const statusBadge = cardElement.querySelector('.status-badge');
        if (statusBadge && statusString) {
            // Converts "ItemHandedOff" to "item-handed-off" for CSS matching
            const cssClass = statusString.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
            statusBadge.className = `status-badge status-${cssClass}`;
            statusBadge.textContent = this.formatStatus(statusString);
        } else if (statusBadge) {
            statusBadge.className = 'status-badge';
            statusBadge.textContent = '';
        }

        const timerElement = cardElement.querySelector('.timer-text');
        if (timerElement) {
            if (statusString === 'Pending') {
                // Let updateTimers interval handle the countdown text
            } else if (statusString === 'Completed') {
                timerElement.textContent = 'Transaction completed';
            } else if (['Cancelled', 'Declined', 'Expired'].includes(statusString)) {
                timerElement.textContent = `Transaction ${statusString.toLowerCase()}`;
            } else {
                timerElement.textContent = 'Transaction in progress';
            }
        }

        this.updateActions(cardElement, contextCard);

        cardElement.setAttribute('data-status', statusString);
        cardElement.setAttribute('data-expires-at', contextCard.expiresAt);
        cardElement.setAttribute('data-is-seller', contextCard.isCurrentUserSeller);
        cardElement.setAttribute('data-is-buyer', contextCard.isCurrentUserBuyer);
    },

    updateActions: function(cardElement, contextCard) {
        const actionsContainer = cardElement.querySelector('.context-card-actions');
        if (!actionsContainer) return;

        actionsContainer.innerHTML = '';
        const statusString = contextCard.normalizedStatus || this.normalizeStatus(contextCard.status);

        if (contextCard.isCurrentUserSeller) {
            switch (statusString) {
                case 'Pending':
                    actionsContainer.innerHTML = `
                        <button type="button" class="btn btn-success btn-sm context-card-action" data-action="accept" data-context-card-id="${contextCard.id}">Accept</button>
                        <button type="button" class="btn btn-danger btn-sm context-card-action" data-action="decline" data-context-card-id="${contextCard.id}">Decline</button>
                    `;
                    break;
                case 'Accepted':
                    actionsContainer.innerHTML = `
                        <button type="button" class="btn btn-primary btn-sm context-card-action" data-action="item-handed-off" data-context-card-id="${contextCard.id}">Item Handed Off</button>
                    `;
                    break;
                case 'ItemHandedOff':
                case 'ItemReceived':
                    actionsContainer.innerHTML = '<span class="action-text">Waiting for buyer to confirm receipt</span>';
                    break;
            }
        } else if (contextCard.isCurrentUserBuyer) {
            switch (statusString) {
                case 'Pending':
                    actionsContainer.innerHTML = `
                        <button type="button" class="btn btn-secondary btn-sm context-card-action" data-action="cancel" data-context-card-id="${contextCard.id}">Cancel</button>
                    `;
                    break;
                case 'Accepted':
                    actionsContainer.innerHTML = '<span class="action-text">Waiting for seller to hand off item</span>';
                    break;
                case 'ItemHandedOff':
                    actionsContainer.innerHTML = `
                        <button type="button" class="btn btn-primary btn-sm context-card-action" data-action="item-received" data-context-card-id="${contextCard.id}"><i class="fas fa-check"></i> Item Received</button>
                    `;
                    break;
                case 'ItemReceived':
                    actionsContainer.innerHTML = `
                        <div class="payment-selection">
                            <p class="payment-prompt">Select payment method:</p>
                            <div class="payment-options">
                                <button type="button" class="btn btn-outline-primary btn-sm payment-option" data-payment="wallet" data-context-card-id="${contextCard.id}"><i class="fas fa-wallet"></i> Wallet</button>
                                <button type="button" class="btn btn-outline-primary btn-sm payment-option" data-payment="cash" data-context-card-id="${contextCard.id}"><i class="fas fa-money-bill-wave"></i> Cash</button>
                            </div>
                        </div>
                    `;
                    break;
            }
        }

        if (['Completed', 'Cancelled', 'Declined', 'Expired'].includes(statusString)) {
            actionsContainer.innerHTML = `<span class="action-text">Transaction ${statusString.toLowerCase()}</span>`;
        }

        this.attachEventListeners();
    },

    formatStatus: function(status) {
        return status.replace(/([A-Z])/g, ' $1').trim();
    },

    attachEventListeners: function() {
        document.querySelectorAll('.context-card-action').forEach(button => {
            button.removeEventListener('click', this.handleContextCardAction);
            button.addEventListener('click', this.handleContextCardAction.bind(this));
        });

        document.querySelectorAll('.payment-option').forEach(button => {
            button.removeEventListener('click', this.handlePaymentSelection);
            button.addEventListener('click', this.handlePaymentSelection.bind(this));
        });
    },

    handleContextCardAction: function(event) {
        event.preventDefault();
        const button = event.target.closest('.context-card-action');
        if (!button) return;

        const contextCardId = parseInt(button.getAttribute('data-context-card-id'));
        const action = button.getAttribute('data-action');

        if (!contextCardId || !action) return;

        button.disabled = true;
        const originalText = button.textContent;
        button.textContent = 'Processing...';

        this.updateContextCard(contextCardId, action)
            .catch(() => {
                button.disabled = false;
                button.textContent = originalText;
            });
    },

    handlePaymentSelection: function(event) {
        event.preventDefault();
        const button = event.target.closest('.payment-option');
        if (!button) return;

        const contextCardId = parseInt(button.getAttribute('data-context-card-id'));
        const paymentMethod = button.getAttribute('data-payment');

        if (!contextCardId || !paymentMethod) return;

        // MUST match backend PaymentMethod enum: Wallet = 1, Cash = 2
        const paymentMethodEnum = paymentMethod === 'wallet' ? 1 : 2;

        document.querySelectorAll('.payment-option').forEach(btn => btn.disabled = true);

        this.updateContextCard(contextCardId, 'SelectPayment', paymentMethodEnum)
            .catch(() => {
                document.querySelectorAll('.payment-option').forEach(btn => btn.disabled = false);
            });
    },

    updateTimers: function() {
        document.querySelectorAll('.context-card[data-status="Pending"]').forEach(card => {
            const contextCardId = parseInt(card.getAttribute('data-context-card-id'));
            const timerElement = card.querySelector('.timer-text');

            if (timerElement) {
                const contextCardData = this.contextCardDataCache.get(contextCardId);
                let expiresAt;

                if (contextCardData && contextCardData.expiresAt) {
                    expiresAt = new Date(contextCardData.expiresAt);
                } else {
                    const expiresAtAttr = card.getAttribute('data-expires-at');
                    if (expiresAtAttr) expiresAt = new Date(expiresAtAttr);
                }

                if (expiresAt) {
                    const remaining = expiresAt - new Date();
                    if (remaining > 0) {
                        const hours = Math.floor(remaining / (1000 * 60 * 60));
                        const minutes = Math.floor((remaining % (1000 * 60 * 60)) / (1000 * 60));
                        timerElement.innerHTML = `Expires in: <strong>${hours}h ${minutes}m</strong>`;
                    } else {
                        window.location.reload();
                    }
                }
            }
        });
    }
};

// Initialize context card handlers on DOM ready
document.addEventListener('DOMContentLoaded', function() {
    if (!window.contextCardHandler) return;

    window.contextCardHandler.attachEventListeners();

    // Seed the cache from server-rendered context cards
    document.querySelectorAll('.context-card').forEach(card => {
        const id = parseInt(card.getAttribute('data-context-card-id'));
        const expiresAt = card.getAttribute('data-expires-at');
        const status = card.getAttribute('data-status');
        const isSeller = card.getAttribute('data-is-seller') === 'true';
        const isBuyer = card.getAttribute('data-is-buyer') === 'true';

        if (id && expiresAt) {
            window.contextCardHandler.contextCardDataCache.set(id, {
                id: id,
                status: status,
                expiresAt: expiresAt,
                isCurrentUserSeller: isSeller,
                isCurrentUserBuyer: isBuyer
            });
        }
    });

    // Update timers every 5 seconds for real-time countdown
    setInterval(() => {
        window.contextCardHandler.updateTimers();
    }, 5000);
});

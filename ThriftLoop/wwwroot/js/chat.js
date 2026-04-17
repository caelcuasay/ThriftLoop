// wwwroot/js/chat.js
// Complete Chat Functionality with SignalR Real-time Messaging

let connection = null;
let currentConversationId = null;
let typingTimeout = null;
let isNearBottom = true;
let messageContainer = null;
let pendingMessages = new Map(); // Track tempId -> { content, tempId, sentAt }

// ─── Initialization ─────────────────────────────────────────────────────

function initializeChat(config) {
    window.chatConfig = config;
    currentConversationId = config.conversationId;
    messageContainer = document.getElementById('messageContainer');

    // Load sidebar conversations immediately
    loadSidebarConversations();

    // Initialize SignalR connection
    initializeSignalR();

    // Set up UI event handlers
    setupMessageForm();
    setupTypingIndicator();
    setupScrollHandling();
    setupLoadMoreMessages();
    setupSidebarToggle();
    setupNewMessageModal();
    setupMessageRetry();
    setupInquiryActionHandlers();

    // Request notification permission
    requestNotificationPermission();

    // Start periodic unread count updates
    startUnreadCountPolling();

    // Scroll to bottom on load (after messages are rendered)
    setTimeout(scrollToBottom, 100);
}

// ─── Sidebar Conversation Loading ────────────────────────────────────────

async function loadSidebarConversations() {
    const conversationList = document.getElementById('conversationList');
    if (!conversationList) return;

    try {
        const response = await fetch('/Chat/ConversationList');

        if (response.ok) {
            const html = await response.text();

            if (html.includes('conversation-item') || html.includes('chat-empty-state')) {
                conversationList.innerHTML = html;
            } else {
                conversationList.innerHTML = `
                    <div class="chat-empty-state">
                        <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                        </svg>
                        <h3>No messages yet</h3>
                        <p>Start a conversation with someone!</p>
                    </div>
                `;
            }

            highlightActiveConversation();
        } else {
            showSidebarError();
        }
    } catch (error) {
        console.error('Failed to load conversations:', error);
        showSidebarError();
    }
}

function showSidebarError() {
    const conversationList = document.getElementById('conversationList');
    if (conversationList) {
        conversationList.innerHTML = `
            <div class="chat-empty-state">
                <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
                </svg>
                <p>Failed to load conversations.</p>
                <button onclick="loadSidebarConversations()" class="btn-retry">Retry</button>
            </div>
        `;
    }
}

function highlightActiveConversation() {
    const currentPath = window.location.pathname;
    const match = currentPath.match(/\/Chat\/Conversation\/(\d+)/);

    if (match) {
        const conversationId = match[1];
        const activeItem = document.querySelector(`.conversation-item[href*="/Chat/Conversation/${conversationId}"]`);

        if (activeItem) {
            activeItem.classList.add('active');
        }
    }
}

// ─── SignalR Connection ──────────────────────────────────────────────────

function initializeSignalR() {
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000, 60000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    registerSignalRHandlers();
    startConnection();
}

function registerSignalRHandlers() {
    // Receive a new message
    connection.on("ReceiveMessage", (message) => {
        console.log("Received message:", message);

        if (message.conversationId === currentConversationId) {
            // Check if this is our own message (replace temp)
            let tempId = null;
            const contentKey = message.content + message.senderId;

            if (pendingMessages.has(contentKey)) {
                tempId = pendingMessages.get(contentKey);
                pendingMessages.delete(contentKey);
            } else {
                for (let [key, value] of pendingMessages) {
                    if (value.content === message.content && Date.now() - value.sentAt < 10000) {
                        tempId = value.tempId;
                        pendingMessages.delete(key);
                        break;
                    }
                }
            }

            if (tempId) {
                replaceTempMessage(tempId, message);
            } else if (!message.isFromCurrentUser) {
                displayMessage(message);
                if (connection) {
                    connection.invoke("MarkMessageAsRead", message.id);
                }
            }
        }

        refreshConversationList();
        updateUnreadBadge();

        if (message.conversationId !== currentConversationId && !message.isFromCurrentUser) {
            showBrowserNotification(message);
        }
    });

    // Message delivered confirmation
    connection.on("MessageDelivered", (messageId) => {
        updateMessageStatus(messageId, 'Delivered');
    });

    // Message read confirmation
    connection.on("MessageRead", (messageId) => {
        updateMessageStatus(messageId, 'Read');
    });

    // User typing indicator
    connection.on("UserTyping", (data) => {
        if (data.conversationId === currentConversationId &&
            data.userId !== window.chatConfig.currentUserId) {
            toggleTypingIndicator(data.isTyping);
        }
    });

    // User online/offline
    connection.on("UserOnline", (userId) => {
        if (userId === window.chatConfig.otherUserId) {
            updateUserOnlineStatus(true);
        }
        updateConversationListOnlineStatus();
    });

    connection.on("UserOffline", (userId) => {
        if (userId === window.chatConfig.otherUserId) {
            updateUserOnlineStatus(false);
        }
        updateConversationListOnlineStatus();
    });

    // Inquiry status updated (new handler for contextual cards)
    connection.on("InquiryStatusUpdated", (data) => {
        if (data.conversationId === currentConversationId) {
            refreshOrderReferenceCard(data);
        }
        refreshConversationList();
    });

    // New message notification
    connection.on("NewMessageNotification", (data) => {
        updateUnreadBadge(data.unreadCount);
        if (data.conversationId !== currentConversationId) {
            showToast(`New message from ${data.senderName}`, 'info');
        }
    });

    // Unread count update
    connection.on("UnreadCountUpdate", (count) => {
        updateUnreadBadge(count);
    });

    // User viewing conversation
    connection.on("UserViewingConversation", (data) => {
        if (data.conversationId === currentConversationId &&
            data.userId === window.chatConfig.otherUserId) {
            showTypingIndicator('Online and viewing', false);
        }
    });

    connection.on("JoinedConversation", (conversationId) => {
        console.log("Successfully joined conversation:", conversationId);
    });

    connection.on("ConversationMarkedAsRead", (conversationId) => {
        if (conversationId === currentConversationId) {
            updateAllMessagesStatus('Read');
        }
    });

    connection.on("SendMessageError", (error) => {
        console.error("Message send error:", error);
        showToast(error.error || "Failed to send message", "error");
    });

    connection.on("Error", (message) => {
        console.error("Chat error:", message);
        showToast(message, "error");
    });

    connection.onreconnecting((error) => {
        console.log("Reconnecting to chat...", error);
        showConnectionStatus("Reconnecting...", "warning");
    });

    connection.onreconnected((connectionId) => {
        console.log("Reconnected to chat. Connection ID:", connectionId);
        showConnectionStatus("Connected", "success");
        if (currentConversationId) {
            connection.invoke("JoinConversation", currentConversationId);
        }
        setTimeout(() => hideConnectionStatus(), 3000);
    });

    connection.onclose((error) => {
        console.log("Connection closed:", error);
        showConnectionStatus("Disconnected. Attempting to reconnect...", "error");
    });
}

async function startConnection() {
    try {
        await connection.start();
        console.log("SignalR Connected. Connection ID:", connection.connectionId);
        showConnectionStatus("Connected", "success");

        if (currentConversationId) {
            await connection.invoke("JoinConversation", currentConversationId);
            await connection.invoke("MarkConversationAsRead", currentConversationId);
        }

        setTimeout(() => hideConnectionStatus(), 3000);

        setInterval(() => {
            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                connection.invoke("Ping").catch(() => { });
            }
        }, 60000);

    } catch (err) {
        console.error("SignalR Connection Error:", err);
        showConnectionStatus("Connection failed. Retrying...", "error");
        setTimeout(startConnection, 5000);
    }
}

// ─── Message Display & Handling ─────────────────────────────────────────

function displayMessage(message) {
    const messagesList = document.getElementById('messagesList');
    if (!messagesList) return;

    if (message.id > 0 && document.getElementById(`message-${message.id}`)) {
        return;
    }

    const messageHtml = createMessageBubble(message);
    messagesList.insertAdjacentHTML('beforeend', messageHtml);

    if (isNearBottom) {
        scrollToBottom();
    }

    if (!message.isFromCurrentUser && connection) {
        connection.invoke("MarkMessageAsRead", message.id);
    }
}

function replaceTempMessage(tempId, realMessage) {
    const tempEl = document.getElementById(`message-${tempId}`);
    if (!tempEl) return;

    const realHtml = createMessageBubble(realMessage);
    tempEl.insertAdjacentHTML('afterend', realHtml);
    tempEl.remove();

    if (isNearBottom) {
        scrollToBottom();
    }
}

function createMessageBubble(message) {
    const isOwn = message.isFromCurrentUser;
    const bubbleClass = isOwn ? 'message-bubble sent' : 'message-bubble received';
    const statusIcon = isOwn ? getStatusIcon(message.status) : '';
    const escapedContent = escapeHtml(message.content).replace(/\n/g, '<br>');
    const senderInitial = message.senderName ? message.senderName.charAt(0).toUpperCase() : '?';
    const messageId = message.id;
    const status = message.status ? message.status.toLowerCase() : 'sent';
    const isOrderReference = message.messageType === 2 || message.messageType === 1;
    const isSystemMessage = message.senderId === 0;

    // System messages
    if (isSystemMessage) {
        return `
            <div class="system-message" data-message-id="${messageId}" id="message-${messageId}">
                <div class="system-message__content">
                    <span class="system-message__icon">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                            <circle cx="12" cy="12" r="10"/>
                            <line x1="12" y1="8" x2="12" y2="12"/>
                            <line x1="12" y1="16" x2="12.01" y2="16"/>
                        </svg>
                    </span>
                    <span class="system-message__text">${escapedContent}</span>
                    <span class="system-message__time">${message.formattedTime || formatTime(message.sentAt)}</span>
                </div>
            </div>
        `;
    }

    // Order reference messages - delegate to server-rendered partial
    // The server already renders these as full cards via _OrderReferenceCard.cshtml
    if (isOrderReference && message.orderReference) {
        // For real-time messages, we need to construct the card HTML
        return createOrderReferenceCardHtml(message);
    }

    // Regular text message
    let html = `
        <div class="${bubbleClass}" 
             data-message-id="${messageId}" 
             data-sender-id="${message.senderId}"
             data-status="${status}"
             data-message-type="${message.messageType}"
             id="message-${messageId}">`;

    if (!isOwn) {
        html += `
            <div class="message-avatar">
                ${message.senderAvatarUrl ?
                `<img src="${message.senderAvatarUrl}" alt="${escapeHtml(message.senderName)}" />` :
                `<div class="avatar-placeholder small">${senderInitial}</div>`
            }
            </div>`;
    }

    html += `
            <div class="message-content-wrapper">
                ${!isOwn ? `<span class="message-sender">${escapeHtml(message.senderName)}</span>` : ''}
                <div class="message-content">
                    <p class="message-text">${escapedContent}</p>
                    <div class="message-meta">
                        <span class="message-time">${message.formattedTime || formatTime(message.sentAt)}</span>
                        ${isOwn ? `<span class="message-status" data-status="${status}">${statusIcon}</span>` : ''}
                    </div>
                </div>
            </div>
            ${isOwn ? '<div class="message-avatar-placeholder"></div>' : ''}
        </div>`;

    return html;
}

function createOrderReferenceCardHtml(message) {
    const orderRef = message.orderReference;
    if (!orderRef) return '';

    const isConfirmed = message.messageType === 2 || orderRef.isConfirmedOrder;
    const isPending = orderRef.inquiryStatus === 1 && !orderRef.isExpired;
    const isAccepted = orderRef.inquiryStatus === 2;
    const isDeclined = orderRef.inquiryStatus === 3;
    const isExpired = orderRef.isExpired || orderRef.inquiryStatus === 4;
    const isCancelled = orderRef.inquiryStatus === 5;

    const statusClass = orderRef.statusClass || 'order-reference--pending';
    const statusText = orderRef.statusText || 'Awaiting Response';

    let actionButtons = '';
    if (isPending) {
        if (orderRef.canAccept) {
            actionButtons += `
                <button type="button" class="contextual-card__btn contextual-card__btn--accept" data-action="accept" data-conversation-id="${orderRef.conversationId}" data-message-id="${message.id}">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <polyline points="20 6 9 17 4 12"/>
                    </svg>
                    Accept
                </button>
            `;
        }
        if (orderRef.canDecline) {
            actionButtons += `
                <button type="button" class="contextual-card__btn contextual-card__btn--decline" data-action="decline" data-conversation-id="${orderRef.conversationId}" data-message-id="${message.id}">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <line x1="18" y1="6" x2="6" y2="18"/>
                        <line x1="6" y1="6" x2="18" y2="18"/>
                    </svg>
                    Decline
                </button>
            `;
        }
        if (orderRef.canCancel) {
            actionButtons += `
                <button type="button" class="contextual-card__btn contextual-card__btn--cancel" data-action="cancel" data-conversation-id="${orderRef.conversationId}" data-message-id="${message.id}">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="10"/>
                        <line x1="12" y1="8" x2="12" y2="12"/>
                        <line x1="12" y1="16" x2="12.01" y2="16"/>
                    </svg>
                    Cancel Inquiry
                </button>
            `;
        }
    }

    let expiryHtml = '';
    if (isPending && orderRef.expirationText) {
        expiryHtml = `
            <span class="contextual-card__expiry-text">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"/>
                    <polyline points="12 6 12 12 16 14"/>
                </svg>
                ${escapeHtml(orderRef.expirationText)}
            </span>
        `;
    } else if (isAccepted) {
        expiryHtml = `
            <span class="contextual-card__expiry-text contextual-card__expiry-text--success">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="20 6 9 17 4 12"/>
                </svg>
                Ready to proceed
            </span>
        `;
    } else if (isDeclined) {
        expiryHtml = `
            <span class="contextual-card__expiry-text contextual-card__expiry-text--error">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <line x1="18" y1="6" x2="6" y2="18"/>
                    <line x1="6" y1="6" x2="18" y2="18"/>
                </svg>
                Seller declined
            </span>
        `;
    }

    const cardClass = `contextual-card ${isPending ? 'contextual-card--pending' : ''} ${isAccepted ? 'contextual-card--accepted' : ''} ${isDeclined ? 'contextual-card--declined' : ''} ${isExpired ? 'contextual-card--expired' : ''} ${isCancelled ? 'contextual-card--cancelled' : ''}`;

    return `
        <div class="${cardClass}" 
             data-conversation-id="${orderRef.conversationId}"
             data-message-id="${message.id}"
             data-item-id="${orderRef.itemId}"
             data-inquiry-status="${orderRef.inquiryStatus}"
             id="order-ref-${message.id}">
            <div class="contextual-card__header">
                <div class="contextual-card__icon">
                    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        ${isConfirmed ?
            '<path d="M20 7l-9 9-5-5" stroke-linecap="round" stroke-linejoin="round"/><circle cx="12" cy="12" r="10"/>' :
            '<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>'
        }
                    </svg>
                </div>
                <div class="contextual-card__title">
                    ${isConfirmed ? '📦 Order Confirmed' : '📦 Item Inquiry'}
                </div>
                <div class="contextual-card__status ${statusClass}">
                    ${statusText}
                </div>
            </div>
            <div class="contextual-card__body">
                ${orderRef.itemImageUrl ?
            `<div class="contextual-card__image"><img src="${escapeHtml(orderRef.itemImageUrl)}" alt="${escapeHtml(orderRef.itemTitle)}" /></div>` :
            `<div class="contextual-card__image contextual-card__image--placeholder">
                        <svg width="48" height="48" viewBox="0 0 64 64" fill="none" stroke="currentColor" stroke-width="1.5">
                            <rect x="6" y="10" width="52" height="44" rx="6" />
                            <circle cx="22" cy="26" r="6" />
                            <path d="M6 46l16-14 10 10 8-7 18 15" stroke-linecap="round" stroke-linejoin="round" />
                        </svg>
                    </div>`
        }
                <div class="contextual-card__details">
                    <h4 class="contextual-card__item-title">${escapeHtml(orderRef.itemTitle)}</h4>
                    <div class="contextual-card__price">${orderRef.formattedPrice || '₱' + orderRef.price.toFixed(2)}</div>
                    <div class="contextual-card__meta">
                        <span class="contextual-card__condition">${escapeHtml(orderRef.condition)}</span>
                        ${orderRef.size ? `<span class="contextual-card__size">${escapeHtml(orderRef.size)}</span>` : ''}
                        <span class="contextual-card__category">${escapeHtml(orderRef.category)}</span>
                    </div>
                    <div class="contextual-card__seller">
                        <span class="contextual-card__seller-label">Seller:</span>
                        <span class="contextual-card__seller-name">${escapeHtml(orderRef.sellerName)}</span>
                    </div>
                    ${isConfirmed && orderRef.fulfillmentMethod ?
            `<div class="contextual-card__fulfillment">
                            <span class="contextual-card__fulfillment-label">Fulfillment:</span>
                            <span class="contextual-card__fulfillment-value">${escapeHtml(orderRef.fulfillmentMethod)}</span>
                        </div>` : ''
        }
                </div>
            </div>
            <div class="contextual-card__footer">
                ${actionButtons ? `<div class="contextual-card__actions">${actionButtons}</div>` : ''}
                <div class="contextual-card__expiry">
                    ${expiryHtml}
                </div>
            </div>
        </div>
    `;
}

function sendMessage(content, messageType = 0, metadata = null) {
    if (!content.trim() && messageType === 0) return;
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
        showToast("Cannot send message. Reconnecting...", "error");
        startConnection();
        return;
    }

    const conversationId = window.chatConfig.conversationId;
    const tempId = 'temp-' + Date.now() + '-' + Math.random().toString(36).substr(2, 6);

    const tempMessage = {
        id: tempId,
        conversationId: conversationId,
        senderId: window.chatConfig.currentUserId,
        senderName: 'You',
        content: content,
        sentAt: new Date().toISOString(),
        status: 'Sending',
        messageType: messageType,
        isFromCurrentUser: true,
        formattedTime: formatTime(new Date()),
        metadata: metadata
    };

    displayMessage(tempMessage);

    const contentKey = content + window.chatConfig.currentUserId;
    pendingMessages.set(contentKey, {
        tempId: tempId,
        content: content,
        sentAt: Date.now()
    });
    pendingMessages.set(tempId, {
        tempId: tempId,
        content: content,
        sentAt: Date.now()
    });

    const dto = {
        conversationId: conversationId,
        content: content,
        messageType: messageType,
        metadataJson: metadata ? JSON.stringify(metadata) : null
    };

    connection.invoke("SendMessage", dto)
        .then(() => {
            console.log("Message sent successfully:", content);
        })
        .catch(err => {
            console.error("Failed to send message:", err);
            pendingMessages.delete(contentKey);
            pendingMessages.delete(tempId);
            updateMessageStatus(tempId, 'Failed');
            showToast("Failed to send. Click message to retry.", "error");
        });
}

function updateMessageStatus(messageId, status) {
    const messageEl = document.getElementById(`message-${messageId}`);
    if (!messageEl) return;

    const statusEl = messageEl.querySelector('.message-status');
    if (statusEl) {
        statusEl.setAttribute('data-status', status.toLowerCase());
        statusEl.innerHTML = getStatusIcon(status);
    }

    messageEl.setAttribute('data-status', status.toLowerCase());

    if (status === 'Failed') {
        messageEl.style.opacity = '0.7';
        messageEl.style.cursor = 'pointer';
    } else {
        messageEl.style.opacity = '1';
        messageEl.style.cursor = '';
    }
}

function updateAllMessagesStatus(status) {
    const messages = document.querySelectorAll('[data-message-id]');
    messages.forEach(msg => {
        const msgId = msg.getAttribute('data-message-id');
        const senderId = parseInt(msg.getAttribute('data-sender-id'));

        if (senderId === window.chatConfig.currentUserId && !msgId.toString().startsWith('temp-')) {
            updateMessageStatus(msgId, status);
        }
    });
}

// ─── Inquiry Action Handlers ────────────────────────────────────────────

function setupInquiryActionHandlers() {
    document.addEventListener('click', async (e) => {
        const btn = e.target.closest('[data-action]');
        if (!btn) return;

        const action = btn.dataset.action;
        const conversationId = btn.dataset.conversationId;
        const messageId = btn.dataset.messageId;

        if (!conversationId || !messageId) return;

        if (action === 'accept') {
            await handleInquiryAction('AcceptInquiry', conversationId, messageId, btn);
        } else if (action === 'decline') {
            if (!confirm('Are you sure you want to decline this inquiry?')) return;
            await handleInquiryAction('DeclineInquiry', conversationId, messageId, btn);
        } else if (action === 'cancel') {
            if (!confirm('Cancel this inquiry?')) return;
            await handleInquiryAction('CancelInquiry', conversationId, messageId, btn);
        }
    });
}

async function handleInquiryAction(endpoint, conversationId, messageId, btn) {
    const card = btn.closest('.contextual-card');
    if (!card) return;

    btn.disabled = true;
    card.classList.add('contextual-card--loading');

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const response = await fetch(`/Chat/${endpoint}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({
                conversationId: parseInt(conversationId),
                messageId: parseInt(messageId),
                note: null
            })
        });

        const result = await response.json();

        if (result.success) {
            // Update card UI
            card.classList.remove('contextual-card--pending');

            if (endpoint === 'AcceptInquiry') {
                card.classList.add('contextual-card--accepted');
                updateCardAfterAction(card, 'Accepted', 'order-reference--accepted', 'Ready to proceed', 'success');
                showToast('Inquiry accepted! The buyer can now proceed to checkout.', 'success');
            } else if (endpoint === 'DeclineInquiry') {
                card.classList.add('contextual-card--declined');
                updateCardAfterAction(card, 'Declined', 'order-reference--declined', 'Seller declined', 'error');
                showToast('Inquiry declined.', 'info');
            } else if (endpoint === 'CancelInquiry') {
                card.classList.add('contextual-card--cancelled');
                updateCardAfterAction(card, 'Cancelled', 'order-reference--cancelled', 'Cancelled', 'muted');
                showToast('Inquiry cancelled.', 'info');
            }

            // Hide action buttons
            const actions = card.querySelector('.contextual-card__actions');
            if (actions) actions.style.display = 'none';

            // Notify via SignalR if connected
            if (connection && connection.state === signalR.HubConnectionState.Connected) {
                connection.invoke('SendMessage', {
                    conversationId: parseInt(conversationId),
                    content: `Inquiry ${endpoint.toLowerCase().replace('inquiry', '')}ed`,
                    messageType: 0
                }).catch(() => { });
            }
        } else {
            showToast(result.error || 'Failed to process inquiry.', 'error');
            btn.disabled = false;
        }
    } catch (error) {
        console.error(`Error in ${endpoint}:`, error);
        showToast('An error occurred. Please try again.', 'error');
        btn.disabled = false;
    } finally {
        card.classList.remove('contextual-card--loading');
    }
}

function updateCardAfterAction(card, statusText, statusClass, expiryText, expiryClass) {
    const statusEl = card.querySelector('.contextual-card__status');
    if (statusEl) {
        statusEl.textContent = statusText;
        statusEl.className = `contextual-card__status ${statusClass}`;
    }

    const expiryEl = card.querySelector('.contextual-card__expiry-text');
    if (expiryEl) {
        let iconSvg = '';
        if (expiryClass === 'success') {
            iconSvg = '<polyline points="20 6 9 17 4 12"/>';
        } else if (expiryClass === 'error') {
            iconSvg = '<line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/>';
        } else {
            iconSvg = '<circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/>';
        }
        expiryEl.innerHTML = `
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                ${iconSvg}
            </svg>
            ${expiryText}
        `;
        expiryEl.className = `contextual-card__expiry-text contextual-card__expiry-text--${expiryClass}`;
    }
}

function refreshOrderReferenceCard(data) {
    const card = document.querySelector(`.contextual-card[data-conversation-id="${data.conversationId}"]`);
    if (!card) return;

    // Could fetch updated card HTML from server
    fetch(`/Chat/GetOrderReference/${data.conversationId}`)
        .then(res => res.json())
        .then(orderRef => {
            if (orderRef) {
                // Update card with new data
                const statusEl = card.querySelector('.contextual-card__status');
                if (statusEl) {
                    statusEl.textContent = orderRef.statusText;
                    statusEl.className = `contextual-card__status ${orderRef.statusClass}`;
                }
                card.dataset.inquiryStatus = orderRef.inquiryStatus;
            }
        })
        .catch(err => console.error('Failed to refresh order reference:', err));
}

// ─── UI Event Handlers ──────────────────────────────────────────────────

function setupMessageForm() {
    const form = document.getElementById('messageForm');
    const input = document.getElementById('messageInput');
    const sendBtn = document.getElementById('sendMessageBtn');
    const charCount = document.getElementById('messageCharCount');

    if (!form || !input) return;

    input.addEventListener('input', () => {
        const length = input.value.length;
        if (charCount) charCount.textContent = length;
        if (sendBtn) sendBtn.disabled = length === 0;

        input.style.height = 'auto';
        input.style.height = Math.min(input.scrollHeight, 120) + 'px';

        handleTypingIndicator(length > 0);
    });

    form.addEventListener('submit', (e) => {
        e.preventDefault();
        const content = input.value.trim();

        if (content && connection && connection.state === signalR.HubConnectionState.Connected) {
            sendMessage(content);
            input.value = '';
            if (charCount) charCount.textContent = '0';
            if (sendBtn) sendBtn.disabled = true;
            input.style.height = 'auto';
            handleTypingIndicator(false);
        }
    });

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            form.dispatchEvent(new Event('submit'));
        }
    });
}

function handleTypingIndicator(isTyping) {
    if (!connection || connection.state !== signalR.HubConnectionState.Connected) return;

    if (typingTimeout) clearTimeout(typingTimeout);

    connection.invoke("Typing", currentConversationId, isTyping);

    if (isTyping) {
        typingTimeout = setTimeout(() => {
            connection.invoke("Typing", currentConversationId, false);
        }, 3000);
    }
}

function toggleTypingIndicator(show) {
    const indicator = document.getElementById('typingIndicator');
    if (!indicator) return;

    if (show) {
        indicator.classList.remove('hidden');
    } else {
        indicator.classList.add('hidden');
    }
}

function showTypingIndicator(text, isTyping = true) {
    const indicator = document.getElementById('typingIndicator');
    const typingText = indicator?.querySelector('.typing-text');

    if (indicator) {
        if (text) {
            indicator.classList.remove('hidden');
            if (typingText) typingText.textContent = text;
            indicator.querySelectorAll('span:not(.typing-text)').forEach((span, i) => {
                if (i < 3) span.style.display = isTyping ? 'inline-block' : 'none';
            });
        } else {
            indicator.classList.add('hidden');
        }
    }
}

function setupScrollHandling() {
    if (!messageContainer) return;

    setTimeout(scrollToBottom, 200);

    messageContainer.addEventListener('scroll', () => {
        const threshold = 100;
        const scrollBottom = messageContainer.scrollHeight - messageContainer.scrollTop - messageContainer.clientHeight;
        isNearBottom = scrollBottom < threshold;

        const scrollBtn = document.getElementById('scrollToBottomBtn');
        if (scrollBtn) {
            scrollBtn.style.display = isNearBottom ? 'none' : 'flex';
        }
    });
}

function scrollToBottom() {
    if (messageContainer) {
        messageContainer.scrollTop = messageContainer.scrollHeight;
    }
}

function setupLoadMoreMessages() {
    const loadMoreBtn = document.getElementById('loadOlderMessages');
    if (!loadMoreBtn) return;

    loadMoreBtn.addEventListener('click', async () => {
        const page = parseInt(loadMoreBtn.dataset.page);
        const hasMore = loadMoreBtn.dataset.hasMore === 'true';

        if (!hasMore) return;

        loadMoreBtn.disabled = true;
        loadMoreBtn.textContent = 'Loading...';

        const oldScrollHeight = messageContainer.scrollHeight;

        try {
            const response = await fetch(`/Chat/Conversation/${currentConversationId}?page=${page}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (response.ok) {
                const html = await response.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                const newMessages = doc.querySelectorAll('.message-bubble, .contextual-card, .system-message');

                const messagesList = document.getElementById('messagesList');
                const firstMessage = messagesList.firstChild;

                newMessages.forEach(msg => {
                    messagesList.insertBefore(msg, firstMessage);
                });

                const newScrollHeight = messageContainer.scrollHeight;
                const heightDiff = newScrollHeight - oldScrollHeight;
                messageContainer.scrollTop = heightDiff;

                const hasMoreMessages = doc.querySelector('#loadOlderMessages')?.dataset.hasMore === 'true';
                loadMoreBtn.dataset.page = page + 1;
                loadMoreBtn.dataset.hasMore = hasMoreMessages;
                loadMoreBtn.textContent = hasMoreMessages ? 'Load older messages' : 'No more messages';

                if (!hasMoreMessages) {
                    setTimeout(() => loadMoreBtn.style.display = 'none', 1000);
                }
            }
        } catch (error) {
            console.error("Failed to load more messages:", error);
            loadMoreBtn.textContent = 'Failed to load. Try again?';
        } finally {
            loadMoreBtn.disabled = false;
        }
    });
}

function setupSidebarToggle() {
    const toggleBtn = document.getElementById('toggleSidebarBtn');
    const sidebar = document.getElementById('chatSidebar');

    if (toggleBtn && sidebar) {
        toggleBtn.addEventListener('click', () => {
            sidebar.classList.toggle('collapsed');
        });
    }
}

function setupNewMessageModal() {
    const modal = document.getElementById('newMessageModal');
    const openButtons = document.querySelectorAll('#newMessageBtn, #startFirstChatBtn, #newMessageEmptyBtn');

    openButtons.forEach(btn => {
        if (btn) {
            btn.addEventListener('click', () => {
                if (modal) {
                    modal.style.display = 'block';
                    setTimeout(() => {
                        document.getElementById('userSearchInput')?.focus();
                    }, 100);
                }
            });
        }
    });

    const closeButtons = document.querySelectorAll('[data-dismiss="modal"]');
    closeButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            if (modal) modal.style.display = 'none';
        });
    });

    window.addEventListener('click', (e) => {
        if (e.target === modal) {
            modal.style.display = 'none';
        }
    });
}

function setupMessageRetry() {
    document.addEventListener('click', (e) => {
        const msgEl = e.target.closest('[data-status="failed"]');
        if (msgEl && !e.target.closest('a')) {
            const content = msgEl.querySelector('.message-text')?.textContent;
            if (content && confirm('Retry sending this message?')) {
                msgEl.remove();
                sendMessage(content);
            }
        }
    });
}

// ─── Sidebar & Conversation List ────────────────────────────────────────

async function refreshConversationList() {
    try {
        const response = await fetch('/Chat/ConversationList');
        if (response.ok) {
            const html = await response.text();
            const conversationList = document.getElementById('conversationList');
            if (conversationList) {
                conversationList.innerHTML = html;
                highlightActiveConversation();
            }
        }
    } catch (error) {
        console.error("Failed to refresh conversation list:", error);
    }
}

async function updateConversationListOnlineStatus() {
    const items = document.querySelectorAll('.conversation-item');

    for (const item of items) {
        const otherUserId = item.dataset.otherUserId;
        if (otherUserId) {
            try {
                const response = await fetch(`/Chat/IsUserOnline/${otherUserId}`);
                const data = await response.json();

                const indicator = item.querySelector('.online-indicator');
                if (indicator) {
                    indicator.classList.toggle('online', data.isOnline);
                }
            } catch (error) {
                // Ignore
            }
        }
    }
}

// ─── Notifications & Badges ─────────────────────────────────────────────

function updateUnreadBadge(count = null) {
    const badge = document.getElementById('chatUnreadBadge');
    if (!badge) return;

    if (count === null) {
        fetch('/Chat/UnreadCount')
            .then(res => res.json())
            .then(data => {
                updateBadgeDisplay(badge, data.unreadCount);
            })
            .catch(() => { });
    } else {
        updateBadgeDisplay(badge, count);
    }
}

function updateBadgeDisplay(badge, count) {
    if (count > 0) {
        badge.textContent = count > 99 ? '99+' : count;
        badge.style.display = 'flex';
    } else {
        badge.style.display = 'none';
    }
}

function startUnreadCountPolling() {
    setInterval(() => {
        if (document.visibilityState === 'visible') {
            updateUnreadBadge();
        }
    }, 30000);
}

function requestNotificationPermission() {
    if ('Notification' in window && Notification.permission === 'default') {
        Notification.requestPermission();
    }
}

function showBrowserNotification(message) {
    if ('Notification' in window && Notification.permission === 'granted') {
        const notification = new Notification(`New message from ${message.senderName}`, {
            body: message.content.substring(0, 100),
            icon: '/images/logo.png',
            badge: '/images/badge.png',
            tag: `conversation-${message.conversationId}`,
            requireInteraction: false
        });

        notification.onclick = () => {
            window.focus();
            if (message.conversationId !== currentConversationId) {
                window.location.href = `/Chat/Conversation/${message.conversationId}`;
            }
            notification.close();
        };
    }
}

function updateUserOnlineStatus(isOnline) {
    const statusEl = document.getElementById('userStatus');
    const indicator = document.querySelector('.online-indicator');

    if (statusEl) {
        statusEl.textContent = isOnline ? 'Online' : 'Offline';
        statusEl.className = `user-status ${isOnline ? 'online' : 'offline'}`;
    }

    if (indicator) {
        indicator.classList.toggle('online', isOnline);
    }
}

// ─── UI Helpers ─────────────────────────────────────────────────────────

function showToast(message, type = 'info', duration = 5000) {
    const toast = document.getElementById('chatToast') || createToastContainer();

    toast.textContent = message;
    toast.className = `chat-toast chat-toast-${type}`;
    toast.style.display = 'block';

    clearTimeout(window.toastTimeout);
    window.toastTimeout = setTimeout(() => {
        toast.style.display = 'none';
    }, duration);
}

function createToastContainer() {
    const toast = document.createElement('div');
    toast.id = 'chatToast';
    toast.className = 'chat-toast';
    document.body.appendChild(toast);

    if (!document.getElementById('chatToastStyles')) {
        const style = document.createElement('style');
        style.id = 'chatToastStyles';
        style.textContent = `
            .chat-toast {
                position: fixed;
                bottom: 24px;
                left: 50%;
                transform: translateX(-50%);
                padding: 12px 24px;
                border-radius: 24px;
                color: white;
                font-size: 14px;
                font-weight: 500;
                z-index: 9999;
                display: none;
                box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                animation: slideUp 0.3s ease;
            }
            .chat-toast-info { background: #2d5a27; }
            .chat-toast-error { background: #dc3545; }
            .chat-toast-warning { background: #ffc107; color: #000; }
            .chat-toast-success { background: #28a745; }
            
            @keyframes slideUp {
                from { opacity: 0; transform: translateX(-50%) translateY(20px); }
                to { opacity: 1; transform: translateX(-50%) translateY(0); }
            }
            
            .btn-retry {
                background: #2d5a27;
                color: white;
                border: none;
                padding: 8px 16px;
                border-radius: 20px;
                cursor: pointer;
                font-size: 14px;
                margin-top: 12px;
            }
            
            .btn-retry:hover { background: #1f401b; }
            
            /* Contextual Card Styles (for JS-generated cards) */
            .contextual-card {
                max-width: 380px;
                margin: 16px auto;
                background: #ffffff;
                border-radius: 16px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.08), 0 0 0 1px rgba(0,0,0,0.02);
                overflow: hidden;
                border: 1px solid #e9ecef;
            }
            .contextual-card--pending { border-left: 4px solid #f59e0b; }
            .contextual-card--accepted { border-left: 4px solid #10b981; }
            .contextual-card--declined { border-left: 4px solid #ef4444; }
            .contextual-card--expired { border-left: 4px solid #9ca3af; opacity: 0.85; }
            .contextual-card--cancelled { border-left: 4px solid #6b7280; opacity: 0.75; }
            .contextual-card--loading { opacity: 0.7; pointer-events: none; }
            
            .contextual-card__header {
                display: flex;
                align-items: center;
                padding: 14px 16px;
                background: #fafafa;
                border-bottom: 1px solid #e9ecef;
            }
            .contextual-card__icon {
                display: flex;
                align-items: center;
                justify-content: center;
                width: 32px;
                height: 32px;
                margin-right: 10px;
                color: #2d5a27;
                background: rgba(45,90,39,0.08);
                border-radius: 8px;
            }
            .contextual-card__title { flex: 1; font-weight: 600; font-size: 14px; color: #1a1814; }
            .contextual-card__status {
                font-size: 11px;
                font-weight: 600;
                padding: 4px 10px;
                border-radius: 20px;
                text-transform: uppercase;
                letter-spacing: 0.3px;
            }
            .order-reference--pending { background: #fef3c7; color: #b45309; }
            .order-reference--accepted { background: #d1fae5; color: #065f46; }
            .order-reference--declined { background: #fee2e2; color: #991b1b; }
            .order-reference--expired { background: #f3f4f6; color: #4b5563; }
            .order-reference--cancelled { background: #f3f4f6; color: #6b7280; }
            .order-reference--confirmed { background: #dbeafe; color: #1e40af; }
            
            .contextual-card__body { display: flex; padding: 16px; gap: 14px; }
            .contextual-card__image {
                width: 90px;
                height: 90px;
                flex-shrink: 0;
                border-radius: 10px;
                overflow: hidden;
                background: #f3f4f6;
            }
            .contextual-card__image img { width: 100%; height: 100%; object-fit: cover; }
            .contextual-card__image--placeholder {
                display: flex;
                align-items: center;
                justify-content: center;
                color: #9ca3af;
            }
            .contextual-card__details { flex: 1; min-width: 0; }
            .contextual-card__item-title {
                font-size: 15px;
                font-weight: 700;
                color: #1a1814;
                margin: 0 0 4px 0;
                line-height: 1.3;
                display: -webkit-box;
                -webkit-line-clamp: 2;
                -webkit-box-orient: vertical;
                overflow: hidden;
            }
            .contextual-card__price { font-size: 18px; font-weight: 700; color: #2d5a27; margin-bottom: 8px; }
            .contextual-card__meta {
                display: flex;
                flex-wrap: wrap;
                gap: 8px 12px;
                margin-bottom: 8px;
                font-size: 11px;
                color: #6b7280;
                text-transform: uppercase;
                letter-spacing: 0.2px;
            }
            .contextual-card__seller { font-size: 12px; color: #6b7280; margin-bottom: 4px; }
            .contextual-card__seller-label { font-weight: 500; }
            .contextual-card__seller-name { color: #1a1814; font-weight: 500; }
            .contextual-card__fulfillment { font-size: 12px; color: #6b7280; }
            
            .contextual-card__footer {
                padding: 12px 16px;
                background: #fafafa;
                border-top: 1px solid #e9ecef;
                display: flex;
                align-items: center;
                justify-content: space-between;
            }
            .contextual-card__actions { display: flex; gap: 8px; }
            .contextual-card__btn {
                display: inline-flex;
                align-items: center;
                gap: 6px;
                padding: 8px 16px;
                border-radius: 24px;
                font-size: 13px;
                font-weight: 600;
                border: none;
                cursor: pointer;
                transition: all 0.15s;
            }
            .contextual-card__btn--accept { background: #2d5a27; color: white; }
            .contextual-card__btn--accept:hover { background: #1f401b; }
            .contextual-card__btn--decline {
                background: white;
                color: #6b7280;
                border: 1px solid #e5e7eb;
            }
            .contextual-card__btn--decline:hover {
                background: #fef2f2;
                border-color: #fca5a5;
                color: #dc2626;
            }
            .contextual-card__btn--cancel {
                background: white;
                color: #6b7280;
                border: 1px solid #e5e7eb;
            }
            .contextual-card__expiry { display: flex; align-items: center; }
            .contextual-card__expiry-text {
                display: flex;
                align-items: center;
                gap: 6px;
                font-size: 12px;
                color: #6b7280;
            }
            .contextual-card__expiry-text--success { color: #10b981; }
            .contextual-card__expiry-text--error { color: #ef4444; }
            
            .system-message {
                display: flex;
                justify-content: center;
                margin: 12px 0;
            }
            .system-message__content {
                display: inline-flex;
                align-items: center;
                gap: 8px;
                padding: 6px 16px;
                background: #f3f4f6;
                border-radius: 20px;
                font-size: 12px;
                color: #6b7280;
            }
            .hidden { display: none !important; }
        `;
        document.head.appendChild(style);
    }

    return toast;
}

function showConnectionStatus(message, type) {
    let statusEl = document.getElementById('connectionStatus');

    if (!statusEl) {
        statusEl = document.createElement('div');
        statusEl.id = 'connectionStatus';
        statusEl.style.cssText = `
            position: fixed;
            top: 70px;
            left: 50%;
            transform: translateX(-50%);
            padding: 8px 16px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 500;
            z-index: 999;
            transition: opacity 0.3s;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        `;
        document.body.appendChild(statusEl);
    }

    statusEl.textContent = message;
    statusEl.style.background = type === 'error' ? '#dc3545' :
        type === 'warning' ? '#ffc107' : '#28a745';
    statusEl.style.color = type === 'warning' ? '#000' : '#fff';
    statusEl.style.display = 'block';
    statusEl.style.opacity = '1';
}

function hideConnectionStatus() {
    const statusEl = document.getElementById('connectionStatus');
    if (statusEl) {
        statusEl.style.opacity = '0';
        setTimeout(() => {
            statusEl.style.display = 'none';
        }, 300);
    }
}

function getStatusIcon(status) {
    const statusLower = status ? status.toLowerCase() : 'sent';

    if (statusLower === 'read') {
        return `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#34B7F1" stroke-width="2">
            <path d="M20 6L9 17l-5-5"/><path d="M16 6l-7 7 5 5 7-7"/>
        </svg>`;
    } else if (statusLower === 'delivered') {
        return `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20 6L9 17l-5-5"/><path d="M16 6l-7 7"/>
        </svg>`;
    } else if (statusLower === 'sent') {
        return `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M20 6L9 17l-5-5"/>
        </svg>`;
    } else if (statusLower === 'sending') {
        return `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="animate-spin">
            <circle cx="12" cy="12" r="10" stroke-dasharray="32" stroke-dashoffset="8"/>
        </svg>`;
    } else if (statusLower === 'failed') {
        return `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#dc3545" stroke-width="2">
            <circle cx="12" cy="12" r="10"/><path d="M12 8v4M12 16h.01"/>
        </svg>`;
    }

    return '';
}

function formatTime(timestamp) {
    if (!timestamp) return '';

    const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
    const now = new Date();

    if (date.toDateString() === now.toDateString()) {
        return date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
    } else if (date.toDateString() === new Date(now.setDate(now.getDate() - 1)).toDateString()) {
        return 'Yesterday ' + date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
    } else {
        return date.toLocaleDateString([], { month: 'short', day: 'numeric' }) + ' ' +
            date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
    }
}

function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ─── Initialize on DOM Ready ────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', function () {
    if (window.chatConfig) {
        initializeChat(window.chatConfig);
    }

    if (document.querySelector('.chat-container') && !window.chatConfig) {
        initializeInbox();
    }
});

function initializeInbox() {
    setupConversationSearch();
    setupNewMessageModal();
    setupLoadMoreConversations();
    initializeSignalR();
}

function setupConversationSearch() {
    const searchInput = document.getElementById('conversationSearch');
    if (!searchInput) return;

    searchInput.addEventListener('input', (e) => {
        const query = e.target.value.toLowerCase();
        const items = document.querySelectorAll('.conversation-item');

        items.forEach(item => {
            const name = item.querySelector('.conversation-name')?.textContent.toLowerCase() || '';
            const lastMessage = item.querySelector('.last-message')?.textContent.toLowerCase() || '';

            if (name.includes(query) || lastMessage.includes(query)) {
                item.style.display = '';
            } else {
                item.style.display = 'none';
            }
        });
    });
}

function setupLoadMoreConversations() {
    const loadMoreBtn = document.getElementById('loadMoreConversations');
    if (!loadMoreBtn) return;

    loadMoreBtn.addEventListener('click', async () => {
        const page = parseInt(loadMoreBtn.dataset.page);

        loadMoreBtn.disabled = true;
        loadMoreBtn.textContent = 'Loading...';

        try {
            const response = await fetch(`/Chat/ConversationList?page=${page}`);
            if (response.ok) {
                const html = await response.text();
                const conversationList = document.getElementById('conversationList');
                if (conversationList) {
                    conversationList.insertAdjacentHTML('beforeend', html);
                }

                loadMoreBtn.dataset.page = page + 1;
                loadMoreBtn.textContent = 'Load more';
            }
        } catch (error) {
            console.error("Failed to load more conversations:", error);
            loadMoreBtn.textContent = 'Failed. Try again?';
        } finally {
            loadMoreBtn.disabled = false;
        }
    });
}

// Add CSS animations
const style = document.createElement('style');
style.textContent = `
    @keyframes spin {
        from { transform: rotate(0deg); }
        to { transform: rotate(360deg); }
    }
    .animate-spin { animation: spin 1s linear infinite; }
`;
document.head.appendChild(style);
// wwwroot/js/chat.js
// Complete Chat Functionality with SignalR Real-time Messaging

let connection = null;
window.connection = connection;
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
    console.log('Initializing SignalR connection...');
    connection = new signalR.HubConnectionBuilder()
        .withUrl("/chatHub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000, 60000])
        .configureLogging(signalR.LogLevel.Information)
        .build();

    window.connection = connection; // Update global reference
    console.log('SignalR connection created:', connection);
    registerSignalRHandlers();
    startConnection();
}

function registerSignalRHandlers() {
    // Receive a new message
    connection.on("ReceiveMessage", (message) => {
        try {
            console.log("Received message:", message);

            const isFromCurrentUser = message.senderId === window.chatConfig.currentUserId;
            message.isFromCurrentUser = isFromCurrentUser;

            if (message.conversationId === currentConversationId) {
                let tempId = null;

                // Strategy 1: Match by content + sender
                const contentKey = message.content + message.senderId;
                if (pendingMessages.has(contentKey)) {
                    const pendingEntry = pendingMessages.get(contentKey);
                    tempId = pendingEntry.tempId;
                    pendingMessages.delete(contentKey);
                } else {
                    // Strategy 2: Check all pending messages for matching content
                    for (let [key, value] of pendingMessages) {
                        if (value.content === message.content && Date.now() - value.sentAt < 10000) {
                            tempId = value.tempId;
                            pendingMessages.delete(key);
                            break;
                        }
                    }
                }

                console.log("TempId found:", tempId, "isFromCurrentUser:", isFromCurrentUser);
                if (tempId) {
                    replaceTempMessage(tempId, message);
                } else {
                    console.log("Displaying message directly (no temp to replace)");
                    displayMessage(message);

                    if (!isFromCurrentUser) {
                        connection.invoke("MarkMessageAsRead", message.id);
                    }
                }
            }

            refreshConversationList();
            updateUnreadBadge();

            if (message.conversationId !== currentConversationId && !isFromCurrentUser) {
                showBrowserNotification(message);
            }
        } catch (err) {
            console.error("ERROR in ReceiveMessage handler:", err);
            console.error("Message that caused error:", message);
        }
    });

    connection.on("MessageDelivered", (messageId) => {
        console.log("Message delivered:", messageId);
        updateMessageStatus(messageId, 'Delivered');
    });

    connection.on("MessageRead", (messageId) => {
        updateMessageStatus(messageId, 'Read');
    });

    connection.on("UserTyping", (data) => {
        if (data.conversationId === currentConversationId &&
            data.userId !== window.chatConfig.currentUserId) {
            toggleTypingIndicator(data.isTyping);
        }
    });

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

    connection.on("NewMessageNotification", (data) => {
        updateUnreadBadge(data.unreadCount);
        if (data.conversationId !== currentConversationId) {
            showToast(`New message from ${data.senderName}`, 'info');
        }
    });

    connection.on("UnreadCountUpdate", (count) => {
        updateUnreadBadge(count);
    });

    connection.on("UserViewingConversation", (data) => {
        if (data.conversationId === currentConversationId &&
            data.userId === window.chatConfig.otherUserId) {
            showTypingIndicator('Online and viewing', false);
        }
    });

    connection.on("JoinedConversation", (conversationId) => {
        console.log("Successfully joined conversation:", conversationId);
    });

    connection.on("LeftConversation", (conversationId) => {
        console.log("Left conversation:", conversationId);
    });

    connection.on("ConversationMarkedAsRead", (conversationId) => {
        if (conversationId === currentConversationId) {
            updateAllMessagesStatus('Read');
        }
    });

    connection.on("Pong", (timestamp) => {
        const latency = new Date() - new Date(timestamp);
        console.log("Connection latency:", latency + "ms");
    });

    connection.on("SendMessageError", (error) => {
        console.error("Message send error:", error);
        showToast(error.error || "Failed to send message", "error");
    });

    connection.on("Error", (message) => {
        console.error("Chat error:", message);
        showToast(message, "error");
    });

    connection.on("ContextCardUpdated", (contextCard) => {
        if (window.contextCardHandler && window.contextCardHandler.updateContextCardUI) {
            window.contextCardHandler.updateContextCardUI(contextCard);
        } else {
            console.error("ContextCard handler or updateContextCardUI function not available!");
        }
    });

    connection.on("ContextCardStatusChanged", (data) => {
        if (data.conversationId === window.chatConfig?.conversationId) {
            refreshConversationList();
        }
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

        const sendBtn = document.getElementById('sendMessageBtn');
        if (sendBtn) sendBtn.disabled = false;

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

function displayMessage(message) {
    try {
        const messagesList = document.getElementById('messagesList');
        if (!messagesList) {
            console.error("displayMessage: messagesList not found");
            return;
        }

        if (message.id > 0 && document.getElementById(`message-${message.id}`)) {
            console.log("displayMessage: message already exists, skipping");
            return;
        }

        console.log("displayMessage: creating bubble for message id:", message.id);
        const messageHtml = createMessageBubble(message);

        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = messageHtml;
        const messageEl = tempDiv.firstElementChild;

        messagesList.appendChild(messageEl);

        if (message.messageType === 1 || message.messageType === 2) {
            setupOrderReferenceHandlers(message);
        }

        if (message.messageType === 3) {
            setupMeetingProposalHandlers(message);
        }

        if (isNearBottom) {
            scrollToBottom();
        }

        if (!message.isFromCurrentUser && connection) {
            connection.invoke("MarkMessageAsRead", message.id);
        }
    } catch (err) {
        console.error("ERROR in displayMessage:", err);
        console.error("Message that caused error:", message);
    }
}

function replaceTempMessage(tempId, realMessage) {
    console.log("replaceTempMessage: looking for temp element:", tempId);
    console.log("replaceTempMessage: real message id:", realMessage.id);

    const tempEl = document.getElementById(`message-${tempId}`);
    if (!tempEl) {
        console.error("replaceTempMessage: temp element NOT FOUND!");
        displayMessage(realMessage);
        return;
    }

    console.log("replaceTempMessage: updating temp element in place");

    // Update attributes in place to prevent animation cancellation invisibility bug
    tempEl.id = `message-${realMessage.id}`;
    tempEl.setAttribute('data-message-id', realMessage.id);

    const statusStr = realMessage.status ?
        (typeof realMessage.status === 'string' ? realMessage.status : String(realMessage.status))
        : 'Sent';
    const statusLower = statusStr.toLowerCase();

    tempEl.setAttribute('data-status', statusLower);

    const timeEl = tempEl.querySelector('.message-time');
    if (timeEl) {
        timeEl.textContent = realMessage.formattedTime || formatTime(realMessage.sentAt);
    }

    const statusEl = tempEl.querySelector('.message-status');
    if (statusEl) {
        statusEl.setAttribute('data-status', statusLower);
        statusEl.innerHTML = getStatusIcon(statusStr);
    }

    if (isNearBottom) {
        scrollToBottom();
    }

    console.log("replaceTempMessage: DONE");
}

function createMessageBubble(message) {
    const isOwn = message.isFromCurrentUser;
    const isEventBubble = message.senderId === null || message.senderId === undefined;
    const bubbleClass = isEventBubble ? 'message-bubble event' : (isOwn ? 'message-bubble sent' : 'message-bubble received');
    const statusIcon = isOwn ? getStatusIcon(message.status) : '';
    const escapedContent = escapeHtml(message.content).replace(/\n/g, '<br>');
    const senderInitial = message.senderName ? message.senderName.charAt(0).toUpperCase() : '?';
    const messageId = message.id;
    const status = typeof message.status === 'string'
        ? message.status.toLowerCase()
        : (typeof message.status === 'number' ? String(message.status).toLowerCase() : 'sent');

    const displayContent = escapedContent || '&nbsp;';

    // Event/Status bubble - centered, no avatar, for system messages
    if (isEventBubble) {
        return `
            <div class="${bubbleClass}" 
                 data-message-id="${messageId}" 
                 data-sender-id=""
                 data-status="${status}"
                 data-message-type="${message.messageType}"
                 id="message-${messageId}">
                <div class="event-content">
                    <span class="event-text">${displayContent}</span>
                    <span class="event-time">${message.formattedTime || formatTime(message.sentAt)}</span>
                </div>
            </div>`;
    }

    let html = `
        <div class="${bubbleClass}" 
             data-message-id="${messageId}" 
             data-sender-id="${message.senderId}"
             data-status="${status}"
             data-message-type="${message.messageType}"
             id="message-${messageId}"
             style="display: flex !important;">`;

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
                ${!isOwn ? `<span class="message-sender">${escapeHtml(message.senderName)}</span>` : ''}`;

    if (message.messageType === 1 || message.messageType === 2) {
        html += createOrderReferenceContent(message);
    } else if (message.messageType === 3) {
        html += createMeetingProposalContent(message);
    } else {
        html += `
                <div class="message-content" data-is-own="${isOwn}">
                    <p class="message-text">${displayContent}</p>
                    <div class="message-meta">
                        <span class="message-time">${message.formattedTime || formatTime(message.sentAt)}</span>
                        ${isOwn ? `<span class="message-status" data-status="${status}">${statusIcon}</span>` : ''}
                    </div>
                </div>`;
    }

    html += `
            </div>
            ${isOwn ? '<div class="message-avatar-placeholder"></div>' : ''}
        </div>`;

    return html;
}

function createOrderReferenceContent(message) {
    const orderRef = message.orderReference;
    if (!orderRef) return '';

    const isConfirmed = message.messageType === 2;
    const title = isConfirmed ? '✅ Order Confirmed' : '📦 Item Inquiry';

    return `
        <div class="message-content order-reference">
            <div class="order-reference-header">
                <span class="order-reference-title">${title}</span>
            </div>
            <div class="order-reference-body">
                ${orderRef.itemImageUrl ?
            `<img src="${orderRef.itemImageUrl}" alt="${escapeHtml(orderRef.itemTitle)}" class="order-reference-image" />` : ''
        }
                <div class="order-reference-details">
                    <h4>${escapeHtml(orderRef.itemTitle)}</h4>
                    <p class="order-reference-price">₱${orderRef.price.toFixed(2)}</p>
                    <p class="order-reference-meta">${escapeHtml(orderRef.condition)}${orderRef.size ? ' • Size ' + escapeHtml(orderRef.size) : ''}</p>
                    ${isConfirmed && orderRef.fulfillmentMethod ?
            `<p class="order-reference-fulfillment">Fulfillment: ${escapeHtml(orderRef.fulfillmentMethod)}</p>` : ''
        }
                </div>
            </div>
            ${message.content ? `<p class="message-text order-reference-message">${escapeHtml(message.content)}</p>` : ''}
            <div class="message-meta">
                <span class="message-time">${message.formattedTime || formatTime(message.sentAt)}</span>
                ${message.isFromCurrentUser ? `<span class="message-status">${getStatusIcon(message.status)}</span>` : ''}
            </div>
        </div>
    `;
}

function createMeetingProposalContent(message) {
    const metadata = message.metadata || {};
    return `
        <div class="message-content meeting-proposal">
            <div class="meeting-proposal-header">
                <span class="meeting-proposal-icon">📍</span>
                <span class="meeting-proposal-title">Meeting Proposal</span>
            </div>
            <div class="meeting-proposal-body">
                <p class="meeting-proposal-location"><strong>Location:</strong> ${escapeHtml(metadata.location || 'TBD')}</p>
                <p class="meeting-proposal-time"><strong>Time:</strong> ${formatDateTime(metadata.proposedTime)}</p>
                ${metadata.notes ? `<p class="meeting-proposal-notes">${escapeHtml(metadata.notes)}</p>` : ''}
            </div>
            ${message.content ? `<p class="message-text meeting-proposal-message">${escapeHtml(message.content)}</p>` : ''}
            <div class="meeting-proposal-actions">
                <button class="btn-accept-meeting" data-message-id="${message.id}">Accept</button>
                <button class="btn-propose-alternative" data-message-id="${message.id}">Propose Alternative</button>
            </div>
            <div class="message-meta">
                <span class="message-time">${message.formattedTime || formatTime(message.sentAt)}</span>
                ${message.isFromCurrentUser ? `<span class="message-status">${getStatusIcon(message.status)}</span>` : ''}
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

    const statusStr = typeof status === 'string' ? status : String(status);
    const statusLower = statusStr.toLowerCase();

    const statusEl = messageEl.querySelector('.message-status');
    if (statusEl) {
        statusEl.setAttribute('data-status', statusLower);
        statusEl.innerHTML = getStatusIcon(statusStr);
    }

    messageEl.setAttribute('data-status', statusLower);

    if (statusLower === 'failed') {
        messageEl.style.opacity = '0.7';
        messageEl.style.cursor = 'pointer';
        messageEl.addEventListener('click', function retryHandler(e) {
            if (e.target.tagName === 'A') return;

            const content = this.querySelector('.message-text')?.textContent;
            if (content && confirm('Retry sending this message?')) {
                this.removeEventListener('click', retryHandler);
                this.remove();
                sendMessage(content);
            }
        });
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

        if (senderId === window.chatConfig.currentUserId && msgId.toString().startsWith('temp-') === false) {
            updateMessageStatus(msgId, status);
        }
    });
}

// ─── UI Event Handlers ──────────────────────────────────────────────────

function setupMessageForm() {
    console.log('Setting up message form.');
    const form = document.getElementById('messageForm');
    const input = document.getElementById('messageInput');
    const sendBtn = document.getElementById('sendMessageBtn');
    const charCount = document.getElementById('messageCharCount');

    if (!form || !input) {
        console.error('Message form or input not found!');
        return;
    }

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
        e.stopPropagation();

        const content = input.value.trim();

        if (content && connection && connection.state === signalR.HubConnectionState.Connected) {
            sendMessage(content);
            input.value = '';
            if (charCount) charCount.textContent = '0';
            if (sendBtn) sendBtn.disabled = true;
            input.style.height = 'auto';
            handleTypingIndicator(false);
        }

        return false;
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

    if (typingTimeout) {
        clearTimeout(typingTimeout);
    }

    connection.invoke("Typing", currentConversationId, isTyping);

    if (isTyping) {
        typingTimeout = setTimeout(() => {
            connection.invoke("Typing", currentConversationId, false);
        }, 3000);
    }
}

function setupTypingIndicator() {
    console.log('Typing indicator setup complete');
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
                const newMessages = doc.querySelectorAll('.message-bubble');

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

function setupOrderReferenceHandlers(message) {
    // Can be extended
}

function setupMeetingProposalHandlers(message) {
    document.addEventListener('click', (e) => {
        if (e.target.classList.contains('btn-accept-meeting')) {
            sendMessage("I accept the proposed meeting time and location.", 0);
        } else if (e.target.classList.contains('btn-propose-alternative')) {
            showAlternativeProposalModal(message);
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

function showAlternativeProposalModal(message) {
    const location = prompt("Enter alternative meeting location:");
    if (!location) return;

    const dateStr = prompt("Enter date and time (e.g., 2024-01-15 14:30):");
    if (!dateStr) return;

    const proposedTime = new Date(dateStr);

    if (isNaN(proposedTime.getTime())) {
        alert("Invalid date format");
        return;
    }

    const notes = prompt("Additional notes (optional):");

    const metadata = {
        location: location,
        proposedTime: proposedTime.toISOString(),
        notes: notes
    };

    sendMessage(`I propose an alternative meeting: ${location} at ${formatDateTime(proposedTime)}`, 3, metadata);
}

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
            
            .btn-retry:hover {
                background: #1f401b;
            }
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
    statusEl.style.background = type === 'error' ? '#dc3545' : type === 'warning' ? '#ffc107' : '#28a745';
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
    const statusStr = typeof status === 'string' ? status : String(status);
    const statusLower = statusStr ? statusStr.toLowerCase() : 'sent';

    if (statusLower === 'read' || statusLower === '1') {
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

function formatDateTime(timestamp) {
    if (!timestamp) return '';

    const date = typeof timestamp === 'string' ? new Date(timestamp) : timestamp;
    return date.toLocaleString([], {
        weekday: 'short',
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        hour12: true
    });
}

function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// ─── Initialize on DOM Ready ────────────────────────────────────────────

function tryInitialize() {
    if (window.chatInitialized) {
        console.log('Skipping initialization - already initialized');
        return;
    }

    if (window.chatConfig) {
        window.chatInitialized = true;
        console.log('Initializing chat from chat.js');
        initializeChat(window.chatConfig);
    } else if (document.querySelector('.chat-container')) {
        window.chatInitialized = true;
        console.log('Initializing inbox from chat.js');
        initializeInbox();
    }
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', tryInitialize);
} else {
    tryInitialize();
}

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
    
    .animate-spin {
        animation: spin 1s linear infinite;
    }
    
    .hidden {
        display: none !important;
    }
`;
document.head.appendChild(style);
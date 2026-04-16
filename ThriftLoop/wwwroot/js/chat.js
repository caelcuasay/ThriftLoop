// wwwroot/js/chat.js

/**
 * ThriftLoop Chat - Real-time messaging with SignalR
 */

(function () {
    'use strict';

    // Configuration
    let config = {
        conversationId: 0,
        currentUserId: 0,
        otherUserId: 0,
        otherUserName: '',
        prefillMessage: ''
    };

    // SignalR connection
    let connection = null;
    let isConnected = false;
    let reconnectAttempts = 0;
    const maxReconnectAttempts = 5;

    // DOM Elements
    const elements = {
        messageContainer: null,
        messagesList: null,
        messageForm: null,
        messageInput: null,
        sendButton: null,
        typingIndicator: null,
        charCount: null,
        userStatus: null,
        loadOlderBtn: null,
        conversationList: null,
        toggleSidebarBtn: null,
        chatSidebar: null,
        newMessageBtn: null,
        newMessageModal: null
    };

    // State
    let typingTimeout = null;
    let isTyping = false;
    let currentPage = 1;
    let isLoadingOlder = false;
    let messageQueue = [];
    let isProcessingQueue = false;

    // Initialize
    function initialize(chatConfig) {
        config = { ...config, ...chatConfig };

        cacheElements();
        initializeSignalR();
        bindEvents();
        setupMessageInput();
        loadConversationList();

        if (config.prefillMessage) {
            elements.messageInput.value = config.prefillMessage;
            updateSendButton();
            updateCharCount();
        }
    }

    // Cache DOM elements
    function cacheElements() {
        elements.messageContainer = document.getElementById('messageContainer');
        elements.messagesList = document.getElementById('messagesList');
        elements.messageForm = document.getElementById('messageForm');
        elements.messageInput = document.getElementById('messageInput');
        elements.sendButton = document.getElementById('sendMessageBtn');
        elements.typingIndicator = document.getElementById('typingIndicator');
        elements.charCount = document.getElementById('messageCharCount');
        elements.userStatus = document.getElementById('userStatus');
        elements.loadOlderBtn = document.getElementById('loadOlderMessages');
        elements.conversationList = document.getElementById('conversationList');
        elements.toggleSidebarBtn = document.getElementById('toggleSidebarBtn');
        elements.chatSidebar = document.getElementById('chatSidebar');
        elements.newMessageBtn = document.getElementById('newMessageBtn');
        elements.newMessageModal = document.getElementById('newMessageModal');
    }

    // Initialize SignalR connection
    function initializeSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/chatHub")
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // Receive new message
        connection.on("ReceiveMessage", handleReceiveMessage);

        // User typing indicator
        connection.on("UserTyping", handleUserTyping);

        // User online/offline status
        connection.on("UserOnline", handleUserOnline);
        connection.on("UserOffline", handleUserOffline);

        // New message notification (when not viewing conversation)
        connection.on("NewMessageNotification", handleNewMessageNotification);

        // Unread count update
        connection.on("UnreadCountUpdate", handleUnreadCountUpdate);

        // Message status updates
        connection.on("MessageMarkedAsRead", handleMessageMarkedAsRead);

        // User viewing conversation
        connection.on("UserViewingConversation", handleUserViewingConversation);

        // Pong response
        connection.on("Pong", () => {
            console.debug('SignalR ping successful');
        });

        // Error handling
        connection.on("Error", (error) => {
            console.error('SignalR error:', error);
            showToast('Connection error. Please refresh.', 'error');
        });

        connection.on("SendMessageError", (error) => {
            console.error('Send message error:', error);
            showToast('Failed to send message. Please try again.', 'error');
        });

        // Start connection
        startConnection();

        // Periodic ping to keep connection alive
        setInterval(() => {
            if (isConnected) {
                connection.invoke("Ping").catch(() => { });
            }
        }, 60000);
    }

    async function startConnection() {
        try {
            await connection.start();
            isConnected = true;
            reconnectAttempts = 0;
            console.log('SignalR connected');

            // Join the conversation room
            if (config.conversationId > 0) {
                await connection.invoke("JoinConversation", config.conversationId);
            }

            // Process any queued messages
            processMessageQueue();
        } catch (err) {
            console.error('SignalR connection failed:', err);
            isConnected = false;

            if (reconnectAttempts < maxReconnectAttempts) {
                reconnectAttempts++;
                setTimeout(startConnection, 5000 * reconnectAttempts);
            } else {
                showToast('Unable to connect to chat. Please refresh the page.', 'error');
            }
        }
    }

    connection.onreconnecting(error => {
        console.log('SignalR reconnecting...', error);
        isConnected = false;
        updateConnectionStatus('reconnecting');
    });

    connection.onreconnected(connectionId => {
        console.log('SignalR reconnected:', connectionId);
        isConnected = true;
        updateConnectionStatus('connected');

        // Rejoin conversation
        if (config.conversationId > 0) {
            connection.invoke("JoinConversation", config.conversationId);
        }
    });

    connection.onclose(error => {
        console.log('SignalR closed:', error);
        isConnected = false;
        updateConnectionStatus('disconnected');
    });

    function updateConnectionStatus(status) {
        const statusElement = document.getElementById('connectionStatus');
        if (statusElement) {
            statusElement.className = `connection-status ${status}`;
            statusElement.textContent = status === 'connected' ? 'Connected' :
                status === 'reconnecting' ? 'Reconnecting...' : 'Disconnected';
        }
    }

    // Bind events
    function bindEvents() {
        // Message form submit
        if (elements.messageForm) {
            elements.messageForm.addEventListener('submit', handleSendMessage);
        }

        // Message input
        if (elements.messageInput) {
            elements.messageInput.addEventListener('input', handleMessageInput);
            elements.messageInput.addEventListener('keydown', handleKeyDown);
        }

        // Load older messages
        if (elements.loadOlderBtn) {
            elements.loadOlderBtn.addEventListener('click', loadOlderMessages);
        }

        // Toggle sidebar (mobile)
        if (elements.toggleSidebarBtn) {
            elements.toggleSidebarBtn.addEventListener('click', toggleSidebar);
        }

        // New message button
        if (elements.newMessageBtn) {
            elements.newMessageBtn.addEventListener('click', openNewMessageModal);
        }

        // Close modal on backdrop click
        if (elements.newMessageModal) {
            elements.newMessageModal.addEventListener('click', (e) => {
                if (e.target === elements.newMessageModal) {
                    closeModal(elements.newMessageModal);
                }
            });
        }

        // Close buttons in modals
        document.querySelectorAll('[data-dismiss="modal"]').forEach(btn => {
            btn.addEventListener('click', () => {
                const modal = btn.closest('.modal');
                if (modal) closeModal(modal);
            });
        });

        // Mark messages as read when scrolled into view
        if (elements.messageContainer) {
            elements.messageContainer.addEventListener('scroll', debounce(checkVisibleMessages, 200));
        }

        // Page visibility change
        document.addEventListener('visibilitychange', handleVisibilityChange);
    }

    // Handle sending message
    async function handleSendMessage(e) {
        e.preventDefault();

        const content = elements.messageInput.value.trim();
        if (!content || content.length > 2000) return;

        const messageData = {
            conversationId: config.conversationId,
            content: content
        };

        // Clear input
        elements.messageInput.value = '';
        updateSendButton();
        updateCharCount();

        // Stop typing indicator
        stopTyping();

        // Send via SignalR
        if (isConnected) {
            try {
                await connection.invoke("SendMessage", messageData);
            } catch (err) {
                console.error('Failed to send message:', err);
                // Add to queue for retry
                messageQueue.push(messageData);
                showToast('Message queued. Will send when connection is restored.', 'warning');
            }
        } else {
            // Queue for later
            messageQueue.push(messageData);
            showToast('You are offline. Message will send when connection is restored.', 'warning');
        }
    }

    // Process queued messages
    async function processMessageQueue() {
        if (isProcessingQueue || messageQueue.length === 0) return;

        isProcessingQueue = true;

        while (messageQueue.length > 0 && isConnected) {
            const message = messageQueue.shift();
            try {
                await connection.invoke("SendMessage", message);
            } catch (err) {
                console.error('Failed to send queued message:', err);
                messageQueue.unshift(message);
                break;
            }
        }

        isProcessingQueue = false;
    }

    // Handle receiving a message
    function handleReceiveMessage(message) {
        if (message.conversationId !== config.conversationId) return;

        // Add message to UI
        appendMessage(message, message.senderId === config.currentUserId ? 'sent' : 'received');

        // Scroll to bottom
        scrollToBottom();

        // Mark as read if from other user
        if (message.senderId !== config.currentUserId) {
            connection.invoke("MarkConversationAsRead", config.conversationId);
        }

        // Refresh conversation list
        loadConversationList();
    }

    // Append message to the list
    function appendMessage(message, type) {
        if (!elements.messagesList) return;

        const messageElement = createMessageElement(message, type);
        elements.messagesList.appendChild(messageElement);

        // Update message status if needed
        if (type === 'sent') {
            observeMessageStatus(message.id);
        }
    }

    // Create message DOM element
    function createMessageElement(message, type) {
        const template = document.getElementById('messageTemplate');
        let html = '';

        if (template) {
            html = template.innerHTML
                .replace(/{{messageId}}/g, message.id)
                .replace(/{{senderId}}/g, message.senderId)
                .replace(/{{senderName}}/g, message.senderName)
                .replace(/{{content}}/g, formatMessageContent(message.content))
                .replace(/{{formattedTime}}/g, message.formattedTime)
                .replace(/{{status}}/g, message.status)
                .replace(/{{bubbleClass}}/g, type === 'sent' ? 'message-bubble sent' : 'message-bubble received')
                .replace(/{{statusIcon}}/g, type === 'sent' ? getStatusIcon(message.status) : '')
                .replace(/{{senderInitial}}/g, message.senderName.charAt(0).toUpperCase())
                .replace(/{{avatarUrl}}/g, message.senderAvatarUrl || '');
        } else {
            // Fallback if template not found
            const div = document.createElement('div');
            div.className = `message-bubble ${type}`;
            div.dataset.messageId = message.id;
            div.dataset.senderId = message.senderId;
            div.dataset.status = message.status.toLowerCase();
            div.id = `message-${message.id}`;
            div.innerHTML = `
                <div class="message-content">
                    <p class="message-text">${formatMessageContent(message.content)}</p>
                    <div class="message-meta">
                        <span class="message-time">${message.formattedTime}</span>
                        ${type === 'sent' ? `<span class="message-status">${getStatusIcon(message.status)}</span>` : ''}
                    </div>
                </div>
            `;
            return div;
        }

        const div = document.createElement('div');
        div.innerHTML = html.trim();
        return div.firstChild;
    }

    function getStatusIcon(status) {
        const icons = {
            'Read': '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#34B7F1" stroke-width="2"><path d="M20 6L9 17l-5-5"/><path d="M16 6l-7 7 5 5 7-7"/></svg>',
            'Delivered': '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 6L9 17l-5-5"/><path d="M16 6l-7 7"/></svg>',
            'Sent': '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 6L9 17l-5-5"/></svg>'
        };
        return icons[status] || '';
    }

    function formatMessageContent(content) {
        if (!content) return '';

        // Escape HTML
        let formatted = escapeHtml(content);

        // Convert URLs to links
        const urlPattern = /(https?:\/\/[^\s]+)/g;
        formatted = formatted.replace(urlPattern, '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>');

        // Convert line breaks
        formatted = formatted.replace(/\n/g, '<br>');

        return formatted;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Handle typing indicator
    function handleUserTyping(data) {
        if (data.userId === config.otherUserId && data.conversationId === config.conversationId) {
            if (data.isTyping) {
                showTypingIndicator();
            } else {
                hideTypingIndicator();
            }
        }
    }

    function showTypingIndicator() {
        if (elements.typingIndicator) {
            elements.typingIndicator.classList.remove('hidden');
        }
    }

    function hideTypingIndicator() {
        if (elements.typingIndicator) {
            elements.typingIndicator.classList.add('hidden');
        }
    }

    // Handle user online/offline
    function handleUserOnline(userId) {
        if (userId === config.otherUserId) {
            updateUserStatus(true);
        }
    }

    function handleUserOffline(userId) {
        if (userId === config.otherUserId) {
            updateUserStatus(false);
        }
    }

    function updateUserStatus(isOnline) {
        const indicator = document.querySelector('.online-indicator');
        if (indicator) {
            indicator.classList.toggle('online', isOnline);
        }
        if (elements.userStatus) {
            elements.userStatus.textContent = isOnline ? 'Online now' : 'Offline';
        }
    }

    // Handle new message notification
    function handleNewMessageNotification(data) {
        // Show browser notification if supported and page not visible
        if (document.hidden && Notification.permission === 'granted') {
            new Notification(`New message from ${data.senderName}`, {
                body: data.message.content,
                icon: '/images/chat-icon.png'
            });
        }

        // Refresh conversation list
        loadConversationList();

        // Update page title
        updatePageTitle();
    }

    // Handle unread count update
    function handleUnreadCountUpdate(count) {
        updateUnreadBadge(count);
    }

    // Handle message marked as read
    function handleMessageMarkedAsRead(messageId) {
        const messageElement = document.getElementById(`message-${messageId}`);
        if (messageElement) {
            messageElement.dataset.status = 'read';
            const statusIcon = messageElement.querySelector('.message-status');
            if (statusIcon) {
                statusIcon.innerHTML = getStatusIcon('Read');
            }
        }
    }

    // Handle user viewing conversation
    function handleUserViewingConversation(data) {
        if (data.userId === config.otherUserId && data.conversationId === config.conversationId) {
            // Other user is viewing the conversation
            console.debug('Other user is viewing conversation');
        }
    }

    // Message input handlers
    function handleMessageInput() {
        updateSendButton();
        updateCharCount();

        // Typing indicator
        if (!isTyping) {
            isTyping = true;
            connection.invoke("Typing", config.conversationId, true);
        }

        clearTimeout(typingTimeout);
        typingTimeout = setTimeout(stopTyping, 2000);
    }

    function stopTyping() {
        if (isTyping) {
            isTyping = false;
            connection.invoke("Typing", config.conversationId, false);
        }
    }

    function handleKeyDown(e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            if (elements.sendButton && !elements.sendButton.disabled) {
                elements.messageForm.dispatchEvent(new Event('submit'));
            }
        }
    }

    function updateSendButton() {
        if (elements.sendButton) {
            const hasContent = elements.messageInput.value.trim().length > 0;
            elements.sendButton.disabled = !hasContent;
        }
    }

    function updateCharCount() {
        if (elements.charCount) {
            const count = elements.messageInput.value.length;
            elements.charCount.textContent = count;
        }
    }

    // Load older messages
    async function loadOlderMessages() {
        if (isLoadingOlder) return;

        isLoadingOlder = true;
        elements.loadOlderBtn.textContent = 'Loading...';
        elements.loadOlderBtn.disabled = true;

        try {
            const nextPage = currentPage + 1;
            const response = await fetch(`/Chat/Conversation/${config.conversationId}?page=${nextPage}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (response.ok) {
                const html = await response.text();
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                const newMessages = doc.querySelectorAll('.message-bubble');

                // Prepend new messages
                newMessages.forEach(msg => {
                    elements.messagesList.insertBefore(msg, elements.messagesList.firstChild);
                });

                currentPage = nextPage;

                // Hide load more button if no more messages
                const hasMore = doc.querySelector('#loadOlderMessages')?.dataset.hasMore === 'true';
                if (!hasMore) {
                    elements.loadOlderBtn.style.display = 'none';
                }
            }
        } catch (err) {
            console.error('Failed to load older messages:', err);
        } finally {
            isLoadingOlder = false;
            elements.loadOlderBtn.textContent = 'Load older messages';
            elements.loadOlderBtn.disabled = false;
        }
    }

    // Load conversation list for sidebar
    async function loadConversationList() {
        if (!elements.conversationList) return;

        try {
            const response = await fetch('/Chat/ConversationList');
            if (response.ok) {
                const html = await response.text();
                elements.conversationList.innerHTML = html;
            }
        } catch (err) {
            console.error('Failed to load conversation list:', err);
        }
    }

    // Check visible messages for read receipts
    function checkVisibleMessages() {
        const messages = document.querySelectorAll('.message-bubble.received');
        const containerRect = elements.messageContainer.getBoundingClientRect();

        messages.forEach(msg => {
            const rect = msg.getBoundingClientRect();
            const isVisible = rect.top >= containerRect.top && rect.bottom <= containerRect.bottom;

            if (isVisible && msg.dataset.status !== 'read') {
                const messageId = parseInt(msg.dataset.messageId);
                connection.invoke("MarkMessageAsRead", messageId);
            }
        });
    }

    // Handle page visibility change
    function handleVisibilityChange() {
        if (!document.hidden && config.conversationId > 0) {
            // Mark conversation as read when user returns to page
            connection.invoke("MarkConversationAsRead", config.conversationId);
            updatePageTitle(false);
        }
    }

    function updatePageTitle(hasUnread = false) {
        if (hasUnread) {
            document.title = '(New message) ' + document.title.replace('(New message) ', '');
        }
    }

    function updateUnreadBadge(count) {
        const badge = document.getElementById('chatUnreadBadge');
        if (badge) {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.style.display = 'flex';
            } else {
                badge.style.display = 'none';
            }
        }
    }

    // Toggle sidebar (mobile)
    function toggleSidebar() {
        if (elements.chatSidebar) {
            elements.chatSidebar.classList.toggle('show');
        }
    }

    // Open new message modal
    function openNewMessageModal() {
        if (elements.newMessageModal) {
            elements.newMessageModal.classList.add('show');
            const searchInput = elements.newMessageModal.querySelector('#userSearchInput');
            if (searchInput) {
                setTimeout(() => searchInput.focus(), 100);
            }
        }
    }

    function closeModal(modal) {
        modal.classList.remove('show');
    }

    // Scroll to bottom of messages
    function scrollToBottom() {
        if (elements.messageContainer) {
            elements.messageContainer.scrollTop = elements.messageContainer.scrollHeight;
        }
    }

    // Setup message input
    function setupMessageInput() {
        if (elements.messageInput) {
            // Auto-resize textarea
            elements.messageInput.addEventListener('input', function () {
                this.style.height = 'auto';
                this.style.height = Math.min(this.scrollHeight, 100) + 'px';
            });
        }
    }

    // Observe message status changes
    function observeMessageStatus(messageId) {
        // Could implement polling or rely on SignalR updates
    }

    // Utility: Debounce function
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Show toast notification
    function showToast(message, type = 'info') {
        // Implement toast notification or use existing system
        console.log(`[${type.toUpperCase()}] ${message}`);
    }

    // Request notification permission
    async function requestNotificationPermission() {
        if ('Notification' in window && Notification.permission === 'default') {
            await Notification.requestPermission();
        }
    }

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', () => {
        requestNotificationPermission();

        // Check if we're on conversation page
        if (typeof window.chatConfig !== 'undefined') {
            initialize(window.chatConfig);
        }
    });

    // Export for global access
    window.initializeChat = initialize;
    window.chatConnection = connection;
})();
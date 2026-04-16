// wwwroot/js/chat-inbox.js

/**
 * ThriftLoop Chat Inbox - Conversation list and search functionality
 */

(function () {
    'use strict';

    // DOM Elements
    const elements = {
        conversationList: null,
        conversationSearch: null,
        newMessageBtn: null,
        newMessageModal: null,
        userSearchInput: null,
        userSearchResults: null,
        searchLoading: null,
        selectedUserId: null,
        selectedUserDisplay: null,
        newMessageForm: null,
        startChatBtn: null,
        initialMessage: null,
        charCount: null,
        loadMoreBtn: null,
        startFirstChatBtn: null,
        newMessageEmptyBtn: null
    };

    // State
    let searchTimeout = null;
    let currentPage = 1;
    let isLoading = false;
    let selectedUser = null;

    // Initialize
    function initialize() {
        cacheElements();
        bindEvents();
        setupSearch();
        setupModal();
    }

    // Cache DOM elements
    function cacheElements() {
        elements.conversationList = document.getElementById('conversationList');
        elements.conversationSearch = document.getElementById('conversationSearch');
        elements.newMessageBtn = document.getElementById('newMessageBtn');
        elements.newMessageModal = document.getElementById('newMessageModal');
        elements.userSearchInput = document.getElementById('userSearchInput');
        elements.userSearchResults = document.getElementById('userSearchResults');
        elements.searchLoading = document.getElementById('searchLoading');
        elements.selectedUserId = document.getElementById('selectedUserId');
        elements.selectedUserDisplay = document.getElementById('selectedUserDisplay');
        elements.newMessageForm = document.getElementById('newMessageForm');
        elements.startChatBtn = document.getElementById('startChatBtn');
        elements.initialMessage = document.getElementById('initialMessage');
        elements.charCount = document.getElementById('charCount');
        elements.loadMoreBtn = document.getElementById('loadMoreConversations');
        elements.startFirstChatBtn = document.getElementById('startFirstChatBtn');
        elements.newMessageEmptyBtn = document.getElementById('newMessageEmptyBtn');
    }

    // Bind events
    function bindEvents() {
        // New message button
        if (elements.newMessageBtn) {
            elements.newMessageBtn.addEventListener('click', openNewMessageModal);
        }

        if (elements.startFirstChatBtn) {
            elements.startFirstChatBtn.addEventListener('click', openNewMessageModal);
        }

        if (elements.newMessageEmptyBtn) {
            elements.newMessageEmptyBtn.addEventListener('click', openNewMessageModal);
        }

        // Conversation search
        if (elements.conversationSearch) {
            elements.conversationSearch.addEventListener('input', filterConversations);
        }

        // Load more button
        if (elements.loadMoreBtn) {
            elements.loadMoreBtn.addEventListener('click', loadMoreConversations);
        }

        // Modal close on backdrop click
        if (elements.newMessageModal) {
            elements.newMessageModal.addEventListener('click', (e) => {
                if (e.target === elements.newMessageModal) {
                    closeModal();
                }
            });
        }

        // Close buttons
        document.querySelectorAll('[data-dismiss="modal"]').forEach(btn => {
            btn.addEventListener('click', () => {
                const modal = btn.closest('.modal');
                if (modal) closeModal();
            });
        });

        // Character count
        if (elements.initialMessage) {
            elements.initialMessage.addEventListener('input', updateCharCount);
        }

        // Escape key to close modal
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && elements.newMessageModal?.classList.contains('show')) {
                closeModal();
            }
        });
    }

    // Setup search functionality
    function setupSearch() {
        if (!elements.userSearchInput) return;

        const clearBtn = document.getElementById('clearSearchBtn');
        const changeUserBtn = document.getElementById('changeUserBtn');

        if (clearBtn) {
            clearBtn.addEventListener('click', clearSearch);
        }

        if (changeUserBtn) {
            changeUserBtn.addEventListener('click', resetToSearch);
        }

        elements.userSearchInput.addEventListener('input', handleSearchInput);
    }

    function handleSearchInput() {
        const query = this.value.trim();
        const clearBtn = document.getElementById('clearSearchBtn');

        if (clearBtn) {
            clearBtn.style.display = query ? 'block' : 'none';
        }

        if (searchTimeout) {
            clearTimeout(searchTimeout);
        }

        if (query.length < 2) {
            showEmptySearchState();
            return;
        }

        searchTimeout = setTimeout(() => performSearch(query), 300);
    }

    async function performSearch(query) {
        if (!elements.searchLoading || !elements.userSearchResults) return;

        elements.searchLoading.style.display = 'flex';
        elements.userSearchResults.innerHTML = '';

        try {
            const response = await fetch(`/Chat/SearchUsers?query=${encodeURIComponent(query)}`);

            if (!response.ok) {
                throw new Error('Search failed');
            }

            const users = await response.json();
            elements.searchLoading.style.display = 'none';

            if (users.length === 0) {
                showNoResultsState();
                return;
            }

            renderSearchResults(users);
        } catch (error) {
            console.error('Search error:', error);
            elements.searchLoading.style.display = 'none';
            showSearchErrorState();
        }
    }

    function renderSearchResults(users) {
        if (!elements.userSearchResults) return;

        let html = '';

        users.forEach(user => {
            const initials = getInitials(user.name);
            const hasExistingChat = user.existingConversationId != null;

            html += `
                <div class="user-search-item" 
                     data-user-id="${user.id}" 
                     data-user-name="${escapeHtml(user.name)}" 
                     data-user-email="${escapeHtml(user.email)}"
                     data-conversation-id="${user.existingConversationId || ''}">
                    <div class="user-avatar">
                        ${user.avatarUrl
                    ? `<img src="${escapeHtml(user.avatarUrl)}" alt="${escapeHtml(user.name)}" />`
                    : `<div class="avatar-placeholder">${initials}</div>`
                }
                    </div>
                    <div class="user-info">
                        <span class="user-name">${escapeHtml(user.name)}</span>
                        <span class="user-email">${escapeHtml(user.email)}</span>
                    </div>
                    ${hasExistingChat ? '<span class="existing-chat-badge">Existing chat</span>' : ''}
                </div>
            `;
        });

        elements.userSearchResults.innerHTML = html;

        // Add click handlers
        elements.userSearchResults.querySelectorAll('.user-search-item').forEach(item => {
            item.addEventListener('click', () => selectUser(item));
        });
    }

    function selectUser(item) {
        const userId = parseInt(item.dataset.userId);
        const userName = item.dataset.userName;
        const userEmail = item.dataset.userEmail;
        const conversationId = item.dataset.conversationId;

        // If there's an existing conversation, redirect directly
        if (conversationId) {
            window.location.href = `/Chat/Conversation/${conversationId}`;
            return;
        }

        selectedUser = {
            id: userId,
            name: userName,
            email: userEmail
        };

        // Update hidden field
        if (elements.selectedUserId) {
            elements.selectedUserId.value = userId;
        }

        // Update display
        if (elements.selectedUserDisplay) {
            const initials = getInitials(userName);
            elements.selectedUserDisplay.innerHTML = `
                <div class="selected-user">
                    <div class="user-avatar-small">
                        <div class="avatar-placeholder small">${initials}</div>
                    </div>
                    <div class="user-info-compact">
                        <span class="user-name">${escapeHtml(userName)}</span>
                        <span class="user-email">${escapeHtml(userEmail)}</span>
                    </div>
                </div>
            `;
        }

        // Switch to message form
        if (elements.userSearchResults) {
            elements.userSearchResults.style.display = 'none';
        }
        if (elements.newMessageForm) {
            elements.newMessageForm.style.display = 'block';
        }
        if (elements.userSearchInput) {
            elements.userSearchInput.disabled = true;
        }
        if (elements.startChatBtn) {
            elements.startChatBtn.disabled = false;
        }

        // Focus on message input
        if (elements.initialMessage) {
            elements.initialMessage.focus();
        }
    }

    function clearSearch() {
        if (elements.userSearchInput) {
            elements.userSearchInput.value = '';
        }
        const clearBtn = document.getElementById('clearSearchBtn');
        if (clearBtn) {
            clearBtn.style.display = 'none';
        }
        showEmptySearchState();
    }

    function resetToSearch() {
        if (elements.newMessageForm) {
            elements.newMessageForm.style.display = 'none';
        }
        if (elements.userSearchResults) {
            elements.userSearchResults.style.display = 'block';
        }
        if (elements.userSearchInput) {
            elements.userSearchInput.disabled = false;
            elements.userSearchInput.focus();
        }
        if (elements.startChatBtn) {
            elements.startChatBtn.disabled = true;
        }
        selectedUser = null;
    }

    function showEmptySearchState() {
        if (!elements.userSearchResults) return;

        elements.userSearchResults.innerHTML = `
            <div class="search-empty-state">
                <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="11" cy="11" r="8"/>
                    <path d="M21 21l-4.35-4.35"/>
                </svg>
                <p>Type at least 2 characters to search for users</p>
            </div>
        `;
    }

    function showNoResultsState() {
        if (!elements.userSearchResults) return;

        elements.userSearchResults.innerHTML = `
            <div class="search-empty-state">
                <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <circle cx="11" cy="11" r="8"/>
                    <path d="M21 21l-4.35-4.35"/>
                </svg>
                <p>No users found</p>
            </div>
        `;
    }

    function showSearchErrorState() {
        if (!elements.userSearchResults) return;

        elements.userSearchResults.innerHTML = `
            <div class="search-empty-state">
                <p>Error searching. Please try again.</p>
            </div>
        `;
    }

    // Filter conversations
    function filterConversations() {
        const query = elements.conversationSearch.value.toLowerCase();
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
    }

    // Load more conversations
    async function loadMoreConversations() {
        if (isLoading || !elements.loadMoreBtn) return;

        isLoading = true;
        const nextPage = currentPage + 1;
        elements.loadMoreBtn.textContent = 'Loading...';
        elements.loadMoreBtn.disabled = true;

        try {
            const response = await fetch(`/Chat/ConversationList?page=${nextPage}`);

            if (response.ok) {
                const html = await response.text();
                const tempDiv = document.createElement('div');
                tempDiv.innerHTML = html;

                const newItems = tempDiv.querySelectorAll('.conversation-item');
                newItems.forEach(item => {
                    elements.conversationList.appendChild(item);
                });

                currentPage = nextPage;
                elements.loadMoreBtn.dataset.page = nextPage;

                // Check if there are more conversations
                const hasMore = tempDiv.querySelector('#hasMoreConversations')?.value === 'true';
                if (!hasMore) {
                    elements.loadMoreBtn.style.display = 'none';
                }
            }
        } catch (error) {
            console.error('Failed to load more conversations:', error);
        } finally {
            isLoading = false;
            elements.loadMoreBtn.textContent = 'Load more';
            elements.loadMoreBtn.disabled = false;
        }
    }

    // Setup modal
    function setupModal() {
        // Form submission
        if (elements.newMessageForm) {
            elements.newMessageForm.addEventListener('submit', (e) => {
                // Let the form submit normally
                // Close modal after submission
                setTimeout(closeModal, 100);
            });
        }
    }

    function openNewMessageModal() {
        if (elements.newMessageModal) {
            elements.newMessageModal.classList.add('show');
            resetModalState();

            // Focus search input
            if (elements.userSearchInput) {
                setTimeout(() => elements.userSearchInput.focus(), 100);
            }
        }
    }

    function closeModal() {
        if (elements.newMessageModal) {
            elements.newMessageModal.classList.remove('show');
        }
        resetModalState();
    }

    function resetModalState() {
        // Reset search
        if (elements.userSearchInput) {
            elements.userSearchInput.value = '';
            elements.userSearchInput.disabled = false;
        }

        // Hide clear button
        const clearBtn = document.getElementById('clearSearchBtn');
        if (clearBtn) {
            clearBtn.style.display = 'none';
        }

        // Show search results, hide form
        if (elements.userSearchResults) {
            elements.userSearchResults.style.display = 'block';
            showEmptySearchState();
        }

        if (elements.newMessageForm) {
            elements.newMessageForm.style.display = 'none';
        }

        // Clear message input
        if (elements.initialMessage) {
            elements.initialMessage.value = '';
            updateCharCount();
        }

        // Disable start button
        if (elements.startChatBtn) {
            elements.startChatBtn.disabled = true;
        }

        // Clear selected user display
        if (elements.selectedUserDisplay) {
            elements.selectedUserDisplay.innerHTML = '<span class="no-user-selected">No user selected</span>';
        }

        if (elements.selectedUserId) {
            elements.selectedUserId.value = '';
        }

        selectedUser = null;
    }

    function updateCharCount() {
        if (elements.charCount && elements.initialMessage) {
            const count = elements.initialMessage.value.length;
            elements.charCount.textContent = count;
        }
    }

    // Utility functions
    function getInitials(name) {
        if (!name) return '?';
        const parts = name.split(' ').filter(p => p);
        if (parts.length >= 2) {
            return (parts[0][0] + parts[1][0]).toUpperCase();
        }
        return name.substring(0, 1).toUpperCase();
    }

    function escapeHtml(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    // Poll for new messages/unread count
    function startUnreadPolling() {
        setInterval(async () => {
            try {
                const response = await fetch('/Chat/UnreadCount');
                if (response.ok) {
                    const data = await response.json();
                    updateUnreadBadge(data.unreadCount);
                }
            } catch (error) {
                // Silently fail
            }
        }, 30000); // Poll every 30 seconds
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

        // Update page title if on inbox page
        const titleElement = document.querySelector('.chat-sidebar-header h2');
        if (titleElement) {
            titleElement.textContent = count > 0 ? `Messages (${count})` : 'Messages';
        }
    }

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', () => {
        initialize();
        startUnreadPolling();
    });

})();
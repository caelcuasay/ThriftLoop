/**
 * ThriftLoop - Items JavaScript
 * Handles: Create, Edit, Details, Index (My Listings)
 */

(function () {
    'use strict';

    // =========================================================================
    // UTILITY FUNCTIONS
    // =========================================================================

    function formatPrice(price) {
        return '₱' + price.toLocaleString('en-PH', { minimumFractionDigits: 2 });
    }

    function getAntiForgeryToken() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    // Toast auto-hide
    const toast = document.querySelector('[data-auto-hide]');
    if (toast) {
        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => toast.remove(), 400);
        }, 5000);
    }

    // =========================================================================
    // CREATE & EDIT PAGE - Photo Upload (FIXED)
    // =========================================================================

    const dropzone = document.getElementById('photoDropzone');
    const photoInput = document.getElementById('photoInput');
    const photoGrid = document.getElementById('photoGrid');
    const newPhotoThumbs = document.getElementById('newPhotoThumbs');

    // For edit page
    const existingPhotoThumbs = document.getElementById('existingPhotoThumbs');
    const removedImagesContainer = document.getElementById('removedImagesContainer');

    let selectedFiles = [];
    let newFiles = [];
    let removedUrls = new Set();

    // Determine which mode we're in
    const isEditMode = !!existingPhotoThumbs;
    const isCreateMode = !!photoGrid && !isEditMode;

    // =========================================================================
    // CREATE MODE - Photo Functions
    // =========================================================================

    if (isCreateMode) {
        function updateCreatePreviews() {
            if (!photoGrid) return;

            photoGrid.innerHTML = '';

            selectedFiles.forEach((file, index) => {
                const reader = new FileReader();
                reader.onload = (e) => {
                    const thumb = document.createElement('div');
                    thumb.className = 'photo-thumb';
                    thumb.innerHTML = `
                    <img src="${e.target.result}" alt="Preview ${index + 1}" />
                    ${index === 0 ? '<span class="photo-thumb__badge">Cover</span>' : ''}
                    <button type="button" class="photo-thumb__remove" data-index="${index}" aria-label="Remove photo">×</button>
                `;
                    photoGrid.appendChild(thumb);
                };
                reader.readAsDataURL(file);
            });

            // Update the actual file input for form submission
            const dt = new DataTransfer();
            selectedFiles.forEach(f => dt.items.add(f));
            photoInput.files = dt.files;

            // Show/hide dropzone based on count
            if (dropzone) {
                dropzone.style.display = selectedFiles.length >= 5 ? 'none' : 'flex';
            }
        }

        function addCreateFiles(files) {
            const remaining = 5 - selectedFiles.length;
            const filesToAdd = Array.from(files).slice(0, remaining);

            let added = 0;
            filesToAdd.forEach(file => {
                if (!file.type.match(/^image\/(jpeg|png|webp)$/)) {
                    alert(`${file.name || 'File'} is not a supported image format.`);
                    return;
                }
                if (file.size > 5 * 1024 * 1024) {
                    alert(`${file.name || 'File'} exceeds 5 MB.`);
                    return;
                }
                selectedFiles.push(file);
                added++;
            });

            if (added > 0) {
                updateCreatePreviews();
            }
        }

        function removeCreateFile(index) {
            selectedFiles.splice(index, 1);
            updateCreatePreviews();
        }
    }

    // =========================================================================
    // EDIT MODE - Photo Functions
    // =========================================================================

    if (isEditMode) {
        function updateNewPreviews() {
            if (!newPhotoThumbs) return;

            newPhotoThumbs.innerHTML = '';

            newFiles.forEach((file, index) => {
                const reader = new FileReader();
                reader.onload = (e) => {
                    const thumb = document.createElement('div');
                    thumb.className = 'photo-thumb';
                    thumb.innerHTML = `
                    <img src="${e.target.result}" alt="New preview" />
                    <span class="photo-thumb__badge photo-thumb__badge--new">New</span>
                    <button type="button" class="photo-thumb__remove" data-new-index="${index}" aria-label="Remove new photo">×</button>
                `;
                    newPhotoThumbs.appendChild(thumb);
                };
                reader.readAsDataURL(file);
            });

            const dt = new DataTransfer();
            newFiles.forEach(f => dt.items.add(f));
            if (photoInput) photoInput.files = dt.files;

            // Show/hide dropzone
            const totalImages = (document.querySelectorAll('#existingPhotoThumbs .photo-thumb').length - removedUrls.size) + newFiles.length;
            if (dropzone) {
                dropzone.style.display = totalImages >= 5 ? 'none' : 'flex';
            }
        }

        function addNewFiles(files) {
            const totalImages = (document.querySelectorAll('#existingPhotoThumbs .photo-thumb').length - removedUrls.size) + newFiles.length;
            const remaining = 5 - totalImages;
            const filesToAdd = Array.from(files).slice(0, remaining);

            filesToAdd.forEach(file => {
                if (!file.type.match(/^image\/(jpeg|png|webp)$/)) {
                    alert(`${file.name} is not a supported image format.`);
                    return;
                }
                if (file.size > 5 * 1024 * 1024) {
                    alert(`${file.name} exceeds 5 MB.`);
                    return;
                }
                newFiles.push(file);
            });

            updateNewPreviews();
        }

        function removeNewFile(index) {
            newFiles.splice(index, 1);
            updateNewPreviews();
        }

        function removeExistingImage(url, thumbElement) {
            removedUrls.add(url);
            thumbElement.remove();

            const hiddenInput = document.querySelector(`input.existing-image-input[value="${url}"]`);
            if (hiddenInput) hiddenInput.remove();

            if (removedImagesContainer) {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'RemovedImageUrls';
                input.value = url;
                removedImagesContainer.appendChild(input);
            }

            const remainingPreviews = existingPhotoThumbs.querySelectorAll('.photo-thumb');
            remainingPreviews.forEach((thumb, i) => {
                const badge = thumb.querySelector('.photo-thumb__badge');
                if (i === 0) {
                    if (!badge) {
                        const newBadge = document.createElement('span');
                        newBadge.className = 'photo-thumb__badge';
                        newBadge.textContent = 'Cover';
                        thumb.appendChild(newBadge);
                    }
                } else {
                    if (badge && badge.textContent === 'Cover') badge.remove();
                }
            });

            // Update dropzone visibility
            const totalImages = remainingPreviews.length + newFiles.length;
            if (dropzone) {
                dropzone.style.display = totalImages >= 5 ? 'none' : 'flex';
            }
        }
    }

    // =========================================================================
    // Photo Upload Event Listeners (shared)
    // =========================================================================

    if (dropzone && photoInput) {
        // Click on dropzone opens file dialog
        dropzone.addEventListener('click', (e) => {
            // Don't trigger if clicking on the input itself
            if (e.target !== photoInput) {
                photoInput.click();
            }
        });

        // Drag and drop
        dropzone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropzone.classList.add('photo-dropzone--drag');
        });

        dropzone.addEventListener('dragleave', () => {
            dropzone.classList.remove('photo-dropzone--drag');
        });

        dropzone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropzone.classList.remove('photo-dropzone--drag');

            const files = e.dataTransfer.files;
            if (files.length > 0) {
                if (isCreateMode) {
                    addCreateFiles(files);
                } else if (isEditMode) {
                    addNewFiles(files);
                }
            }
        });

        // File input change
        photoInput.addEventListener('change', (e) => {
            if (e.target.files.length > 0) {
                if (isCreateMode) {
                    addCreateFiles(e.target.files);
                } else if (isEditMode) {
                    addNewFiles(e.target.files);
                }
            }
            // Clear input so same file can be selected again
            photoInput.value = '';
        });

        // Keyboard accessibility
        dropzone.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                photoInput.click();
            }
        });
    }

    // Remove photo handlers (event delegation)
    document.addEventListener('click', (e) => {
        const removeBtn = e.target.closest('.photo-thumb__remove');
        if (!removeBtn) return;

        e.stopPropagation();

        if (isCreateMode) {
            const index = removeBtn.dataset.index;
            if (index !== undefined) {
                removeCreateFile(parseInt(index));
            }
        } else if (isEditMode) {
            const newIndex = removeBtn.dataset.newIndex;
            if (newIndex !== undefined) {
                removeNewFile(parseInt(newIndex));
            } else {
                const url = removeBtn.dataset.url;
                const thumb = removeBtn.closest('.photo-thumb');
                if (url && thumb) {
                    removeExistingImage(url, thumb);
                }
            }
        }
    });

    // =========================================================================
    // CREATE & EDIT - Form Fields
    // =========================================================================

    // Character counters
    const titleInput = document.getElementById('titleInput');
    const descInput = document.getElementById('descInput');
    const titleCounter = document.getElementById('titleCounter');
    const descCounter = document.getElementById('descCounter');

    if (titleInput && titleCounter) {
        const updateTitleCounter = () => {
            const len = titleInput.value.length;
            titleCounter.textContent = `${len}/100`;
            titleCounter.classList.toggle('create-field__counter--warn', len >= 90);
        };
        titleInput.addEventListener('input', updateTitleCounter);
        updateTitleCounter();
    }

    if (descInput && descCounter) {
        const updateDescCounter = () => {
            const len = descInput.value.length;
            descCounter.textContent = `${len}/1000`;
            descCounter.classList.toggle('create-field__counter--warn', len >= 900);
        };
        descInput.addEventListener('input', updateDescCounter);
        updateDescCounter();
    }

    // =========================================================================
    // Category Chips
    // =========================================================================

    const categoryChips = document.getElementById('categoryChips');
    const categoryHidden = document.getElementById('categoryHidden');

    if (categoryChips && categoryHidden) {
        categoryChips.querySelectorAll('.create-chip').forEach(chip => {
            chip.addEventListener('click', () => {
                categoryChips.querySelectorAll('.create-chip').forEach(c => c.classList.remove('create-chip--active'));
                chip.classList.add('create-chip--active');
                categoryHidden.value = chip.dataset.value;
            });

            chip.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    chip.click();
                }
            });
        });
    }

    // =========================================================================
    // Condition Pills
    // =========================================================================

    const conditionPills = document.getElementById('conditionPills');
    const conditionHidden = document.getElementById('conditionHidden');

    if (conditionPills && conditionHidden) {
        conditionPills.querySelectorAll('.create-condition__pill').forEach(pill => {
            pill.addEventListener('click', () => {
                conditionPills.querySelectorAll('.create-condition__pill').forEach(p => p.classList.remove('create-condition__pill--active'));
                pill.classList.add('create-condition__pill--active');
                conditionHidden.value = pill.dataset.value;
            });

            pill.addEventListener('keydown', (e) => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    pill.click();
                }
            });
        });
    }

    // =========================================================================
    // Stealable Toggle
    // =========================================================================

    const stealableToggle = document.getElementById('stealableToggle');
    const stealDurationPanel = document.getElementById('stealDurationPanel');
    const stealDurationSelect = document.getElementById('stealDurationSelect');

    if (stealableToggle && stealDurationPanel) {
        const toggleStealDuration = () => {
            stealDurationPanel.hidden = !stealableToggle.checked;
            if (!stealableToggle.checked && stealDurationSelect) {
                stealDurationSelect.value = '';
            }
        };

        stealableToggle.addEventListener('change', toggleStealDuration);
        toggleStealDuration();
    }

    // =========================================================================
    // Fulfillment Options Validation
    // =========================================================================

    const fulfillmentForm = document.getElementById('createItemForm') || document.getElementById('editItemForm');
    const allowDelivery = document.querySelector('[name="AllowDelivery"]');
    const allowHalfway = document.querySelector('[name="AllowHalfway"]');
    const allowPickup = document.querySelector('[name="AllowPickup"]');
    const fulfillmentError = document.getElementById('fulfillmentError');

    if (fulfillmentForm && allowDelivery && allowHalfway && allowPickup) {
        fulfillmentForm.addEventListener('submit', (e) => {
            if (!allowDelivery.checked && !allowHalfway.checked && !allowPickup.checked) {
                e.preventDefault();
                if (fulfillmentError) {
                    fulfillmentError.textContent = 'Please select at least one fulfillment option.';
                } else {
                    alert('Please select at least one fulfillment option.');
                }

                // Highlight the options
                document.querySelector('.fulfillment-options')?.classList.add('chips-error');
                setTimeout(() => {
                    document.querySelector('.fulfillment-options')?.classList.remove('chips-error');
                }, 2000);
            }
        });
    }

    // Submit button loading state
    const submitBtn = document.getElementById('submitBtn');
    if (submitBtn && fulfillmentForm) {
        fulfillmentForm.addEventListener('submit', () => {
            submitBtn.disabled = true;
            submitBtn.textContent = isEditMode ? 'Saving...' : 'Creating...';
        });
    }

    // =========================================================================
    // DETAILS PAGE - Gallery
    // =========================================================================

    const mainImg = document.getElementById('mainGalleryImg');
    const thumbsEl = document.getElementById('galleryThumbs');
    const prevBtn = document.getElementById('galleryPrev');
    const nextBtn = document.getElementById('galleryNext');
    const counterEl = document.getElementById('galleryCounter');

    if (mainImg && thumbsEl) {
        const thumbBtns = Array.from(thumbsEl.querySelectorAll('.gallery-thumb'));
        const total = thumbBtns.length;
        let current = 0;

        function goTo(idx) {
            if (idx === current) return;

            mainImg.classList.add('gallery-fading');
            setTimeout(() => {
                mainImg.src = thumbBtns[idx].dataset.src;
                mainImg.classList.remove('gallery-fading');
            }, 200);

            thumbBtns[current].classList.remove('gallery-thumb--active');
            thumbBtns[idx].classList.add('gallery-thumb--active');

            if (counterEl) {
                counterEl.textContent = `${idx + 1} / ${total}`;
            }

            current = idx;
        }

        thumbBtns.forEach((btn, i) => {
            btn.addEventListener('click', () => goTo(i));
        });

        if (prevBtn) {
            prevBtn.addEventListener('click', () => goTo((current - 1 + total) % total));
        }

        if (nextBtn) {
            nextBtn.addEventListener('click', () => goTo((current + 1) % total));
        }

        // Keyboard navigation
        document.addEventListener('keydown', (e) => {
            if (e.key === 'ArrowLeft') goTo((current - 1 + total) % total);
            if (e.key === 'ArrowRight') goTo((current + 1) % total);
        });

        // Touch swipe
        let touchStartX = null;
        mainImg.addEventListener('touchstart', (e) => {
            touchStartX = e.touches[0].clientX;
        }, { passive: true });

        mainImg.addEventListener('touchend', (e) => {
            if (touchStartX === null) return;
            const dx = e.changedTouches[0].clientX - touchStartX;
            touchStartX = null;
            if (Math.abs(dx) < 40) return;
            if (dx < 0) goTo((current + 1) % total);
            else goTo((current - 1 + total) % total);
        }, { passive: true });
    }

    // =========================================================================
    // DETAILS PAGE - Countdown Timers
    // =========================================================================

    function startCountdown(el) {
        if (!el) return;

        const endsAt = new Date(el.dataset.endsAt);
        if (isNaN(endsAt.getTime())) return;

        if (endsAt <= new Date()) {
            el.textContent = '00:00:00';
            return;
        }

        function tick() {
            const diffMs = endsAt - new Date();
            if (diffMs <= 0) {
                el.textContent = '00:00:00';
                clearInterval(intervalId);
                window.location.reload();
                return;
            }

            const s = Math.floor(diffMs / 1000);
            const h = Math.floor(s / 3600);
            const m = Math.floor((s % 3600) / 60);

            el.textContent = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
        }

        tick();
        const intervalId = setInterval(tick, 1000);
    }

    startCountdown(document.getElementById('stealTimer'));
    startCountdown(document.getElementById('finalizeTimer'));

    // =========================================================================
    // DETAILS PAGE - How-It-Works Modals
    // =========================================================================

    const TODAY = new Date().toISOString().slice(0, 10);

    function wasDismissedToday(key) {
        try {
            return localStorage.getItem(key) === TODAY;
        } catch (e) {
            return false;
        }
    }

    function dismissForToday(key) {
        try {
            localStorage.setItem(key, TODAY);
        } catch (e) { }
    }

    function initModal(options) {
        const overlay = document.getElementById(options.overlayId);
        const proceedBtn = document.getElementById(options.proceedId);
        const dismissChk = document.getElementById(options.dismissCheckId);
        const triggers = document.querySelectorAll(options.triggerSelector);

        if (!overlay || !triggers.length) return;

        function showModal() {
            overlay.classList.add('hiw-overlay--visible');
            if (proceedBtn) proceedBtn.focus();
        }

        function hideModal() {
            overlay.classList.remove('hiw-overlay--visible');
        }

        function proceed() {
            if (dismissChk && dismissChk.checked) {
                dismissForToday(options.storageKey);
            }
            hideModal();
            options.onProceed();
        }

        triggers.forEach(trigger => {
            const form = trigger.closest('form');
            if (form) {
                form.addEventListener('submit', (e) => {
                    if (wasDismissedToday(options.storageKey)) return;
                    e.preventDefault();
                    showModal();
                    options.onProceed = () => form.submit();
                });
            }
        });

        if (proceedBtn) {
            proceedBtn.addEventListener('click', proceed);
        }

        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) hideModal();
        });

        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && overlay.classList.contains('hiw-overlay--visible')) {
                hideModal();
            }
        });
    }

    initModal({
        storageKey: 'thriftloop_hiw_get',
        overlayId: 'hiwGetOverlay',
        proceedId: 'hiwGetProceed',
        dismissCheckId: 'hiwGetDismiss',
        triggerSelector: '.get-item-form button[type="submit"]',
        onProceed: () => { }
    });

    initModal({
        storageKey: 'thriftloop_hiw_steal',
        overlayId: 'hiwStealOverlay',
        proceedId: 'hiwStealProceed',
        dismissCheckId: 'hiwStealDismiss',
        triggerSelector: '.steal-item-form button[type="submit"]',
        onProceed: () => { }
    });

    // =========================================================================
    // INDEX PAGE - Discount Modal
    // =========================================================================

    const discountModal = document.getElementById('discountModal');
    const modalItemImage = document.getElementById('modalItemImage');
    const modalItemTitle = document.getElementById('modalItemTitle');
    const modalCurrentPrice = document.getElementById('modalCurrentPrice');
    const modalItemId = document.getElementById('modalItemId');
    const discountPercent = document.getElementById('discountPercent');
    const previewOriginal = document.getElementById('previewOriginal');
    const previewSavings = document.getElementById('previewSavings');
    const previewNewPrice = document.getElementById('previewNewPrice');
    const modalError = document.getElementById('modalError');

    let currentItemId = 0;
    let currentOriginalPrice = 0;
    let selectedDuration = null;

    if (discountModal) {
        // Open modal via data attributes
        document.querySelectorAll('[data-discount-btn]').forEach(btn => {
            btn.addEventListener('click', () => {
                currentItemId = parseInt(btn.dataset.itemId);
                currentOriginalPrice = parseFloat(btn.dataset.itemPrice);
                selectedDuration = null;

                modalItemId.value = currentItemId;
                modalItemTitle.textContent = btn.dataset.itemTitle;
                modalCurrentPrice.textContent = currentOriginalPrice.toLocaleString('en-PH', { minimumFractionDigits: 2 });
                modalItemImage.src = btn.dataset.itemImage || '/images/placeholder.png';

                discountPercent.value = 10;

                // Reset active states
                document.querySelectorAll('.discount-preset').forEach(p => p.classList.remove('discount-preset--active'));
                document.querySelector('.discount-preset[data-percent="10"]')?.classList.add('discount-preset--active');

                document.querySelectorAll('.discount-expiry-option').forEach(o => o.classList.remove('discount-expiry-option--active'));
                document.querySelector('.discount-expiry-option[data-duration="indefinite"]')?.classList.add('discount-expiry-option--active');

                if (modalError) modalError.style.display = 'none';

                updatePreview();

                discountModal.style.display = 'flex';
            });
        });

        // Close modal
        function closeModal() {
            discountModal.style.display = 'none';
        }

        document.querySelector('[data-close-modal]')?.addEventListener('click', closeModal);

        discountModal.addEventListener('click', (e) => {
            if (e.target === discountModal) closeModal();
        });

        // Preset buttons
        document.querySelectorAll('.discount-preset').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('.discount-preset').forEach(p => p.classList.remove('discount-preset--active'));
                btn.classList.add('discount-preset--active');
                discountPercent.value = btn.dataset.percent;
                updatePreview();
            });
        });

        // Expiry options
        document.querySelectorAll('.discount-expiry-option').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('.discount-expiry-option').forEach(o => o.classList.remove('discount-expiry-option--active'));
                btn.classList.add('discount-expiry-option--active');
                selectedDuration = btn.dataset.duration === 'indefinite' ? null : parseInt(btn.dataset.duration);
            });
        });

        // Manual percent input
        discountPercent?.addEventListener('input', () => {
            let val = parseInt(discountPercent.value) || 10;
            val = Math.max(1, Math.min(99, val));
            discountPercent.value = val;

            document.querySelectorAll('.discount-preset').forEach(p => p.classList.remove('discount-preset--active'));
            updatePreview();
        });

        function updatePreview() {
            const percent = parseFloat(discountPercent.value) || 10;
            const newPrice = currentOriginalPrice * (1 - percent / 100);
            const savings = currentOriginalPrice - newPrice;

            if (previewOriginal) {
                previewOriginal.textContent = formatPrice(currentOriginalPrice);
            }
            if (previewSavings) {
                previewSavings.textContent = formatPrice(savings);
            }
            if (previewNewPrice) {
                previewNewPrice.textContent = formatPrice(newPrice);
            }
        }

        // Apply discount
        async function applyDiscount() {
            const percent = parseFloat(discountPercent.value);
            if (percent < 1 || percent > 99) {
                if (modalError) {
                    modalError.textContent = 'Discount must be between 1% and 99%.';
                    modalError.style.display = 'block';
                }
                return;
            }

            let expiresAt = null;
            if (selectedDuration) {
                const date = new Date();
                date.setHours(date.getHours() + selectedDuration);
                expiresAt = date.toISOString();
            }

            const applyBtn = document.querySelector('[data-apply-discount]');
            if (applyBtn) {
                applyBtn.disabled = true;
                applyBtn.textContent = 'Applying...';
            }

            try {
                const response = await fetch('/Items/ApplyDiscount', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': getAntiForgeryToken()
                    },
                    body: JSON.stringify({
                        itemId: currentItemId,
                        discountPercentage: percent,
                        expiresAt: expiresAt
                    })
                });

                const data = await response.json();

                if (data.success) {
                    window.location.reload();
                } else {
                    if (modalError) {
                        modalError.textContent = data.error || 'Failed to apply discount.';
                        modalError.style.display = 'block';
                    }
                    if (applyBtn) {
                        applyBtn.disabled = false;
                        applyBtn.textContent = 'Apply Discount';
                    }
                }
            } catch (err) {
                if (modalError) {
                    modalError.textContent = 'Network error. Please try again.';
                    modalError.style.display = 'block';
                }
                if (applyBtn) {
                    applyBtn.disabled = false;
                    applyBtn.textContent = 'Apply Discount';
                }
            }
        }

        document.querySelector('[data-apply-discount]')?.addEventListener('click', applyDiscount);

        // Remove discount
        document.querySelectorAll('[data-remove-discount]').forEach(btn => {
            btn.addEventListener('click', async () => {
                if (!confirm('Remove the discount and restore the original price?')) return;

                const itemId = btn.dataset.itemId;

                try {
                    const response = await fetch(`/Items/RemoveDiscount?itemId=${itemId}`, {
                        method: 'POST',
                        headers: {
                            'RequestVerificationToken': getAntiForgeryToken()
                        }
                    });

                    const data = await response.json();

                    if (data.success) {
                        window.location.reload();
                    } else {
                        alert(data.error || 'Failed to remove discount.');
                    }
                } catch (err) {
                    alert('Network error. Please try again.');
                }
            });
        });
    }

    // =========================================================================
    // DELETE PAGE - Form Submit
    // =========================================================================

    const deleteForm = document.getElementById('deleteForm');
    const deleteBtn = document.getElementById('deleteBtn');

    if (deleteForm && deleteBtn) {
        deleteForm.addEventListener('submit', () => {
            deleteBtn.disabled = true;
            deleteBtn.textContent = 'Deleting...';
        });
    }

    // =========================================================================
    // ESC key to close modals
    // =========================================================================

    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') {
            if (discountModal && discountModal.style.display === 'flex') {
                discountModal.style.display = 'none';
            }
        }
    });

})();
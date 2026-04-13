// wwwroot/js/map-picker.js
// Leaflet + OpenStreetMap address picker.
// Reverse-geocoding via Nominatim (no API key required).
// ------------------------------------------------------------------
// Public API (called from inline onclick / _AddressInputWithMap):
//   openMapPicker(inputId)   – opens the modal targeting <inputId>
//   confirmMapAddress()      – copies preview into target input & closes
//   closeMapPicker()         – closes the modal without saving
//   searchMapAddress()       – geocodes the search-box query
// ------------------------------------------------------------------

(function () {
    'use strict';

    /* ── State ──────────────────────────────────────────────────── */
    let map = null;
    let marker = null;
    let targetId = null;   // id of the textarea we are filling
    let userCallback = null; // optional callback passed by caller
    let altTextId = null; // optional id of a visible text input to use for forward geocoding
    let geocodeCtrl = null;   // AbortController for in-flight fetch
    let mapInitialized = false;
    let allowEdit = true; // when false, map is view-only (no placing/draggable)
    let cityDetectionInProgress = false;
    let cachedCity = null; // Cache the detected city for faster subsequent opens
    let cityDetectionTimeout = null;

    /* ── DOM helpers ────────────────────────────────────────────── */
    const $ = id => document.getElementById(id);

    function setSpinner(on) {
        const el = $('map-geocode-spinner');
        if (el) el.classList.toggle('is-loading', on);
    }

    function setPreview(text) {
        const el = $('map-picker-address-preview');
        if (el) el.value = text;
    }

    function showPinpointPreview(lat, lng) {
        // Hide editing UI when viewing-only
        const confirmBtn = $('map-picker-confirm-btn');
        if (confirmBtn) confirmBtn.style.display = 'none';
        const searchWrapper = $('map-picker-search-wrapper');
        if (searchWrapper) searchWrapper.style.display = 'none';
        const footer = $('map-picker-footer');
        if (footer) footer.style.justifyContent = 'flex-end';
        // preview text will be filled by reverseGeocode; leave it intact otherwise
    }

    /* ── Map initialisation ─────────────────────────────────────── */

    function initMap() {
        const mapContainer = $('map-picker-map');

        // Check if container actually has dimensions
        if (!mapContainer || mapContainer.clientHeight === 0) {
            console.warn('[map-picker] Map container has zero height. Retrying...');
            setTimeout(initMap, 50);
            return;
        }

        if (map) {
            // Already initialised — invalidate size to recalculate tiles
            map.invalidateSize();
            // If a marker exists, ensure we pan to it so the user sees the selected location
            setTimeout(() => {
                try {
                    map.invalidateSize();
                    if (marker) {
                        map.panTo(marker.getLatLng());
                    }
                } catch (err) { /* ignore */ }
            }, 100);
            return;
        }

        // Default centre: Manila, Philippines
        map = L.map('map-picker-map', {
            zoomControl: true,
            attributionControl: true
        }).setView([14.5995, 120.9842], 13);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright" target="_blank">OpenStreetMap</a> contributors',
            maxZoom: 19
        }).addTo(map);

        map.on('click', onMapClick);

        mapInitialized = true;
        console.log('[map-picker] Map initialized successfully');
    }

    /* ── Map interactions ───────────────────────────────────────── */

    function onMapClick(e) {
        if (!allowEdit) return;
        placeMarker(e.latlng.lat, e.latlng.lng);
        reverseGeocode(e.latlng.lat, e.latlng.lng);
    }

    function placeMarker(lat, lng) {
        if (marker) {
            marker.setLatLng([lat, lng]);
        } else {
            marker = L.marker([lat, lng], { draggable: allowEdit }).addTo(map);
            if (allowEdit) {
                marker.on('dragend', function () {
                    const pos = marker.getLatLng();
                    reverseGeocode(pos.lat, pos.lng);
                });
            }
        }
        map.panTo([lat, lng]);
        // Write coordinates to the hidden inputs associated with the current target
        if (targetId) {
            const latEl = $(targetId + '-latitude');
            const lngEl = $(targetId + '-longitude');
            if (latEl) latEl.value = lat.toFixed(6);
            if (lngEl) lngEl.value = lng.toFixed(6);
            // If view-only, also show pinpoint preview
            if (!allowEdit) showPinpointPreview(lat, lng);
        }
    }

    /* ── Geocoding ──────────────────────────────────────────────── */

    /**
     * Nominatim reverse geocode: coords → address string.
     */
    function reverseGeocode(lat, lng) {
        // Cancel any previous in-flight request
        if (geocodeCtrl) geocodeCtrl.abort();
        geocodeCtrl = new AbortController();

        setSpinner(true);
        setPreview('Fetching address…');

        fetch(
            `https://nominatim.openstreetmap.org/reverse?lat=${lat}&lon=${lng}&format=json`,
            {
                headers: {
                    'Accept-Language': 'en',
                    'User-Agent': 'ThriftLoop/1.0'
                },
                signal: geocodeCtrl.signal
            }
        )
            .then(r => {
                if (!r.ok) throw new Error('Network error');
                return r.json();
            })
            .then(data => {
                setPreview(data.display_name ?? '');
            })
            .catch(err => {
                if (err.name !== 'AbortError') {
                    setPreview('');
                    console.warn('[map-picker] Reverse geocode failed:', err);
                }
            })
            .finally(() => setSpinner(false));
    }

    /**
     * Nominatim forward geocode: address string → coords.
     * Called when the user types in the search box and hits Search.
     */
    function geocodeQuery(query) {
        if (!query.trim()) return;
        if (geocodeCtrl) geocodeCtrl.abort();
        geocodeCtrl = new AbortController();

        setSpinner(true);

        // Update search bar to show searching state
        const searchBox = $('map-picker-search');
        if (searchBox) {
            searchBox.classList.add('searching');
        }

        fetch(
            `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=1`,
            {
                headers: {
                    'Accept-Language': 'en',
                    'User-Agent': 'ThriftLoop/1.0'
                },
                signal: geocodeCtrl.signal
            }
        )
            .then(r => r.json())
            .then(data => {
                if (!data.length) {
                    alert('Address not found. Try a more specific query.');
                    return;
                }
                const lat = parseFloat(data[0].lat);
                const lng = parseFloat(data[0].lon);

                // Ensure map is initialized before using
                if (map) {
                    map.setView([lat, lng], 17);
                    placeMarker(lat, lng);
                    reverseGeocode(lat, lng);
                } else {
                    console.warn('[map-picker] Map not ready yet for geocode');
                }
            })
            .catch(err => {
                if (err.name !== 'AbortError')
                    console.warn('[map-picker] Forward geocode failed:', err);
            })
            .finally(() => {
                setSpinner(false);
                if (searchBox) {
                    searchBox.classList.remove('searching');
                }
            });
    }

    /* ── City Detection ─────────────────────────────────────────── */

    /**
     * Detect user's city using browser geolocation and reverse geocoding
     * Uses caching for faster subsequent detections
     */
    function detectUserCity() {
        return new Promise((resolve, reject) => {
            // Return cached city if available (detected in last 5 minutes)
            if (cachedCity) {
                console.log('[map-picker] Using cached city:', cachedCity);
                resolve(cachedCity);
                return;
            }

            if (!navigator.geolocation) {
                console.log('[map-picker] Geolocation not supported');
                reject(new Error('Geolocation not supported'));
                return;
            }

            // Set a timeout to avoid hanging
            const timeoutId = setTimeout(() => {
                reject(new Error('Geolocation timeout'));
            }, 3000);

            navigator.geolocation.getCurrentPosition(
                async (position) => {
                    clearTimeout(timeoutId);
                    const { latitude, longitude } = position.coords;

                    try {
                        // Reverse geocode to get city name
                        const response = await fetch(
                            `https://nominatim.openstreetmap.org/reverse?lat=${latitude}&lon=${longitude}&format=json`,
                            {
                                headers: {
                                    'Accept-Language': 'en',
                                    'User-Agent': 'ThriftLoop/1.0'
                                }
                            }
                        );

                        if (!response.ok) throw new Error('Failed to fetch city');

                        const data = await response.json();

                        // Extract city from address components
                        const city = data.address?.city ||
                            data.address?.town ||
                            data.address?.village ||
                            data.address?.suburb ||
                            data.address?.municipality ||
                            data.address?.county ||
                            '';

                        let detectedCity = '';
                        if (city) {
                            console.log('[map-picker] Detected city:', city);
                            detectedCity = city;
                        } else {
                            // Fallback: use display_name and extract city portion
                            const displayParts = data.display_name?.split(',') || [];
                            detectedCity = displayParts[1]?.trim() || displayParts[0]?.trim() || '';
                            console.log('[map-picker] Detected city (fallback):', detectedCity);
                        }

                        // Cache the city for 5 minutes
                        cachedCity = detectedCity;
                        setTimeout(() => {
                            cachedCity = null;
                        }, 300000); // Clear cache after 5 minutes

                        resolve(detectedCity);
                    } catch (error) {
                        console.warn('[map-picker] City detection failed:', error);
                        reject(error);
                    }
                },
                (error) => {
                    clearTimeout(timeoutId);
                    console.log('[map-picker] Geolocation error:', error.message);
                    reject(error);
                },
                {
                    enableHighAccuracy: false,
                    timeout: 3000, // Reduced timeout for faster failure
                    maximumAge: 300000 // Cache for 5 minutes
                }
            );
        });
    }

    /**
     * Show a prominent loading indicator for city detection
     */
    function showCityDetectionIndicator(searchBox) {
        // Create or get loading overlay
        let loadingOverlay = $('map-city-loading');
        if (!loadingOverlay) {
            loadingOverlay = document.createElement('div');
            loadingOverlay.id = 'map-city-loading';
            loadingOverlay.className = 'map-city-loading-overlay';
            loadingOverlay.innerHTML = `
                <div class="map-city-loading-content">
                    <div class="map-city-loading-spinner">
                        <div class="map-spinner-ring"></div>
                    </div>
                    <div class="map-city-loading-text">
                        <div class="map-city-loading-title">Detecting your location</div>
                        <div class="map-city-loading-subtitle">Please wait while we find your city...</div>
                    </div>
                </div>
            `;

            const searchWrapper = $('map-picker-search-wrapper');
            if (searchWrapper) {
                searchWrapper.style.position = 'relative';
                searchWrapper.appendChild(loadingOverlay);
            }
        }

        loadingOverlay.style.display = 'flex';

        if (searchBox) {
            searchBox.classList.add('detecting-city');
            searchBox.placeholder = '📍 Detecting your city...';
        }
    }

    /**
     * Hide the city detection loading indicator
     */
    function hideCityDetectionIndicator(searchBox) {
        const loadingOverlay = $('map-city-loading');
        if (loadingOverlay) {
            loadingOverlay.style.display = 'none';
        }

        if (searchBox) {
            searchBox.classList.remove('detecting-city');
        }
    }

    /* ── Pre-populate helper ────────────────────────────────────── */

    /**
     * If the target textarea already holds a value when the modal opens,
     * forward-geocode it so the map centres on the current address.
     */
    function tryPrePopulate(inputId) {
        // If coordinates hidden inputs exist, use them to position the marker
        const latEl = $(inputId + '-latitude');
        const lngEl = $(inputId + '-longitude');
        const existing = ($(inputId)?.value ?? '').trim();

        const tryMap = () => {
            if (!map) {
                setTimeout(tryMap, 100);
                return;
            }

            if (latEl && lngEl && latEl.value && lngEl.value) {
                const lat = parseFloat(latEl.value);
                const lng = parseFloat(lngEl.value);
                if (!isNaN(lat) && !isNaN(lng)) {
                    map.setView([lat, lng], 17);
                    placeMarker(lat, lng);
                    reverseGeocode(lat, lng);
                    return;
                }
            }

            if (existing) {
                geocodeQuery(existing);
            }
        };

        tryMap();
    }

    /* ── Public API ─────────────────────────────────────────────── */

    /**
     * Open the map picker targeting the <textarea id="inputId">.
     */
    // signature: openMapPicker(inputId, [callbackOrAlt], [mode])
    window.openMapPicker = function (inputId, secondArg, mode) {
        // Clear any pending timeout
        if (cityDetectionTimeout) {
            clearTimeout(cityDetectionTimeout);
            cityDetectionTimeout = null;
        }

        // Support optional second arg: a callback function to receive (latLng, addressString)
        targetId = inputId;
        userCallback = null;
        altTextId = null;
        if (typeof secondArg === 'function') userCallback = secondArg;
        if (typeof secondArg === 'string') altTextId = secondArg;

        // determine view-only vs editable mode early so pre-population uses correct behaviour
        allowEdit = true;
        if (typeof mode === 'string' && mode === 'view') allowEdit = false;

        // Reset modal UI to editable defaults (undo any inline styles set by showPinpointPreview)
        const confirmBtn = $('map-picker-confirm-btn');
        if (confirmBtn) confirmBtn.style.display = '';
        const searchWrapper = $('map-picker-search-wrapper');
        if (searchWrapper) searchWrapper.style.display = '';
        const footer = $('map-picker-footer');
        if (footer) footer.style.justifyContent = '';

        if (!allowEdit && marker && marker.dragging) {
            try { marker.dragging.disable(); } catch (e) { /* ignore */ }
        }

        const overlay = $('map-picker-modal');
        if (!overlay) {
            console.error('[map-picker] Map modal element #map-picker-modal not found.');
            return;
        }

        // Reset search box
        const searchBox = $('map-picker-search');
        if (searchBox) {
            searchBox.value = '';
            searchBox.placeholder = 'Search for an address or place…';
            searchBox.classList.remove('auto-detecting', 'detecting-city');
        }

        // Hide any existing loading indicator
        hideCityDetectionIndicator(searchBox);

        overlay.classList.add('is-open');

        // Wait for the modal's CSS transition to complete (0.22s)
        // before initializing the map to ensure the container has dimensions
        setTimeout(() => {
            initMap();

            // Check if there's existing data to pre-populate
            const hasExistingData = (() => {
                const latEl = $(inputId + '-latitude');
                const lngEl = $(inputId + '-longitude');
                const existing = ($(inputId)?.value ?? '').trim();
                return (latEl && lngEl && latEl.value && lngEl.value) || existing;
            })();

            // Pre-populate if there's existing data
            if (hasExistingData) {
                tryPrePopulate(inputId);
            }

            // Auto-detect city and populate search bar (only if no existing data and editable)
            if (searchBox && !hasExistingData && allowEdit) {
                // Show prominent loading indicator
                showCityDetectionIndicator(searchBox);
                cityDetectionInProgress = true;

                detectUserCity()
                    .then(city => {
                        if (city && searchBox) {
                            hideCityDetectionIndicator(searchBox);
                            searchBox.value = city;
                            searchBox.placeholder = `📍 ${city} (auto-detected)`;
                            searchBox.classList.add('auto-detected');
                            cityDetectionInProgress = false;

                            // AUTO-SUBMIT: Automatically search for the city
                            console.log('[map-picker] Auto-searching for detected city:', city);

                            // Wait a bit for map to be fully ready, then search
                            const performAutoSearch = () => {
                                if (map) {
                                    geocodeQuery(city);
                                } else {
                                    setTimeout(performAutoSearch, 50);
                                }
                            };

                            performAutoSearch();
                        }
                    })
                    .catch(() => {
                        // Silent fail - just leave search box empty
                        hideCityDetectionIndicator(searchBox);
                        if (searchBox) {
                            searchBox.placeholder = 'Search for an address or place…';
                        }
                        cityDetectionInProgress = false;
                        console.log('[map-picker] Could not detect city automatically');
                    });
            }

            // If view-only and coords exist, ensure UI reflects view-only immediately
            if (!allowEdit) {
                const latEl = $(inputId + '-latitude');
                const lngEl = $(inputId + '-longitude');
                if (latEl && lngEl && latEl.value && lngEl.value) {
                    const lat = parseFloat(latEl.value);
                    const lng = parseFloat(lngEl.value);
                    if (!isNaN(lat) && !isNaN(lng)) {
                        // ensure marker placed and map centered
                        placeMarker(lat, lng);
                        showPinpointPreview(lat, lng);
                    }
                }
            }

            // After a short delay ensure map repaint and marker centering
            setTimeout(() => {
                try {
                    if (map) map.invalidateSize();
                    const latEl = $(inputId + '-latitude');
                    const lngEl = $(inputId + '-longitude');
                    if (map && marker == null && latEl && lngEl && latEl.value && lngEl.value) {
                        const lat = parseFloat(latEl.value);
                        const lng = parseFloat(lngEl.value);
                        if (!isNaN(lat) && !isNaN(lng)) {
                            map.setView([lat, lng], 17);
                            placeMarker(lat, lng);
                            // ensure preview populated for view-only
                            if (!allowEdit) reverseGeocode(lat, lng);
                        }
                    } else if (map && marker) {
                        map.panTo(marker.getLatLng());
                        // if view-only, refresh preview from marker
                        if (!allowEdit) {
                            const pos = marker.getLatLng();
                            showPinpointPreview(pos.lat, pos.lng);
                            reverseGeocode(pos.lat, pos.lng);
                        }
                    }
                } catch (err) { /* ignore */ }
            }, 200);
        }, 250);
    };

    /**
     * Forward-geocode whatever is in the search input.
     * Wired to the Search button inside the modal.
     */
    window.searchMapAddress = function () {
        const q = ($('map-picker-search')?.value ?? '').trim();
        if (q) geocodeQuery(q);
    };

    /**
     * Confirm selection: copy preview text into the target textarea, then close.
     */
    window.confirmMapAddress = function () {
        const preview = ($('map-picker-address-preview')?.value ?? '').trim();
        if (preview && targetId) {
            const target = $(targetId);
            if (target) {
                target.value = preview;

                // Fire a native 'input' event so frameworks/validation pick up the change
                target.dispatchEvent(new Event('input', { bubbles: true }));
                target.dispatchEvent(new Event('change', { bubbles: true }));

                // Also ensure hidden fields are updated (in case marker was placed but not dragged)
                const latEl = $(targetId + '-latitude');
                const lngEl = $(targetId + '-longitude');
                if (marker && latEl && lngEl) {
                    const pos = marker.getLatLng();
                    latEl.value = pos.lat.toFixed(6);
                    lngEl.value = pos.lng.toFixed(6);
                }
            }
        }
        // If caller provided a callback, invoke it with selected coords and preview text
        try {
            if (userCallback && marker) {
                const pos = marker.getLatLng();
                userCallback({ lat: pos.lat, lng: pos.lng }, preview);
            }
        } catch (err) {
            console.warn('[map-picker] user callback threw:', err);
        }

        closeMapPicker();
    };

    /**
     * Close the modal without saving anything.
     */
    window.closeMapPicker = function () {
        // Clear any pending timeout
        if (cityDetectionTimeout) {
            clearTimeout(cityDetectionTimeout);
            cityDetectionTimeout = null;
        }

        const overlay = $('map-picker-modal');
        if (overlay) overlay.classList.remove('is-open');

        // Cancel any pending geocode request
        if (geocodeCtrl) {
            geocodeCtrl.abort();
            geocodeCtrl = null;
        }

        setSpinner(false);
        // clear optional callback so it doesn't leak between opens
        userCallback = null;
        cityDetectionInProgress = false;

        // Reset modal UI to editable defaults so next open isn't stuck in view-only state
        const confirmBtn = $('map-picker-confirm-btn');
        if (confirmBtn) confirmBtn.style.display = '';
        const searchWrapper = $('map-picker-search-wrapper');
        if (searchWrapper) searchWrapper.style.display = '';
        const footer = $('map-picker-footer');
        if (footer) footer.style.justifyContent = '';

        // Reset search box styling
        const searchBox = $('map-picker-search');
        if (searchBox) {
            searchBox.classList.remove('auto-detecting', 'detecting-city', 'auto-detected', 'searching');
            searchBox.placeholder = 'Search for an address or place…';
        }

        // Hide loading indicator
        hideCityDetectionIndicator(searchBox);
    };

    /* ── Keyboard support ───────────────────────────────────────── */

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            const overlay = $('map-picker-modal');
            if (overlay && overlay.classList.contains('is-open')) closeMapPicker();
        }
    });

    // Allow pressing Enter in the search box to trigger search
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && document.activeElement?.id === 'map-picker-search') {
            e.preventDefault();
            window.searchMapAddress();
        }
    });

    /* ── Click-outside-to-close ─────────────────────────────────── */

    document.addEventListener('click', function (e) {
        const overlay = $('map-picker-modal');
        if (overlay && overlay.classList.contains('is-open') && e.target === overlay) {
            closeMapPicker();
        }
    });

})();
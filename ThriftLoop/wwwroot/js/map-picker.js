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
    let geocodeCtrl = null;   // AbortController for in-flight fetch
    let mapInitialized = false;

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
        placeMarker(e.latlng.lat, e.latlng.lng);
        reverseGeocode(e.latlng.lat, e.latlng.lng);
    }

    function placeMarker(lat, lng) {
        if (marker) {
            marker.setLatLng([lat, lng]);
        } else {
            marker = L.marker([lat, lng], { draggable: true }).addTo(map);
            marker.on('dragend', function () {
                const pos = marker.getLatLng();
                reverseGeocode(pos.lat, pos.lng);
            });
        }
        map.panTo([lat, lng]);
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
                headers: { 'Accept-Language': 'en' },
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

        fetch(
            `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=1`,
            {
                headers: { 'Accept-Language': 'en' },
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
            .finally(() => setSpinner(false));
    }

    /* ── Pre-populate helper ────────────────────────────────────── */

    /**
     * If the target textarea already holds a value when the modal opens,
     * forward-geocode it so the map centres on the current address.
     */
    function tryPrePopulate(inputId) {
        const existing = ($(inputId)?.value ?? '').trim();
        if (existing) {
            // Wait for map to be ready before geocoding
            const waitForMap = () => {
                if (map) {
                    geocodeQuery(existing);
                } else {
                    setTimeout(waitForMap, 100);
                }
            };
            waitForMap();
        }
    }

    /* ── Public API ─────────────────────────────────────────────── */

    /**
     * Open the map picker targeting the <textarea id="inputId">.
     */
    window.openMapPicker = function (inputId) {
        targetId = inputId;

        const overlay = $('map-picker-modal');
        if (!overlay) {
            console.error('[map-picker] Modal element #map-picker-modal not found.');
            return;
        }

        // Reset search box
        const searchBox = $('map-picker-search');
        if (searchBox) searchBox.value = '';

        overlay.classList.add('is-open');

        // Wait for the modal's CSS transition to complete (0.22s)
        // before initializing the map to ensure the container has dimensions
        setTimeout(() => {
            initMap();
            tryPrePopulate(inputId);
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
            }
        }
        closeMapPicker();
    };

    /**
     * Close the modal without saving anything.
     */
    window.closeMapPicker = function () {
        const overlay = $('map-picker-modal');
        if (overlay) overlay.classList.remove('is-open');

        // Cancel any pending geocode request
        if (geocodeCtrl) {
            geocodeCtrl.abort();
            geocodeCtrl = null;
        }

        setSpinner(false);
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
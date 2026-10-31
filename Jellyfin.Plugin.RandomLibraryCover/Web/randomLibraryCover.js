// Random Library Cover: adds a "Random cover" item to the ⋮ menu of the library cards in My Media.
(function () {
    'use strict';

    if (window.RandomLibraryCoverLoaded) {
        return;
    }
    window.RandomLibraryCoverLoaded = true;

    const MENU_ID = 'randomlibrarycover';
    const MARK = 'data-randomlibrarycover';
    const OVERRIDES_KEY = 'randomLibraryCover.overrides';
    // jellyfin-web caches the library list (in memory and in IndexedDB) for about a minute, so for a while the
    // home screen keeps rendering the old image tag. Remember new tags a little longer and patch them back in.
    const OVERRIDE_TTL = 5 * 60 * 1000;

    // The card whose menu was opened last. jellyfin-web builds the menu (an action sheet) asynchronously and
    // doesn't say which card it belongs to, so remember the card on the click and match the sheet by time.
    let pending = null;

    function libraryCard(target) {
        const card = target && target.closest ? target.closest('.card[data-id]') : null;
        if (!card || card.getAttribute('data-type') !== 'CollectionFolder' || !card.closest('.homeSectionsContainer')) {
            return null;
        }
        return card;
    }

    function remember(card) {
        pending = card ? { card: card, time: Date.now() } : null;
    }

    document.addEventListener('click', function (event) {
        if (event.target.closest && event.target.closest('[data-action="menu"]')) {
            remember(libraryCard(event.target));
        }
    }, true);

    // Right click and long press open the same menu.
    document.addEventListener('contextmenu', function (event) {
        remember(libraryCard(event.target));
    }, true);

    function isAdmin() {
        const apiClient = window.ApiClient;
        if (!apiClient || typeof apiClient.getCurrentUser !== 'function') {
            return Promise.resolve(false);
        }
        return apiClient.getCurrentUser()
            .then(function (user) { return !!(user && user.Policy && user.Policy.IsAdministrator); })
            .catch(function () { return false; });
    }

    function addMenuItem(sheet) {
        sheet.setAttribute(MARK, '');
        if (!pending || Date.now() - pending.time > 5000) {
            return;
        }

        const card = pending.card;
        pending = null;

        // Only item menus have "Edit images"; it is also where a new cover fits best.
        const editImages = sheet.querySelector('.actionSheetMenuItem[data-id="editimages"]');
        if (!editImages) {
            return;
        }

        isAdmin().then(function (admin) {
            if (!admin || !sheet.isConnected) {
                return;
            }

            const item = editImages.cloneNode(true);
            item.setAttribute('data-id', MENU_ID);
            item.removeAttribute('autofocus');
            const icon = item.querySelector('.actionsheetMenuItemIcon');
            if (icon) {
                icon.className = 'actionsheetMenuItemIcon listItemIcon listItemIcon-transparent material-icons shuffle';
                icon.style.visibility = '';
            }
            const text = item.querySelector('.actionSheetItemText');
            if (text) {
                text.textContent = 'Random cover';
            }

            // The sheet's own click handler closes it; its command handler ignores ids it doesn't know.
            item.addEventListener('click', function () {
                shuffle(card);
            });
            editImages.after(item);
        });
    }

    async function shuffle(card) {
        const apiClient = window.ApiClient;
        const libraryId = card.getAttribute('data-id');
        if (!apiClient || !libraryId) {
            return;
        }

        const cards = document.querySelectorAll('.card[data-id="' + libraryId + '"]');
        cards.forEach(function (c) { c.style.transition = 'opacity .2s'; c.style.opacity = '0.5'; });

        try {
            let result;
            try {
                result = await apiClient.ajax({
                    type: 'POST',
                    url: apiClient.getUrl('RandomLibraryCover/Libraries/' + libraryId + '/Shuffle'),
                    dataType: 'json'
                });
            } catch (response) {
                // ajax rejects with the fetch Response for error status codes.
                const body = response && typeof response.json === 'function' ? await response.json().catch(function () { return null; }) : null;
                toast((body && body.Error) || 'Could not change the cover' + (response && response.status ? ' (' + response.status + ')' : ''));
                return;
            }

            const library = await apiClient.getItem(apiClient.getCurrentUserId(), libraryId);
            const tag = library && library.ImageTags && library.ImageTags.Primary;
            if (tag) {
                saveOverride(libraryId, tag);
            }
            cards.forEach(function (c) { setCardImage(apiClient, c, libraryId, tag); });
            toast('New cover from ' + result.SourceName);
        } catch (err) {
            console.error('[RandomLibraryCover] Shuffle failed', err);
            toast('Could not change the cover');
        } finally {
            cards.forEach(function (c) { c.style.opacity = ''; });
        }
    }

    function loadOverrides() {
        try {
            const all = JSON.parse(sessionStorage.getItem(OVERRIDES_KEY) || '{}');
            const now = Date.now();
            Object.keys(all).forEach(function (id) {
                if (!all[id] || all[id].until < now) {
                    delete all[id];
                }
            });
            return all;
        } catch (e) {
            return {};
        }
    }

    function saveOverride(libraryId, tag) {
        try {
            const all = loadOverrides();
            all[libraryId] = { tag: tag, until: Date.now() + OVERRIDE_TTL };
            sessionStorage.setItem(OVERRIDES_KEY, JSON.stringify(all));
        } catch (e) {
            // Storage blocked: the card was already updated, it just may show the old cover for a minute.
        }
    }

    function currentTag(image) {
        const url = image.getAttribute('data-src') || image.style.backgroundImage || '';
        const match = url.match(/[?&]tag=([^&"')]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function applyOverrides() {
        const overrides = loadOverrides();
        const ids = Object.keys(overrides);
        if (!ids.length || !window.ApiClient) {
            return;
        }
        ids.forEach(function (id) {
            document.querySelectorAll('.homeSectionsContainer .card[data-id="' + id + '"]').forEach(function (card) {
                const image = card.querySelector('.cardImageContainer');
                if (image && currentTag(image) !== overrides[id].tag) {
                    setCardImage(window.ApiClient, card, id, overrides[id].tag);
                }
            });
        });
    }

    function setCardImage(apiClient, card, libraryId, tag) {
        const image = card.querySelector('.cardImageContainer');
        if (!image || !tag) {
            return;
        }

        // Keep the size the card asked for, only swap the tag (it changes with every new image).
        let url = image.getAttribute('data-src') || (image.style.backgroundImage || '').replace(/^url\(["']?|["']?\)$/g, '');
        if (url && /[?&]tag=/.test(url)) {
            url = url.replace(/([?&]tag=)[^&]*/, '$1' + encodeURIComponent(tag));
        } else {
            url = apiClient.getScaledImageUrl(libraryId, { type: 'Primary', tag: tag, maxWidth: Math.round(image.offsetWidth * (window.devicePixelRatio || 1)) || 800 });
        }

        // A library without a cover shows an icon on a colored background instead.
        image.querySelectorAll('.cardImageIcon').forEach(function (el) { el.remove(); });
        image.className = image.className.replace(/\bdefaultCardBackground\d*\b/g, '').trim();
        if (!image.classList.contains('coveredImage')) {
            image.classList.add('coveredImage');
        }

        image.setAttribute('data-src', url);
        image.classList.remove('lazy');
        image.style.backgroundImage = 'url("' + url + '")';
        const blurhash = image.querySelector('.blurhash-canvas');
        if (blurhash) {
            blurhash.remove();
        }
    }

    function toast(message) {
        const el = document.createElement('div');
        el.textContent = message;
        el.style.cssText = 'position:fixed;left:50%;bottom:2em;transform:translateX(-50%);z-index:999999;'
            + 'padding:.8em 1.4em;border-radius:.3em;background:rgba(32,32,32,.95);color:#fff;'
            + 'box-shadow:0 2px 8px rgba(0,0,0,.4);font-size:95%;max-width:90vw;text-align:center;'
            + 'opacity:0;transition:opacity .2s;pointer-events:none;';
        document.body.appendChild(el);
        requestAnimationFrame(function () { el.style.opacity = '1'; });
        setTimeout(function () {
            el.style.opacity = '0';
            setTimeout(function () { el.remove(); }, 300);
        }, 3500);
    }

    let scheduled = false;

    function scan() {
        scheduled = false;
        document.querySelectorAll('.actionSheet:not([' + MARK + '])').forEach(addMenuItem);
        applyOverrides();
    }

    new MutationObserver(function () {
        if (!scheduled) {
            scheduled = true;
            requestAnimationFrame(scan);
        }
    }).observe(document.body, { childList: true, subtree: true });
})();

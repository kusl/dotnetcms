/**
 * MyBlog Theme Manager
 * Handles theme switching, persistence, and system preference detection
 */
window.themeManager = {
    storageKey: 'myblog-theme',
    defaultTheme: 'light',
    dotNetRef: null,

    /**
     * Available themes with their dark/light classification
     */
    themes: {
        'light': { isDark: false },
        'dark': { isDark: true },
        'sepia': { isDark: false },
        'nord': { isDark: true },
        'solarized-light': { isDark: false },
        'dracula': { isDark: true }
    },

    /**
     * Initialize the theme system
     * @param {object} dotNetRef - Reference to the Blazor component
     * @returns {string} The current theme ID
     */
    init: function(dotNetRef) {
        this.dotNetRef = dotNetRef;

        // Check for saved preference
        var savedTheme = localStorage.getItem(this.storageKey);

        // If no saved preference, check system preference
        if (!savedTheme) {
            var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            savedTheme = prefersDark ? 'dark' : 'light';
        }

        // Validate theme exists
        if (!this.themes[savedTheme]) {
            savedTheme = this.defaultTheme;
        }

        // Apply theme
        this.applyTheme(savedTheme);

        // Listen for system preference changes
        var self = this;
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function(e) {
            // Only auto-switch if user hasn't explicitly chosen a theme
            if (!localStorage.getItem(self.storageKey)) {
                var newTheme = e.matches ? 'dark' : 'light';
                self.setTheme(newTheme);
            }
        });

        return savedTheme;
    },

    /**
     * Apply a theme to the document
     * @param {string} themeId - The theme identifier
     */
    applyTheme: function(themeId) {
        document.documentElement.setAttribute('data-theme', themeId);

        // Update meta theme-color for mobile browsers
        var isDark = this.themes[themeId] ? this.themes[themeId].isDark : false;
        this.updateMetaThemeColor(isDark);
    },

    /**
     * Set and persist a theme
     * @param {string} themeId - The theme identifier
     */
    setTheme: function(themeId) {
        if (!this.themes[themeId]) {
            console.warn('Theme "' + themeId + '" not found, using default');
            themeId = this.defaultTheme;
        }

        // Apply immediately
        this.applyTheme(themeId);

        // Persist to localStorage
        localStorage.setItem(this.storageKey, themeId);

        // Announce change to screen readers
        this.announceThemeChange(themeId);
    },

    /**
     * Update the meta theme-color for mobile browser UI
     * @param {boolean} isDark - Whether the theme is dark
     */
    updateMetaThemeColor: function(isDark) {
        var metaThemeColor = document.querySelector('meta[name="theme-color"]');

        if (!metaThemeColor) {
            metaThemeColor = document.createElement('meta');
            metaThemeColor.name = 'theme-color';
            document.head.appendChild(metaThemeColor);
        }

        // Set color based on theme type
        metaThemeColor.content = isDark ? '#0f172a' : '#f8f9fa';
    },

    /**
     * Announce theme change to screen readers
     * @param {string} themeId - The theme identifier
     */
    announceThemeChange: function(themeId) {
        var themeName = themeId.split('-').map(function(w) {
            return w.charAt(0).toUpperCase() + w.slice(1);
        }).join(' ');

        // Create or reuse announcement element
        var announcer = document.getElementById('theme-announcer');
        if (!announcer) {
            announcer = document.createElement('div');
            announcer.id = 'theme-announcer';
            announcer.setAttribute('role', 'status');
            announcer.setAttribute('aria-live', 'polite');
            announcer.setAttribute('aria-atomic', 'true');
            announcer.className = 'visually-hidden';
            document.body.appendChild(announcer);
        }

        // Announce the change
        announcer.textContent = 'Theme changed to ' + themeName;
    },

    /**
     * Register click outside handler to close menu
     * @param {object} dotNetRef - Reference to the Blazor component
     */
    registerClickOutside: function(dotNetRef) {
        this.dotNetRef = dotNetRef;
        var self = this;

        document.addEventListener('click', function(e) {
            var themeSwitcher = e.target.closest('.theme-switcher');
            if (!themeSwitcher && self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('CloseMenu');
            }
        });

        // Also close on escape key
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('CloseMenu');
            }
        });
    },

    /**
     * Get current theme
     * @returns {string} Current theme ID
     */
    getCurrentTheme: function() {
        return document.documentElement.getAttribute('data-theme') || this.defaultTheme;
    },

    /**
     * Check if current theme is dark
     * @returns {boolean}
     */
    isDarkMode: function() {
        var current = this.getCurrentTheme();
        return this.themes[current] ? this.themes[current].isDark : false;
    }
};

/**
 * Share post using Web Share API with clipboard fallback
 * Works on mobile browsers including Chrome on iOS
 * @param {string} title - The post title to share
 */
async function sharePost(title) {
    var url = window.location.href;
    var shareData = {
        title: title,
        text: title,
        url: url
    };

    // Find the share button to update its state
    var shareBtn = document.querySelector('.share-btn');

    // Check if Web Share API is available and we're in a secure context
    if (navigator.share && window.isSecureContext) {
        try {
            // Web Share API requires user activation, which we have from the click
            await navigator.share(shareData);
            // Success - the share dialog was shown
            showShareSuccess(shareBtn, 'Shared!');
        } catch (err) {
            // User cancelled or share failed
            if (err.name === 'AbortError') {
                // User cancelled - this is fine, no need to show error
                return;
            }
            // For other errors, fall back to clipboard
            console.warn('Web Share API failed, falling back to clipboard:', err);
            await copyToClipboard(url, shareBtn);
        }
    } else {
        // Web Share API not available, use clipboard fallback
        await copyToClipboard(url, shareBtn);
    }
}

/**
 * Copy URL to clipboard with visual feedback
 * @param {string} url - The URL to copy
 * @param {Element} shareBtn - The share button element for visual feedback
 */
async function copyToClipboard(url, shareBtn) {
    try {
        // Modern clipboard API
        if (navigator.clipboard && navigator.clipboard.writeText) {
            await navigator.clipboard.writeText(url);
            showShareSuccess(shareBtn, 'Link copied!');
        } else {
            // Fallback for older browsers
            var textArea = document.createElement('textarea');
            textArea.value = url;
            textArea.style.position = 'fixed';
            textArea.style.left = '-999999px';
            textArea.style.top = '-999999px';
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();

            var successful = document.execCommand('copy');
            document.body.removeChild(textArea);

            if (successful) {
                showShareSuccess(shareBtn, 'Link copied!');
            } else {
                showShareError(shareBtn, 'Copy failed');
            }
        }
    } catch (err) {
        console.error('Failed to copy to clipboard:', err);
        showShareError(shareBtn, 'Copy failed');
    }
}

/**
 * Show success state on share button
 * @param {Element} shareBtn - The share button element
 * @param {string} message - Success message to display
 */
function showShareSuccess(shareBtn, message) {
    if (!shareBtn) return;

    var originalText = shareBtn.innerHTML;
    shareBtn.classList.add('success');
    shareBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg> ' + message;

    setTimeout(function() {
        shareBtn.classList.remove('success');
        shareBtn.innerHTML = originalText;
    }, 2000);
}

/**
 * Show error state on share button
 * @param {Element} shareBtn - The share button element
 * @param {string} message - Error message to display
 */
function showShareError(shareBtn, message) {
    if (!shareBtn) return;

    var originalText = shareBtn.innerHTML;
    shareBtn.classList.add('error');
    shareBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg> ' + message;

    setTimeout(function() {
        shareBtn.classList.remove('error');
        shareBtn.innerHTML = originalText;
    }, 2000);
}

/**
 * Initialize theme immediately to prevent flash of wrong theme
 * This runs before Blazor initializes
 */
(function() {
    var storageKey = 'myblog-theme';
    var defaultTheme = 'light';

    var theme = localStorage.getItem(storageKey);

    if (!theme) {
        var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        theme = prefersDark ? 'dark' : 'light';
    }

    // Apply theme immediately (before page renders)
    document.documentElement.setAttribute('data-theme', theme);
})();

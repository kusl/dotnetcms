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

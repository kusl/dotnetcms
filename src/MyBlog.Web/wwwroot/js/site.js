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
        let savedTheme = localStorage.getItem(this.storageKey);

        // If no saved preference, check system preference
        if (!savedTheme) {
            const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            savedTheme = prefersDark ? 'dark' : 'light';
        }

        // Validate theme exists
        if (!this.themes[savedTheme]) {
            savedTheme = this.defaultTheme;
        }

        // Apply theme
        this.applyTheme(savedTheme);

        // Listen for system preference changes
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            // Only auto-switch if user hasn't explicitly chosen a theme
            if (!localStorage.getItem(this.storageKey)) {
                const newTheme = e.matches ? 'dark' : 'light';
                this.setTheme(newTheme);
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
        const isDark = this.themes[themeId]?.isDark ?? false;
        this.updateMetaThemeColor(isDark);
    },

    /**
     * Set and persist a theme
     * @param {string} themeId - The theme identifier
     */
    setTheme: function(themeId) {
        if (!this.themes[themeId]) {
            console.warn(`Theme "${themeId}" not found, using default`);
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
        let metaThemeColor = document.querySelector('meta[name="theme-color"]');

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
        const themeName = themeId.split('-').map(w =>
            w.charAt(0).toUpperCase() + w.slice(1)
        ).join(' ');

        // Create or reuse announcement element
        let announcer = document.getElementById('theme-announcer');
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
        announcer.textContent = `Theme changed to ${themeName}`;
    },

    /**
     * Register click outside handler to close menu
     * @param {object} dotNetRef - Reference to the Blazor component
     */
    registerClickOutside: function(dotNetRef) {
        document.addEventListener('click', (e) => {
            const themeSwitcher = e.target.closest('.theme-switcher');
            if (!themeSwitcher && dotNetRef) {
                dotNetRef.invokeMethodAsync('CloseMenu');
            }
        });

        // Also close on escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && dotNetRef) {
                dotNetRef.invokeMethodAsync('CloseMenu');
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
        const current = this.getCurrentTheme();
        return this.themes[current]?.isDark ?? false;
    }
};

/**
 * Initialize theme immediately to prevent flash of wrong theme
 * This runs before Blazor initializes
 */
(function() {
    const storageKey = 'myblog-theme';
    const defaultTheme = 'light';

    let theme = localStorage.getItem(storageKey);

    if (!theme) {
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        theme = prefersDark ? 'dark' : 'light';
    }

    // Apply theme immediately (before page renders)
    document.documentElement.setAttribute('data-theme', theme);
})();

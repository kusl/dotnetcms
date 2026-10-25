window.themeManager = {
    storageKey: 'myblog-theme',
    defaultTheme: 'light',
    dotNetRef: null,

    themes: {
        'light': { isDark: false },
        'dark': { isDark: true },
        'sepia': { isDark: false },
        'nord': { isDark: true },
        'solarized-light': { isDark: false },
        'dracula': { isDark: true }
    },

    initialTheme: function() {
        var theme = localStorage.getItem(this.storageKey);

        if (!theme) {
            var prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            theme = prefersDark ? 'dark' : 'light';
        }

        return this.themes[theme] ? theme : this.defaultTheme;
    },

    init: function(dotNetRef) {
        this.dotNetRef = dotNetRef;

        var savedTheme = this.initialTheme();

        this.applyTheme(savedTheme);

        var self = this;
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function(e) {
            if (!localStorage.getItem(self.storageKey)) {
                var newTheme = e.matches ? 'dark' : 'light';
                self.setTheme(newTheme);
            }
        });

        return savedTheme;
    },

    applyTheme: function(themeId) {
        document.documentElement.setAttribute('data-theme', themeId);

        var isDark = this.themes[themeId] ? this.themes[themeId].isDark : false;
        this.updateMetaThemeColor(isDark);
    },

    setTheme: function(themeId) {
        if (!this.themes[themeId]) {
            console.warn('Theme "' + themeId + '" not found, using default');
            themeId = this.defaultTheme;
        }

        this.applyTheme(themeId);

        localStorage.setItem(this.storageKey, themeId);

        this.announceThemeChange(themeId);
    },

    updateMetaThemeColor: function(isDark) {
        var metaThemeColor = document.querySelector('meta[name="theme-color"]');

        if (!metaThemeColor) {
            metaThemeColor = document.createElement('meta');
            metaThemeColor.name = 'theme-color';
            document.head.appendChild(metaThemeColor);
        }

        metaThemeColor.content = isDark ? '#0f172a' : '#f8f9fa';
    },

    announceThemeChange: function(themeId) {
        var themeName = themeId.split('-').map(function(w) {
            return w.charAt(0).toUpperCase() + w.slice(1);
        }).join(' ');

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

        announcer.textContent = 'Theme changed to ' + themeName;
    },

    registerClickOutside: function(dotNetRef) {
        this.dotNetRef = dotNetRef;
        var self = this;

        document.addEventListener('click', function(e) {
            var themeSwitcher = e.target.closest('.theme-switcher');
            if (!themeSwitcher && self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('CloseMenu');
            }
        });

        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && self.dotNetRef) {
                self.dotNetRef.invokeMethodAsync('CloseMenu');
            }
        });
    }
};

function sharePost(title) {
    var url = window.location.href;
    var shareBtn = document.querySelector('.share-btn');

    if (typeof navigator.share === 'function' && window.isSecureContext) {
        navigator.share({
            title: title,
            text: title,
            url: url
        }).then(function() {
            showShareSuccess(shareBtn, 'Shared!');
        }).catch(function(err) {
            if (err.name !== 'AbortError') {
                console.warn('Share failed, falling back to clipboard:', err.message);
                copyToClipboard(url, shareBtn);
            }
        });
    } else {
        copyToClipboard(url, shareBtn);
    }
}

function copyToClipboard(url, shareBtn) {
    if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
        navigator.clipboard.writeText(url).then(function() {
            showShareSuccess(shareBtn, 'Link copied!');
        }).catch(function(err) {
            console.error('Clipboard API failed:', err);
            fallbackCopyToClipboard(url, shareBtn);
        });
    } else {
        fallbackCopyToClipboard(url, shareBtn);
    }
}

function fallbackCopyToClipboard(url, shareBtn) {
    var textArea = document.createElement('textarea');
    textArea.value = url;
    
    textArea.style.cssText = 'position:fixed;top:0;left:0;width:2em;height:2em;padding:0;border:none;outline:none;box-shadow:none;background:transparent;';
    
    document.body.appendChild(textArea);
    
    if (navigator.userAgent.match(/ipad|iphone/i)) {
        var range = document.createRange();
        range.selectNodeContents(textArea);
        var selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
        textArea.setSelectionRange(0, 999999);
    } else {
        textArea.focus();
        textArea.select();
    }

    var successful = false;
    try {
        successful = document.execCommand('copy');
    } catch (err) {
        console.error('execCommand copy failed:', err);
    }

    document.body.removeChild(textArea);

    if (successful) {
        showShareSuccess(shareBtn, 'Link copied!');
    } else {
        showShareError(shareBtn, 'Copy failed');
    }
}

function showShareSuccess(shareBtn, message) {
    if (!shareBtn) return;

    var originalHTML = shareBtn.innerHTML;
    shareBtn.classList.add('success');
    shareBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg> ' + message;

    setTimeout(function() {
        shareBtn.classList.remove('success');
        shareBtn.innerHTML = originalHTML;
    }, 2000);
}

function showShareError(shareBtn, message) {
    if (!shareBtn) return;

    var originalHTML = shareBtn.innerHTML;
    shareBtn.classList.add('error');
    shareBtn.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg> ' + message;

    setTimeout(function() {
        shareBtn.classList.remove('error');
        shareBtn.innerHTML = originalHTML;
    }, 2000);
}

window.themeManager.applyTheme(window.themeManager.initialTheme());

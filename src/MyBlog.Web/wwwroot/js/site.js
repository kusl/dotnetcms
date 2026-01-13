window.sharePost = async (title, text, url) => {
    if (navigator.share) {
        try {
            await navigator.share({
                title: title,
                text: text,
                url: url
            });
        } catch (err) {
            // User cancelled or share failed
            console.log('Share dismissed or failed', err);
        }
    } else {
        // Fallback for desktop browsers: Copy URL to clipboard
        try {
            await navigator.clipboard.writeText(url);
            // Optional: You could show a toast notification here instead of alert
            alert('Link copied to clipboard!');
        } catch (err) {
            console.error('Failed to copy to clipboard', err);
            prompt('Copy this link:', url);
        }
    }
};

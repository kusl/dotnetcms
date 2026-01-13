// Wait for the document to be ready
document.addEventListener('DOMContentLoaded', () => {

    // Attach a global click listener (Event Delegation)
    // This handles clicks for elements that exist now OR are added later by Blazor
    document.body.addEventListener('click', async (e) => {

        // Check if the clicked element (or its parent) has the 'js-share-btn' class
        const shareBtn = e.target.closest('.js-share-btn');

        // If not a share button, ignore
        if (!shareBtn) return;

        // Prevent default button behavior
        e.preventDefault();

        // Get data from data-attributes
        const shareData = {
            title: shareBtn.getAttribute('data-title'),
            text: shareBtn.getAttribute('data-text'),
            url: shareBtn.getAttribute('data-url') || window.location.href
        };

        // 1. Try Native Share (Mobile)
        if (navigator.share) {
            try {
                await navigator.share(shareData);
                console.log('Shared successfully');
            } catch (err) {
                // User cancelled or share failed
                console.log('Share dismissed', err);
            }
        }
        // 2. Fallback to Clipboard (Desktop / Non-HTTPS)
        else {
            try {
                await navigator.clipboard.writeText(shareData.url);
                alert('Link copied to clipboard!');
            } catch (err) {
                console.error('Clipboard failed', err);
                prompt('Copy this link:', shareData.url);
            }
        }
    });
});

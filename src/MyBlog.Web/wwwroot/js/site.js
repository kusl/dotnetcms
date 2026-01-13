window.sharePost = async (title) => {
    const url = window.location.href;

    // Try native share (mobile/supported browsers)
    if (navigator.share) {
        try {
            await navigator.share({
                title: title,
                url: url
            });
        } catch (err) {
            // User cancelled share, ignore
        }
    } else {
        // Fallback to clipboard
        try {
            await navigator.clipboard.writeText(url);

            // Visual feedback
            const btn = document.querySelector('.share-btn');
            if (btn) {
                const originalHtml = btn.innerHTML;
                btn.innerText = 'Copied!';
                btn.classList.add('success'); // Uses your existing success color var

                setTimeout(() => {
                    btn.innerHTML = originalHtml;
                    btn.classList.remove('success');
                }, 2000);
            } else {
                alert('Link copied to clipboard!');
            }
        } catch (err) {
            // Final fallback
            prompt('Copy this link:', url);
        }
    }
};

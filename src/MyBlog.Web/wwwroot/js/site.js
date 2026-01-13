function initShareButton() {
    const btn = document.querySelector('.js-share-btn');
    if (btn && !btn.getAttribute('data-bound')) {
        btn.setAttribute('data-bound', 'true'); // Prevent double-binding
        btn.addEventListener('click', async () => {
            const shareData = {
                title: btn.dataset.title,
                text: btn.dataset.text,
                url: btn.dataset.url
            };

            if (navigator.share) {
                try { await navigator.share(shareData); } catch (err) {}
            } else {
                await navigator.clipboard.writeText(shareData.url);
                alert("Link copied to clipboard!");
            }
        });
    }
}

// Initial load
initShareButton();

// Listen for Blazor's "Enhanced Navigation" updates
Blazor.addEventListener('enhancedload', initShareButton);

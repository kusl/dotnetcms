window.sharePost = async (title) => {
    const url = window.location.href;

    if (navigator.share) {
        try {
            await navigator.share({
                title: title,
                url: url
            });
        } catch (err) {
            // User cancelled share
        }
    } else {
        try {
            await navigator.clipboard.writeText(url);

            const btn = document.querySelector('.share-btn');
            if (btn) {
                const originalHtml = btn.innerHTML;
                btn.innerText = 'Copied!';
                btn.classList.add('success');

                setTimeout(() => {
                    btn.innerHTML = originalHtml;
                    btn.classList.remove('success');
                }, 2000);
            } else {
                alert('Link copied to clipboard!');
            }
        } catch (err) {
            prompt('Copy this link:', url);
        }
    }
};

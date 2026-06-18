// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', function () {
    const shell = document.querySelector('[data-app-shell]');
    const sidebarButtons = document.querySelectorAll('[data-app-sidebar-toggle]');
    const searchInput = document.querySelector('[data-live-search]');
    const searchResults = document.querySelector('[data-live-search-results]');
    let searchTimer;

    document.body.classList.toggle('app-dark', localStorage.getItem('app-theme') === 'dark');
    document.body.classList.toggle('admin-compact-tables', localStorage.getItem('app-density') === 'compact');

    if (shell && localStorage.getItem('app-sidebar') === 'collapsed') {
        shell.classList.add('is-collapsed');
    }

    sidebarButtons.forEach((button) => {
        button.addEventListener('click', () => {
            shell?.classList.toggle('is-collapsed');
            localStorage.setItem('app-sidebar', shell?.classList.contains('is-collapsed') ? 'collapsed' : 'expanded');
        });
    });

    const renderSearchResults = function (items) {
        if (!searchResults) {
            return;
        }

        if (!items.length) {
            searchResults.innerHTML = '<div class="app-search-empty">Không có kết quả phù hợp</div>';
            searchResults.hidden = false;
            return;
        }

        searchResults.innerHTML = items.map((item) => `
            <a href="${item.url}">
                <span>${item.type}</span>
                <strong>${item.title}</strong>
                <small>${item.detail || ''}</small>
            </a>
        `).join('');
        searchResults.hidden = false;
    };

    searchInput?.addEventListener('input', function () {
        const query = searchInput.value.trim();
        window.clearTimeout(searchTimer);

        if (query.length < 2) {
            if (searchResults) {
                searchResults.hidden = true;
                searchResults.innerHTML = '';
            }
            return;
        }

        searchTimer = window.setTimeout(async () => {
            try {
                const response = await fetch(`/Search/Live?q=${encodeURIComponent(query)}`, {
                    headers: { 'Accept': 'application/json' }
                });
                if (!response.ok) {
                    return;
                }
                renderSearchResults(await response.json());
            } catch {
                if (searchResults) {
                    searchResults.hidden = true;
                }
            }
        }, 180);
    });

    document.addEventListener('click', function (event) {
        if (!searchResults || !searchInput) {
            return;
        }

        if (!searchResults.contains(event.target) && event.target !== searchInput) {
            searchResults.hidden = true;
        }
    });
});

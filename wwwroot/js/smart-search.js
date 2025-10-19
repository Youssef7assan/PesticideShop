/**
 * Smart Search System - Modern & Intelligent
 * نظام بحث ذكي وحديث
 */

class SmartSearch {
    constructor() {
        this.searchInput = null;
        this.searchResults = null;
        this.cache = new Map();
        this.debounceTimer = null;
        this.isLoading = false;
        
        this.init();
    }

    init() {
        console.log('🚀 Initializing Smart Search System...');
        this.setupDOM();
        this.bindEvents();
        this.setupKeyboardShortcuts();
        console.log('✅ Smart Search System Ready!');
    }

    setupDOM() {
        // Find search input
        this.searchInput = document.getElementById('qrCodeInput');
        if (!this.searchInput) {
            console.error('❌ Search input not found!');
            return;
        }

        // Create results container if not exists
        this.searchResults = document.getElementById('searchResults');
        if (!this.searchResults) {
            this.createResultsContainer();
        }

        // Update placeholder
        this.searchInput.placeholder = '🔍 ابحث عن منتج (ID, اسم, باركود)...';
        this.searchInput.style.fontSize = '16px';
        this.searchInput.style.padding = '12px';
    }

    createResultsContainer() {
        const container = document.createElement('div');
        container.id = 'searchResults';
        container.className = 'smart-search-results';
        container.innerHTML = `
            <div id="searchLoading" class="search-loading" style="display: none;">
                <div class="spinner"></div>
                <span>جاري البحث...</span>
            </div>
            <div id="searchResultsList" class="search-results-list"></div>
        `;
        
        // Insert after search input
        this.searchInput.parentNode.insertBefore(container, this.searchInput.nextSibling);
        this.searchResults = container;
    }

    bindEvents() {
        // Real-time search with debounce
        this.searchInput.addEventListener('input', (e) => {
            this.handleInput(e.target.value);
        });

        // Handle keyboard navigation
        this.searchInput.addEventListener('keydown', (e) => {
            this.handleKeyboard(e);
        });

        // Handle focus
        this.searchInput.addEventListener('focus', () => {
            this.showResults();
        });

        // Handle blur (hide results after delay)
        this.searchInput.addEventListener('blur', () => {
            setTimeout(() => this.hideResults(), 200);
        });
    }

    setupKeyboardShortcuts() {
        // Global shortcuts
        document.addEventListener('keydown', (e) => {
            // Ctrl + / to focus search
            if (e.ctrlKey && e.key === '/') {
                e.preventDefault();
                this.focusSearch();
            }
            
            // Escape to clear search
            if (e.key === 'Escape' && document.activeElement === this.searchInput) {
                this.clearSearch();
            }
        });
    }

    handleInput(value) {
        const query = value.trim();
        
        // Clear previous timer
        if (this.debounceTimer) {
            clearTimeout(this.debounceTimer);
        }

        // If empty, show recent searches or hide
        if (!query) {
            this.showRecentSearches();
            return;
        }

        // Debounce search (300ms)
        this.debounceTimer = setTimeout(() => {
            this.performSearch(query);
        }, 300);
    }

    handleKeyboard(e) {
        const results = this.searchResults.querySelectorAll('.search-result-item');
        let currentSelected = this.searchResults.querySelector('.selected');
        let currentIndex = currentSelected ? 
            Array.from(results).indexOf(currentSelected) : -1;

        switch (e.key) {
            case 'Enter':
                e.preventDefault();
                if (currentSelected) {
                    this.selectResult(currentSelected);
                } else if (results.length > 0) {
                    this.selectResult(results[0]);
                } else {
                    // Direct search with entered value
                    this.directSearch(this.searchInput.value.trim());
                }
                break;

            case 'ArrowDown':
                e.preventDefault();
                currentIndex = (currentIndex + 1) % results.length;
                this.highlightResult(results[currentIndex]);
                break;

            case 'ArrowUp':
                e.preventDefault();
                currentIndex = currentIndex <= 0 ? results.length - 1 : currentIndex - 1;
                this.highlightResult(results[currentIndex]);
                break;

            case 'Tab':
                if (currentSelected) {
                    e.preventDefault();
                    this.selectResult(currentSelected);
                }
                break;
        }
    }

    async performSearch(query) {
        console.log(`🔍 Smart searching for: "${query}"`);
        
        // Check cache first
        if (this.cache.has(query)) {
            console.log('📦 Using cached results');
            this.displayResults(this.cache.get(query), query);
            return;
        }

        this.showLoading();

        try {
            const response = await fetch(`/Cashier/SearchProducts?term=${encodeURIComponent(query)}&includeOutOfStock=true&limit=10`);
            const result = await response.json();

            if (result.success) {
                // Cache results
                this.cache.set(query, result.products);
                
                // Limit cache size
                if (this.cache.size > 20) {
                    const firstKey = this.cache.keys().next().value;
                    this.cache.delete(firstKey);
                }

                this.displayResults(result.products, query);
                this.saveRecentSearch(query);
            } else {
                this.displayNoResults(query);
            }
        } catch (error) {
            console.error('❌ Search error:', error);
            this.displayError();
        } finally {
            this.hideLoading();
        }
    }

    displayResults(products, query) {
        const resultsList = document.getElementById('searchResultsList');
        
        if (!products || products.length === 0) {
            this.displayNoResults(query);
            return;
        }

        const html = products.map((product, index) => `
            <div class="search-result-item ${index === 0 ? 'selected' : ''}" 
                 data-product-id="${product.id}"
                 data-product-name="${this.escapeHtml(product.name)}"
                 data-product-price="${product.price}">
                
                <div class="product-info">
                    <div class="product-name">
                        <strong>${this.highlightMatch(product.name, query)}</strong>
                        <span class="stock-badge ${product.quantity > 0 ? 'in-stock' : 'out-of-stock'}">
                            ${product.quantity > 0 ? '✅ متوفر' : '❌ نفد'}
                        </span>
                    </div>
                    
                    <div class="product-details">
                        <span class="product-id">ID: ${product.id}</span>
                        ${product.qrCode ? `<span class="product-qr">📊 ${product.qrCode}</span>` : ''}
                        <span class="product-price">💰 ${product.price.toFixed(2)} ج.م</span>
                        <span class="product-quantity">📦 ${product.quantity} قطعة</span>
                    </div>
                </div>
                
                <div class="product-actions">
                    <button class="add-btn" onclick="smartSearch.addToCart('${product.id}', '${this.escapeHtml(product.name)}', ${product.price})">
                        <i class="fas fa-plus"></i>
                        إضافة
                    </button>
                </div>
            </div>
        `).join('');

        resultsList.innerHTML = html;
        this.showResults();
        
        // Add click handlers
        resultsList.querySelectorAll('.search-result-item').forEach(item => {
            item.addEventListener('click', () => this.selectResult(item));
        });
    }

    displayNoResults(query) {
        const resultsList = document.getElementById('searchResultsList');
        resultsList.innerHTML = `
            <div class="no-results">
                <div class="no-results-icon">🔍</div>
                <div class="no-results-text">
                    <strong>لا توجد نتائج لـ "${query}"</strong>
                    <p>جرب البحث بـ:</p>
                    <ul>
                        <li>رقم المنتج (ID)</li>
                        <li>اسم المنتج</li>
                        <li>رمز الباركود</li>
                    </ul>
                </div>
                <button class="try-again-btn" onclick="smartSearch.clearSearch()">
                    محاولة أخرى
                </button>
            </div>
        `;
        this.showResults();
    }

    displayError() {
        const resultsList = document.getElementById('searchResultsList');
        resultsList.innerHTML = `
            <div class="search-error">
                <div class="error-icon">⚠️</div>
                <div class="error-text">
                    <strong>حدث خطأ في البحث</strong>
                    <p>تحقق من الاتصال بالإنترنت وحاول مرة أخرى</p>
                </div>
                <button class="retry-btn" onclick="smartSearch.performSearch('${this.searchInput.value.trim()}')">
                    إعادة المحاولة
                </button>
            </div>
        `;
        this.showResults();
    }

    showRecentSearches() {
        const recent = this.getRecentSearches();
        if (recent.length === 0) return;

        const resultsList = document.getElementById('searchResultsList');
        const html = `
            <div class="recent-searches">
                <div class="recent-header">
                    <span>🕐 عمليات البحث الأخيرة</span>
                    <button onclick="smartSearch.clearRecentSearches()">مسح</button>
                </div>
                ${recent.map(term => `
                    <div class="recent-item" onclick="smartSearch.searchInput.value='${term}'; smartSearch.performSearch('${term}')">
                        <span>🔍 ${term}</span>
                    </div>
                `).join('')}
            </div>
        `;
        resultsList.innerHTML = html;
        this.showResults();
    }

    selectResult(resultItem) {
        const productId = resultItem.dataset.productId;
        const productName = resultItem.dataset.productName;
        const productPrice = parseFloat(resultItem.dataset.productPrice);

        this.addToCart(productId, productName, productPrice);
        this.clearSearch();
    }

    addToCart(productId, productName, productPrice) {
        console.log(`🛒 Adding to cart: ${productName}`);
        
        try {
            // Call the existing addProductToCart function
            if (typeof addProductToCart === 'function') {
                addProductToCart(productId, productName, productPrice);
            } else {
                console.error('❌ addProductToCart function not found');
                alert('❌ خطأ في إضافة المنتج');
                return;
            }

            // Show success message
            this.showSuccessMessage(`✅ تم إضافة ${productName} للعربة`);
            
            // Focus back to search for next item
            setTimeout(() => {
                this.focusSearch();
                this.clearSearch();
            }, 1000);

        } catch (error) {
            console.error('❌ Error adding to cart:', error);
            alert('❌ خطأ في إضافة المنتج للعربة');
        }
    }

    directSearch(query) {
        if (!query) return;
        
        console.log(`⚡ Direct search: "${query}"`);
        
        // If it looks like a number, try exact ID match first
        if (/^\d+$/.test(query)) {
            this.searchById(query);
        } else {
            this.performSearch(query);
        }
    }

    async searchById(id) {
        try {
            const response = await fetch(`/Cashier/SearchProducts?term=${id}&includeOutOfStock=true`);
            const result = await response.json();

            if (result.success && result.products && result.products.length > 0) {
                const product = result.products[0];
                this.addToCart(product.id, product.name, product.price);
            } else {
                this.performSearch(id);
            }
        } catch (error) {
            console.error('❌ Direct search error:', error);
            this.performSearch(id);
        }
    }

    // Utility functions
    highlightMatch(text, query) {
        if (!query) return text;
        const regex = new RegExp(`(${this.escapeRegex(query)})`, 'gi');
        return text.replace(regex, '<mark>$1</mark>');
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    escapeRegex(string) {
        return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    highlightResult(item) {
        // Remove previous selection
        this.searchResults.querySelectorAll('.selected').forEach(el => {
            el.classList.remove('selected');
        });
        
        // Add selection to current item
        if (item) {
            item.classList.add('selected');
            item.scrollIntoView({ block: 'nearest' });
        }
    }

    showResults() {
        this.searchResults.style.display = 'block';
    }

    hideResults() {
        this.searchResults.style.display = 'none';
    }

    showLoading() {
        this.isLoading = true;
        document.getElementById('searchLoading').style.display = 'block';
        document.getElementById('searchResultsList').style.display = 'none';
    }

    hideLoading() {
        this.isLoading = false;
        document.getElementById('searchLoading').style.display = 'none';
        document.getElementById('searchResultsList').style.display = 'block';
    }

    focusSearch() {
        this.searchInput.focus();
        this.searchInput.select();
    }

    clearSearch() {
        this.searchInput.value = '';
        this.hideResults();
        if (this.debounceTimer) {
            clearTimeout(this.debounceTimer);
        }
    }

    showSuccessMessage(message) {
        // Create temporary success message
        const toast = document.createElement('div');
        toast.className = 'success-toast';
        toast.textContent = message;
        toast.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: #28a745;
            color: white;
            padding: 12px 20px;
            border-radius: 8px;
            z-index: 10000;
            font-weight: bold;
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
            animation: slideInRight 0.3s ease;
        `;
        
        document.body.appendChild(toast);
        
        // Remove after 3 seconds
        setTimeout(() => {
            toast.style.animation = 'slideOutRight 0.3s ease';
            setTimeout(() => toast.remove(), 300);
        }, 3000);
    }

    // Recent searches management
    saveRecentSearch(query) {
        let recent = this.getRecentSearches();
        recent = recent.filter(item => item !== query); // Remove if exists
        recent.unshift(query); // Add to beginning
        recent = recent.slice(0, 5); // Keep only 5
        localStorage.setItem('smartSearchRecent', JSON.stringify(recent));
    }

    getRecentSearches() {
        try {
            return JSON.parse(localStorage.getItem('smartSearchRecent') || '[]');
        } catch {
            return [];
        }
    }

    clearRecentSearches() {
        localStorage.removeItem('smartSearchRecent');
        this.hideResults();
    }
}

// Initialize when DOM is ready
let smartSearch;
document.addEventListener('DOMContentLoaded', () => {
    smartSearch = new SmartSearch();
    
    // Make it globally available
    window.smartSearch = smartSearch;
    
    // Expose useful functions
    window.focusSearch = () => smartSearch.focusSearch();
    window.clearSearch = () => smartSearch.clearSearch();
    window.searchFor = (query) => smartSearch.performSearch(query);
});

console.log('📄 Smart Search System loaded successfully!');

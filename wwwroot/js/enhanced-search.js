/**
 * Simple Name Search System
 * نظام البحث البسيط بالاسم
 */

class SimpleNameSearch {
    constructor() {
        this.nameSearchInput = null;
        this.init();
    }

    init() {
        console.log('🚀 Initializing Simple Name Search...');
        this.setupNameSearch();
        console.log('✅ Simple Name Search Ready!');
    }

    setupNameSearch() {
        this.nameSearchInput = document.getElementById('productNameInput');
        if (!this.nameSearchInput) {
            console.error('❌ productNameInput not found!');
            return;
        }

        // Real-time search
        this.nameSearchInput.addEventListener('input', (e) => {
            this.handleNameSearch(e.target.value);
        });

        // Focus to show results
        this.nameSearchInput.addEventListener('focus', () => {
            const resultsContainer = document.getElementById('nameSearchResults');
            if (resultsContainer && resultsContainer.innerHTML.trim()) {
                resultsContainer.style.display = 'block';
            }
        });

        // Only hide results when clicking completely outside the search area
        document.addEventListener('click', (e) => {
            const resultsContainer = document.getElementById('nameSearchResults');
            const searchSection = document.querySelector('#nameSearchMode');
            
            // Only hide if clicking outside the entire search section
            if (resultsContainer && searchSection && !searchSection.contains(e.target)) {
                setTimeout(() => {
                    if (resultsContainer.style.display !== 'none') {
                        resultsContainer.style.display = 'none';
                    }
                }, 300);
            }
        });

        console.log('✅ Name search setup complete');
    }

    async handleNameSearch(value) {
        const query = value.trim();
        const resultsContainer = document.getElementById('nameSearchResults');
        
        if (!resultsContainer) {
            console.error('❌ nameSearchResults container not found!');
            return;
        }
        
        if (!query || query.length < 2) {
            resultsContainer.style.display = 'none';
            return;
        }

        try {
            resultsContainer.style.display = 'block';
            resultsContainer.innerHTML = '<div class="text-center p-3">جاري البحث...</div>';

            const response = await fetch(`/Cashier/SearchProducts?term=${encodeURIComponent(query)}&limit=10`);
            const data = await response.json();

            if (data.success && data.products && data.products.length > 0) {
                this.showSimpleResults(data.products, resultsContainer);
            } else {
                resultsContainer.innerHTML = '<div class="text-center p-3 text-muted">لا توجد نتائج</div>';
            }

        } catch (error) {
            console.error('❌ Search error:', error);
            resultsContainer.innerHTML = '<div class="text-center p-3 text-danger">حدث خطأ في البحث</div>';
        }
    }

    showSimpleResults(products, container) {
        const html = products.map(product => `
            <div class="simple-result-item" style="padding: 10px; border-bottom: 1px solid #eee; cursor: pointer;" 
                 onclick="addSimpleProductSafe(${product.id}, '${product.name.replace(/'/g, "\\'")}', ${product.price})"
                 onmousedown="event.stopPropagation()">
                <div style="font-weight: bold;">${product.name}</div>
                <div style="color: #666; font-size: 0.9em;">
                    السعر: ${product.price} ج.م | الكمية: ${product.quantity}
                </div>
            </div>
        `).join('');
        
        container.innerHTML = html;
    }

    clearNameSearch() {
        const nameInput = document.getElementById('productNameInput');
        const resultsContainer = document.getElementById('nameSearchResults');
        if (nameInput) nameInput.value = '';
        if (resultsContainer) resultsContainer.style.display = 'none';
    }
}

// Super safe product addition
window.addSimpleProductSafe = (productId, productName, productPrice) => {
    const now = Date.now();
    const addKey = `${productId}`;
    
    // Initialize tracking if not exists
    if (!window.productAddTracker) {
        window.productAddTracker = {};
    }
    
    // Check if this product was added recently (within 2 seconds)
    if (window.productAddTracker[addKey] && (now - window.productAddTracker[addKey]) < 2000) {
        console.log(`⚠️ Product ${productName} was added recently, ignoring duplicate`);
        return;
    }
    
    // Check global adding flag
    if (window.addingProduct) {
        console.log('⚠️ Already adding a product, please wait...');
        return;
    }
    
    // Set flags
    window.addingProduct = true;
    window.productAddTracker[addKey] = now;
    
    console.log(`🛒 Adding: ${productName}`);
    
    try {
        if (window.addProductToCart) {
            window.addProductToCart(productId, productName, productPrice);
            
            // Clear search immediately after successful add
            const nameInput = document.getElementById('productNameInput');
            const resultsContainer = document.getElementById('nameSearchResults');
            if (nameInput) nameInput.value = '';
            if (resultsContainer) resultsContainer.style.display = 'none';
        }
    } catch (error) {
        console.error('Error adding product:', error);
    } finally {
        // Reset global flag after 2 seconds
        setTimeout(() => {
            window.addingProduct = false;
        }, 2000);
    }
};

// Keep the old function for compatibility
window.addSimpleProduct = window.addSimpleProductSafe;

// Simple clear function
window.clearNameSearch = () => {
    const nameInput = document.getElementById('productNameInput');
    const resultsContainer = document.getElementById('nameSearchResults');
    if (nameInput) nameInput.value = '';
    if (resultsContainer) resultsContainer.style.display = 'none';
};

// Clean up old tracking data every 10 seconds
setInterval(() => {
    if (window.productAddTracker) {
        const now = Date.now();
        Object.keys(window.productAddTracker).forEach(key => {
            if ((now - window.productAddTracker[key]) > 10000) { // 10 seconds old
                delete window.productAddTracker[key];
            }
        });
    }
    if (window.cartAddTracker) {
        const now = Date.now();
        Object.keys(window.cartAddTracker).forEach(key => {
            if ((now - window.cartAddTracker[key]) > 5000) { // 5 seconds old
                delete window.cartAddTracker[key];
            }
        });
    }
}, 10000);

// Initialize - Disabled old name search
let simpleSearch;
document.addEventListener('DOMContentLoaded', () => {
    // Old name search is disabled - we now use category products display instead
    console.log('🚫 Name search disabled - using category products display instead');
});

console.log('📄 Simple Search loaded successfully!');

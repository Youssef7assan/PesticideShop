// ========================================
// نظام البحث المتقدم البسيط - Simple Advanced Search
// ========================================

// متغيرات النظام
let simpleAdvancedSearch = {
    isActive: false,
    currentMode: 'barcode',
    isProcessing: false,
    lastSearchTime: 0,
    searchCooldown: 500
};

// تهيئة النظام
function initSimpleAdvancedSearch() {
    console.log('🔧 تهيئة نظام البحث المتقدم البسيط...');
    
    // إعداد event listeners
    setupSimpleAdvancedSearchListeners();
    
    // تفعيل النظام
    simpleAdvancedSearch.isActive = true;
    
    console.log('✅ تم تهيئة نظام البحث المتقدم البسيط بنجاح');
}

// إعداد event listeners
function setupSimpleAdvancedSearchListeners() {
    const searchInput = document.getElementById('advancedSearchInput');
    if (!searchInput) {
        console.error('❌ لم يتم العثور على حقل البحث المتقدم');
        return;
    }
    
    // معالجة الإدخال
    searchInput.addEventListener('input', handleSimpleAdvancedSearchInput);
    
    // معالجة الضغط على المفاتيح
    searchInput.addEventListener('keydown', handleSimpleAdvancedSearchKeydown);
    
    // معالجة اللصق
    searchInput.addEventListener('paste', handleSimpleAdvancedSearchPaste);
    
    console.log('✅ تم إعداد Simple Advanced Search Event Listeners');
}

// معالجة الإدخال
function handleSimpleAdvancedSearchInput(e) {
    const value = e.target.value;
    updateSimpleAdvancedSearchStatus('typing', `جاري الكتابة... (${value.length} حرف)`);
}

// معالجة الضغط على المفاتيح
function handleSimpleAdvancedSearchKeydown(e) {
    // معالجة Enter
    if (e.key === 'Enter') {
        e.preventDefault();
        processSimpleAdvancedSearch(e.target.value.trim());
    }
    
    // معالجة Escape
    if (e.key === 'Escape') {
        clearSimpleAdvancedSearch();
    }
}

// معالجة اللصق
function handleSimpleAdvancedSearchPaste(e) {
    e.preventDefault();
    const pastedData = (e.clipboardData || window.clipboardData).getData('text');
    if (pastedData && pastedData.trim()) {
        processSimpleAdvancedSearch(pastedData.trim());
    }
}

// معالجة البحث
async function processSimpleAdvancedSearch(searchTerm) {
    console.log('🔍 بدء معالجة البحث:', searchTerm);
    
    if (!searchTerm || searchTerm.trim() === '') {
        console.log('❌ نص البحث فارغ');
        updateSimpleAdvancedSearchStatus('error', 'نص البحث فارغ');
        return;
    }
    
    // التحقق من التكرار
    const now = Date.now();
    if (now - simpleAdvancedSearch.lastSearchTime < simpleAdvancedSearch.searchCooldown) {
        console.log('❌ بحث سريع جداً');
        updateSimpleAdvancedSearchStatus('error', 'بحث سريع جداً');
        return;
    }
    
    // التحقق من المعالجة الجارية
    if (simpleAdvancedSearch.isProcessing) {
        console.log('❌ جاري المعالجة...');
        updateSimpleAdvancedSearchStatus('error', 'جاري المعالجة...');
        return;
    }
    
    // تنظيف النص
    const cleanSearchTerm = searchTerm.trim();
    console.log('🔍 النص المنظف:', cleanSearchTerm);
    
    if (cleanSearchTerm.length < 1) {
        console.log('❌ نص البحث غير صحيح');
        updateSimpleAdvancedSearchStatus('error', 'نص البحث غير صحيح');
        return;
    }
    
    // تحديث الوقت
    simpleAdvancedSearch.lastSearchTime = now;
    simpleAdvancedSearch.isProcessing = true;
    
    console.log('🔍 بدء البحث...');
    updateSimpleAdvancedSearchStatus('searching', 'جاري البحث...');
    
    try {
        // تنفيذ البحث
        console.log('🔍 تنفيذ البحث...');
        const results = await executeSimpleAdvancedSearch(cleanSearchTerm);
        console.log('🔍 نتائج البحث:', results);
        
        if (results.success && results.products && results.products.length > 0) {
            console.log('✅ تم العثور على نتائج:', results.products.length);
            // عرض النتائج
            displaySimpleAdvancedSearchResults(results.products);
            
            updateSimpleAdvancedSearchStatus('success', `✅ تم العثور على ${results.products.length} منتج`);
            
        } else {
            console.log('❌ لم يتم العثور على نتائج');
            updateSimpleAdvancedSearchStatus('error', `❌ لم يتم العثور على نتائج لـ: ${cleanSearchTerm}`);
        }
        
    } catch (error) {
        console.error('❌ خطأ في البحث:', error);
        updateSimpleAdvancedSearchStatus('error', 'خطأ في البحث');
        
    } finally {
        simpleAdvancedSearch.isProcessing = false;
        console.log('🔍 انتهى البحث');
    }
}

// تنفيذ البحث
async function executeSimpleAdvancedSearch(searchTerm) {
    try {
        console.log(`🔍 البحث عن: ${searchTerm}`);
        
        // جرب QR أولاً للأرقام والأحرف (باركود)
        if (/^[A-Za-z0-9\-_]+$/.test(searchTerm)) {
            console.log('🔍 باركود محتمل، جرب QR أولاً...');
            try {
                console.log('🔍 جرب QR أولاً...');
                const qrResponse = await fetch(`/Cashier/GetProductByQR?qrCode=${encodeURIComponent(searchTerm)}`);
                if (qrResponse.ok) {
                    const qrResult = await qrResponse.json();
                    console.log('🔍 نتيجة QR:', qrResult);
                    if (qrResult.success && qrResult.product) {
                        console.log('✅ تم العثور على المنتج في QR');
                        return {
                            success: true,
                            products: [qrResult.product]
                        };
                    }
                }
            } catch (qrError) {
                console.log('🔍 QR فشل، جرب البحث العادي...');
            }
        } else {
            console.log('🔍 نص عادي، جرب البحث العادي مباشرة...');
        }
        
        // البحث العادي
        console.log('🔍 البحث العادي...');
        const endpoint = '/Cashier/SearchProducts';
        const params = `term=${encodeURIComponent(searchTerm)}&includeOutOfStock=true`;
        const response = await fetch(`${endpoint}?${params}`);
        
        console.log(`🔍 Response status: ${response.status}`);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        const result = await response.json();
        console.log('🔍 نتيجة البحث:', result);
        
        if (result.success && result.products && result.products.length > 0) {
            return {
                success: true,
                products: result.products
            };
        } else {
            return {
                success: false,
                message: result.message || 'لم يتم العثور على نتائج'
            };
        }
        
    } catch (error) {
        console.error('❌ خطأ في تنفيذ البحث:', error);
        throw error;
    }
}

// عرض النتائج
function displaySimpleAdvancedSearchResults(products) {
    const resultsContainer = document.getElementById('advancedSearchResults');
    if (!resultsContainer) {
        console.error('❌ لم يتم العثور على حاوية النتائج');
        return;
    }
    
    // إظهار الحاوية
    resultsContainer.style.display = 'block';
    
    // مسح النتائج السابقة
    resultsContainer.innerHTML = '';
    
    // إضافة عنوان النتائج
    const resultsHeader = document.createElement('div');
    resultsHeader.className = 'search-results-header';
    resultsHeader.innerHTML = `
        <h5><i class="fas fa-search"></i> نتائج البحث المتقدم</h5>
        <p class="text-muted">تم العثور على ${products.length} منتج</p>
    `;
    resultsContainer.appendChild(resultsHeader);
    
    // إضافة المنتجات
    products.forEach((product, index) => {
        const productCard = createSimpleAdvancedSearchProductCard(product, index);
        resultsContainer.appendChild(productCard);
    });
}

// إنشاء بطاقة منتج
function createSimpleAdvancedSearchProductCard(product, index) {
    const card = document.createElement('div');
    card.className = 'product-card';
    card.setAttribute('data-product-id', product.id);
    
    // تسجيل بيانات المنتج للتشخيص
    console.log('🔍 بيانات المنتج:', {
        id: product.id,
        name: product.name,
        hasMultipleColors: product.hasMultipleColors,
        hasMultipleSizes: product.hasMultipleSizes,
        availableColors: product.availableColors,
        availableSizes: product.availableSizes
    });
    
    // إظهار اللون والمقاس المحددين في قاعدة البيانات
    let colorSizeOptions = '';
    
    console.log('🔍 فحص البيانات:', {
        color: product.color,
        size: product.size,
        hasColor: !!product.color,
        hasSize: !!product.size
    });
    
    // إظهار اللون والمقاس إذا كانا محددين في قاعدة البيانات
    if (product.color || product.size) {
        colorSizeOptions = `
            <div class="product-options">
                ${product.color ? `
                    <div class="color-options">
                        <label>اللون:</label>
                        <span class="selected-color">${product.color}</span>
                    </div>
                ` : ''}
                ${product.size ? `
                    <div class="size-options">
                        <label>المقاس:</label>
                        <span class="selected-size">${product.size}</span>
                    </div>
                ` : ''}
            </div>
        `;
    }
    
    console.log('✅ تم إنشاء خيارات اللون والمقاس');
    
    console.log('🔍 خيارات اللون والمقاس:', colorSizeOptions);
    
    card.innerHTML = `
        <div class="product-info">
            <div class="product-header">
                <h6 class="product-name">${product.name}</h6>
                <span class="product-price">${product.price} ج.م</span>
            </div>
            <div class="product-details">
                <span class="product-id">ID: ${product.id}</span>
                <span class="product-stock ${product.quantity > 0 ? 'in-stock' : 'out-of-stock'}">
                    ${product.quantity > 0 ? 'متوفر' : 'نفد المخزون'}
                </span>
            </div>
            ${colorSizeOptions}
        </div>
        <div class="product-actions">
            <button class="btn-add-to-cart" onclick="addProductToCartFromSimpleAdvancedSearch(${product.id}, '${product.name}', ${product.price})">
                <i class="fas fa-plus"></i> إضافة للسلة
            </button>
        </div>
    `;
    
    return card;
}

// إضافة منتج للسلة
function addProductToCartFromSimpleAdvancedSearch(productId, productName, productPrice) {
    try {
        // الحصول على اللون والمقاس المحددين
        const colorSelect = document.querySelector(`[data-product-id="${productId}"] .color-select`);
        const sizeSelect = document.querySelector(`[data-product-id="${productId}"] .size-select`);
        
        const selectedColor = colorSelect ? colorSelect.value : '';
        const selectedSize = sizeSelect ? sizeSelect.value : '';
        
        if (typeof addProductToCart === 'function') {
            addProductToCart(productId, productName, productPrice, selectedColor, selectedSize);
            console.log(`✅ تم إضافة: ${productName} - اللون: ${selectedColor} - المقاس: ${selectedSize}`);
        } else {
            throw new Error('دالة إضافة المنتج غير متاحة');
        }
    } catch (error) {
        console.error('❌ خطأ في إضافة المنتج:', error);
    }
}

// تحديث لون المنتج
function updateProductColor(productId, color) {
    console.log(`🎨 تحديث لون المنتج ${productId} إلى: ${color}`);
    // يمكن إضافة منطق إضافي هنا
}

// تحديث مقاس المنتج
function updateProductSize(productId, size) {
    console.log(`📏 تحديث مقاس المنتج ${productId} إلى: ${size}`);
    // يمكن إضافة منطق إضافي هنا
}

// تحديث الحالة
function updateSimpleAdvancedSearchStatus(status, message) {
    const statusElement = document.getElementById('advancedSearchStatus');
    if (!statusElement) return;
    
    const statusText = statusElement.querySelector('.status-text');
    if (statusText) {
        statusText.textContent = message;
    } else {
        statusElement.textContent = message;
    }
    
    // إزالة الكلاسات القديمة
    statusElement.classList.remove('status-ready', 'status-typing', 'status-searching', 'status-success', 'status-error');
    
    // إضافة الكلاس الجديد
    switch(status) {
        case 'ready':
            statusElement.classList.add('status-ready');
            statusElement.style.color = '#007bff';
            break;
        case 'typing':
            statusElement.classList.add('status-typing');
            statusElement.style.color = '#ffc107';
            break;
        case 'searching':
            statusElement.classList.add('status-searching');
            statusElement.style.color = '#17a2b8';
            break;
        case 'success':
            statusElement.classList.add('status-success');
            statusElement.style.color = '#28a745';
            break;
        case 'error':
            statusElement.classList.add('status-error');
            statusElement.style.color = '#dc3545';
            break;
        default:
            statusElement.style.color = '#6c757d';
    }
}

// مسح البحث
function clearSimpleAdvancedSearch() {
    const searchInput = document.getElementById('advancedSearchInput');
    const resultsContainer = document.getElementById('advancedSearchResults');
    
    if (searchInput) {
        searchInput.value = '';
        searchInput.focus();
    }
    
    if (resultsContainer) {
        resultsContainer.style.display = 'none';
        resultsContainer.innerHTML = '';
    }
    
    updateSimpleAdvancedSearchStatus('ready', 'جاهز للبحث');
}

// لصق من الحافظة
async function pasteFromClipboard() {
    try {
        console.log('📋 محاولة لصق من الحافظة...');
        const text = await navigator.clipboard.readText();
        console.log('📋 النص المنسوخ:', text);
        
        if (text && text.trim()) {
            console.log('📋 بدء البحث عن:', text.trim());
            await processSimpleAdvancedSearch(text.trim());
        } else {
            console.log('📋 الحافظة فارغة');
            updateSimpleAdvancedSearchStatus('error', 'الحافظة فارغة');
        }
    } catch (error) {
        console.error('❌ خطأ في قراءة الحافظة:', error);
        updateSimpleAdvancedSearchStatus('error', 'لا يمكن قراءة الحافظة');
    }
}

// عرض المنتجات حسب التصنيفات
async function showCategoryProducts() {
    console.log('📁 عرض المنتجات حسب التصنيفات...');
    
    try {
        updateSimpleAdvancedSearchStatus('loading', 'جاري تحميل المنتجات...');
        
        // إخفاء واجهة البحث
        const searchInterface = document.querySelector('.advanced-search-interface');
        if (searchInterface) {
            searchInterface.style.display = 'none';
        }
        
        // إظهار حاوية النتائج
        const resultsContainer = document.getElementById('advancedSearchResults');
        if (resultsContainer) {
            resultsContainer.style.display = 'block';
            resultsContainer.innerHTML = '<div class="text-center"><i class="fas fa-spinner fa-spin"></i> جاري تحميل المنتجات...</div>';
        }
        
        // تحميل المنتجات
        const response = await fetch('/Cashier/GetAllProductsByCategory');
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        const result = await response.json();
        
        if (result.success && result.productsByCategory) {
            displayCategoryProducts(result.productsByCategory);
            updateSimpleAdvancedSearchStatus('success', 'تم تحميل المنتجات بنجاح');
        } else {
            throw new Error(result.message || 'فشل في تحميل المنتجات');
        }
        
    } catch (error) {
        console.error('❌ خطأ في تحميل المنتجات:', error);
        updateSimpleAdvancedSearchStatus('error', 'خطأ في تحميل المنتجات');
        
        const resultsContainer = document.getElementById('advancedSearchResults');
        if (resultsContainer) {
            resultsContainer.innerHTML = `
                <div class="text-center text-danger">
                    <i class="fas fa-exclamation-triangle"></i>
                    <p>خطأ في تحميل المنتجات: ${error.message}</p>
                </div>
            `;
        }
    }
}

// عرض المنتجات حسب التصنيفات
function displayCategoryProducts(productsByCategory) {
    const resultsContainer = document.getElementById('advancedSearchResults');
    if (!resultsContainer) {
        console.error('❌ لم يتم العثور على حاوية النتائج');
        return;
    }
    
    // مسح النتائج السابقة
    resultsContainer.innerHTML = '';
    
    // إضافة عنوان النتائج
    const resultsHeader = document.createElement('div');
    resultsHeader.className = 'search-results-header';
    resultsHeader.innerHTML = `
        <h5><i class="fas fa-th-list"></i> المنتجات مصنفة حسب التصنيفات</h5>
        <p class="text-muted">جميع المنتجات المتوفرة مرتبة حسب الفئات</p>
        <button class="btn btn-outline-primary btn-sm" onclick="hideCategoryProducts()">
            <i class="fas fa-arrow-left"></i> العودة للبحث
        </button>
    `;
    resultsContainer.appendChild(resultsHeader);
    
    // إضافة المنتجات لكل تصنيف
    Object.keys(productsByCategory).forEach(categoryName => {
        const products = productsByCategory[categoryName];
        if (products && products.length > 0) {
            const categoryGroup = createCategoryGroup(categoryName, products);
            resultsContainer.appendChild(categoryGroup);
        }
    });
    
    // إضافة إحصائيات
    const stats = createCategoryStats(productsByCategory);
    resultsContainer.appendChild(stats);
}

// إنشاء مجموعة تصنيف
function createCategoryGroup(categoryName, products) {
    const group = document.createElement('div');
    group.className = 'category-group';
    group.innerHTML = `
        <div class="category-header">
            <h6 class="category-name">
                <i class="fas fa-folder"></i>
                ${categoryName}
                <span class="category-count">(${products.length} منتج)</span>
            </h6>
        </div>
        <div class="category-products">
            ${products.map(product => createCategoryProductItem(product)).join('')}
        </div>
    `;
    
    return group;
}

// إنشاء عنصر منتج في التصنيف
function createCategoryProductItem(product) {
    return `
        <div class="category-product-item">
            <div class="category-product-info">
                <h6 class="category-product-name">${product.name}</h6>
                <div class="category-product-details">
                    <span class="category-product-id">ID: ${product.id}</span>
                    <span class="category-product-stock ${product.quantity > 0 ? 'in-stock' : 'out-of-stock'}">
                        ${product.quantity > 0 ? 'متوفر' : 'نفد المخزون'}
                    </span>
                </div>
            </div>
            <div class="category-product-actions">
                <span class="category-product-price">${product.price} ج.م</span>
                <button class="btn-add-to-cart-category" onclick="addProductToCartFromCategory(${product.id}, '${product.name}', ${product.price})">
                    <i class="fas fa-plus"></i> إضافة
                </button>
            </div>
        </div>
    `;
}

// إنشاء إحصائيات التصنيفات
function createCategoryStats(productsByCategory) {
    const stats = document.createElement('div');
    stats.className = 'category-stats';
    
    const totalCategories = Object.keys(productsByCategory).length;
    const totalProducts = Object.values(productsByCategory).reduce((sum, products) => sum + products.length, 0);
    
    stats.innerHTML = `
        <div class="stat-item">
            <span class="stat-label">عدد التصنيفات:</span>
            <span class="stat-value">${totalCategories}</span>
        </div>
        <div class="stat-item">
            <span class="stat-label">إجمالي المنتجات:</span>
            <span class="stat-value">${totalProducts}</span>
        </div>
    `;
    
    return stats;
}

// إضافة منتج للسلة من التصنيف
function addProductToCartFromCategory(productId, productName, productPrice) {
    try {
        if (typeof addProductToCart === 'function') {
            addProductToCart(productId, productName, productPrice);
            console.log(`✅ تم إضافة: ${productName}`);
        } else {
            throw new Error('دالة إضافة المنتج غير متاحة');
        }
    } catch (error) {
        console.error('❌ خطأ في إضافة المنتج:', error);
    }
}

// إخفاء المنتجات حسب التصنيفات
function hideCategoryProducts() {
    const searchInterface = document.querySelector('.advanced-search-interface');
    const resultsContainer = document.getElementById('advancedSearchResults');
    
    if (searchInterface) {
        searchInterface.style.display = 'block';
    }
    
    if (resultsContainer) {
        resultsContainer.style.display = 'none';
        resultsContainer.innerHTML = '';
    }
    
    updateSimpleAdvancedSearchStatus('ready', 'جاهز للبحث');
}

// تفعيل النظام عند تحميل الصفحة
document.addEventListener('DOMContentLoaded', function() {
    setTimeout(() => {
        initSimpleAdvancedSearch();
    }, 1000);
});

console.log('🔍 تم تحميل نظام البحث المتقدم البسيط');

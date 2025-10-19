/**
 * ===== ملف JavaScript الرئيسي للنظام =====
 * الشركة المصرية للمبيدات والأسمدة
 * نظام إدارة الأعمال الزراعية
 */

// ===== متغيرات عامة =====
const APP_CONFIG = {
    primaryColor: '#28a745',
    successColor: '#00b894',
    warningColor: '#fdcb6e',
    dangerColor: '#e74c3c',
    infoColor: '#3498db',
    animationDuration: 300,
    apiTimeout: 10000
};

// Auto-hide alerts after 5 seconds
document.addEventListener('DOMContentLoaded', function() {
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(function(alert) {
        setTimeout(function() {
            if (alert.parentNode) {
                alert.style.transition = 'opacity 0.5s ease-out';
                alert.style.opacity = '0';
                setTimeout(function() {
                    if (alert.parentNode) {
                        alert.parentNode.removeChild(alert);
                    }
                }, 500);
            }
        }, 5000);
    });
});

// ===== إعدادات عامة =====
document.addEventListener('DOMContentLoaded', function() {
    // تم إلغاء نظام Heartbeat لتجنب الطلبات التلقائية كل 10 دقائق
    // Keep session alive - send heartbeat every 10 minutes
    // setInterval(function() {
    //     fetch('/api/heartbeat', {
    //         method: 'POST',
    //         headers: {
    //             'Content-Type': 'application/json',
    //             'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
    //         }
    //     }).catch(function(error) {
    //         console.log('Heartbeat failed:', error);
    //     });
    // }, 10 * 60 * 1000); // 10 minutes

    // Track user activity to keep session alive
    // let lastActivity = Date.now();
    // const activityEvents = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];
    
    // activityEvents.forEach(function(event) {
    //     document.addEventListener(event, function() {
    //         lastActivity = Date.now();
    //     }, true);
    // });

    // Check for inactivity every 5 minutes
    // setInterval(function() {
    //     const now = Date.now();
    //     const timeSinceLastActivity = now - lastActivity;
        
    //     // If user has been inactive for more than 25 minutes, send a heartbeat
    //     if (timeSinceLastActivity > 25 * 60 * 1000) {
    //         fetch('/api/heartbeat', {
    //             method: 'POST',
    //             headers: {
    //                 'Content-Type': 'application/json',
    //                 'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
    //             }
    //         }).catch(function(error) {
    //             console.log('Inactivity heartbeat failed:', error);
    //         });
    //     }
    // }, 5 * 60 * 1000); // 5 minutes
});

document.addEventListener('DOMContentLoaded', function() {
    // تأخير قصير لضمان تحميل Bootstrap
    setTimeout(() => {
        initializeApp();
        setupEventListeners();
        setupAnimations();
        setupFormValidation();
        setupDataTables();
        setupModals();
        setupSearchFunctionality();
        setupNotifications();
    }, 100);
});

// Initialize Bootstrap components and event handlers
function initializeBootstrapComponents() {
    // No dropdown initialization needed since we removed dropdowns
    console.log('✅ تم تحميل الناف بار بنجاح بدون قوائم منسدلة');
}

// Setup Bootstrap event listeners
function setupBootstrapEventListeners() {
    // No dropdown event listeners needed since we removed dropdowns
    console.log('✅ تم إعداد مستمعي الأحداث للناف بار');
}

// Initialize the application
function initializeApp() {
    // Wait for Bootstrap to load
    setTimeout(() => {
        initializeBootstrapComponents();
        setupBootstrapEventListeners();
        
        // Re-initialize after a short delay to ensure everything is loaded
        setTimeout(() => {
            initializeBootstrapComponents();
        }, 500);
    }, 100);
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    initializeApp();
});

// Re-initialize if Bootstrap loads after DOM
if (typeof bootstrap !== 'undefined') {
    initializeApp();
} else {
    // Wait for Bootstrap to load
    window.addEventListener('load', function() {
        setTimeout(initializeApp, 100);
    });
}

// ===== إعداد مستمعي الأحداث =====
function setupEventListeners() {
    // مستمعي الأحداث للنماذج
    document.addEventListener('submit', handleFormSubmit);
    
    // مستمعي الأحداث للأزرار
    document.addEventListener('click', handleButtonClick);
    
    // مستمعي الأحداث للروابط
    document.addEventListener('click', handleLinkClick);
    
    // مستمعي الأحداث للجداول
    document.addEventListener('click', handleTableClick);
    
    // مستمعي الأحداث للبحث
    document.addEventListener('input', handleSearchInput);
    
    // مستمعي الأحداث للطباعة
    document.addEventListener('keydown', handleKeyboardShortcuts);
    
    // مستمعي الأحداث للدروب داون والنافبار
    setupBootstrapEventListeners();
}

// ===== إعداد التحقق من النماذج =====
function setupFormValidation() {
    // إعداد التحقق من النماذج باستخدام Bootstrap
    if (typeof $ !== 'undefined') {
        // تحقق jQuery متوفر
        console.log('✅ jQuery loaded, form validation ready');
    } else {
        console.warn('⚠️ jQuery not loaded, form validation disabled');
    }
}

// ===== إعداد DataTables =====
function setupDataTables() {
    // إعداد جداول البيانات
    if (typeof $ !== 'undefined' && typeof $.fn.dataTable !== 'undefined') {
        console.log('✅ DataTables ready');
    } else {
        console.warn('⚠️ DataTables not loaded');
    }
}

// ===== إعداد النوافذ المنبثقة =====
function setupModals() {
    // إعداد النوافذ المنبثقة
    console.log('✅ Modals setup completed');
}

// ===== إعداد وظائف البحث =====
function setupSearchFunctionality() {
    // إعداد وظائف البحث
    console.log('✅ Search functionality setup completed');
}

// ===== إعداد الإشعارات =====
function setupNotifications() {
    // إعداد نظام الإشعارات
    console.log('✅ Notifications setup completed');
}

// ===== إعداد الحركات والتأثيرات =====
function setupAnimations() {
    // إعداد الحركات والتأثيرات
    console.log('✅ Animations setup completed');
}

// ===== إدارة النماذج =====
function handleFormSubmit(event) {
    const form = event.target;
    
    // التحقق من صحة النموذج
    if (!validateForm(form)) {
        event.preventDefault();
        showNotification('يرجى تصحيح الأخطاء في النموذج', 'error');
        return;
    }
    
    // إظهار مؤشر التحميل
    showLoadingIndicator();
    
    // إضافة تأخير صغير لتحسين تجربة المستخدم
    setTimeout(() => {
        hideLoadingIndicator();
    }, 500);
}

// ===== التحقق من صحة النماذج =====
function validateForm(form) {
    let isValid = true;
    const inputs = form.querySelectorAll('input, select, textarea');
    
    inputs.forEach(input => {
        if (input.hasAttribute('required') && !input.value.trim()) {
            markFieldAsInvalid(input, 'هذا الحقل مطلوب');
            isValid = false;
        } else if (input.type === 'email' && input.value && !isValidEmail(input.value)) {
            markFieldAsInvalid(input, 'يرجى إدخال بريد إلكتروني صحيح');
            isValid = false;
        } else if (input.type === 'tel' && input.value && !isValidPhone(input.value) && !window.location.pathname.includes('Cashier')) {
            markFieldAsInvalid(input, 'يرجى إدخال رقم هاتف صحيح');
            isValid = false;
        } else if (input.type === 'number' && input.value && !isValidNumber(input.value)) {
            markFieldAsInvalid(input, 'يرجى إدخال رقم صحيح');
            isValid = false;
        } else {
            markFieldAsValid(input);
        }
    });
    
    return isValid;
}

// ===== التحقق من صحة البريد الإلكتروني =====
function isValidEmail(email) {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
}

// ===== التحقق من صحة رقم الهاتف =====
function isValidPhone(phone) {
    // تحقق بسيط: 11 رقم فقط
    const phoneRegex = /^[0-9]{11}$/;
    return phoneRegex.test(phone);
}

// ===== التحقق من صحة الرقم =====
function isValidNumber(value) {
    return !isNaN(value) && value >= 0; // السماح بالصفر
}

// ===== تمييز الحقول غير الصحيحة =====
function markFieldAsInvalid(field, message) {
    field.classList.add('is-invalid');
    field.classList.remove('is-valid');
    
    // إزالة رسالة الخطأ السابقة
    const existingError = field.parentNode.querySelector('.invalid-feedback');
    if (existingError) {
        existingError.remove();
    }
    
    // إضافة رسالة الخطأ الجديدة
    const errorDiv = document.createElement('div');
    errorDiv.className = 'invalid-feedback';
    errorDiv.textContent = message;
    field.parentNode.appendChild(errorDiv);
}

// ===== تمييز الحقول الصحيحة =====
function markFieldAsValid(field) {
    field.classList.remove('is-invalid');
    field.classList.add('is-valid');
    
    // إزالة رسالة الخطأ
    const existingError = field.parentNode.querySelector('.invalid-feedback');
    if (existingError) {
        existingError.remove();
    }
}

// ===== إدارة الأزرار =====
function handleButtonClick(event) {
    const button = event.target.closest('button');
    if (!button) return;
    
    // تجاهل أزرار Bootstrap (dropdown و navbar-toggler)
    if (button.classList.contains('dropdown-toggle') || 
        button.classList.contains('navbar-toggler') ||
        button.hasAttribute('data-bs-toggle') ||
        button.hasAttribute('data-bs-target')) {
        return;
    }
    
    // تجاهل جميع أزرار الكاشير بشكل كامل
    if (window.location.pathname.includes('Cashier') || 
        button.closest('.cashier-container') ||
        button.id === 'searchCustomerBtn' || 
        button.id === 'scanQRBtn' ||
        button.id === 'processTransactionBtn' ||
        button.id === 'clearCartBtn' ||
        button.onclick) {
        return; // لا تتدخل في أزرار الكاشير أبداً
    }
    
    // أزرار الطباعة
    if (button.classList.contains('print') || button.textContent.includes('🖨️')) {
        event.preventDefault();
        handlePrintAction(button);
    }
    
    // أزرار التصدير
    if (button.classList.contains('export') || button.textContent.includes('📄')) {
        event.preventDefault();
        handleExportAction(button);
    }
    
    // أزرار النسخ
    if (button.classList.contains('copy') || button.textContent.includes('📋')) {
        event.preventDefault();
        handleCopyAction(button);
    }
}

// ===== إدارة الطباعة =====
function handlePrintAction(button) {
    const printArea = button.getAttribute('data-print-area') || 'body';
    printElement(printArea);
}

// ===== إدارة التصدير =====
function handleExportAction(button) {
    const exportType = button.getAttribute('data-export-type') || 'pdf';
    const tableId = button.getAttribute('data-table-id');
    
    if (exportType === 'pdf') {
        exportToPDF(tableId);
    } else if (exportType === 'excel') {
        exportToExcel(tableId);
    }
}

// ===== إدارة النسخ =====
function handleCopyAction(button) {
    const textToCopy = button.getAttribute('data-copy-text') || button.textContent;
    
    navigator.clipboard.writeText(textToCopy).then(() => {
        showNotification('تم نسخ النص بنجاح', 'success');
    }).catch(() => {
        showNotification('فشل في نسخ النص', 'error');
    });
}

// ===== إدارة الروابط =====
function handleLinkClick(event) {
    const link = event.target.closest('a');
    if (!link) return;
    
    // روابط خارجية
    if (link.hostname !== window.location.hostname) {
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
    }
    
    // روابط التنزيل
    if (link.download) {
        showNotification('جاري تحميل الملف...', 'info');
    }
}

// ===== إدارة الجداول =====
function handleTableClick(event) {
    const table = event.target.closest('table');
    if (!table) return;
    
    // تحديد الصفوف
    const row = event.target.closest('tr');
    if (row && !row.classList.contains('header-row')) {
        toggleRowSelection(row);
    }
}

// ===== تبديل تحديد الصف =====
function toggleRowSelection(row) {
    if (row.classList.contains('selected')) {
        row.classList.remove('selected');
    } else {
        row.classList.add('selected');
    }
}

// ===== إدارة البحث =====
function handleSearchInput(event) {
    const input = event.target;
    const searchTerm = input.value.toLowerCase();
    const tableId = input.getAttribute('data-table-id');
    
    if (tableId) {
        filterTable(tableId, searchTerm);
    }
}

// ===== تصفية الجداول =====
function filterTable(tableId, searchTerm) {
    const table = document.getElementById(tableId);
    if (!table) return;
    
    const rows = table.querySelectorAll('tbody tr');
    
    rows.forEach(row => {
        const text = row.textContent.toLowerCase();
        if (text.includes(searchTerm)) {
            row.style.display = '';
        } else {
            row.style.display = 'none';
        }
    });
    
    // إظهار عدد النتائج
    const visibleRows = Array.from(rows).filter(row => row.style.display !== 'none');
    updateSearchResults(visibleRows.length, rows.length);
}

// ===== تحديث نتائج البحث =====
function updateSearchResults(visible, total) {
    const resultsElement = document.querySelector('.search-results');
    if (resultsElement) {
        resultsElement.textContent = `عرض ${visible} من ${total} نتيجة`;
    }
}

// ===== إعداد النافبار النشط =====
function setActiveNavbarItem() {
    const currentPath = window.location.pathname;
    const navLinks = document.querySelectorAll('.navbar-nav .nav-link');
    
    navLinks.forEach(link => {
        const href = link.getAttribute('href');
        if (href && currentPath.includes(href.split('/')[1])) {
            link.classList.add('active');
        }
    });
}

// ===== إعداد التصميم المتجاوب =====
function setupResponsiveDesign() {
    const resizeObserver = new ResizeObserver(entries => {
        entries.forEach(entry => {
            const width = entry.contentRect.width;
            
            if (width < 768) {
                document.body.classList.add('mobile-view');
            } else {
                document.body.classList.remove('mobile-view');
            }
        });
    });
    
    resizeObserver.observe(document.body);
}

// ===== إعداد الطباعة =====
function setupPrintFunctionality() {
    // إضافة أزرار الطباعة للجداول
    const tables = document.querySelectorAll('.data-table');
    tables.forEach(table => {
        if (!table.querySelector('.print-button')) {
            addPrintButton(table);
        }
    });
}

// ===== إضافة زر الطباعة =====
function addPrintButton(table) {
    const button = document.createElement('button');
    button.className = 'btn btn-outline-secondary btn-sm print-button';
    button.innerHTML = '🖨️ طباعة';
    button.onclick = () => printElement(table);
    
    const container = table.parentElement;
    if (container) {
        container.insertBefore(button, table);
    }
}

// ===== طباعة العنصر =====
function printElement(element) {
    const printWindow = window.open('', '_blank');
    const elementToPrint = typeof element === 'string' ? document.querySelector(element) : element;
    
    if (elementToPrint) {
        printWindow.document.write(`
            <html dir="rtl">
            <head>
                <title>طباعة - الشركة المصرية للمبيدات والأسمدة</title>
                <style>
                    body { font-family: 'Cairo', sans-serif; direction: rtl; }
                    table { width: 100%; border-collapse: collapse; }
                    th, td { border: 1px solid #ddd; padding: 8px; text-align: right; }
                    th { background-color: #28a745; color: white; }
                    .print-header { text-align: center; margin-bottom: 20px; }
                    @media print { body { margin: 0; } }
                </style>
            </head>
            <body>
                <div class="print-header">
                    <h2>الشركة المصرية للمبيدات والأسمدة</h2>
                    <p>تاريخ الطباعة: ${new Date().toLocaleDateString('ar-EG')}</p>
                </div>
                ${elementToPrint.outerHTML}
            </body>
            </html>
        `);
        
        printWindow.document.close();
        printWindow.print();
    }
}

// ===== إعداد الموديلات =====
function setupModals() {
    // إغلاق الموديل عند النقر خارجه
    document.addEventListener('click', event => {
        if (event.target.classList.contains('modal')) {
            closeModal(event.target.id);
        }
    });
    
    // إغلاق الموديل بالضغط على ESC
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
            closeAllModals();
        }
    });
}

// ===== إغلاق الموديل =====
function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.style.display = 'none';
    }
}

// ===== إغلاق جميع الموديلات =====
function closeAllModals() {
    const modals = document.querySelectorAll('.modal');
    modals.forEach(modal => {
        modal.style.display = 'none';
    });
}

// ===== إعداد الإشعارات =====
function setupNotifications() {
    // إنشاء حاوية الإشعارات
    const notificationContainer = document.createElement('div');
    notificationContainer.id = 'notification-container';
    notificationContainer.className = 'notification-container';
    document.body.appendChild(notificationContainer);
}

// ===== إظهار الإشعار =====
function showNotification(message, type = 'info', duration = 5000) {
    const notification = document.createElement('div');
    notification.className = `notification notification-${type}`;
    notification.innerHTML = `
        <div class="notification-content">
            <span class="notification-message">${message}</span>
            <button class="notification-close" onclick="this.parentElement.parentElement.remove()">×</button>
        </div>
    `;
    
    const container = document.getElementById('notification-container');
    container.appendChild(notification);
    
    // إظهار الإشعار
    setTimeout(() => {
        notification.classList.add('show');
    }, 100);
    
    // إخفاء الإشعار تلقائياً
    if (duration > 0) {
        setTimeout(() => {
            hideNotification(notification);
        }, duration);
    }
}

// ===== إخفاء الإشعار =====
function hideNotification(notification) {
    notification.classList.remove('show');
    setTimeout(() => {
        if (notification.parentElement) {
            notification.parentElement.removeChild(notification);
        }
    }, 300);
}

// ===== إضافة مؤشر التحميل =====
function addLoadingIndicator() {
    const loading = document.createElement('div');
    loading.id = 'loading-indicator';
    loading.className = 'loading-indicator';
    loading.innerHTML = `
        <div class="loading-spinner">
            <div class="spinner"></div>
            <p>جاري التحميل...</p>
        </div>
    `;
    document.body.appendChild(loading);
}

// ===== إظهار مؤشر التحميل =====
function showLoadingIndicator() {
    const loading = document.getElementById('loading-indicator');
    if (loading) {
        loading.style.display = 'flex';
    }
}

// ===== إخفاء مؤشر التحميل =====
function hideLoadingIndicator() {
    const loading = document.getElementById('loading-indicator');
    if (loading) {
        loading.style.display = 'none';
    }
}

// ===== إعداد الرسوم المتحركة =====
function setupAnimations() {
    // إضافة تأثيرات التمرير
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    };
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('animate-in');
            }
        });
    }, observerOptions);
    
    // مراقبة العناصر للرسوم المتحركة
    const animatedElements = document.querySelectorAll('.card, .dashboard-card, .data-table');
    animatedElements.forEach(el => {
        observer.observe(el);
    });
}

// ===== إعداد الجداول =====
function setupDataTables() {
    const tables = document.querySelectorAll('.data-table');
    
    tables.forEach(table => {
        // إضافة ترتيب للجداول
        addTableSorting(table);
        
        // إضافة تصفية للجداول
        // addTableFiltering(table); // تم تعطيلها لتجنب التعارض مع البحث المخصص
        
        // إضافة ترقيم الصفحات
        // addTablePagination(table); // تم تعطيلها - نستخدم الترقيم المخصص في كل صفحة
        
        // إضافة التمرير للجداول
        setupTableScrolling(table);
    });
}

// ===== إضافة ترتيب للجداول =====
function addTableSorting(table) {
    const headers = table.querySelectorAll('th[data-sortable]');
    
    headers.forEach(header => {
        header.style.cursor = 'pointer';
        header.addEventListener('click', () => {
            sortTable(table, header);
        });
    });
}

// ===== ترتيب الجدول =====
function sortTable(table, header) {
    const column = Array.from(header.parentElement.children).indexOf(header);
    const tbody = table.querySelector('tbody');
    const rows = Array.from(tbody.querySelectorAll('tr'));
    
    const isAscending = header.classList.contains('sort-asc');
    
    rows.sort((a, b) => {
        const aValue = a.children[column].textContent.trim();
        const bValue = b.children[column].textContent.trim();
        
        if (isAscending) {
            return bValue.localeCompare(aValue, 'ar');
        } else {
            return aValue.localeCompare(bValue, 'ar');
        }
    });
    
    // إعادة ترتيب الصفوف
    rows.forEach(row => tbody.appendChild(row));
    
    // تحديث حالة الترتيب
    table.querySelectorAll('th').forEach(th => {
        th.classList.remove('sort-asc', 'sort-desc');
    });
    
    header.classList.add(isAscending ? 'sort-desc' : 'sort-asc');
}

// ===== إضافة تصفية للجداول =====
function addTableFiltering(table) {
    const filterInput = document.createElement('input');
    filterInput.type = 'text';
    filterInput.placeholder = '🔍 بحث في الجدول...';
    filterInput.className = 'table-filter';
    
    filterInput.addEventListener('input', (e) => {
        const searchTerm = e.target.value.toLowerCase();
        const rows = table.querySelectorAll('tbody tr');
        
        rows.forEach(row => {
            const text = row.textContent.toLowerCase();
            row.style.display = text.includes(searchTerm) ? '' : 'none';
        });
    });
    
    table.parentElement.insertBefore(filterInput, table);
}

// ===== إضافة ترقيم الصفحات =====
function addTablePagination(table) {
    const rowsPerPage = 10;
    const rows = table.querySelectorAll('tbody tr');
    const totalPages = Math.ceil(rows.length / rowsPerPage);
    
    if (totalPages > 1) {
        const pagination = createPagination(totalPages, (page) => {
            showTablePage(table, page, rowsPerPage);
        });
        
        table.parentElement.appendChild(pagination);
        showTablePage(table, 1, rowsPerPage);
    }
}

// ===== إنشاء ترقيم الصفحات =====
function createPagination(totalPages, onPageChange) {
    const pagination = document.createElement('div');
    pagination.className = 'pagination-container';
    
    for (let i = 1; i <= totalPages; i++) {
        const button = document.createElement('button');
        button.textContent = i;
        button.className = 'pagination-btn';
        button.onclick = () => onPageChange(i);
        pagination.appendChild(button);
    }
    
    return pagination;
}

// ===== إظهار صفحة الجدول =====
function showTablePage(table, page, rowsPerPage) {
    const rows = table.querySelectorAll('tbody tr');
    const start = (page - 1) * rowsPerPage;
    const end = start + rowsPerPage;
    
    rows.forEach((row, index) => {
        row.style.display = (index >= start && index < end) ? '' : 'none';
    });
    
    // تحديث الأزرار النشطة
    const buttons = table.parentElement.querySelectorAll('.pagination-btn');
    buttons.forEach((btn, index) => {
        btn.classList.toggle('active', index + 1 === page);
    });
}

// ===== إعداد البحث =====
function setupSearchFunctionality() {
    const searchInputs = document.querySelectorAll('.search-input');
    
    searchInputs.forEach(input => {
        // إضافة تأخير للبحث
        let timeout;
        input.addEventListener('input', (e) => {
            clearTimeout(timeout);
            timeout = setTimeout(() => {
                performSearch(e.target.value, input.getAttribute('data-search-type'));
            }, 300);
        });
    });
}

// ===== تنفيذ البحث =====
function performSearch(term, type) {
    if (!term.trim()) {
        clearSearchResults();
        return;
    }
    
    showLoadingIndicator();
    
    // محاكاة البحث (يمكن استبدالها بطلب API حقيقي)
    setTimeout(() => {
        hideLoadingIndicator();
        showSearchResults(term, type);
    }, 500);
}

// ===== إظهار نتائج البحث =====
function showSearchResults(term, type) {
    // تنفيذ البحث حسب النوع
    switch (type) {
        case 'products':
            searchProducts(term);
            break;

        case 'customers':
            searchCustomers(term);
            break;
        default:
            searchAll(term);
    }
}

// ===== البحث في المنتجات =====
function searchProducts(term) {
    const productRows = document.querySelectorAll('.data-table tbody tr');
    filterTableRows(productRows, term);
}



// ===== البحث في العملاء =====
function searchCustomers(term) {
    const customerRows = document.querySelectorAll('.data-table tbody tr');
    filterTableRows(customerRows, term);
}

// ===== البحث الشامل =====
function searchAll(term) {
    const allRows = document.querySelectorAll('.data-table tbody tr');
    filterTableRows(allRows, term);
}

// ===== تصفية صفوف الجدول =====
function filterTableRows(rows, term) {
    const searchTerm = term.toLowerCase();
    let visibleCount = 0;
    
    rows.forEach(row => {
        const text = row.textContent.toLowerCase();
        if (text.includes(searchTerm)) {
            row.style.display = '';
            visibleCount++;
        } else {
            row.style.display = 'none';
        }
    });
    
    updateSearchResults(visibleCount, rows.length);
}

// ===== مسح نتائج البحث =====
function clearSearchResults() {
    const rows = document.querySelectorAll('.data-table tbody tr');
    rows.forEach(row => {
        row.style.display = '';
    });
    
    updateSearchResults(rows.length, rows.length);
}

// ===== إدارة اختصارات لوحة المفاتيح =====
function handleKeyboardShortcuts(event) {
    // Ctrl/Cmd + P للطباعة
    if ((event.ctrlKey || event.metaKey) && event.key === 'p') {
        event.preventDefault();
        printElement('body');
    }
    
    // Ctrl/Cmd + F للبحث
    if ((event.ctrlKey || event.metaKey) && event.key === 'f') {
        event.preventDefault();
        focusSearchInput();
    }
    
    // ESC لإغلاق الموديلات
    if (event.key === 'Escape') {
        closeAllModals();
    }
}

// ===== تركيز حقل البحث =====
function focusSearchInput() {
    const searchInput = document.querySelector('.search-input');
    if (searchInput) {
        searchInput.focus();
        searchInput.select();
    }
}

// ===== تصدير إلى PDF =====
function exportToPDF(tableId) {
    showNotification('جاري تحضير ملف PDF...', 'info');
    
    // هنا يمكن إضافة مكتبة لتصدير PDF مثل jsPDF
    setTimeout(() => {
        showNotification('تم تصدير الملف بنجاح', 'success');
    }, 2000);
}

// ===== تصدير إلى Excel =====
function exportToExcel(tableId) {
    showNotification('جاري تحضير ملف Excel...', 'info');
    
    // هنا يمكن إضافة مكتبة لتصدير Excel
    setTimeout(() => {
        showNotification('تم تصدير الملف بنجاح', 'success');
    }, 2000);
}

// ===== دوال مساعدة =====

// تنسيق الأرقام
function formatNumber(number) {
    return new Intl.NumberFormat('ar-EG').format(number);
}

// تنسيق التاريخ
function formatDate(date) {
    return new Intl.DateTimeFormat('ar-EG').format(new Date(date));
}

// تنسيق العملة
function formatCurrency(amount) {
    return new Intl.NumberFormat('ar-EG', {
        style: 'currency',
        currency: 'EGP'
    }).format(amount);
}

// تحويل النص إلى عنوان URL آمن
function slugify(text) {
    return text
        .toString()
        .toLowerCase()
        .replace(/\s+/g, '-')
        .replace(/[^\w\-]+/g, '')
        .replace(/\-\-+/g, '-')
        .replace(/^-+/, '')
        .replace(/-+$/, '');
}

// التحقق من وجود العنصر في المصفوفة
function arrayContains(array, item) {
    return array.indexOf(item) !== -1;
}

// إزالة العنصر من المصفوفة
function arrayRemove(array, item) {
    const index = array.indexOf(item);
    if (index > -1) {
        array.splice(index, 1);
    }
    return array;
}

// ===== دوال عامة للاستخدام في الصفحات =====

// إغلاق موديل
function closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
        modal.style.display = 'none';
    }
}

// إظهار رسالة نجاح
function showSuccess(message) {
    showNotification(message, 'success');
}

// إظهار رسالة خطأ
function showError(message) {
    showNotification(message, 'error');
}

// إظهار رسالة تحذير
function showWarning(message) {
    showNotification(message, 'warning');
}

// إظهار رسالة معلومات
function showInfo(message) {
    showNotification(message, 'info');
}

// ===== إعداد التمرير للجداول =====
function setupTableScrolling(table) {
    const container = table.closest('.table-container');
    if (!container) return;
    
    // Add scroll event listener
    container.addEventListener('scroll', function() {
        updateScrollIndicators(container);
    });
    
    // Add touch event listeners for mobile
    let startX = 0;
    let scrollLeft = 0;
    
    container.addEventListener('touchstart', function(e) {
        startX = e.touches[0].pageX - container.offsetLeft;
        scrollLeft = container.scrollLeft;
    });
    
    container.addEventListener('touchmove', function(e) {
        if (!startX) return;
        e.preventDefault();
        const x = e.touches[0].pageX - container.offsetLeft;
        const walk = (x - startX) * 2;
        container.scrollLeft = scrollLeft - walk;
    });
    
    container.addEventListener('touchend', function() {
        startX = 0;
    });
    
    // Initialize scroll indicators
    updateScrollIndicators(container);
}

// ===== تحديث مؤشرات التمرير =====
function updateScrollIndicators(container) {
    const scrollLeft = container.scrollLeft;
    const scrollWidth = container.scrollWidth;
    const clientWidth = container.clientWidth;
    
    // Remove existing classes
    container.classList.remove('scroll-left', 'scroll-right');
    
    // Add appropriate classes
    if (scrollLeft > 0) {
        container.classList.add('scroll-left');
    }
    
    if (scrollLeft < scrollWidth - clientWidth - 1) {
        container.classList.add('scroll-right');
    }
}

// ===== تصدير الدوال للاستخدام العام =====
window.PesticideShop = {
    showNotification,
    closeModal,
    formatNumber,
    formatDate,
    formatCurrency,
    showSuccess,
    showError,
    showWarning,
    showInfo,
    setupTableScrolling,
    updateScrollIndicators
};

// ===== JavaScript لصفحة تفاصيل العميل =====
function initializeCustomersDetailsPage() {
    try {
        // Initialize table sorting
        initializeTableSorting();
        
        // Initialize search functionality
        initializeSearch();
        
        // Initialize filter functionality
        initializeFilters();
        
        // Initialize sort dropdown
        initializeSortDropdown();
        
        // Hide any JavaScript code that might be displayed
        hideJavaScriptCode();
    } catch (error) {
        console.error('Error initializing customers details page:', error);
    }
}

function hideJavaScriptCode() {
    try {
        // Hide any script tags that might be visible
        const scripts = document.querySelectorAll('script');
        scripts.forEach(script => {
            script.style.display = 'none';
        });
        
        // Hide any elements with JavaScript code
        const jsElements = document.querySelectorAll('.js-code-display, [class*="js-"], [id*="js-"]');
        jsElements.forEach(element => {
            element.style.display = 'none';
        });
    } catch (error) {
        console.error('Error hiding JavaScript code:', error);
    }
}

function initializeTableSorting() {
    try {
        const headers = document.querySelectorAll('th.sortable');
        headers.forEach(header => {
            header.addEventListener('click', function() {
                const sortField = this.dataset.sort;
                const isAscending = !this.classList.contains('sort-asc');
                
                // Remove existing sort classes
                headers.forEach(h => h.classList.remove('sort-asc', 'sort-desc'));
                
                // Add sort class
                this.classList.add(isAscending ? 'sort-asc' : 'sort-desc');
                
                // Sort table
                sortTable(sortField, isAscending);
            });
        });
    } catch (error) {
        console.error('Error initializing table sorting:', error);
    }
}

function sortTable(field, ascending) {
    try {
        const table = document.getElementById('transactionsTable');
        if (!table) return;
        
        const tbody = table.querySelector('tbody');
        const rows = Array.from(tbody.querySelectorAll('tr'));
        
        rows.sort((a, b) => {
            let aValue, bValue;
            
            switch(field) {
                case 'date':
                    aValue = parseInt(a.querySelector(`td[data-date]`)?.dataset.date || 0);
                    bValue = parseInt(b.querySelector(`td[data-date]`)?.dataset.date || 0);
                    break;
                case 'product':
                    aValue = a.querySelector('td:nth-child(2)')?.textContent.trim() || '';
                    bValue = b.querySelector('td:nth-child(2)')?.textContent.trim() || '';
                    break;
                case 'quantity':
                    aValue = parseFloat(a.querySelector(`td[data-quantity]`)?.dataset.quantity || 0);
                    bValue = parseFloat(b.querySelector(`td[data-quantity]`)?.dataset.quantity || 0);
                    break;
                case 'price':
                    aValue = parseFloat(a.querySelector(`td[data-price]`)?.dataset.price || 0);
                    bValue = parseFloat(b.querySelector(`td[data-price]`)?.dataset.price || 0);
                    break;
                case 'discount':
                    aValue = parseFloat(a.querySelector(`td[data-discount]`)?.dataset.discount || 0);
                    bValue = parseFloat(b.querySelector(`td[data-discount]`)?.dataset.discount || 0);
                    break;
                case 'total':
                    aValue = parseFloat(a.querySelector(`td[data-total]`)?.dataset.total || 0);
                    bValue = parseFloat(b.querySelector(`td[data-total]`)?.dataset.total || 0);
                    break;
                case 'paid':
                    aValue = parseFloat(a.querySelector(`td[data-paid]`)?.dataset.paid || 0);
                    bValue = parseFloat(b.querySelector(`td[data-paid]`)?.dataset.paid || 0);
                    break;
                case 'remaining':
                    aValue = parseFloat(a.querySelector(`td[data-remaining]`)?.dataset.remaining || 0);
                    bValue = parseFloat(b.querySelector(`td[data-remaining]`)?.dataset.remaining || 0);
                    break;
                default:
                    return 0;
            }
            
            if (typeof aValue === 'string') {
                return ascending ? aValue.localeCompare(bValue, 'ar') : bValue.localeCompare(aValue, 'ar');
            } else {
                return ascending ? aValue - bValue : bValue - aValue;
            }
        });
        
        // Reorder rows with animation
        rows.forEach((row, index) => {
            row.style.opacity = '0';
            row.style.transform = 'translateY(10px)';
            setTimeout(() => {
                tbody.appendChild(row);
                row.style.transition = 'all 0.3s ease';
                row.style.opacity = '1';
                row.style.transform = 'translateY(0)';
            }, index * 50);
        });
    } catch (error) {
        console.error('Error sorting table:', error);
    }
}

function initializeSearch() {
    try {
        const searchInput = document.getElementById('searchInput');
        if (searchInput) {
            searchInput.addEventListener('input', function() {
                const searchTerm = this.value.toLowerCase();
                const rows = document.querySelectorAll('#transactionsTable tbody tr');
                
                rows.forEach(row => {
                    const product = row.querySelector('td:nth-child(2)')?.textContent.toLowerCase() || '';
                    const notes = row.dataset.notes?.toLowerCase() || '';
                    const isVisible = product.includes(searchTerm) || notes.includes(searchTerm);
                    
                    row.style.display = isVisible ? '' : 'none';
                });
                
                updateTransactionsCount();
            });
        }
    } catch (error) {
        console.error('Error initializing search:', error);
    }
}

function initializeFilters() {
    try {
        const filterButtons = document.querySelectorAll('.filter-btn');
        filterButtons.forEach(btn => {
            btn.addEventListener('click', function() {
                const filterType = this.dataset.filter;
                
                // Update active button
                filterButtons.forEach(b => b.classList.remove('active'));
                this.classList.add('active');
                
                // Filter rows
                const rows = document.querySelectorAll('#transactionsTable tbody tr');
                rows.forEach(row => {
                    const rowType = row.dataset.type;
                    const isVisible = filterType === 'all' || rowType === filterType;
                    row.style.display = isVisible ? '' : 'none';
                });
                
                updateTransactionsCount();
            });
        });
    } catch (error) {
        console.error('Error initializing filters:', error);
    }
}

function initializeSortDropdown() {
    try {
        const sortSelect = document.getElementById('sortSelect');
        if (sortSelect) {
            sortSelect.addEventListener('change', function() {
                const [field, direction] = this.value.split('-');
                const isAscending = direction === 'asc';
                
                // Update header classes
                const headers = document.querySelectorAll('th.sortable');
                headers.forEach(h => h.classList.remove('sort-asc', 'sort-desc'));
                
                const targetHeader = document.querySelector(`th[data-sort="${field}"]`);
                if (targetHeader) {
                    targetHeader.classList.add(isAscending ? 'sort-asc' : 'sort-desc');
                }
                
                // Sort table
                sortTable(field, isAscending);
            });
        }
    } catch (error) {
        console.error('Error initializing sort dropdown:', error);
    }
}

function updateTransactionsCount() {
    try {
        const visibleRows = document.querySelectorAll('#transactionsTable tbody tr:not([style*="display: none"])');
        const countElement = document.getElementById('transactionsCount');
        if (countElement) {
            countElement.textContent = `${visibleRows.length} معاملة`;
        }
    } catch (error) {
        console.error('Error updating transactions count:', error);
    }
}

function toggleExportOptions() {
    try {
        const exportOptions = document.getElementById('exportOptions');
        if (exportOptions) {
            exportOptions.classList.toggle('show');
            
            // Close when clicking outside
            document.addEventListener('click', function closeExportOptions(e) {
                if (!e.target.closest('.quick-action-card')) {
                    exportOptions.classList.remove('show');
                    document.removeEventListener('click', closeExportOptions);
                }
            });
        }
    } catch (error) {
        console.error('Error toggling export options:', error);
    }
}

function exportCustomerData(format = 'csv') {
    try {
        const table = document.getElementById('transactionsTable');
        if (!table) return;
        
        const rows = Array.from(table.querySelectorAll('tr:not([style*="display: none"])'));
        
        let data = [];
        rows.forEach(row => {
            const cols = Array.from(row.querySelectorAll('td, th'));
            const rowData = cols.map(col => {
                // Remove emojis and get clean text
                return col.textContent.replace(/[\u{1F600}-\u{1F64F}]|[\u{1F300}-\u{1F5FF}]|[\u{1F680}-\u{1F6FF}]|[\u{1F1E0}-\u{1F1FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/gu, '').trim();
            });
            data.push(rowData);
        });
        
        const customerName = document.querySelector('.customer-name')?.textContent || 'customer';
        const filename = `customer_transactions_${customerName}.${format}`;
        
        switch(format) {
            case 'csv':
                exportToCSV(data, filename);
                break;
            case 'excel':
                exportToExcel(data, filename);
                break;
            case 'pdf':
                exportToPDF(data, filename);
                break;
        }
        
        // Close export options
        const exportOptions = document.getElementById('exportOptions');
        if (exportOptions) {
            exportOptions.classList.remove('show');
        }
    } catch (error) {
        console.error('Error exporting customer data:', error);
    }
}

function exportToCSV(data, filename) {
    try {
        const csvContent = data.map(row => row.join(',')).join('\n');
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        downloadFile(blob, filename);
    } catch (error) {
        console.error('Error exporting to CSV:', error);
    }
}

function exportToExcel(data, filename) {
    try {
        // Simple Excel-like format (CSV with BOM)
        const csvContent = '\ufeff' + data.map(row => row.join(',')).join('\n');
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        downloadFile(blob, filename.replace('.excel', '.csv'));
    } catch (error) {
        console.error('Error exporting to Excel:', error);
    }
}

function exportToPDF(data, filename) {
    try {
        // For PDF, we'll use the print function as a simple alternative
        printCustomerReport();
    } catch (error) {
        console.error('Error exporting to PDF:', error);
    }
}

function downloadFile(blob, filename) {
    try {
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);
        link.setAttribute('href', url);
        link.setAttribute('download', filename);
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (error) {
        console.error('Error downloading file:', error);
    }
}

function printCustomerReport() {
    try {
        const printWindow = window.open('', '_blank');
        if (printWindow) {
            const customerName = document.querySelector('.customer-name')?.textContent || 'العميل';
            const customerPhone = document.querySelector('.customer-phone')?.textContent || '';
            const customerAddress = document.querySelector('.customer-address')?.textContent || '';
            const customerEmail = document.querySelector('.customer-email')?.textContent || '';
            
            printWindow.document.write(`
                <html>
                    <head>
                        <title>تقرير العميل - ${customerName}</title>
                        <style>
                            body { font-family: Arial, sans-serif; direction: rtl; margin: 20px; }
                            .header { text-align: center; margin-bottom: 30px; }
                            .customer-info { margin-bottom: 20px; }
                            table { width: 100%; border-collapse: collapse; margin: 20px 0; }
                            th, td { border: 1px solid #ddd; padding: 8px; text-align: right; }
                            th { background-color: #f2f2f2; font-weight: bold; }
                            .summary { margin: 20px 0; }
                        </style>
                    </head>
                    <body>
                        <div class="header">
                            <h1>تقرير العميل</h1>
                            <h2>${customerName}</h2>
                        </div>
                        <div class="customer-info">
                            <p><strong>رقم الهاتف:</strong> ${customerPhone}</p>
                            <p><strong>العنوان:</strong> ${customerAddress}</p>
                            <p><strong>البريد الإلكتروني:</strong> ${customerEmail}</p>
                        </div>
                        <div class="summary">
                            <h3>ملخص الحساب</h3>
                            <p><strong>إجمالي المبيعات:</strong> <span id="totalSales">0.00</span> جنيه</p>
                            <p><strong>إجمالي المرتجعات:</strong> <span id="totalReturns">0.00</span> جنيه</p>
                            <p><strong>صافي المشتريات:</strong> <span id="netPurchases">0.00</span> جنيه</p>
                            <p><strong>الرصيد الفعلي:</strong> <span id="actualBalance">0.00</span> جنيه</p>
                        </div>
                        <table>
                            <thead>
                                <tr>
                                    <th>التاريخ</th>
                                    <th>المنتج</th>
                                    <th>الكمية</th>
                                    <th>السعر</th>
                                    <th>الخصم</th>
                                    <th>الإجمالي</th>
                                    <th>المدفوع</th>
                                    <th>المتبقي</th>
                                </tr>
                            </thead>
                            <tbody id="transactionsBody">
                                <!-- Transactions will be populated here -->
                            </tbody>
                        </table>
                    </body>
                </html>
            `);
            
            // Populate transactions data
            const table = document.getElementById('transactionsTable');
            if (table) {
                const rows = Array.from(table.querySelectorAll('tbody tr'));
                const printBody = printWindow.document.getElementById('transactionsBody');
                
                rows.forEach(row => {
                    const cells = Array.from(row.querySelectorAll('td'));
                    if (cells.length >= 8) {
                        const tr = printWindow.document.createElement('tr');
                        cells.forEach(cell => {
                            const td = printWindow.document.createElement('td');
                            td.textContent = cell.textContent.trim();
                            tr.appendChild(td);
                        });
                        printBody.appendChild(tr);
                    }
                });
            }
            
            // Populate summary data
            const totalSalesElement = printWindow.document.getElementById('totalSales');
            const totalReturnsElement = printWindow.document.getElementById('totalReturns');
            const netPurchasesElement = printWindow.document.getElementById('netPurchases');
            const actualBalanceElement = printWindow.document.getElementById('actualBalance');
            
            if (totalSalesElement) totalSalesElement.textContent = document.querySelector('[data-total-sales]')?.dataset.totalSales || '0.00';
            if (totalReturnsElement) totalReturnsElement.textContent = document.querySelector('[data-total-returns]')?.dataset.totalReturns || '0.00';
            if (netPurchasesElement) netPurchasesElement.textContent = document.querySelector('[data-net-purchases]')?.dataset.netPurchases || '0.00';
            if (actualBalanceElement) actualBalanceElement.textContent = document.querySelector('[data-actual-balance]')?.dataset.actualBalance || '0.00';
            
            printWindow.document.close();
            printWindow.print();
        }
    } catch (error) {
        console.error('Error printing customer report:', error);
    }
}

// Initialize customers details page when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    // Check if we're on the customers details page
    if (document.getElementById('transactionsTable')) {
        initializeCustomersDetailsPage();
    }
});

console.log('🚀 تم تحميل نظام إدارة المبيدات والأسمدة بنجاح!'); 
// ========================================
// نظام QR Scanner الجديد - بدقة عالية
// ========================================

// متغيرات النظام
let qrSystem = {
    isActive: false,
    isProcessing: false,
    lastScanTime: 0,
    scanCooldown: 1000, // 1 ثانية بين المسحات
    searchHistory: new Map(),
    maxHistorySize: 100,
    currentMode: 'qr'
};

// تهيئة نظام QR
function initQRSystem() {
    console.log('🔧 تهيئة نظام QR Scanner...');
    
    // إعداد event listeners
    setupQREventListeners();
    
    // إعداد واجهة المستخدم
    setupQRInterface();
    
    // تفعيل النظام
    qrSystem.isActive = true;
    
    console.log('✅ تم تهيئة نظام QR Scanner بنجاح');
}

// إعداد event listeners
function setupQREventListeners() {
    const qrInput = document.getElementById('qrCodeInput');
    if (!qrInput) {
        console.error('❌ لم يتم العثور على حقل QR Input');
        return;
    }
    
    // منع الاختصارات الخطيرة
    qrInput.addEventListener('keydown', handleQRKeydown);
    
    // معالجة الإدخال
    qrInput.addEventListener('input', handleQRInput);
    
    // معالجة اللصق
    qrInput.addEventListener('paste', handleQRPaste);
    
    // منع right-click
    qrInput.addEventListener('contextmenu', (e) => e.preventDefault());
    
    console.log('✅ تم إعداد QR Event Listeners');
}

// معالجة الضغط على المفاتيح
function handleQRKeydown(e) {
    // منع الاختصارات الخطيرة
    if (e.key === 'F12' || e.key === 'F11' || e.key === 'F5' || 
        (e.ctrlKey && e.shiftKey && e.key === 'I') ||
        (e.ctrlKey && e.shiftKey && e.key === 'C') ||
        (e.ctrlKey && e.key === 'U')) {
        e.preventDefault();
        return false;
    }
    
    // معالجة Enter
    if (e.key === 'Enter') {
        e.preventDefault();
        processQRCode(e.target.value.trim());
        e.target.value = '';
    }
}

// معالجة الإدخال
function handleQRInput(e) {
    const value = e.target.value;
    updateQRStatus('loading', `جاري الكتابة... (${value.length} حرف)`);
}

// معالجة اللصق
function handleQRPaste(e) {
    e.preventDefault();
    const pastedData = (e.clipboardData || window.clipboardData).getData('text');
    if (pastedData && pastedData.trim()) {
        processQRCode(pastedData.trim());
        e.target.value = '';
    }
}

// معالجة رمز QR
async function processQRCode(qrCode) {
    if (!qrCode || qrCode.trim() === '') {
        updateQRStatus('error', 'رمز QR فارغ');
        showMessage('رجاءً امسح QR Code صحيح', 'warning');
        return;
    }
    
    // التحقق من التكرار
    const now = Date.now();
    if (now - qrSystem.lastScanTime < qrSystem.scanCooldown) {
        updateQRStatus('error', 'مسح سريع جداً');
        showMessage('انتظر قليلاً قبل المسح التالي', 'warning');
        return;
    }
    
    // التحقق من المعالجة الجارية
    if (qrSystem.isProcessing) {
        updateQRStatus('error', 'جاري المعالجة...');
        showMessage('جاري معالجة المسح السابق', 'warning');
        return;
    }
    
    // تنظيف الرمز
    const cleanQRCode = qrCode.trim().replace(/[^\w\s\-_\.]/g, '');
    
    if (cleanQRCode.length < 1) {
        updateQRStatus('error', 'رمز QR غير صحيح');
        showMessage('رمز QR غير صحيح', 'error');
        return;
    }
    
    // تحديث الوقت
    qrSystem.lastScanTime = now;
    qrSystem.isProcessing = true;
    
    updateQRStatus('loading', 'جاري البحث...');
    
    try {
        // البحث عن المنتج
        const result = await searchProductByQR(cleanQRCode);
        
        if (result.success && result.product) {
            // إضافة المنتج للسلة
            await addProductToCartFromQR(result.product);
            
            updateQRStatus('success', `✅ ${result.product.name}`);
            showMessage(`تم إضافة: ${result.product.name}`, 'success');
            
            // إعادة التركيز
            setTimeout(() => {
                refocusQRInput();
            }, 1000);
            
        } else {
            updateQRStatus('error', `❌ لم يتم العثور على: ${cleanQRCode}`);
            showMessage(`المنتج غير موجود: ${cleanQRCode}`, 'error');
            
            // إعادة التركيز
            setTimeout(() => {
                refocusQRInput();
            }, 2000);
        }
        
    } catch (error) {
        console.error('❌ خطأ في معالجة QR:', error);
        updateQRStatus('error', 'خطأ في البحث');
        showMessage('خطأ في البحث', 'error');
        
        // إعادة التركيز
        setTimeout(() => {
            refocusQRInput();
        }, 2000);
        
    } finally {
        qrSystem.isProcessing = false;
    }
}

// البحث عن المنتج بواسطة QR
async function searchProductByQR(qrCode) {
    try {
        const response = await fetch(`/Cashier/SearchProducts?term=${encodeURIComponent(qrCode)}&includeOutOfStock=true`);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        const result = await response.json();
        
        if (result.success && result.products && result.products.length > 0) {
            // استخدام أول منتج (الأكثر تطابقاً)
            const product = result.products[0];
            
            // التحقق من صحة البيانات
            if (!product.id || !product.name || !product.price) {
                throw new Error('بيانات المنتج غير صحيحة');
            }
            
            return {
                success: true,
                product: product
            };
        } else {
            return {
                success: false,
                message: 'المنتج غير موجود'
            };
        }
        
    } catch (error) {
        console.error('❌ خطأ في البحث:', error);
        throw error;
    }
}

// إضافة المنتج للسلة من QR
async function addProductToCartFromQR(product) {
    try {
        // التحقق من وجود دالة الإضافة
        if (typeof addProductToCart === 'function') {
            addProductToCart(product.id, product.name, product.price, product.color || '', product.size || '');
        } else {
            throw new Error('دالة إضافة المنتج غير متاحة');
        }
        
    } catch (error) {
        console.error('❌ خطأ في إضافة المنتج:', error);
        throw error;
    }
}

// تحديث حالة QR
function updateQRStatus(status, message) {
    const qrStatus = document.getElementById('qrStatus');
    if (!qrStatus) return;
    
    // تحديث النص
    const statusText = qrStatus.querySelector('.status-text');
    if (statusText) {
        statusText.textContent = message;
    } else {
        qrStatus.textContent = message;
    }
    
    // إزالة الكلاسات القديمة
    qrStatus.classList.remove('status-ready', 'status-loading', 'status-success', 'status-error');
    
    // إضافة الكلاس الجديد
    switch(status) {
        case 'ready':
            qrStatus.classList.add('status-ready');
            qrStatus.style.color = '#007bff';
            break;
        case 'loading':
            qrStatus.classList.add('status-loading');
            qrStatus.style.color = '#ffc107';
            break;
        case 'success':
            qrStatus.classList.add('status-success');
            qrStatus.style.color = '#28a745';
            break;
        case 'error':
            qrStatus.classList.add('status-error');
            qrStatus.style.color = '#dc3545';
            break;
        default:
            qrStatus.style.color = '#6c757d';
    }
}

// إعادة التركيز على حقل QR
function refocusQRInput() {
    const qrInput = document.getElementById('qrCodeInput');
    if (qrInput && qrSystem.isActive) {
        qrInput.focus();
        updateQRStatus('ready', 'جاهز للمسح');
    }
}

// مسح حقل QR
function clearQRInput() {
    const qrInput = document.getElementById('qrCodeInput');
    if (qrInput) {
        qrInput.value = '';
        qrInput.focus();
        updateQRStatus('ready', 'جاهز للمسح');
    }
}

// إعداد واجهة المستخدم
function setupQRInterface() {
    // إضافة أزرار التحكم
    addQRControlButtons();
    
    // إعداد الحالة الافتراضية
    updateQRStatus('ready', 'جاهز للمسح');
    
    console.log('✅ تم إعداد واجهة QR');
}

// إضافة أزرار التحكم
function addQRControlButtons() {
    const qrInputGroup = document.querySelector('.qr-input-group');
    if (!qrInputGroup) return;
    
    // زر المسح
    const clearBtn = document.createElement('button');
    clearBtn.type = 'button';
    clearBtn.className = 'btn-qr-clear';
    clearBtn.innerHTML = '<i class="fas fa-broom"></i>';
    clearBtn.title = 'مسح';
    clearBtn.onclick = clearQRInput;
    
    // زر اللصق
    const pasteBtn = document.createElement('button');
    pasteBtn.type = 'button';
    pasteBtn.className = 'btn-qr-paste';
    pasteBtn.innerHTML = '<i class="fas fa-clipboard"></i>';
    pasteBtn.title = 'لصق من الحافظة';
    pasteBtn.onclick = pasteFromClipboard;
    
    // إضافة الأزرار
    qrInputGroup.appendChild(clearBtn);
    qrInputGroup.appendChild(pasteBtn);
}

// لصق من الحافظة
async function pasteFromClipboard() {
    try {
        const text = await navigator.clipboard.readText();
        if (text && text.trim()) {
            processQRCode(text.trim());
        } else {
            showMessage('الحافظة فارغة', 'warning');
        }
    } catch (error) {
        console.error('❌ خطأ في قراءة الحافظة:', error);
        showMessage('لا يمكن قراءة الحافظة', 'error');
    }
}

// إظهار رسالة
function showMessage(message, type = 'info') {
    // استخدام دالة showMessage الموجودة أو إنشاء واحدة جديدة
    if (typeof window.showMessage === 'function') {
        window.showMessage(message, type);
    } else {
        console.log(`${type.toUpperCase()}: ${message}`);
    }
}

// تفعيل النظام عند تحميل الصفحة
document.addEventListener('DOMContentLoaded', function() {
    // انتظار تحميل الصفحة بالكامل
    setTimeout(() => {
        initQRSystem();
    }, 1000);
});

// تصدير الدوال للاستخدام العام
window.QRSystem = {
    init: initQRSystem,
    process: processQRCode,
    clear: clearQRInput,
    refocus: refocusQRInput,
    updateStatus: updateQRStatus
};

console.log('📱 تم تحميل نظام QR Scanner الجديد');

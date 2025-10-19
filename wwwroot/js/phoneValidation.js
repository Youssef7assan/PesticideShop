// Phone Number Auto-Formatting for WhatsApp
// تنسيق تلقائي لأرقام الهواتف للواتساب
// 
// هذا الملف يقوم بتنسيق أرقام الهواتف تلقائياً للواتساب
// عند إدخال رقم هاتف، سيتم تنسيقه تلقائياً بالشكل الصحيح
// مثال: 01012345678 → +20 101 234 5678
// 
// عند إرسال الفاتورة للواتساب من النظام، سيتم استخدام الرقم المُنسق

const PhoneFormatter = {
    // دالة تنظيف رقم الهاتف
    cleanPhoneNumber: function(phone) {
        if (!phone) return '';
        
        // إزالة جميع الأحرف غير الرقمية
        let cleaned = phone.replace(/[^\d]/g, '');
        
        // إزالة الأصفار في البداية
        while (cleaned.startsWith('0') && cleaned.length > 1) {
            cleaned = cleaned.substring(1);
        }
        
        return cleaned;
    },

    // دالة تنسيق رقم الهاتف للواتساب
    formatPhoneForWhatsApp: function(phone) {
        const cleaned = this.cleanPhoneNumber(phone);
        
        // إذا كان الرقم يبدأ بـ 20 (مصر)، أضف + في البداية
        if (cleaned.startsWith('20')) {
            return '+' + cleaned;
        }
        
        // إذا كان الرقم يبدأ بـ 1 أو 2 أو 5 أو 7 أو 9 (أرقام مصرية)، أضف +20
        if (cleaned.startsWith('1') || cleaned.startsWith('2') || 
            cleaned.startsWith('5') || cleaned.startsWith('7') || 
            cleaned.startsWith('9')) {
            return '+20' + cleaned;
        }
        
        // إذا كان الرقم يبدأ بـ 0، أضف +20
        if (cleaned.startsWith('0')) {
            return '+20' + cleaned.substring(1);
        }
        
        // في جميع الحالات الأخرى، أضف + في البداية
        return '+' + cleaned;
    },

    // دالة تنسيق رقم الهاتف للعرض
    formatPhoneForDisplay: function(phone) {
        const cleaned = this.cleanPhoneNumber(phone);
        
        // إذا كان الرقم يبدأ بـ 20 (مصر)، أضف + في البداية
        if (cleaned.startsWith('20')) {
            return '+' + cleaned;
        }
        
        // إذا كان الرقم يبدأ بـ 1 أو 2 أو 5 أو 7 أو 9 (أرقام مصرية)، أضف +20
        if (cleaned.startsWith('1') || cleaned.startsWith('2') || 
            cleaned.startsWith('5') || cleaned.startsWith('7') || 
            cleaned.startsWith('9')) {
            return '+20' + cleaned;
        }
        
        // إذا كان الرقم يبدأ بـ 0، أضف +20
        if (cleaned.startsWith('0')) {
            return '+20' + cleaned.substring(1);
        }
        
        // في جميع الحالات الأخرى، أضف + في البداية
        return '+' + cleaned;
    }
};

// دالة تنسيق رقم الهاتف تلقائياً
function formatPhoneNumber(inputElement) {
    let value = inputElement.value;
    let cursorPosition = inputElement.selectionStart;
    
    // إزالة جميع الأحرف غير الرقمية
    const cleaned = value.replace(/[^\d]/g, '');
    
    // تنسيق الرقم
    let formatted = '';
    
    if (cleaned.startsWith('20')) {
        // مصر
        formatted = '+20 ' + cleaned.substring(2);
    } else if (cleaned.startsWith('1') || cleaned.startsWith('2') || 
               cleaned.startsWith('5') || cleaned.startsWith('7') || 
               cleaned.startsWith('9')) {
        // أرقام مصرية
        formatted = '+20 ' + cleaned;
    } else if (cleaned.startsWith('0')) {
        // رقم يبدأ بصفر
        formatted = '+20 ' + cleaned.substring(1);
    } else {
        // رقم آخر
        formatted = '+' + cleaned;
    }
    
    // إضافة مسافات كل 4 أرقام للقراءة
    if (formatted.length > 4) {
        let parts = formatted.split(' ');
        let numberPart = parts[parts.length - 1];
        let formattedNumber = '';
        
        for (let i = 0; i < numberPart.length; i++) {
            formattedNumber += numberPart[i];
            if ((i + 1) % 4 === 0 && i < numberPart.length - 1) {
                formattedNumber += ' ';
            }
        }
        
        parts[parts.length - 1] = formattedNumber;
        formatted = parts.join(' ');
    }
    
    inputElement.value = formatted;
    
    // إعادة موضع المؤشر
    const newCursorPosition = Math.min(cursorPosition, formatted.length);
    inputElement.setSelectionRange(newCursorPosition, newCursorPosition);
}

// دالة الحصول على رقم الهاتف المناسب للواتساب
function getWhatsAppNumber(phoneInput) {
    const value = phoneInput.value;
    return PhoneFormatter.formatPhoneForWhatsApp(value);
}

// دالة فتح الواتساب مع رقم الهاتف
function openWhatsApp(phoneInput) {
    const whatsappNumber = getWhatsAppNumber(phoneInput);
    if (whatsappNumber) {
        const whatsappUrl = `https://wa.me/${whatsappNumber}`;
        window.open(whatsappUrl, '_blank');
    }
}

// دالة إرسال رسالة واتساب
function sendWhatsAppMessage(phoneInput, message = '') {
    const whatsappNumber = getWhatsAppNumber(phoneInput);
    if (whatsappNumber) {
        const whatsappUrl = `https://wa.me/${whatsappNumber}?text=${encodeURIComponent(message)}`;
        window.open(whatsappUrl, '_blank');
    }
}

// تصدير الدوال للاستخدام العام
window.PhoneFormatter = PhoneFormatter;
window.formatPhoneNumber = formatPhoneNumber;
window.getWhatsAppNumber = getWhatsAppNumber;
window.openWhatsApp = openWhatsApp;
window.sendWhatsAppMessage = sendWhatsAppMessage;

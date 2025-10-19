using PesticideShop.Models;

namespace PesticideShop.Services
{
    public interface ICashierService
    {
        Task<Product?> FindProductAsync(int productId, string? productName = null);
        Task<Customer?> ValidateOrCreateCustomerAsync(TransactionRequest request);
        Task<(bool isValid, string? errorMessage)> ValidateReturnRequestAsync(TransactionRequest request);
        Task<List<CustomerTransaction>> ProcessTransactionItemsAsync(TransactionRequest request, Customer customer);
        Task<Invoice> CreateInvoiceAsync(TransactionRequest request, Customer customer, List<CustomerTransaction> transactions, string? cashierName = null);
        Task SaveReturnTrackingAsync(TransactionRequest request);
        Task SaveExchangeTrackingAsync(TransactionRequest request);
        string GenerateInvoiceNumber();
        string GenerateOrderNumber();
    }
}

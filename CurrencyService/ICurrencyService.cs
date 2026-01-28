using System.Collections.Generic;
using System.ServiceModel;

namespace CurrencyService
{
    [ServiceContract]
    public interface ICurrencyService
    {
        [OperationContract]
        string GetMessage(string name);

        [OperationContract]
        double GetExchangeRate(string currencyCode);

        [OperationContract]
        double GetHistoricalExchangeRate(string currencyCode, string date);



        [OperationContract]
        bool CreateAccount(string username, string password);

        [OperationContract]
        bool TopUpAccount(string username, double amount);

        [OperationContract]
        double GetBalance(string username);

        [OperationContract]
        bool Login(string username, string password);

        // Currency Exchange System

        [OperationContract]
        bool BuyCurrency(string username, string currencyCode, double amount);

        [OperationContract]
        bool SellCurrency(string username, string currencyCode, double amount);

        // Transaction History

        [OperationContract]
        List<Transaction> GetTransactionHistory(string username);

        // Virtual Payment Feature (PayPal-like)
        [OperationContract]
        bool VirtualPayment(string fromUser, string toUser, double amount);
    }
}

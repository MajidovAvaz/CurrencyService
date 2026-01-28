using System;

namespace CurrencyClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new CurrencyServiceRef.CurrencyServiceClient();


            Console.WriteLine(client.GetMessage("Avaz"));

            double usd = client.GetExchangeRate("USD");
            Console.WriteLine("USD Rate: " + usd);

            double history = client.GetHistoricalExchangeRate("USD", "2023-12-01");
            Console.WriteLine("USD on 2023-12-01: " + history);

            // Account System
            Console.WriteLine("----- Account System -----");

            if (client.CreateAccount("Avaz", "1234"))
                Console.WriteLine("Account created successfully!");
            else
                Console.WriteLine("Account already exists!");

            if (client.TopUpAccount("Avaz", 1000))
                Console.WriteLine("Top up successful!");
            else
                Console.WriteLine("Top up failed!");

            double balance = client.GetBalance("Avaz");
            Console.WriteLine("Avaz's Balance: " + balance);

            // Buy/Sell Currency
            Console.WriteLine("----- Currency Exchange -----");

            if (client.BuyCurrency("Avaz", "USD", 50))
                Console.WriteLine("Bought 50 USD successfully!");
            else
                Console.WriteLine("Buy Currency failed!");

            if (client.SellCurrency("Avaz", "USD", 20))
                Console.WriteLine("Sold 20 USD successfully!");
            else
                Console.WriteLine("Sell Currency failed!");

            double balanceAfter = client.GetBalance("Avaz");
            Console.WriteLine("Avaz's Balance after buy/sell: " + balanceAfter);

        
            Console.WriteLine("\n----- Transaction History -----");

            var transactions = client.GetTransactionHistory("Avaz");
            foreach (var tx in transactions)
            {
                Console.WriteLine($"{tx.Date:yyyy-MM-dd HH:mm:ss} | {tx.Type} | {tx.Amount} {tx.CurrencyCode} @ {tx.Rate}");
            }

            client.Close();

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
